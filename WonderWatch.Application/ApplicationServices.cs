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

        public async Task<List<Watch>> GetAllAsync(WatchFilterDto filter)
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

            return await query.ToListAsync();
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
            return await _context.Watches
                .Where(w => w.IsPublished)
                .Select(w => w.Brand.ToUpper())
                .Distinct()
                .OrderBy(b => b)
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

            foreach (var item in dto.Items)
            {
                var watch = await _context.Watches.FindAsync(item.WatchId)
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
            var order = await _context.Orders.FindAsync(orderId)
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
                var items = await _context.OrderItems.Where(i => i.OrderId == orderId).ToListAsync();
                foreach (var item in items)
                {
                    var watch = await _context.Watches.FindAsync(item.WatchId);
                    if (watch != null)
                    {
                        watch.StockQuantity += item.Quantity;
                        watch.IsSoldOut = watch.StockQuantity == 0;
                    }
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Order {OrderId} transitioned from {OldStatus} to {NewStatus}", orderId, oldStatus, newStatus);
        }

        public async Task BulkTransitionAsync(List<Guid> orderIds, OrderStatus status)
        {
            foreach (var id in orderIds)
            {
                await TransitionStatusAsync(id, status);
            }
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

        public EmailService(ILogger<EmailService> logger)
        {
            _logger = logger;
        }

        public Task SendOrderConfirmationAsync(Order order)
        {
            // In a production environment, this would integrate with SendGrid/AWS SES.
            // For this blueprint, we log the action to satisfy the interface without throwing NotImplementedException.
            _logger.LogInformation("Order Confirmation Email queued for Order {OrderId}", order.Id);
            return Task.CompletedTask;
        }

        public Task SendShippingUpdateAsync(Order order)
        {
            _logger.LogInformation("Shipping Update Email queued for Order {OrderId} with Status {Status}", order.Id, order.Status);
            return Task.CompletedTask;
        }
    }
}