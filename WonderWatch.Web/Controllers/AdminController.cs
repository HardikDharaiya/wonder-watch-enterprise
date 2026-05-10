using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WonderWatch.Application.Interfaces;
using WonderWatch.Domain.Entities;
using WonderWatch.Domain.Enums;
using WonderWatch.Domain.Identity;
using WonderWatch.Infrastructure;
using WonderWatch.Web.ViewModels;
using Microsoft.AspNetCore.Identity;

namespace WonderWatch.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("admin")]
    public class AdminController : Controller
    {
        private readonly IAdminService _adminService;
        private readonly IAssetService _assetService;
        private readonly AppDbContext _context;
        private readonly ILogger<AdminController> _logger;
        private readonly IConfiguration _config;
        private readonly IEmailService _emailService;
        private readonly IDatabaseManagementService _dbManagementService;
        private readonly IConfigurationRoot? _configRoot;
        private readonly IOrderService _orderService;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminController(
            IAdminService adminService,
            IAssetService assetService,
            AppDbContext context,
            ILogger<AdminController> logger,
            IConfiguration config,
            IEmailService emailService,
            IDatabaseManagementService dbManagementService,
            IOrderService orderService,
            UserManager<ApplicationUser> userManager)
        {
            _adminService = adminService;
            _assetService = assetService;
            _context = context;
            _logger = logger;
            _config = config;
            _emailService = emailService;
            _dbManagementService = dbManagementService;
            _configRoot = config as IConfigurationRoot;
            _orderService = orderService;
            _userManager = userManager;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var kpis = await _adminService.GetDashboardKPIsAsync();
            var alerts = await _adminService.GetInventoryAlertsAsync();
            var topSellers = await _adminService.GetTopSellingWatchesAsync(3);
            var recentOrders = await _adminService.GetRecentOrdersAsync(5);
            var indiaCulture = new CultureInfo("hi-IN");

            var viewModel = new AdminDashboardViewModel
            {
                // Zone 1
                TotalRevenueFormatted = kpis.TotalRevenue.ToString("C0", indiaCulture),
                OrdersPending = kpis.OrdersPending,
                ActiveUsers = kpis.ActiveUsers,
                LowStockCount = kpis.LowStockCount,

                // Zone 2
                TotalOrders = kpis.TotalOrders,
                OrdersShipped = kpis.OrdersShipped,

                // Zone 3: Chart.js JSON
                RevenueLabelsJson = System.Text.Json.JsonSerializer.Serialize(
                    kpis.RevenueTimeline.Select(r => r.Date)),
                RevenueDataJson = System.Text.Json.JsonSerializer.Serialize(
                    kpis.RevenueTimeline.Select(r => r.Amount)),

                // Zone 4: Pipeline
                PipelinePending = kpis.PipelinePending,
                PipelinePaid = kpis.PipelinePaid,
                PipelineProcessing = kpis.PipelineProcessing,
                PipelineShipped = kpis.PipelineShipped,
                PipelineDelivered = kpis.PipelineDelivered,

                // Zone 5: Top Sellers
                TopSellers = topSellers.Select(ts => new TopSellingWatchViewModel
                {
                    Id = ts.Id,
                    Name = ts.Name,
                    Brand = ts.Brand,
                    ImageUrl = ts.ImageUrl,
                    UnitsSold = ts.UnitsSold,
                    RevenueFormatted = ts.RevenueFormatted
                }).ToList(),

                // Zone 6: Recent Orders + Alerts
                RecentOrders = recentOrders.Select(ro => new RecentOrderViewModel
                {
                    Id = ro.Id,
                    OrderNumber = ro.OrderNumber,
                    CustomerName = ro.CustomerName,
                    TotalFormatted = ro.TotalFormatted,
                    Status = ro.Status,
                    TimeAgo = ro.TimeAgo,
                    IsPayOnDelivery = ro.IsPayOnDelivery
                }).ToList(),
                InventoryAlerts = alerts.Select(w => new AdminInventoryAlertViewModel
                {
                    WatchId = w.Id,
                    Name = w.Name,
                    ReferenceNumber = w.ReferenceNumber,
                    StockQuantity = w.StockQuantity
                }).ToList(),

                // Zone 7: System Health
                TotalWatches = kpis.TotalWatches,
                PublishedWatches = kpis.PublishedWatches,
                PendingReviews = kpis.PendingReviews
            };

            return View(viewModel);
        }

        [HttpGet("watches")]
        public async Task<IActionResult> Watches()
        {
            var watches = await _context.Watches
                .Include(w => w.Images)
                .OrderByDescending(w => w.Id)
                .ToListAsync();

            var indiaCulture = new CultureInfo("hi-IN");

            var viewModel = new AdminWatchListViewModel
            {
                Watches = watches.Select(w => new AdminWatchItemViewModel
                {
                    Id = w.Id,
                    Name = w.Name,
                    ReferenceNumber = w.ReferenceNumber,
                    RetailPriceFormatted = w.RetailPrice.ToString("C0", indiaCulture),
                    StockQuantity = w.StockQuantity,
                    IsPublished = w.IsPublished,
                    ImageUrl = w.Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.Path ?? "/images/placeholder.webp"
                }).ToList()
            };

            return View(viewModel);
        }

        [HttpGet("watches/create")]
        public IActionResult CreateWatch()
        {
            return View(new WatchCreateViewModel());
        }

        [HttpPost("watches/create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateWatch(WatchCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var watchId = Guid.NewGuid();

                // Generate Slug from Name
                var slug = Regex.Replace(model.Name.ToLower().Replace(" ", "-"), "[^a-z0-9-]", "");

                var watch = new Watch
                {
                    Id = watchId,
                    Name = model.Name,
                    Brand = model.Brand,
                    ReferenceNumber = model.ReferenceNumber,
                    Slug = slug,
                    Description = model.Description,
                    RetailPrice = model.RetailPrice,
                    CostPrice = model.CostPrice,
                    ComparePrice = model.ComparePrice,
                    CaseSize = model.CaseSize,
                    MovementType = model.MovementType,
                    StockQuantity = model.StockQuantity,
                    IsPublished = model.IsPublished,
                    IsSoldOut = model.StockQuantity <= 0
                };

                // Handle GLB Upload
                if (model.GlbFile != null && model.GlbFile.Length > 0)
                {
                    watch.GlbAssetPath = await _assetService.SaveGlbAsync(model.GlbFile, watchId);
                }

                // Handle Image Uploads
                if (model.ImageFiles != null && model.ImageFiles.Any())
                {
                    var imagePaths = await _assetService.SaveImagesAsync(model.ImageFiles, watchId);
                    for (int i = 0; i < imagePaths.Count; i++)
                    {
                        watch.Images.Add(new WatchImage
                        {
                            Id = Guid.NewGuid(),
                            WatchId = watchId,
                            Path = imagePaths[i],
                            SortOrder = i + 1
                        });
                    }
                }

                _context.Watches.Add(watch);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Admin created new watch: {WatchName} ({WatchId})", watch.Name, watch.Id);
                TempData["WatchSuccess"] = $"'{watch.Name}' has been added to the inventory.";
                return RedirectToAction(nameof(Watches));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating watch.");
                ModelState.AddModelError(string.Empty, "An error occurred while saving the watch. Please check the logs.");
                return View(model);
            }
        }

        // =============================================================
        // EDIT WATCH
        // =============================================================

        [HttpGet("watches/edit/{id}")]
        public async Task<IActionResult> EditWatch(Guid id)
        {
            var watch = await _adminService.GetWatchByIdForEditAsync(id);
            if (watch == null)
            {
                TempData["WatchError"] = "Reference not found.";
                return RedirectToAction(nameof(Watches));
            }

            var model = new WatchEditViewModel
            {
                Id = watch.Id,
                Name = watch.Name,
                Brand = watch.Brand,
                ReferenceNumber = watch.ReferenceNumber,
                Description = watch.Description,
                RetailPrice = watch.RetailPrice,
                CostPrice = watch.CostPrice,
                ComparePrice = watch.ComparePrice,
                CaseSize = watch.CaseSize,
                MovementType = watch.MovementType,
                StockQuantity = watch.StockQuantity,
                IsPublished = watch.IsPublished,
                StrapMaterial = watch.StrapMaterial,
                ExistingImageUrls = watch.Images.Select(i => i.Path).ToList(),
                ExistingGlbPath = watch.GlbAssetPath
            };

            return View(model);
        }

        [HttpPost("watches/edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditWatch(Guid id, WatchEditViewModel model)
        {
            if (!ModelState.IsValid)
            {
                // Re-populate existing images for the form
                var existingWatch = await _adminService.GetWatchByIdForEditAsync(id);
                if (existingWatch != null)
                {
                    model.ExistingImageUrls = existingWatch.Images.Select(i => i.Path).ToList();
                    model.ExistingGlbPath = existingWatch.GlbAssetPath;
                }
                return View(model);
            }

            try
            {
                var updatedWatch = new Watch
                {
                    Name = model.Name,
                    Brand = model.Brand,
                    ReferenceNumber = model.ReferenceNumber,
                    Description = model.Description,
                    RetailPrice = model.RetailPrice,
                    CostPrice = model.CostPrice,
                    ComparePrice = model.ComparePrice,
                    CaseSize = model.CaseSize,
                    MovementType = model.MovementType,
                    StockQuantity = model.StockQuantity,
                    IsPublished = model.IsPublished,
                    StrapMaterial = model.StrapMaterial ?? string.Empty
                };

                await _adminService.UpdateWatchAsync(id, updatedWatch, model.ImageFiles, model.GlbFile);
                _logger.LogInformation("Admin updated watch: {WatchName} ({WatchId})", model.Name, id);
                TempData["WatchSuccess"] = $"'{model.Name}' has been updated successfully.";
                return RedirectToAction(nameof(Watches));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating watch {WatchId}.", id);
                ModelState.AddModelError(string.Empty, "An error occurred while updating. Please check the logs.");
                var existingWatch = await _adminService.GetWatchByIdForEditAsync(id);
                if (existingWatch != null)
                {
                    model.ExistingImageUrls = existingWatch.Images.Select(i => i.Path).ToList();
                    model.ExistingGlbPath = existingWatch.GlbAssetPath;
                }
                return View(model);
            }
        }

        // =============================================================
        // DELETE WATCH
        // =============================================================

        [HttpPost("watches/delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteWatch(Guid id)
        {
            try
            {
                var hardDeleted = await _adminService.DeleteWatchAsync(id);
                if (hardDeleted)
                {
                    TempData["WatchSuccess"] = "Reference permanently removed from inventory.";
                }
                else
                {
                    TempData["WatchSuccess"] = "Reference archived (unpublished). It has existing orders and cannot be permanently deleted.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting watch {WatchId}.", id);
                TempData["WatchError"] = "Failed to remove reference: " + ex.Message;
            }

            return RedirectToAction(nameof(Watches));
        }

        // =============================================================
        // VIEW WATCH (Read-Only Detail)
        // =============================================================

        [HttpGet("watches/view/{id}")]
        public async Task<IActionResult> ViewWatch(Guid id)
        {
            var watch = await _adminService.GetWatchByIdForEditAsync(id);
            if (watch == null)
            {
                TempData["WatchError"] = "Reference not found.";
                return RedirectToAction(nameof(Watches));
            }

            var indiaCulture = new CultureInfo("hi-IN");
            var model = new AdminWatchDetailViewModel
            {
                Id = watch.Id,
                Name = watch.Name,
                Brand = watch.Brand,
                ReferenceNumber = watch.ReferenceNumber,
                Slug = watch.Slug,
                Description = watch.Description,
                RetailPriceFormatted = watch.RetailPrice.ToString("C0", indiaCulture),
                CostPriceFormatted = watch.CostPrice.ToString("C0", indiaCulture),
                ComparePriceFormatted = watch.ComparePrice.ToString("C0", indiaCulture),
                CaseSize = watch.CaseSize,
                MovementType = watch.MovementType.ToString(),
                StockQuantity = watch.StockQuantity,
                IsPublished = watch.IsPublished,
                IsSoldOut = watch.IsSoldOut,
                StrapMaterial = watch.StrapMaterial,
                GlbAssetPath = watch.GlbAssetPath,
                ImageUrls = watch.Images.Select(i => i.Path).ToList()
            };

            return View(model);
        }

        [HttpGet("orders")]
        public async Task<IActionResult> Orders(string? status)
        {
            var query = _context.Orders
                .Include(o => o.Items)
                .ThenInclude(i => i.Watch)
                .AsQueryable();

            // Apply status filter if provided
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<OrderStatus>(status, true, out var filterStatus))
            {
                query = query.Where(o => o.Status == filterStatus);
            }

            var orders = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();

            // Get all unique user IDs and fetch user info for customer names
            var userIds = orders.Select(o => o.UserId).Distinct().ToList();
            var users = await _context.Users
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => new { u.FullName, u.Email });

            // Compute KPI counts (always from ALL orders, not filtered)
            var allOrders = await _context.Orders.ToListAsync();
            var totalCount = allOrders.Count;
            var pendingCount = allOrders.Count(o => o.Status == OrderStatus.Pending);
            var processingCount = allOrders.Count(o => o.Status == OrderStatus.Processing || o.Status == OrderStatus.Paid);
            var shippedCount = allOrders.Count(o => o.Status == OrderStatus.Shipped);
            var deliveredCount = allOrders.Count(o => o.Status == OrderStatus.Delivered || o.Status == OrderStatus.Confirmed);

            var indiaCulture = new CultureInfo("hi-IN");

            var viewModel = new AdminOrderListViewModel
            {
                StatusFilter = status,
                TotalOrderCount = totalCount,
                PendingCount = pendingCount,
                ProcessingCount = processingCount,
                ShippedCount = shippedCount,
                DeliveredCount = deliveredCount,
                Orders = orders.Select(o =>
                {
                    users.TryGetValue(o.UserId, out var user);
                    return new AdminOrderItemViewModel
                    {
                        Id = o.Id,
                        OrderNumber = $"#WW-{o.Id.ToString().Substring(0, 8).ToUpper()}",
                        CustomerName = user?.FullName ?? "Unknown",
                        CustomerEmail = user?.Email ?? "",
                        Date = o.CreatedAt,
                        TotalFormatted = o.TotalAmount.ToString("C0", indiaCulture),
                        Status = o.Status.ToString(),
                        ItemCount = o.Items.Sum(i => i.Quantity),
                        IsPayOnDelivery = o.IsPayOnDelivery
                    };
                }).ToList()
            };

            return View(viewModel);
        }

        [HttpGet("orders/detail/{id}")]
        public async Task<IActionResult> OrderDetail(Guid id)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .ThenInclude(i => i.Watch)
                .ThenInclude(w => w.Images)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                TempData["OrderError"] = "Order not found.";
                return RedirectToAction(nameof(Orders));
            }

            var customer = await _context.Users.FirstOrDefaultAsync(u => u.Id == order.UserId);
            var indiaCulture = new CultureInfo("hi-IN");

            // Compute allowed transitions based on state machine
            var allowedTransitions = GetAllowedTransitions(order.Status);

            // Build status timeline
            var timeline = BuildStatusTimeline(order.Status, order.CreatedAt, order.UpdatedAt);

            var viewModel = new AdminOrderDetailViewModel
            {
                Id = order.Id,
                OrderNumber = $"#WW-{order.Id.ToString().Substring(0, 8).ToUpper()}",
                Status = order.Status.ToString(),
                StatusEnum = order.Status,
                CreatedAt = order.CreatedAt,
                UpdatedAt = order.UpdatedAt,
                TotalFormatted = order.TotalAmount.ToString("C0", indiaCulture),
                SubtotalFormatted = order.TotalAmount.ToString("C0", indiaCulture),
                IsPayOnDelivery = order.IsPayOnDelivery,
                RazorpayOrderId = order.RazorpayOrderId,
                RazorpayPaymentId = order.RazorpayPaymentId,
                CustomerName = customer?.FullName ?? "Unknown",
                CustomerEmail = customer?.Email ?? "",
                ShippingLine1 = order.ShippingAddress.Line1,
                ShippingLine2 = order.ShippingAddress.Line2 ?? "",
                ShippingCity = order.ShippingAddress.City,
                ShippingState = order.ShippingAddress.State,
                ShippingPinCode = order.ShippingAddress.PinCode,
                ShippingPhone = order.ShippingAddress.Phone,
                Items = order.Items.Select(i => new AdminOrderDetailItemViewModel
                {
                    WatchName = i.Watch?.Name ?? "Deleted Watch",
                    WatchBrand = i.Watch?.Brand ?? "",
                    WatchRef = i.Watch?.ReferenceNumber ?? "",
                    ImageUrl = i.Watch?.Images?.OrderBy(img => img.SortOrder).FirstOrDefault()?.Path ?? "/images/placeholder.webp",
                    Quantity = i.Quantity,
                    UnitPriceFormatted = i.UnitPrice.ToString("C0", indiaCulture),
                    LineTotalFormatted = (i.UnitPrice * i.Quantity).ToString("C0", indiaCulture)
                }).ToList(),
                AllowedTransitions = allowedTransitions,
                StatusTimeline = timeline
            };

            return View("OrderDetail", viewModel);
        }

        [HttpPost("orders/update-status/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateOrderStatus(Guid id, string newStatus)
        {
            try
            {
                if (!Enum.TryParse<OrderStatus>(newStatus, true, out var status))
                {
                    TempData["OrderError"] = $"Invalid status: {newStatus}";
                    return RedirectToAction(nameof(OrderDetail), new { id });
                }

                await _orderService.TransitionStatusAsync(id, status);
                TempData["OrderSuccess"] = $"Order status updated to {status}.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update order {OrderId} status.", id);
                TempData["OrderError"] = $"Failed to update status: {ex.Message}";
            }

            return RedirectToAction(nameof(OrderDetail), new { id });
        }

        [HttpPost("orders/bulk-update-status")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkUpdateStatus(List<Guid> orderIds, string newStatus)
        {
            try
            {
                if (!orderIds.Any())
                {
                    TempData["OrderError"] = "No orders selected.";
                    return RedirectToAction(nameof(Orders));
                }

                if (!Enum.TryParse<OrderStatus>(newStatus, true, out var status))
                {
                    TempData["OrderError"] = $"Invalid status: {newStatus}";
                    return RedirectToAction(nameof(Orders));
                }

                await _orderService.BulkTransitionAsync(orderIds, status);
                TempData["OrderSuccess"] = $"{orderIds.Count} order(s) updated to {status}.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed bulk status update.");
                TempData["OrderError"] = $"Bulk update failed: {ex.Message}";
            }

            return RedirectToAction(nameof(Orders));
        }

        // ── Helper: Compute valid next statuses from current ──
        private List<string> GetAllowedTransitions(OrderStatus current)
        {
            return current switch
            {
                OrderStatus.Pending => new List<string> { "Paid", "Cancelled" },
                OrderStatus.Paid => new List<string> { "Processing", "Cancelled" },
                OrderStatus.Processing => new List<string> { "Shipped" },
                OrderStatus.Shipped => new List<string> { "Delivered" },
                OrderStatus.Delivered => new List<string> { "Confirmed" },
                _ => new List<string>()
            };
        }

        // ── Helper: Build status timeline for the stepper UI ──
        private List<AdminOrderTimelineEntry> BuildStatusTimeline(OrderStatus current, DateTime created, DateTime updated)
        {
            var steps = new[] { "Pending", "Paid", "Processing", "Shipped", "Delivered", "Confirmed" };
            var currentIndex = current == OrderStatus.Cancelled ? -1 : Array.IndexOf(steps, current.ToString());
            var isCancelled = current == OrderStatus.Cancelled;

            var timeline = new List<AdminOrderTimelineEntry>();
            for (int i = 0; i < steps.Length; i++)
            {
                timeline.Add(new AdminOrderTimelineEntry
                {
                    Label = steps[i],
                    IsCompleted = !isCancelled && i < currentIndex,
                    IsCurrent = !isCancelled && i == currentIndex,
                    IsCancelled = isCancelled && steps[i] == current.ToString(),
                    Timestamp = i == 0 ? created.ToString("dd MMM yyyy HH:mm") : (i == currentIndex ? updated.ToString("dd MMM yyyy HH:mm") : null)
                });
            }

            if (isCancelled)
            {
                timeline.Add(new AdminOrderTimelineEntry
                {
                    Label = "Cancelled",
                    IsCompleted = false,
                    IsCurrent = true,
                    IsCancelled = true,
                    Timestamp = updated.ToString("dd MMM yyyy HH:mm")
                });
            }

            return timeline;
        }
        [HttpGet("reviews")]
        public async Task<IActionResult> Reviews()
        {
            var reviews = await _context.Reviews
                .Include(r => r.Watch)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var viewModel = new AdminReviewListViewModel
            {
                Reviews = reviews.Select(r => new AdminReviewItemViewModel
                {
                    Id = r.Id,
                    WatchName = r.Watch.Name,
                    Rating = r.Rating,
                    Body = r.Body,
                    Status = r.Status.ToString(),
                    Date = r.CreatedAt
                }).ToList()
            };

            return View(viewModel);
        }

        [HttpGet("settings")]
        public IActionResult Settings()
        {
            ViewBag.SmtpHost = _config["SmtpSettings:Host"];
            ViewBag.SmtpPort = _config["SmtpSettings:Port"];
            ViewBag.SmtpUsername = _config["SmtpSettings:Username"];
            
            // Pass actual password so the admin can view it via the toggle in the UI
            ViewBag.SmtpPassword = _config["SmtpSettings:Password"] ?? "";

            ViewBag.SmtpFromEmail = _config["SmtpSettings:FromEmail"];
            ViewBag.SmtpFromName = _config["SmtpSettings:FromName"];
            return View();
        }

        [HttpPost("settings/save")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSettings(string SmtpHost, string SmtpPort, string SmtpUsername, string SmtpPassword, string SmtpFromEmail, string SmtpFromName)
        {
            try
            {
                // ── Write to .NET User Secrets instead of appsettings.json ─────────
                var userSecretsId = System.Reflection.Assembly.GetExecutingAssembly()
                    .GetCustomAttribute<Microsoft.Extensions.Configuration.UserSecrets.UserSecretsIdAttribute>()?.UserSecretsId;

                if (string.IsNullOrEmpty(userSecretsId))
                {
                    throw new Exception("UserSecretsId not found. User Secrets are only available in Development mode.");
                }

                var secretsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Microsoft", "UserSecrets", userSecretsId, "secrets.json"
                );

                string existingJson = System.IO.File.Exists(secretsPath)
                    ? await System.IO.File.ReadAllTextAsync(secretsPath)
                    : "{}";

                if (string.IsNullOrWhiteSpace(existingJson))
                {
                    existingJson = "{}";
                }

                var documentOptions = new System.Text.Json.JsonDocumentOptions
                {
                    CommentHandling = System.Text.Json.JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };

                var nodeOptions = new System.Text.Json.Nodes.JsonNodeOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var root = System.Text.Json.Nodes.JsonNode.Parse(existingJson, nodeOptions, documentOptions) as System.Text.Json.Nodes.JsonObject
                           ?? new System.Text.Json.Nodes.JsonObject();

                root["SmtpSettings:Host"] = SmtpHost ?? "";
                root["SmtpSettings:Port"] = SmtpPort ?? "587";
                root["SmtpSettings:Username"] = SmtpUsername ?? "";
                
                // Only update password if a new one is provided and it's not the mask
                if (!string.IsNullOrWhiteSpace(SmtpPassword) && SmtpPassword != "••••••••")
                {
                    root["SmtpSettings:Password"] = SmtpPassword;
                }
                // If the user completely cleared the password field, we can clear it
                else if (string.IsNullOrWhiteSpace(SmtpPassword))
                {
                    root["SmtpSettings:Password"] = "";
                }

                root["SmtpSettings:FromEmail"] = SmtpFromEmail ?? "";
                root["SmtpSettings:FromName"] = SmtpFromName ?? "Wonder Watch";

                var writeOptions = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                
                var directory = Path.GetDirectoryName(secretsPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await System.IO.File.WriteAllTextAsync(secretsPath, root.ToJsonString(writeOptions));

                // ── Reload IConfiguration so EmailService picks up new values immediately ──
                _configRoot?.Reload();

                TempData["SmtpSuccess"] = "✓ SMTP settings saved to User Secrets and applied.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save SMTP settings.");
                TempData["SmtpError"] = "Failed to save settings: " + ex.Message;
            }

            return RedirectToAction("Settings");
        }

        [HttpPost("settings/test-email")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TestEmail(string TestEmailTo)
        {
            if (string.IsNullOrWhiteSpace(TestEmailTo))
            {
                TempData["SmtpError"] = "Please enter a recipient email address.";
                return RedirectToAction("Settings");
            }

            // Guard: SMTP must be configured before testing
            var smtpHost = _config["SmtpSettings:Host"];
            var smtpUser = _config["SmtpSettings:Username"];
            if (string.IsNullOrWhiteSpace(smtpHost) || string.IsNullOrWhiteSpace(smtpUser))
            {
                TempData["SmtpError"] = "SMTP is not configured yet. Please fill in and save the SMTP settings first.";
                return RedirectToAction("Settings");
            }

            try
            {
                await _emailService.SendTestEmailAsync(TestEmailTo);
                TempData["SmtpSuccess"] = $"✓ Test email dispatched to {TestEmailTo}. Check your inbox (and spam folder).";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Test email failed to {Email}.", TestEmailTo);
                TempData["SmtpError"] = $"Delivery failed — {ex.Message}";
            }

            return RedirectToAction("Settings");
        }

        // =============================================================
        // FILTERS MANAGEMENT
        // =============================================================

        [HttpGet("filters")]
        public async Task<IActionResult> Filters()
        {
            var brands = await _context.Brands.OrderBy(b => b.SortOrder).ToListAsync();
            var config = await _context.FilterConfigs.FirstOrDefaultAsync();

            var vm = new AdminFiltersViewModel
            {
                Brands = brands,
                MinPrice = config?.MinPrice ?? 0,
                MaxPrice = config?.MaxPrice ?? 10000000
            };

            return View(vm);
        }

        [HttpPost("filters/add-brand")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddBrand(string brandName)
        {
            if (!string.IsNullOrWhiteSpace(brandName))
            {
                var exists = await _context.Brands.AnyAsync(b => b.Name == brandName);
                if (!exists)
                {
                    var maxOrder = await _context.Brands.AnyAsync()
                        ? await _context.Brands.MaxAsync(b => b.SortOrder)
                        : -1;

                    _context.Brands.Add(new Brand
                    {
                        Id = Guid.NewGuid(),
                        Name = brandName.Trim(),
                        SortOrder = maxOrder + 1,
                        IsActive = true
                    });
                    await _context.SaveChangesAsync();
                    TempData["FilterSuccess"] = $"Brand '{brandName}' added.";
                }
                else
                {
                    TempData["FilterError"] = $"Brand '{brandName}' already exists.";
                }
            }
            return RedirectToAction("Filters");
        }

        [HttpPost("filters/delete-brand/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBrand(Guid id)
        {
            var brand = await _context.Brands.FindAsync(id);
            if (brand != null)
            {
                _context.Brands.Remove(brand);
                await _context.SaveChangesAsync();
                TempData["FilterSuccess"] = $"Brand '{brand.Name}' removed.";
            }
            return RedirectToAction("Filters");
        }

        [HttpPost("filters/update-config")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateFilterConfig(decimal minPrice, decimal maxPrice)
        {
            var config = await _context.FilterConfigs.FirstOrDefaultAsync();
            if (config == null)
            {
                config = new FilterConfig { Id = Guid.NewGuid() };
                _context.FilterConfigs.Add(config);
            }
            config.MinPrice = minPrice;
            config.MaxPrice = maxPrice;
            await _context.SaveChangesAsync();
            TempData["FilterSuccess"] = "Price range updated.";
            return RedirectToAction("Filters");
        }

        [HttpPost("settings/reset-database")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetDatabaseToFactory(string confirmationText)
        {
            if (string.IsNullOrWhiteSpace(confirmationText) || !confirmationText.Equals("RESET DATABASE", StringComparison.OrdinalIgnoreCase))
            {
                TempData["SmtpError"] = "Invalid confirmation text. Type 'RESET DATABASE' exactly to confirm.";
                return RedirectToAction("Settings");
            }

            try
            {
                await _dbManagementService.ResetDatabaseAsync();
                TempData["SmtpSuccess"] = "Database has been successfully reset to factory defaults and seeded with initial data.";
                // We're signing the user out essentially or telling them it's done.
                // It should preserve their session since admin is seeded too, 
                // but let's just log and redirect.
                _logger.LogWarning("Admin User requested full database reset to factory defaults.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reset database.");
                TempData["SmtpError"] = "Failed to reset database: " + ex.Message;
            }

            return RedirectToAction("Settings");
        }
    }
}

namespace WonderWatch.Web.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using Microsoft.AspNetCore.Http;
    using WonderWatch.Domain.Enums;

    // ---------------------------------------------------------
    // DASHBOARD VIEW MODELS
    // ---------------------------------------------------------
    public class AdminDashboardViewModel
    {
        // Zone 1: Primary KPI Cards
        public string TotalRevenueFormatted { get; set; } = string.Empty;
        public int OrdersPending { get; set; }
        public int ActiveUsers { get; set; }
        public int LowStockCount { get; set; }

        // Zone 2: Extended KPI Strip
        public int TotalOrders { get; set; }
        public int OrdersShipped { get; set; }

        // Zone 3: Revenue Chart (JSON for Chart.js)
        public string RevenueLabelsJson { get; set; } = "[]";
        public string RevenueDataJson { get; set; } = "[]";

        // Zone 4: Order Pipeline
        public int PipelinePending { get; set; }
        public int PipelinePaid { get; set; }
        public int PipelineProcessing { get; set; }
        public int PipelineShipped { get; set; }
        public int PipelineDelivered { get; set; }

        // Zone 5: Top Sellers
        public List<TopSellingWatchViewModel> TopSellers { get; set; } = new();

        // Zone 6: Recent Orders + Inventory Alerts
        public List<RecentOrderViewModel> RecentOrders { get; set; } = new();
        public List<AdminInventoryAlertViewModel> InventoryAlerts { get; set; } = new();

        // Zone 7: System Health
        public int TotalWatches { get; set; }
        public int PublishedWatches { get; set; }
        public int PendingReviews { get; set; }
    }

    public class TopSellingWatchViewModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public int UnitsSold { get; set; }
        public string RevenueFormatted { get; set; } = string.Empty;
    }

    public class RecentOrderViewModel
    {
        public Guid Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string TotalFormatted { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string TimeAgo { get; set; } = string.Empty;
        public bool IsPayOnDelivery { get; set; }
    }

    public class AdminInventoryAlertViewModel
    {
        public Guid WatchId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ReferenceNumber { get; set; } = string.Empty;
        public int StockQuantity { get; set; }
    }

    // ---------------------------------------------------------
    // WATCHES VIEW MODELS
    // ---------------------------------------------------------
    public class AdminWatchListViewModel
    {
        public List<AdminWatchItemViewModel> Watches { get; set; } = new();
    }

    public class AdminWatchItemViewModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ReferenceNumber { get; set; } = string.Empty;
        public string RetailPriceFormatted { get; set; } = string.Empty;
        public int StockQuantity { get; set; }
        public bool IsPublished { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
    }

    public class WatchCreateViewModel
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        [Required]
        public string Brand { get; set; } = string.Empty;
        [Required]
        public string ReferenceNumber { get; set; } = string.Empty;
        [Required]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Range(0, double.MaxValue)]
        public decimal RetailPrice { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public decimal CostPrice { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public decimal ComparePrice { get; set; }
        [Required]
        public int CaseSize { get; set; }

        [Required]
        public MovementType MovementType { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        public int StockQuantity { get; set; }

        public bool IsPublished { get; set; } = true;

        public IFormFile? GlbFile { get; set; }
        public List<IFormFile>? ImageFiles { get; set; }
        public string StrapMaterial { get; set; } = string.Empty;
    }

    public class WatchEditViewModel : WatchCreateViewModel
    {
        public Guid Id { get; set; }
        public List<string> ExistingImageUrls { get; set; } = new();
        public string ExistingGlbPath { get; set; } = string.Empty;
    }

    // ---------------------------------------------------------
    // WATCH DETAIL VIEW MODEL (Read-Only)
    // ---------------------------------------------------------
    public class AdminWatchDetailViewModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string ReferenceNumber { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string RetailPriceFormatted { get; set; } = string.Empty;
        public string CostPriceFormatted { get; set; } = string.Empty;
        public string ComparePriceFormatted { get; set; } = string.Empty;
        public int CaseSize { get; set; }
        public string MovementType { get; set; } = string.Empty;
        public int StockQuantity { get; set; }
        public bool IsPublished { get; set; }
        public bool IsSoldOut { get; set; }
        public string StrapMaterial { get; set; } = string.Empty;
        public string GlbAssetPath { get; set; } = string.Empty;
        public List<string> ImageUrls { get; set; } = new();
    }

    // ---------------------------------------------------------
    // ORDERS VIEW MODELS
    // ---------------------------------------------------------
    public class AdminOrderListViewModel
    {
        public List<AdminOrderItemViewModel> Orders { get; set; } = new();
        public string? StatusFilter { get; set; }
        public int TotalOrderCount { get; set; }
        public int PendingCount { get; set; }
        public int ProcessingCount { get; set; }
        public int ShippedCount { get; set; }
        public int DeliveredCount { get; set; }
    }

    public class AdminOrderItemViewModel
    {
        public Guid Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string TotalFormatted { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int ItemCount { get; set; }
        public bool IsPayOnDelivery { get; set; }
    }

    // ---------------------------------------------------------
    // ORDER DETAIL VIEW MODELS
    // ---------------------------------------------------------
    public class AdminOrderDetailViewModel
    {
        public Guid Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public OrderStatus StatusEnum { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string TotalFormatted { get; set; } = string.Empty;
        public string SubtotalFormatted { get; set; } = string.Empty;
        public bool IsPayOnDelivery { get; set; }
        public string RazorpayOrderId { get; set; } = string.Empty;
        public string RazorpayPaymentId { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string ShippingLine1 { get; set; } = string.Empty;
        public string ShippingLine2 { get; set; } = string.Empty;
        public string ShippingCity { get; set; } = string.Empty;
        public string ShippingState { get; set; } = string.Empty;
        public string ShippingPinCode { get; set; } = string.Empty;
        public string ShippingPhone { get; set; } = string.Empty;
        public List<AdminOrderDetailItemViewModel> Items { get; set; } = new();
        public List<string> AllowedTransitions { get; set; } = new();
        public List<AdminOrderTimelineEntry> StatusTimeline { get; set; } = new();
    }

    public class AdminOrderDetailItemViewModel
    {
        public string WatchName { get; set; } = string.Empty;
        public string WatchBrand { get; set; } = string.Empty;
        public string WatchRef { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string UnitPriceFormatted { get; set; } = string.Empty;
        public string LineTotalFormatted { get; set; } = string.Empty;
    }

    public class AdminOrderTimelineEntry
    {
        public string Label { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
        public bool IsCurrent { get; set; }
        public bool IsCancelled { get; set; }
        public string? Timestamp { get; set; }
    }

    // ---------------------------------------------------------
    // REVIEWS VIEW MODELS
    // ---------------------------------------------------------
    public class AdminReviewListViewModel
    {
        public List<AdminReviewItemViewModel> Reviews { get; set; } = new();
    }

    public class AdminReviewItemViewModel
    {
        public Guid Id { get; set; }
        public string WatchName { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string Body { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime Date { get; set; }
    }

    // ---------------------------------------------------------
    // FILTERS VIEW MODELS
    // ---------------------------------------------------------
    public class AdminFiltersViewModel
    {
        public List<Brand> Brands { get; set; } = new();
        public decimal MinPrice { get; set; }
        public decimal MaxPrice { get; set; }
    }
}