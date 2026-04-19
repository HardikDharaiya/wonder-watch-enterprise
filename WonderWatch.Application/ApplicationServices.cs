using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Razorpay.Api;
using WonderWatch.Application.DTOs;
using WonderWatch.Application.Interfaces;
using WonderWatch.Domain.Entities;
using WonderWatch.Domain.Enums;
using WonderWatch.Infrastructure;
using Order = WonderWatch.Domain.Entities.Order;

namespace WonderWatch.Application.Services
{
    public class CatalogService : ICatalogService
    {
        private readonly AppDbContext _context;

        public CatalogService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<(List<Watch> Watches, int TotalCount)> GetAllAsync(WatchFilterDto filter, int page = 1, int pageSize = 12)
        {
            var query = _context.Watches
                .Include(w => w.Images)
                .Where(w => w.IsPublished)
                .AsQueryable();

            // 1. Search Query Filter
            if (!string.IsNullOrWhiteSpace(filter.SearchQuery))
            {
                var search = filter.SearchQuery.ToLower();
                query = query.Where(w => w.Name.ToLower().Contains(search) || w.Brand.ToLower().Contains(search) || w.ReferenceNumber.ToLower().Contains(search));
            }

            // 2. Price Filters
            if (filter.MinPrice.HasValue) query = query.Where(w => w.RetailPrice >= filter.MinPrice.Value);
            if (filter.MaxPrice.HasValue) query = query.Where(w => w.RetailPrice <= filter.MaxPrice.Value);

            // 3. Movement Type Filter
            if (filter.MovementType.HasValue) query = query.Where(w => w.MovementType == filter.MovementType.Value);

            // 4. Brand Array Filter
            if (filter.Brands != null && filter.Brands.Any())
            {
                var upperBrands = filter.Brands.Select(b => b.ToUpper()).ToList();
                query = query.Where(w => upperBrands.Contains(w.Brand.ToUpper()));
            }

            // 5. Case Size Filter
            if (filter.CaseSize.HasValue)
            {
                query = query.Where(w => w.CaseSize == filter.CaseSize.Value);
            }

            // 6. FIXED: Strap Material Filter
            if (!string.IsNullOrWhiteSpace(filter.StrapMaterial))
            {
                var strap = filter.StrapMaterial.ToLower();
                query = query.Where(w => w.StrapMaterial.ToLower() == strap);
            }

            // 7. Sorting
            query = filter.SortBy switch
            {
                "price-asc" => query.OrderBy(w => w.RetailPrice),
                "price-desc" => query.OrderByDescending(w => w.RetailPrice),
                "newest" => query.OrderByDescending(w => w.Id),
                _ => query.OrderByDescending(w => w.Id) // Default to newest
            };

            var totalCount = await query.CountAsync();
            var watches = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            return (watches, totalCount);
        }

        public async Task<Watch?> GetByIdAsync(Guid id)
        {
            return await _context.Watches
                .Include(w => w.Images)
                .Include(w => w.Reviews.Where(r => r.Status == ReviewStatus.Approved))
                .FirstOrDefaultAsync(w => w.Id == id);
        }

        public async Task<Watch?> GetBySlugAsync(string slug)
        {
            return await _context.Watches
                .Include(w => w.Images)
                .Include(w => w.Reviews.Where(r => r.Status == ReviewStatus.Approved))
                .FirstOrDefaultAsync(w => w.Slug == slug);
        }

        public async Task<List<Watch>> SearchAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return new List<Watch>();
            var search = query.ToLower();
            return await _context.Watches
                .Include(w => w.Images)
                .Where(w => w.IsPublished && (w.Name.ToLower().Contains(search) || w.ReferenceNumber.ToLower().Contains(search)))
                .Take(5)
                .ToListAsync();
        }

        public async Task<List<string>> GetAvailableBrandsAsync()
        {
            // Pull from admin-managed Brand table instead of Watch.Brand strings
            return await _context.Brands
                .Where(b => b.IsActive)
                .OrderBy(b => b.SortOrder)
                .Select(b => b.Name)
                .ToListAsync();
        }

