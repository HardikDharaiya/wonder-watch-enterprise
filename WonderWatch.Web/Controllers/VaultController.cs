using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WonderWatch.Application.DTOs;
using WonderWatch.Application.Interfaces;
using WonderWatch.Domain.Identity;
using WonderWatch.Web.ViewModels;
using Microsoft.Extensions.Configuration;

namespace WonderWatch.Web.Controllers
{
    [Authorize]
    [Route("vault")]
    public class VaultController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IOrderService _orderService;
        private readonly IWishlistService _wishlistService;
        private readonly IAddressService _addressService;
        private readonly INotificationService _notificationService;
        private readonly IMembershipService _membershipService;
        private readonly IPaymentProvider _paymentProvider;
        private readonly IConfiguration _config;
        private readonly ILogger<VaultController> _logger;

        public VaultController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IOrderService orderService,
            IWishlistService wishlistService,
            IAddressService addressService,
            INotificationService notificationService,
            IMembershipService membershipService,
            IPaymentProvider paymentProvider,
            IConfiguration config,
            ILogger<VaultController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _orderService = orderService;
            _wishlistService = wishlistService;
            _addressService = addressService;
            _notificationService = notificationService;
            _membershipService = membershipService;
            _paymentProvider = paymentProvider;
            _config = config;
            _logger = logger;
        }

        [HttpGet("entry")]
        [AllowAnonymous]
        public IActionResult Entry()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index");
            }
            return View();
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var orders = await _orderService.GetUserOrdersAsync(user.Id);
            var wishlist = await _wishlistService.GetUserWishlistAsync(user.Id);
            var indiaCulture = new CultureInfo("hi-IN");

            // Calculate Time of Day in IST (UTC+5:30)
            var istZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            var istTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);
            var hour = istTime.Hour;

            string timeOfDay = "EVENING";
            if (hour >= 5 && hour < 12) timeOfDay = "MORNING";
            else if (hour >= 12 && hour < 17) timeOfDay = "AFTERNOON";

            var recentOrder = orders.FirstOrDefault();
            OrderSummaryDto? recentOrderDto = null;

            if (recentOrder != null)
            {
                var firstItem = recentOrder.Items.FirstOrDefault();
                recentOrderDto = new OrderSummaryDto
                {
                    Id = recentOrder.Id,
                    OrderNumber = $"#{recentOrder.Id.ToString().Substring(0, 7).ToUpper()}",
                    Date = recentOrder.CreatedAt,
                    UpdatedAt = recentOrder.UpdatedAt,
                    TotalFormatted = recentOrder.TotalAmount.ToString("C0", indiaCulture),
                    Status = recentOrder.Status,
                    ItemCount = recentOrder.Items.Sum(i => i.Quantity),
                    ImageUrl = firstItem?.Watch.Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.Path ?? "/images/placeholder.webp",
                    WatchName = firstItem?.Watch.Name ?? "Multiple Items"
                };
            }

            var wishlistPreview = wishlist.Take(3).Select(w => new WatchCardDto
            {
                Id = w.Id,
                Name = w.Name,
                Brand = w.Brand,
                Slug = w.Slug,
                PriceFormatted = w.RetailPrice.ToString("C0", indiaCulture),
                ImageUrl = w.Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.Path ?? "/images/placeholder.webp",
                IsSoldOut = w.IsSoldOut
            }).ToList();

            var viewModel = new VaultDashboardViewModel
            {
                FirstName = user.DisplayName,
                AvatarUrl = string.IsNullOrWhiteSpace(user.AvatarUrl) ? "https://ui-avatars.com/api/?name=" + Uri.EscapeDataString(user.DisplayName) + "&background=1a1a1a&color=C9A74A&bold=true&format=svg" : user.AvatarUrl,
                TimeOfDay = timeOfDay,
                MembershipTier = user.MembershipTier.ToString(),
                MemberSince = user.MemberSince,
                OrderCount = orders.Count,
                WishlistCount = wishlist.Count,
                RecentOrder = recentOrderDto,
                WishlistPreview = wishlistPreview
            };

            return View(viewModel);
        }
        [HttpGet("orders")]
        public async Task<IActionResult> Orders(string? filter)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var orders = await _orderService.GetUserOrdersAsync(user.Id);
            var indiaCulture = new CultureInfo("hi-IN");

            var viewModel = new VaultOrdersViewModel
            {
                ActiveFilter = filter?.ToUpper() ?? "ALL",
                RazorpayKeyId = _config["Razorpay:KeyId"] ?? string.Empty,
                Orders = orders.Select(o => new OrderSummaryDto
                {
                    Id = o.Id,
                    OrderNumber = $"#{o.Id.ToString().Substring(0, 7).ToUpper()}",
                    Date = o.CreatedAt,
                    UpdatedAt = o.UpdatedAt,
                    TotalFormatted = o.TotalAmount.ToString("C0", indiaCulture),
                    TotalAmount = o.TotalAmount,
                    Status = o.Status,
                    ItemCount = o.Items.Sum(i => i.Quantity),
                    ImageUrl = o.Items.FirstOrDefault()?.Watch.Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.Path ?? "/images/placeholder.webp",
                    WatchName = o.Items.Count == 1 ? o.Items.First().Watch.Name : $"{o.Items.First().Watch.Name} + {o.Items.Count - 1} more",
                    IsPayOnDelivery = o.IsPayOnDelivery
                }).ToList()
            };

            return View(viewModel);
        }

        [HttpPost("orders/{id}/confirm-delivery")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmDelivery(Guid id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            try
            {
                await _orderService.ConfirmDeliveryAsync(id, user.Id);
                await _notificationService.CreateAsync(user.Id, "Delivery Confirmed", "Your order has been confirmed as received. Thank you for your acquisition.", Domain.Enums.NotificationType.Order);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming delivery for order {OrderId}", id);
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("wishlist")]
        public async Task<IActionResult> Wishlist()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var wishlist = await _wishlistService.GetUserWishlistAsync(user.Id);
            var indiaCulture = new CultureInfo("hi-IN");

            var viewModel = new VaultWishlistViewModel
            {
                Count = wishlist.Count,
                Watches = wishlist.Select(w => new WatchCardDto
                {
                    Id = w.Id,
                    Name = w.Name,
                    Brand = w.Brand,
                    Slug = w.Slug,
                    PriceFormatted = w.RetailPrice.ToString("C0", indiaCulture),
                    ImageUrl = w.Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.Path ?? "/images/placeholder.webp",
                    IsSoldOut = w.IsSoldOut
                }).ToList()
            };

            return View(viewModel);
        }

        // =============================================================
        // PROFILE — GET + POST
        // =============================================================
        [HttpGet("profile")]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var viewModel = new VaultProfileViewModel
            {
                FullName = user.FullName,
                DisplayName = user.DisplayName,
                AvatarUrl = string.IsNullOrWhiteSpace(user.AvatarUrl) ? "https://ui-avatars.com/api/?name=" + Uri.EscapeDataString(user.DisplayName) + "&background=1a1a1a&color=C9A74A&bold=true&format=svg" : user.AvatarUrl,
                Email = user.Email ?? string.Empty,
                Nationality = user.Nationality,
                DateOfBirth = user.DateOfBirth,
                MemberSince = user.MemberSince
            };

            return View(viewModel);
        }

        [HttpPost("profile/update")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(string FullName, string DisplayName, string Nationality, DateTime DateOfBirth)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            user.FullName = FullName;
            user.DisplayName = DisplayName;
            user.Nationality = Nationality;
            user.DateOfBirth = DateOfBirth;

            await _userManager.UpdateAsync(user);
            TempData["ProfileSuccess"] = "Profile updated successfully.";
            return RedirectToAction("Profile");
        }

        [HttpPost("profile/password")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string CurrentPassword, string NewPassword, string ConfirmNewPassword)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (NewPassword != ConfirmNewPassword)
            {
                TempData["PasswordError"] = "New passwords do not match.";
                return RedirectToAction("Profile");
            }

            var result = await _userManager.ChangePasswordAsync(user, CurrentPassword, NewPassword);
            if (result.Succeeded)
            {
                TempData["PasswordSuccess"] = "Password updated successfully.";
            }
            else
            {
                TempData["PasswordError"] = string.Join(" ", result.Errors.Select(e => e.Description));
            }

            return RedirectToAction("Profile");
        }

        // =============================================================
        // ADDRESSES — GET + CRUD
        // =============================================================
        [HttpGet("addresses")]
        public async Task<IActionResult> Addresses()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var addresses = await _addressService.GetByUserAsync(user.Id);
            return View(new VaultAddressesViewModel { Addresses = addresses });
        }

        [HttpPost("addresses/add")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAddress(CreateAddressDto dto)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            await _addressService.AddAsync(user.Id, dto);
            TempData["AddressSuccess"] = "Address added successfully.";
            return RedirectToAction("Addresses");
        }

        [HttpPost("addresses/update/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAddress(Guid id, CreateAddressDto dto)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            await _addressService.UpdateAsync(id, user.Id, dto);
            TempData["AddressSuccess"] = "Address updated successfully.";
            return RedirectToAction("Addresses");
        }

        [HttpPost("addresses/delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAddress(Guid id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            await _addressService.DeleteAsync(id, user.Id);
            TempData["AddressSuccess"] = "Address removed.";
            return RedirectToAction("Addresses");
        }

        [HttpPost("addresses/default/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetDefaultAddress(Guid id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            await _addressService.SetDefaultAsync(id, user.Id);
            TempData["AddressSuccess"] = "Default address updated.";
            return RedirectToAction("Addresses");
        }

        // =============================================================
        // NOTIFICATIONS — GET + Mark Read
        // =============================================================
        [HttpGet("notifications")]
        public async Task<IActionResult> Notifications(string? filter)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var notifications = await _notificationService.GetByUserAsync(user.Id);
            var unreadCount = await _notificationService.GetUnreadCountAsync(user.Id);

            return View(new VaultNotificationsViewModel
            {
                Notifications = notifications,
                UnreadCount = unreadCount,
                ActiveFilter = filter?.ToUpper() ?? "ALL"
            });
        }

        [HttpPost("notifications/mark-all-read")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllRead()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            await _notificationService.MarkAllReadAsync(user.Id);
            return RedirectToAction("Notifications");
        }

        [HttpPost("notifications/mark-read/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkRead(Guid id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            await _notificationService.MarkReadAsync(id, user.Id);
            return RedirectToAction("Notifications");
        }

        // =============================================================
        // INVOICE (unchanged)
        // =============================================================
        [HttpGet("orders/{id}/invoice")]
        public async Task<IActionResult> Invoice(Guid id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var orders = await _orderService.GetUserOrdersAsync(user.Id);
            var order = orders.FirstOrDefault(o => o.Id == id);

            if (order == null) return NotFound();

            var indiaCulture = new CultureInfo("hi-IN");

            var viewModel = new VaultInvoiceViewModel
            {
                OrderId = order.Id,
                OrderNumber = $"#{order.Id.ToString().Substring(0, 7).ToUpper()}",
                OrderDate = order.CreatedAt,
                Status = order.Status.ToString(),
                CustomerName = user.FullName,
                CustomerEmail = user.Email ?? "",
                ShippingAddress = $"{order.ShippingAddress.Line1}, {order.ShippingAddress.Line2}, {order.ShippingAddress.City}, {order.ShippingAddress.State} - {order.ShippingAddress.PinCode}",
                ShippingPhone = order.ShippingAddress.Phone,
                Items = order.Items.Select(i => new InvoiceItemDto
                {
                    WatchName = i.Watch.Name,
                    Brand = i.Watch.Brand,
                    ReferenceNumber = i.Watch.ReferenceNumber,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice.ToString("C0", indiaCulture),
                    LineTotal = (i.UnitPrice * i.Quantity).ToString("C0", indiaCulture)
                }).ToList(),
                TotalFormatted = order.TotalAmount.ToString("C0", indiaCulture)
            };

            return View(viewModel);
        }

        // =============================================================
        // MEMBERSHIP — Upgrade Plan Page + Razorpay Payment
        // =============================================================
        [HttpGet("membership")]
        public async Task<IActionResult> Membership()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var plans = await _membershipService.GetActivePlansAsync();
            var razorpayKeyId = HttpContext.RequestServices
                .GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()["Razorpay:KeyId"] ?? "";

            var viewModel = new VaultMembershipViewModel
            {
                CurrentTier = user.MembershipTier.ToString(),
                Plans = plans,
                RazorpayKeyId = razorpayKeyId,
                UserEmail = user.Email ?? "",
                UserName = user.FullName
            };

            return View(viewModel);
        }

        [HttpPost("membership/create-order")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MembershipCreateOrder([FromBody] MembershipOrderRequest request)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return Unauthorized();

                var plan = await _membershipService.GetPlanByIdAsync(request.PlanId);
                if (plan == null) return BadRequest(new { error = "Plan not found" });

                var razorpayOrderId = await _paymentProvider.CreateRazorpayOrderAsync(plan.Price);

                return Json(new { orderId = razorpayOrderId, amount = plan.Price * 100 });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create membership order");
                return StatusCode(500, new { error = "Payment initialization failed" });
            }
        }

        [HttpPost("membership/verify-payment")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MembershipVerifyPayment([FromBody] MembershipPaymentVerification verification)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return Unauthorized();

                var isValid = _paymentProvider.VerifySignature(
                    verification.RazorpayOrderId,
                    verification.RazorpayPaymentId,
                    verification.RazorpaySignature);

                if (!isValid)
                    return BadRequest(new { error = "Payment verification failed" });

                await _membershipService.UpgradeUserPlanAsync(user.Id, verification.PlanId);

                _logger.LogInformation("User {UserId} upgraded to plan {PlanId}", user.Id, verification.PlanId);

                return Json(new { success = true, redirectUrl = "/vault/profile" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Membership payment verification failed");
                return StatusCode(500, new { error = "Verification failed" });
            }
        }

        // =============================================================
        // DELETE ACCOUNT
        // =============================================================
        [HttpPost("profile/delete-account")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAccount()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            await _signInManager.SignOutAsync();
            var result = await _userManager.DeleteAsync(user);

            if (result.Succeeded)
            {
                _logger.LogInformation("User {UserId} deleted their account.", user.Id);
                return RedirectToAction("Index", "Home");
            }

            _logger.LogWarning("Failed to delete user {UserId}: {Errors}", user.Id, string.Join(", ", result.Errors.Select(e => e.Description)));
            TempData["PasswordError"] = "Account deletion failed. Please contact support.";
            return RedirectToAction("Profile");
        }
    }
}

