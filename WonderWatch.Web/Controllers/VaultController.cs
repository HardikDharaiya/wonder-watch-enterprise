using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WonderWatch.Application.DTOs;
using WonderWatch.Application.Interfaces;
using WonderWatch.Domain.Identity;
using WonderWatch.Web.ViewModels;

namespace WonderWatch.Web.Controllers
{
    [Authorize]
    [Route("vault")]
    public class VaultController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IOrderService _orderService;
        private readonly IWishlistService _wishlistService;

        public VaultController(
            UserManager<ApplicationUser> userManager,
            IOrderService orderService,
            IWishlistService wishlistService)
        {
            _userManager = userManager;
            _orderService = orderService;
            _wishlistService = wishlistService;
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
                    OrderNumber = $"#WW-{recentOrder.Id.ToString().Substring(0, 8).ToUpper()}",
                    Date = recentOrder.CreatedAt,
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
        public async Task<IActionResult> Orders()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var orders = await _orderService.GetUserOrdersAsync(user.Id);
            var indiaCulture = new CultureInfo("hi-IN");

            var viewModel = new VaultOrdersViewModel
            {
                Orders = orders.Select(o => new OrderSummaryDto
                {
                    Id = o.Id,
                    OrderNumber = $"#WW-{o.Id.ToString().Substring(0, 8).ToUpper()}",
                    Date = o.CreatedAt,
                    TotalFormatted = o.TotalAmount.ToString("C0", indiaCulture),
                    Status = o.Status,
                    ItemCount = o.Items.Sum(i => i.Quantity),
                    ImageUrl = o.Items.FirstOrDefault()?.Watch.Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.Path ?? "/images/placeholder.webp",
                    WatchName = o.Items.Count == 1 ? o.Items.First().Watch.Name : $"{o.Items.First().Watch.Name} + {o.Items.Count - 1} more"
                }).ToList()
            };

            return View(viewModel);
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

        [HttpGet("profile")]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var viewModel = new VaultProfileViewModel
            {
                FullName = user.FullName,
                DisplayName = user.DisplayName,
                Email = user.Email ?? string.Empty,
                Nationality = user.Nationality,
                DateOfBirth = user.DateOfBirth,
                MemberSince = user.MemberSince // FIXED: Added MemberSince mapping
            };

            return View(viewModel);
        }

        [HttpGet("addresses")]
        public IActionResult Addresses()
        {
            return View(new VaultAddressesViewModel());
        }

        [HttpGet("notifications")]
        public IActionResult Notifications()
        {
            return View(new VaultNotificationsViewModel());
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
        public List<OrderSummaryDto> Orders { get; set; } = new();
    }

    public class VaultWishlistViewModel
    {
        public List<WatchCardDto> Watches { get; set; } = new();
    }

    public class VaultProfileViewModel
    {
        public string FullName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Nationality { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }
        public DateTime MemberSince { get; set; } // FIXED: Added MemberSince property
    }

    public class VaultAddressesViewModel
    {
        // Placeholder for address management
    }

    public class VaultNotificationsViewModel
    {
        // Placeholder for notification preferences
    }
}