        public async Task<List<int>> GetAvailableCaseSizesAsync()
        {
            return await _context.Watches
                .Where(w => w.IsPublished)
                .Select(w => w.CaseSize)
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync();
        }

        // FIXED: Added method to fetch dynamic strap materials
        public async Task<List<string>> GetAvailableStrapMaterialsAsync()
        {
            return await _context.Watches
                .Where(w => w.IsPublished && !string.IsNullOrEmpty(w.StrapMaterial))
                .Select(w => w.StrapMaterial)
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync();
        }

        public async Task<FilterConfigDto> GetFilterConfigAsync()
        {
            var config = await _context.FilterConfigs.FirstOrDefaultAsync();
            if (config == null)
            {
                // Fallback: derive from actual watch data
                var watches = _context.Watches.Where(w => w.IsPublished);
                var min = await watches.AnyAsync() ? await watches.MinAsync(w => w.RetailPrice) : 0m;
                var max = await watches.AnyAsync() ? await watches.MaxAsync(w => w.RetailPrice) : 10000000m;
                return new FilterConfigDto { MinPrice = min, MaxPrice = max };
            }
            return new FilterConfigDto { MinPrice = config.MinPrice, MaxPrice = config.MaxPrice };
        }
    }

    public class OrderService : IOrderService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<OrderService> _logger;

        public OrderService(AppDbContext context, ILogger<OrderService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Order> CreateOrderAsync(Guid userId, CreateOrderDto dto)
        {
            var order = new Order
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Status = OrderStatus.Pending,
                IsPayOnDelivery = dto.IsPayOnDelivery,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                ShippingAddress = new Address
                {
                    Line1 = dto.Line1,
                    Line2 = dto.Line2,
                    City = dto.City,
                    State = dto.State,
                    PinCode = dto.PinCode,
                    Phone = dto.Phone,
                    IsDefault = false
                }
            };

            decimal totalAmount = 0;
            var watchIds = dto.Items.Select(i => i.WatchId).ToList();
            var watches = await _context.Watches.Where(w => watchIds.Contains(w.Id)).ToListAsync();

            foreach (var item in dto.Items)
            {
                var watch = watches.FirstOrDefault(w => w.Id == item.WatchId)
                    ?? throw new InvalidOperationException($"Watch {item.WatchId} not found.");

                if (watch.StockQuantity < item.Quantity)
                    throw new InvalidOperationException($"Insufficient stock for {watch.Name}.");

                var orderItem = new OrderItem
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    WatchId = watch.Id,
                    Quantity = item.Quantity,
                    UnitPrice = watch.RetailPrice
                };

                order.Items.Add(orderItem);
                totalAmount += (orderItem.UnitPrice * orderItem.Quantity);

                // Deduct stock temporarily (will be restored if cancelled)
                watch.StockQuantity -= item.Quantity;
                if (watch.StockQuantity == 0) watch.IsSoldOut = true;
            }

            order.TotalAmount = totalAmount;
            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Order {OrderId} created for User {UserId} with Total {TotalAmount}", order.Id, userId, totalAmount);
            return order;
        }

        public async Task TransitionStatusAsync(Guid orderId, OrderStatus newStatus)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .ThenInclude(i => i.Watch)
                .FirstOrDefaultAsync(o => o.Id == orderId)
                ?? throw new InvalidOperationException($"Order {orderId} not found.");

            var oldStatus = order.Status;

            // Strict State Machine Validation
            bool isValidTransition = (oldStatus, newStatus) switch
            {
                (OrderStatus.Pending, OrderStatus.Paid) => true,
                (OrderStatus.Pending, OrderStatus.Cancelled) => true,
                (OrderStatus.Paid, OrderStatus.Processing) => true,
                (OrderStatus.Paid, OrderStatus.Cancelled) => true,
                (OrderStatus.Processing, OrderStatus.Shipped) => true,
                (OrderStatus.Shipped, OrderStatus.Delivered) => true,
                (OrderStatus.Delivered, OrderStatus.Confirmed) => true,
                _ => false
            };

            if (!isValidTransition)
            {
                _logger.LogWarning("Illegal order transition attempted: Order {OrderId} from {OldStatus} to {NewStatus}", orderId, oldStatus, newStatus);
                throw new InvalidOperationException($"Illegal state transition from {oldStatus} to {newStatus}.");
            }

            order.Status = newStatus;
            order.UpdatedAt = DateTime.UtcNow;

            // Restore stock if cancelled
            if (newStatus == OrderStatus.Cancelled)
            {
                foreach (var item in order.Items)
                {
                    if (item.Watch != null)
                    {
                        item.Watch.StockQuantity += item.Quantity;
                        item.Watch.IsSoldOut = item.Watch.StockQuantity == 0;
                    }
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Order {OrderId} transitioned from {OldStatus} to {NewStatus}", orderId, oldStatus, newStatus);
        }

        public async Task BulkTransitionAsync(List<Guid> orderIds, OrderStatus status)
        {
            var orders = await _context.Orders
                .Include(o => o.Items)
                .ThenInclude(i => i.Watch)
                .Where(o => orderIds.Contains(o.Id))
                .ToListAsync();

            foreach (var order in orders)
            {
                var oldStatus = order.Status;

                bool isValidTransition = (oldStatus, status) switch
                {
                    (OrderStatus.Pending, OrderStatus.Paid) => true,
                    (OrderStatus.Pending, OrderStatus.Cancelled) => true,
                    (OrderStatus.Paid, OrderStatus.Processing) => true,
                    (OrderStatus.Paid, OrderStatus.Cancelled) => true,
                    (OrderStatus.Processing, OrderStatus.Shipped) => true,
                    (OrderStatus.Shipped, OrderStatus.Delivered) => true,
                    (OrderStatus.Delivered, OrderStatus.Confirmed) => true,
                    _ => false
                };

                if (!isValidTransition)
                {
                    _logger.LogWarning("Illegal bulk transition skipped: Order {OrderId} from {OldStatus} to {NewStatus}", order.Id, oldStatus, status);
                    continue;
                }

                order.Status = status;
                order.UpdatedAt = DateTime.UtcNow;

                if (status == OrderStatus.Cancelled)
                {
                    foreach (var item in order.Items)
                    {
                        if (item.Watch != null)
                        {
                            item.Watch.StockQuantity += item.Quantity;
                            item.Watch.IsSoldOut = item.Watch.StockQuantity == 0;
                        }
                    }
                }
            }

            await _context.SaveChangesAsync();
        }

        public async Task<List<Order>> GetUserOrdersAsync(Guid userId)
        {
            return await _context.Orders
                .Include(o => o.Items)
                .ThenInclude(i => i.Watch)
                .ThenInclude(w => w.Images)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }

        public async Task<Order?> GetOrderByIdAsync(Guid orderId, Guid userId)
        {
            return await _context.Orders
                .Include(o => o.Items)
                .ThenInclude(i => i.Watch)
                .ThenInclude(w => w.Images)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);
        }

        public async Task PayPendingOrderAsync(Guid orderId, Guid userId, string razorpayPaymentId)
        {
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId)
                ?? throw new InvalidOperationException($"Order {orderId} not found.");

            if (order.Status != OrderStatus.Pending)
                throw new InvalidOperationException($"Order {orderId} is not in Pending status.");

            order.RazorpayPaymentId = razorpayPaymentId;
            order.Status = OrderStatus.Paid;
            order.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Order {OrderId} paid by user {UserId}. PaymentId: {PaymentId}", orderId, userId, razorpayPaymentId);
        }

        public async Task ConfirmDeliveryAsync(Guid orderId, Guid userId)
        {
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId)
                ?? throw new InvalidOperationException($"Order {orderId} not found.");

            if (order.Status != OrderStatus.Delivered)
                throw new InvalidOperationException($"Order {orderId} is not in Delivered status.");

            // For PoD orders, user must have paid before confirming
            if (order.IsPayOnDelivery && string.IsNullOrEmpty(order.RazorpayPaymentId))
                throw new InvalidOperationException("Payment is required before confirming delivery for Pay on Delivery orders.");

            order.Status = OrderStatus.Confirmed;
            order.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Order {OrderId} delivery confirmed by user {UserId}", orderId, userId);
        }
    }

    public class PaymentService : IPaymentProvider
    {
        private readonly IConfiguration _config;
        private readonly ILogger<PaymentService> _logger;

        public PaymentService(IConfiguration config, ILogger<PaymentService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public Task<string> CreateRazorpayOrderAsync(decimal amount)
        {
            var keyId = _config["Razorpay:KeyId"] ?? throw new InvalidOperationException("Razorpay KeyId missing.");
            var keySecret = _config["Razorpay:KeySecret"] ?? throw new InvalidOperationException("Razorpay KeySecret missing.");

            var client = new RazorpayClient(keyId, keySecret);

            // Amount must be in paise (multiply by 100)
            var options = new Dictionary<string, object>
            {
                { "amount", (int)(amount * 100) },
                { "currency", "INR" },
                { "receipt", Guid.NewGuid().ToString().Substring(0, 20) }
            };

            var order = client.Order.Create(options);
            return Task.FromResult(order["id"].ToString());
        }

        public bool VerifySignature(string orderId, string paymentId, string signature)
        {
            var keySecret = _config["Razorpay:KeySecret"] ?? throw new InvalidOperationException("Razorpay KeySecret missing.");

            var payload = $"{orderId}|{paymentId}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(keySecret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var generatedSignature = BitConverter.ToString(hash).Replace("-", "").ToLower();

            var isValid = generatedSignature == signature;

            if (!isValid)
            {
                _logger.LogError("Razorpay signature verification failed for OrderId: {OrderId}, PaymentId: {PaymentId}", orderId, paymentId);
            }

            return isValid;
        }
    }

    public class WishlistService : IWishlistService
    {
        private readonly AppDbContext _context;

        public WishlistService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> ToggleAsync(Guid userId, Guid watchId)
        {
            var existing = await _context.Wishlists.FirstOrDefaultAsync(w => w.UserId == userId && w.WatchId == watchId);
            if (existing != null)
            {
                _context.Wishlists.Remove(existing);
                await _context.SaveChangesAsync();
                return false;
            }

            _context.Wishlists.Add(new Wishlist
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                WatchId = watchId,
                AddedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<Watch>> GetUserWishlistAsync(Guid userId)
        {
            return await _context.Wishlists
                .Where(w => w.UserId == userId)
                .Include(w => w.Watch)
                .ThenInclude(w => w.Images)
                .OrderByDescending(w => w.AddedAt)
                .Select(w => w.Watch)
                .ToListAsync();
        }

        public async Task<bool> IsWishlistedAsync(Guid userId, Guid watchId)
        {
            return await _context.Wishlists.AnyAsync(w => w.UserId == userId && w.WatchId == watchId);
        }
    }

    public class AssetService : IAssetService
    {
        private readonly string _webRootPath;

        public AssetService()
        {
            // Resolves to WonderWatch.Web/wwwroot when running the application
            _webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        }

        public async Task<string> SaveGlbAsync(IFormFile file, Guid watchId)
        {
            var directory = Path.Combine(_webRootPath, "models");
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

            var fileName = $"{watchId}.glb";
            var filePath = Path.Combine(directory, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            return $"/models/{fileName}";
        }

        public async Task<List<string>> SaveImagesAsync(List<IFormFile> files, Guid watchId)
        {
            var paths = new List<string>();
            var directory = Path.Combine(_webRootPath, "images", "watches", watchId.ToString());
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

            for (int i = 0; i < files.Count; i++)
            {
                var fileName = $"{i + 1}.webp";
                var filePath = Path.Combine(directory, fileName);

                using var stream = new FileStream(filePath, FileMode.Create);
                await files[i].CopyToAsync(stream);

                paths.Add($"/images/watches/{watchId}/{fileName}");
            }

            return paths;
        }

        public Task DeleteAssetAsync(string path)
        {
            var fullPath = Path.Combine(_webRootPath, path.TrimStart('/'));
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
            return Task.CompletedTask;
        }
    }

    public class AdminService : IAdminService
    {
        private readonly AppDbContext _context;

        public AdminService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DashboardKpiDto> GetDashboardKPIsAsync()
        {
            var totalRevenue = await _context.Orders
                .Where(o => o.Status == OrderStatus.Delivered || o.Status == OrderStatus.Shipped || o.Status == OrderStatus.Paid)
                .SumAsync(o => o.TotalAmount);

            var pendingOrders = await _context.Orders.CountAsync(o => o.Status == OrderStatus.Pending || o.Status == OrderStatus.Paid);
            var activeUsers = await _context.Users.CountAsync();
            var lowStock = await _context.Watches.CountAsync(w => w.StockQuantity <= 4);

            return new DashboardKpiDto
            {
                TotalRevenue = totalRevenue,
                OrdersPending = pendingOrders,
                ActiveUsers = activeUsers,
                LowStockCount = lowStock
            };
        }

        public async Task<List<Watch>> GetInventoryAlertsAsync()
        {
            return await _context.Watches
                .Where(w => w.StockQuantity <= 4)
                .OrderBy(w => w.StockQuantity)
                .ToListAsync();
        }

        public async Task ModerateReviewAsync(Guid reviewId, ReviewStatus status)
        {
            var review = await _context.Reviews.FindAsync(reviewId)
                ?? throw new InvalidOperationException("Review not found.");

            review.Status = status;
            await _context.SaveChangesAsync();
        }
    }

    public class EmailService : IEmailService
    {
        private readonly ILogger<EmailService> _logger;
        private readonly IConfiguration _config;

        public EmailService(ILogger<EmailService> logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;
        }

        private async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            var smtpHost = _config["SmtpSettings:Host"];
            var smtpPort = int.TryParse(_config["SmtpSettings:Port"], out var p) ? p : 587;
            var smtpUser = _config["SmtpSettings:Username"];
            var smtpPass = _config["SmtpSettings:Password"];
            var fromEmail = _config["SmtpSettings:FromEmail"] ?? smtpUser;
            var fromName = _config["SmtpSettings:FromName"] ?? "Wonder Watch";

            if (string.IsNullOrWhiteSpace(smtpHost) || string.IsNullOrWhiteSpace(smtpUser))
            {
                throw new InvalidOperationException(
                    "SMTP is not configured. Please save SMTP settings (Host + Username) in the Admin → Settings page before sending emails.");
            }

            var message = new MimeKit.MimeMessage();
            message.From.Add(new MimeKit.MailboxAddress(fromName, fromEmail));
            message.To.Add(MimeKit.MailboxAddress.Parse(toEmail));
            message.Subject = subject;
            message.Body = new MimeKit.BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

            using var client = new MailKit.Net.Smtp.SmtpClient();
            await client.ConnectAsync(smtpHost, smtpPort, MailKit.Security.SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(smtpUser, smtpPass);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Email sent to {To} with subject '{Subject}'", toEmail, subject);
        }

        public async Task SendOrderConfirmationAsync(Order order)
        {
            _logger.LogInformation("Order Confirmation Email queued for Order {OrderId}", order.Id);
            // In production, resolve user email from UserId. For now, log.
            await Task.CompletedTask;
        }

        public async Task SendShippingUpdateAsync(Order order)
        {
            _logger.LogInformation("Shipping Update Email queued for Order {OrderId} with Status {Status}", order.Id, order.Status);
            await Task.CompletedTask;
        }

        public async Task SendTestEmailAsync(string toEmail)
        {
            var html = @"
                <div style='font-family:sans-serif;max-width:560px;margin:0 auto;padding:40px;background:#0A0A0A;color:#F0E6D3;'>
                    <h1 style='color:#C9A74A;font-size:24px;margin-bottom:16px;'>Wonder Watch</h1>
                    <p style='font-size:14px;line-height:1.7;'>This is a test email from your Wonder Watch Enterprise platform.</p>
                    <p style='font-size:14px;line-height:1.7;color:#999;'>SMTP configuration is working correctly.</p>
                    <hr style='border:none;border-top:1px solid #222;margin:32px 0;' />
                    <p style='font-size:11px;color:#555;'>Sent at " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC") + @"</p>
                </div>";

            await SendEmailAsync(toEmail, "Wonder Watch — SMTP Test", html);
        }
    }

    // =============================================================
    // ADDRESS SERVICE — CRUD operations for Vault → Addresses
    // =============================================================
    public class AddressService : IAddressService
    {
        private readonly AppDbContext _context;

        public AddressService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<UserAddressDto>> GetByUserAsync(Guid userId)
        {
            return await _context.UserAddresses
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.IsDefault)
                .ThenByDescending(a => a.CreatedAt)
                .Select(a => new UserAddressDto
                {
                    Id = a.Id,
                    Label = a.Label,
                    FullName = a.FullName,
                    Line1 = a.Line1,
                    Line2 = a.Line2,
                    City = a.City,
                    State = a.State,
                    PinCode = a.PinCode,
                    Country = a.Country,
                    Phone = a.Phone,
                    IsDefault = a.IsDefault,
                    CreatedAt = a.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<UserAddressDto> AddAsync(Guid userId, CreateAddressDto dto)
        {
            // If setting as default, unset existing defaults
            if (dto.IsDefault)
            {
                var existing = await _context.UserAddresses
                    .Where(a => a.UserId == userId && a.IsDefault)
                    .ToListAsync();
                foreach (var e in existing) e.IsDefault = false;
            }

            // If this is the first address, make it default
            var hasAny = await _context.UserAddresses.AnyAsync(a => a.UserId == userId);

            var entity = new UserAddress
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Label = dto.Label,
                FullName = dto.FullName,
                Line1 = dto.Line1,
                Line2 = dto.Line2,
                City = dto.City,
                State = dto.State,
                PinCode = dto.PinCode,
                Country = dto.Country,
                Phone = dto.Phone,
                IsDefault = dto.IsDefault || !hasAny,
                CreatedAt = DateTime.UtcNow
            };

            _context.UserAddresses.Add(entity);
            await _context.SaveChangesAsync();

            return new UserAddressDto
            {
                Id = entity.Id,
                Label = entity.Label,
                FullName = entity.FullName,
                Line1 = entity.Line1,
                Line2 = entity.Line2,
                City = entity.City,
                State = entity.State,
                PinCode = entity.PinCode,
                Country = entity.Country,
                Phone = entity.Phone,
                IsDefault = entity.IsDefault,
                CreatedAt = entity.CreatedAt
            };
        }

        public async Task UpdateAsync(Guid addressId, Guid userId, CreateAddressDto dto)
        {
            var entity = await _context.UserAddresses
                .FirstOrDefaultAsync(a => a.Id == addressId && a.UserId == userId)
                ?? throw new InvalidOperationException("Address not found.");

            if (dto.IsDefault)
            {
                var others = await _context.UserAddresses
                    .Where(a => a.UserId == userId && a.Id != addressId && a.IsDefault)
                    .ToListAsync();
                foreach (var o in others) o.IsDefault = false;
            }

            entity.Label = dto.Label;
            entity.FullName = dto.FullName;
            entity.Line1 = dto.Line1;
            entity.Line2 = dto.Line2;
            entity.City = dto.City;
            entity.State = dto.State;
            entity.PinCode = dto.PinCode;
            entity.Country = dto.Country;
            entity.Phone = dto.Phone;
            entity.IsDefault = dto.IsDefault;

            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid addressId, Guid userId)
        {
            var entity = await _context.UserAddresses
                .FirstOrDefaultAsync(a => a.Id == addressId && a.UserId == userId)
                ?? throw new InvalidOperationException("Address not found.");

            bool wasDefault = entity.IsDefault;
            _context.UserAddresses.Remove(entity);
            await _context.SaveChangesAsync();

            // If we deleted the default, promote the first remaining
            if (wasDefault)
            {
                var next = await _context.UserAddresses
                    .Where(a => a.UserId == userId)
                    .OrderByDescending(a => a.CreatedAt)
                    .FirstOrDefaultAsync();
                if (next != null)
                {
                    next.IsDefault = true;
                    await _context.SaveChangesAsync();
                }
            }
        }

        public async Task SetDefaultAsync(Guid addressId, Guid userId)
        {
            var all = await _context.UserAddresses
                .Where(a => a.UserId == userId)
                .ToListAsync();

            foreach (var a in all)
                a.IsDefault = a.Id == addressId;

            await _context.SaveChangesAsync();
        }
    }

    // =============================================================
    // NOTIFICATION SERVICE — Feed management for Vault → Notifications
    // =============================================================
    public class NotificationService : INotificationService
    {
        private readonly AppDbContext _context;

        public NotificationService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<NotificationDto>> GetByUserAsync(Guid userId)
        {
            return await _context.UserNotifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new NotificationDto
                {
                    Id = n.Id,
                    Title = n.Title,
                    Body = n.Body,
                    Type = n.Type.ToString(),
                    IsRead = n.IsRead,
                    CreatedAt = n.CreatedAt
                })
                .ToListAsync();
        }

        public async Task MarkAllReadAsync(Guid userId)
        {
            await _context.UserNotifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        }

        public async Task MarkReadAsync(Guid notificationId, Guid userId)
        {
            var entity = await _context.UserNotifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);
            if (entity != null)
            {
                entity.IsRead = true;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<int> GetUnreadCountAsync(Guid userId)
        {
            return await _context.UserNotifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);
        }

        public async Task CreateAsync(Guid userId, string title, string body, NotificationType type)
        {
            _context.UserNotifications.Add(new UserNotification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = title,
                Body = body,
                Type = type,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }
    }

    // =============================================================
    // MEMBERSHIP SERVICE — Admin & User Subscription Management
    // =============================================================
    public class MembershipService : IMembershipService
    {
        private readonly AppDbContext _context;

        public MembershipService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<MembershipPlanDto>> GetActivePlansAsync()
        {
            return await _context.MembershipPlans
                .Where(p => p.IsActive)
                .OrderBy(p => p.Price)
                .Select(p => new MembershipPlanDto
                {
                    Id = p.Id,
                    Tier = p.Tier,
                    Name = p.Name,
                    Price = p.Price,
                    PriceFormatted = $"₹{p.Price:N0}",
                    BillingCycle = p.BillingCycle,
                    Features = System.Text.Json.JsonSerializer.Deserialize<List<string>>(p.Features, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)) ?? new List<string>(),
                    IsActive = p.IsActive,
                    SubscriberCount = p.Subscribers.Count
                })
                .ToListAsync();
        }

        public async Task<List<MembershipPlanDto>> GetAllPlansAsync()
        {
            return await _context.MembershipPlans
                .OrderBy(p => p.Price)
                .Select(p => new MembershipPlanDto
                {
                    Id = p.Id,
                    Tier = p.Tier,
                    Name = p.Name,
                    Price = p.Price,
                    PriceFormatted = $"₹{p.Price:N0}",
                    BillingCycle = p.BillingCycle,
                    Features = System.Text.Json.JsonSerializer.Deserialize<List<string>>(p.Features, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)) ?? new List<string>(),
                    IsActive = p.IsActive,
                    SubscriberCount = p.Subscribers.Count
                })
                .ToListAsync();
        }

        public async Task<MembershipPlanDto?> GetPlanByIdAsync(Guid id)
        {
            var plan = await _context.MembershipPlans
                .Include(p => p.Subscribers)
                .FirstOrDefaultAsync(p => p.Id == id);
                
            if (plan == null) return null;

            return new MembershipPlanDto
            {
                Id = plan.Id,
                Tier = plan.Tier,
                Name = plan.Name,
                Price = plan.Price,
                PriceFormatted = $"₹{plan.Price:N0}",
                BillingCycle = plan.BillingCycle,
                Features = System.Text.Json.JsonSerializer.Deserialize<List<string>>(plan.Features, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)) ?? new List<string>(),
                IsActive = plan.IsActive,
                SubscriberCount = plan.Subscribers.Count
            };
        }

        public async Task<MembershipPlanDto> CreatePlanAsync(CreateMembershipPlanDto dto)
        {
            var plan = new MembershipPlan
            {
                Id = Guid.NewGuid(),
                Tier = dto.Tier,
                Name = dto.Name,
                Price = dto.Price,
                BillingCycle = dto.BillingCycle,
                Features = System.Text.Json.JsonSerializer.Serialize(dto.Features),
                IsActive = dto.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            _context.MembershipPlans.Add(plan);
            await _context.SaveChangesAsync();
            return await GetPlanByIdAsync(plan.Id) ?? throw new Exception("Plan created but not found.");
        }

        public async Task UpdatePlanAsync(Guid id, CreateMembershipPlanDto dto)
        {
            var plan = await _context.MembershipPlans.FindAsync(id);
            if (plan != null)
            {
                plan.Tier = dto.Tier;
                plan.Name = dto.Name;
                plan.Price = dto.Price;
                plan.BillingCycle = dto.BillingCycle;
                plan.Features = System.Text.Json.JsonSerializer.Serialize(dto.Features);
                plan.IsActive = dto.IsActive;
                await _context.SaveChangesAsync();
            }
        }

        public async Task TogglePlanActiveAsync(Guid id)
        {
            var plan = await _context.MembershipPlans.FindAsync(id);
            if (plan != null)
            {
                plan.IsActive = !plan.IsActive;
                await _context.SaveChangesAsync();
            }
        }

        public async Task DeletePlanAsync(Guid id)
        {
            var plan = await _context.MembershipPlans.FindAsync(id);
            if (plan != null)
            {
                _context.MembershipPlans.Remove(plan);
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpgradeUserPlanAsync(Guid userId, Guid planId)
        {
            var user = await _context.Users.FindAsync(userId);
            var plan = await _context.MembershipPlans.FindAsync(planId);

            if (user != null && plan != null)
            {
                user.CurrentMembershipPlanId = plan.Id;
                user.MembershipTier = plan.Tier;
                await _context.SaveChangesAsync();
            }
        }
    }

    // =============================================================
    // JOURNAL SUBSCRIPTION SERVICE
    // =============================================================
    public class JournalService : IJournalService
    {
        private readonly AppDbContext _context;

        public JournalService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> SubscribeAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;

            var normalizedEmail = email.Trim().ToLowerInvariant();
            var exists = await _context.JournalSubscriptions
                .AnyAsync(s => s.Email == normalizedEmail);

            if (exists) return false; // Already subscribed

            _context.JournalSubscriptions.Add(new Domain.Entities.JournalSubscription
            {
                Id = Guid.NewGuid(),
                Email = normalizedEmail,
                SubscribedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return true;
        }
    }

    // =============================================================
    // DATABASE MANAGEMENT SERVICE
    // =============================================================
    public class DatabaseManagementService : IDatabaseManagementService
    {
        private readonly AppDbContext _context;
        private readonly IServiceProvider _serviceProvider;

        public DatabaseManagementService(AppDbContext context, IServiceProvider serviceProvider)
        {
            _context = context;
            _serviceProvider = serviceProvider;
        }

        public async Task ResetDatabaseAsync()
        {
            await _context.Database.EnsureDeletedAsync();
            await SeedData.InitializeAsync(_serviceProvider);
        }
    }

    // =============================================================
    // ENQUIRY SERVICE
    // =============================================================
    public class EnquiryService : IEnquiryService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<EnquiryService> _logger;

        public EnquiryService(AppDbContext context, ILogger<EnquiryService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<bool> SubmitEnquiryAsync(SubmitEnquiryDto dto)
        {
            try
            {
                var enquiry = new Enquiry
                {
                    Id = Guid.NewGuid(),
                    Name = dto.Name,
                    Email = dto.Email,
                    Phone = dto.Phone,
                    Subject = dto.Subject,
                    Message = dto.Message,
                    CreatedAt = DateTime.UtcNow,
                    IsResponded = false
                };

                _context.Enquiries.Add(enquiry);
                await _context.SaveChangesAsync();
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting enquiry.");
                return false;
            }
        }
    }
}