namespace WonderWatch.Web.ViewModels
{
    using System;
    using System.Collections.Generic;
    using WonderWatch.Application.DTOs;

    public class VaultDashboardViewModel
    {
        public string FirstName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string TimeOfDay { get; set; } = string.Empty;
        public string MembershipTier { get; set; } = string.Empty;
        public DateTime MemberSince { get; set; }
        public int OrderCount { get; set; }
        public int WishlistCount { get; set; }
        public OrderSummaryDto? RecentOrder { get; set; }
        public List<WatchCardDto> WishlistPreview { get; set; } = new();
    }

    public class VaultOrdersViewModel
    {
        public string ActiveFilter { get; set; } = "ALL";
        public string RazorpayKeyId { get; set; } = string.Empty;
        public List<OrderSummaryDto> Orders { get; set; } = new();
    }

    public class VaultWishlistViewModel
    {
        public int Count { get; set; }
        public List<WatchCardDto> Watches { get; set; } = new();
    }

    public class VaultProfileViewModel
    {
        public string FullName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Nationality { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }
        public DateTime MemberSince { get; set; }
    }

    public class VaultAddressesViewModel
    {
        public List<UserAddressDto> Addresses { get; set; } = new();
    }

    public class VaultNotificationsViewModel
    {
        public List<NotificationDto> Notifications { get; set; } = new();
        public int UnreadCount { get; set; }
        public string ActiveFilter { get; set; } = "ALL";
    }

    public class VaultInvoiceViewModel
    {
        public Guid OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string ShippingAddress { get; set; } = string.Empty;
        public string ShippingPhone { get; set; } = string.Empty;
        public List<InvoiceItemDto> Items { get; set; } = new();
        public string TotalFormatted { get; set; } = string.Empty;
    }

    public class InvoiceItemDto
    {
        public string WatchName { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string ReferenceNumber { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string UnitPrice { get; set; } = string.Empty;
        public string LineTotal { get; set; } = string.Empty;
    }

    public class VaultMembershipViewModel
    {
        public string CurrentTier { get; set; } = string.Empty;
        public List<MembershipPlanDto> Plans { get; set; } = new();
        public string RazorpayKeyId { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
    }

    public class MembershipOrderRequest
    {
        public Guid PlanId { get; set; }
    }

    public class MembershipPaymentVerification
    {
        public string RazorpayOrderId { get; set; } = string.Empty;
        public string RazorpayPaymentId { get; set; } = string.Empty;
        public string RazorpaySignature { get; set; } = string.Empty;
        public Guid PlanId { get; set; }
    }
}