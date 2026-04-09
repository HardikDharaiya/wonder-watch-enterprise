using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WonderWatch.Application.Interfaces;
using WonderWatch.Domain.Entities;
using WonderWatch.Domain.Enums;
using WonderWatch.Infrastructure;
using WonderWatch.Web.ViewModels;

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

        public AdminController(
            IAdminService adminService,
            IAssetService assetService,
            AppDbContext context,
            ILogger<AdminController> logger,
            IConfiguration config,
            IEmailService emailService)
        {
            _adminService = adminService;
            _assetService = assetService;
            _context = context;
            _logger = logger;
            _config = config;
            _emailService = emailService;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var kpis = await _adminService.GetDashboardKPIsAsync();
            var alerts = await _adminService.GetInventoryAlertsAsync();
            var indiaCulture = new CultureInfo("hi-IN");

            var viewModel = new AdminDashboardViewModel
            {
                TotalRevenueFormatted = kpis.TotalRevenue.ToString("C0", indiaCulture),
                OrdersPending = kpis.OrdersPending,
                ActiveUsers = kpis.ActiveUsers,
                LowStockCount = kpis.LowStockCount,
                InventoryAlerts = alerts.Select(w => new AdminInventoryAlertViewModel
                {
                    WatchId = w.Id,
                    Name = w.Name,
                    ReferenceNumber = w.ReferenceNumber,
                    StockQuantity = w.StockQuantity
                }).ToList()
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
                var slug = model.Name.ToLower().Replace(" ", "-").Replace("[^a-z0-9-]", "");

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
                return RedirectToAction(nameof(Watches));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating watch.");
                ModelState.AddModelError(string.Empty, "An error occurred while saving the watch. Please check the logs.");
                return View(model);
            }
        }

        [HttpGet("orders")]
        public async Task<IActionResult> Orders()
        {
            var orders = await _context.Orders
                .Include(o => o.Items)
                .ThenInclude(i => i.Watch)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            var indiaCulture = new CultureInfo("hi-IN");

            var viewModel = new AdminOrderListViewModel
            {
                Orders = orders.Select(o => new AdminOrderItemViewModel
                {
                    Id = o.Id,
                    OrderNumber = $"#WW-{o.Id.ToString().Substring(0, 8).ToUpper()}",
                    CustomerName = o.ShippingAddress.Line1, // Simplified for list view
                    Date = o.CreatedAt,
                    TotalFormatted = o.TotalAmount.ToString("C0", indiaCulture),
                    Status = o.Status.ToString(),
                    ItemCount = o.Items.Sum(i => i.Quantity)
                }).ToList()
            };

            return View(viewModel);
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
            ViewBag.SmtpPassword = _config["SmtpSettings:Password"];
            ViewBag.SmtpFromEmail = _config["SmtpSettings:FromEmail"];
            ViewBag.SmtpFromName = _config["SmtpSettings:FromName"];
            return View();
        }

        [HttpPost("settings/save")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSettings(string SmtpHost, string SmtpPort, string SmtpUsername, string SmtpPassword, string SmtpFromEmail, string SmtpFromName)
        {
            // Write to appsettings.json (runtime config update)
            try
            {
                var appSettingsPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
                var json = await System.IO.File.ReadAllTextAsync(appSettingsPath);
                var doc = System.Text.Json.JsonDocument.Parse(json);
                var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new();

                var smtpSection = new Dictionary<string, string>
                {
                    ["Host"] = SmtpHost ?? "",
                    ["Port"] = SmtpPort ?? "587",
                    ["Username"] = SmtpUsername ?? "",
                    ["Password"] = SmtpPassword ?? "",
                    ["FromEmail"] = SmtpFromEmail ?? "",
                    ["FromName"] = SmtpFromName ?? "Wonder Watch"
                };

                dict["SmtpSettings"] = smtpSection;

                var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                await System.IO.File.WriteAllTextAsync(appSettingsPath, System.Text.Json.JsonSerializer.Serialize(dict, options));

                TempData["SmtpSuccess"] = "SMTP settings saved. Restart the application for changes to take effect.";
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

            try
            {
                await _emailService.SendTestEmailAsync(TestEmailTo);
                TempData["SmtpSuccess"] = $"Test email sent to {TestEmailTo}. Check your inbox.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Test email failed.");
                TempData["SmtpError"] = "Failed to send test email: " + ex.Message;
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
        public string TotalRevenueFormatted { get; set; } = string.Empty;
        public int OrdersPending { get; set; }
        public int ActiveUsers { get; set; }
        public int LowStockCount { get; set; }
        public List<AdminInventoryAlertViewModel> InventoryAlerts { get; set; } = new();
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
    }

    // ---------------------------------------------------------
    // ORDERS VIEW MODELS
    // ---------------------------------------------------------
    public class AdminOrderListViewModel
    {
        public List<AdminOrderItemViewModel> Orders { get; set; } = new();
    }

    public class AdminOrderItemViewModel
    {
        public Guid Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string TotalFormatted { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int ItemCount { get; set; }
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