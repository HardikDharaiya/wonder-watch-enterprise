using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WonderWatch.Application.DTOs;
using WonderWatch.Application.Interfaces;
using WonderWatch.Domain.Enums;
using WonderWatch.Domain.Identity;
using WonderWatch.Web.Extensions;
using WonderWatch.Web.ViewModels;

namespace WonderWatch.Web.Controllers
{
    [Authorize]
    [Route("checkout")]
    public class CheckoutController : Controller
    {
        private const string CartSessionKey = "WonderWatch_Cart";
        private readonly IOrderService _orderService;
        private readonly IPaymentProvider _paymentProvider;
        private readonly ICatalogService _catalogService;
        private readonly IAddressService _addressService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _config;
        private readonly ILogger<CheckoutController> _logger;

        public CheckoutController(
            IOrderService orderService,
            IPaymentProvider paymentProvider,
            ICatalogService catalogService,
            IAddressService addressService,
            UserManager<ApplicationUser> userManager,
            IConfiguration config,
            ILogger<CheckoutController> logger)
        {
            _orderService = orderService;
            _paymentProvider = paymentProvider;
            _catalogService = catalogService;
            _addressService = addressService;
            _userManager = userManager;
            _config = config;
            _logger = logger;
        }

        private List<CartItemDto> GetCart()
        {
            return HttpContext.Session.GetJson<List<CartItemDto>>(CartSessionKey) ?? new List<CartItemDto>();
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var cart = GetCart();
            if (!cart.Any())
            {
                return RedirectToAction("Index", "Catalog");
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var indiaCulture = new CultureInfo("hi-IN");
            decimal subtotal = 0;
            var items = new List<CheckoutItemViewModel>();

            foreach (var item in cart)
            {
                var watch = await _catalogService.GetByIdAsync(item.WatchId);
                if (watch != null)
                {
                    subtotal += watch.RetailPrice * item.Quantity;
                    items.Add(new CheckoutItemViewModel
                    {
                        WatchId = watch.Id,
                        WatchName = watch.Name,
                        Brand = watch.Brand,
                        Quantity = item.Quantity,
                        TotalFormatted = (watch.RetailPrice * item.Quantity).ToString("C0", indiaCulture),
                        ImageUrl = watch.Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.Path ?? "/images/placeholder.webp"
                    });
                }
            }

            var addresses = await _addressService.GetByUserAsync(user.Id);

            var viewModel = new CheckoutIndexViewModel
            {
                Items = items,
                SubtotalFormatted = subtotal.ToString("C0", indiaCulture),
                TotalAmount = subtotal,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                Phone = user.PhoneNumber ?? string.Empty,
                SavedAddresses = addresses
            };

            return View(viewModel);
        }

        [HttpPost("create-order")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
        {
            var cart = GetCart();
            if (!cart.Any()) return BadRequest(new { error = "Cart is empty." });

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            try
            {
                // 1. Map to Application DTO
                var createOrderDto = new CreateOrderDto
                {
                    Line1 = request.Line1,
                    Line2 = request.Line2 ?? string.Empty,
                    City = request.City,
                    State = request.State,
                    PinCode = request.PinCode,
                    Phone = request.Phone,
                    IsPayOnDelivery = request.IsPayOnDelivery,
                    Items = cart
                };

                // 2. Calculate Order Total
                decimal totalAmount = 0;
                foreach (var item in cart)
                {
                    var watch = await _catalogService.GetByIdAsync(item.WatchId);
                    if (watch != null)
                    {
                        totalAmount += (watch.RetailPrice * item.Quantity);
                    }
                }

                if (totalAmount == 0) return BadRequest(new { error = "Invalid cart total." });

                // PAY ON DELIVERY FLOW
                if (request.IsPayOnDelivery)
                {
                    var order = await _orderService.CreateOrderAsync(user.Id, createOrderDto);
                    HttpContext.Session.Remove(CartSessionKey);
                    return Json(new { success = true, isPayOnDelivery = true, redirectUrl = $"/checkout/confirmation/{order.Id}" });
                }

                // STANDARD RAZORPAY FLOW
                HttpContext.Session.SetJson("WonderWatch_PendingCheckout", createOrderDto);
                var razorpayOrderId = await _paymentProvider.CreateRazorpayOrderAsync(totalAmount);
                var keyId = _config["Razorpay:KeyId"] ?? throw new InvalidOperationException("Razorpay KeyId missing.");

                return Json(new
                {
                    success = true,
                    isPayOnDelivery = false,
                    razorpayOrderId = razorpayOrderId,
                    amount = totalAmount * 100,
                    keyId = keyId,
                    prefill = new { name = user.FullName, email = user.Email, contact = request.Phone }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating order for user {UserId}", user.Id);
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("verify")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyPayment([FromBody] VerifyPaymentRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            try
            {
                var isValid = _paymentProvider.VerifySignature(request.RazorpayOrderId, request.RazorpayPaymentId, request.RazorpaySignature);

                if (!isValid)
                {
                    _logger.LogWarning("Payment verification failed. Invalid signature.");
                    return BadRequest(new { error = "Payment verification failed. Invalid signature." });
                }

                // 2. Retrieve pending order data from session
                var createOrderDto = HttpContext.Session.GetJson<CreateOrderDto>("WonderWatch_PendingCheckout");
                if (createOrderDto == null)
                {
                    return BadRequest(new { error = "Checkout session expired or missing." });
                }

                // 3. Create Order in Database (Status: Pending)
                var order = await _orderService.CreateOrderAsync(user.Id, createOrderDto);

                // 4. Transition Order Status to Paid
                await _orderService.TransitionStatusAsync(order.Id, OrderStatus.Paid);

                // 5. Clear the Cart and Pending Checkout
                HttpContext.Session.Remove(CartSessionKey);
                HttpContext.Session.Remove("WonderWatch_PendingCheckout");

                return Json(new { success = true, redirectUrl = $"/checkout/confirmation/{order.Id}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying payment for Razorpay Order {RazorpayOrderId}", request.RazorpayOrderId);
                return BadRequest(new { error = "An error occurred during payment verification." });
            }
        }

        [HttpGet("confirmation/{id}")]
        public async Task<IActionResult> Confirmation(Guid id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var orders = await _orderService.GetUserOrdersAsync(user.Id);
            var order = orders.FirstOrDefault(o => o.Id == id);

            if (order == null) return NotFound();

            // Security check: Ensure the order belongs to the current user
            if (order.UserId != user.Id) return Forbid();

            var indiaCulture = new CultureInfo("hi-IN");

            var viewModel = new OrderConfirmationViewModel
            {
                OrderId = order.Id,
                OrderNumber = $"#WW-{order.Id.ToString().Substring(0, 8).ToUpper()}",
                Status = order.Status.ToString(),
                TotalFormatted = order.TotalAmount.ToString("C0", indiaCulture),
                CreatedAt = order.CreatedAt,
                ShippingAddress = $"{order.ShippingAddress.Line1}, {order.ShippingAddress.City}, {order.ShippingAddress.State} {order.ShippingAddress.PinCode}",
                Items = order.Items.Select(i => new CheckoutItemViewModel
                {
                    WatchId = i.Watch.Id,
                    WatchName = i.Watch.Name,
                    Brand = i.Watch.Brand,
                    Quantity = i.Quantity,
                    TotalFormatted = (i.UnitPrice * i.Quantity).ToString("C0", indiaCulture),
                    ImageUrl = i.Watch.Images.OrderBy(img => img.SortOrder).FirstOrDefault()?.Path ?? "/images/placeholder.webp"
                }).ToList()
            };

            return View(viewModel);
        }
    }

    // ===========================================================
    // PAY UNPAID ORDERS (from Vault Orders page)
    // ===========================================================
    [Authorize]
    [Route("checkout")]
    public class PayUnpaidController : Controller
    {
        private readonly IOrderService _orderService;
        private readonly IPaymentProvider _paymentProvider;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _config;
        private readonly ILogger<PayUnpaidController> _logger;

        public PayUnpaidController(
            IOrderService orderService,
            IPaymentProvider paymentProvider,
            UserManager<ApplicationUser> userManager,
            IConfiguration config,
            ILogger<PayUnpaidController> logger)
        {
            _orderService = orderService;
            _paymentProvider = paymentProvider;
            _userManager = userManager;
            _config = config;
            _logger = logger;
        }

        [HttpPost("pay-unpaid/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePaymentForUnpaidOrder(Guid id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            try
            {
                var order = await _orderService.GetOrderByIdAsync(id, user.Id);
                if (order == null) return NotFound(new { error = "Order not found." });
                if (order.Status != OrderStatus.Pending && order.Status != OrderStatus.Delivered)
                    return BadRequest(new { error = "This order cannot be paid at this time." });

                var razorpayOrderId = await _paymentProvider.CreateRazorpayOrderAsync(order.TotalAmount);
                var keyId = _config["Razorpay:KeyId"] ?? throw new InvalidOperationException("Razorpay KeyId missing.");

                return Json(new
                {
                    success = true,
                    razorpayOrderId,
                    amount = order.TotalAmount * 100,
                    keyId,
                    prefill = new { name = user.FullName, email = user.Email, contact = user.PhoneNumber ?? "" }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating payment for unpaid order {OrderId}", id);
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("pay-unpaid/{id}/verify")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyUnpaidOrderPayment(Guid id, [FromBody] VerifyPaymentRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            try
            {
                var isValid = _paymentProvider.VerifySignature(request.RazorpayOrderId, request.RazorpayPaymentId, request.RazorpaySignature);
                if (!isValid) return BadRequest(new { error = "Payment verification failed. Invalid signature." });

                await _orderService.PayPendingOrderAsync(id, user.Id, request.RazorpayPaymentId);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying unpaid order payment {OrderId}", id);
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}

namespace WonderWatch.Web.ViewModels
{
    using System;
    using System.Collections.Generic;

    public class CheckoutIndexViewModel
    {
        public List<CheckoutItemViewModel> Items { get; set; } = new();
        public string SubtotalFormatted { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }

        // Pre-fill data
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        
        public List<WonderWatch.Application.DTOs.UserAddressDto> SavedAddresses { get; set; } = new();
    }

    public class CheckoutItemViewModel
    {
        public Guid WatchId { get; set; }
        public string WatchName { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string TotalFormatted { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
    }

    public class CreateOrderRequest
    {
        public string Line1 { get; set; } = string.Empty;
        public string? Line2 { get; set; }
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string PinCode { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public bool IsPayOnDelivery { get; set; } = false;
    }

    public class VerifyPaymentRequest
    {
        public string RazorpayOrderId { get; set; } = string.Empty;
        public string RazorpayPaymentId { get; set; } = string.Empty;
        public string RazorpaySignature { get; set; } = string.Empty;
    }

    public class OrderConfirmationViewModel
    {
        public Guid OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string TotalFormatted { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string ShippingAddress { get; set; } = string.Empty;
        public List<CheckoutItemViewModel> Items { get; set; } = new();
    }
}