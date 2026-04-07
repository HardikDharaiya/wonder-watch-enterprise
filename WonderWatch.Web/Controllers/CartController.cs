using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WonderWatch.Application.DTOs;
using WonderWatch.Application.Interfaces;

namespace WonderWatch.Web.Extensions
{
    /// <summary>
    /// Extension methods to handle JSON serialization for ISession.
    /// </summary>
    public static class SessionExtensions
    {
        public static void SetJson<T>(this ISession session, string key, T value)
        {
            session.SetString(key, JsonSerializer.Serialize(value));
        }

        public static T? GetJson<T>(this ISession session, string key)
        {
            var value = session.GetString(key);
            return value == null ? default : JsonSerializer.Deserialize<T>(value);
        }
    }
}

namespace WonderWatch.Web.Controllers
{
    using WonderWatch.Web.Extensions;

    [ApiController]
    [Route("api/cart")]
    public class CartController : ControllerBase
    {
        private const string CartSessionKey = "WonderWatch_Cart";
        private readonly ICatalogService _catalogService;
        private readonly ILogger<CartController> _logger;

        public CartController(ICatalogService catalogService, ILogger<CartController> logger)
        {
            _catalogService = catalogService;
            _logger = logger;
        }

        private List<CartItemDto> GetCart()
        {
            return HttpContext.Session.GetJson<List<CartItemDto>>(CartSessionKey) ?? new List<CartItemDto>();
        }

        private void SaveCart(List<CartItemDto> cart)
        {
            HttpContext.Session.SetJson(CartSessionKey, cart);
        }

        [HttpPost("add")]
        public IActionResult AddToCart([FromBody] AddToCartRequest request)
        {
            var cart = GetCart();
            var existingItem = cart.FirstOrDefault(c => c.WatchId == request.WatchId);

            if (existingItem != null)
            {
                existingItem.Quantity += request.Quantity;
            }
            else
            {
                cart.Add(new CartItemDto { WatchId = request.WatchId, Quantity = request.Quantity });
            }

            SaveCart(cart);
            _logger.LogInformation("Added Watch {WatchId} to cart. Total items: {Count}", request.WatchId, cart.Sum(c => c.Quantity));

            return Ok(new { success = true, count = cart.Sum(c => c.Quantity) });
        }

        [HttpPost("remove")]
        public IActionResult RemoveFromCart([FromBody] RemoveFromCartRequest request)
        {
            var cart = GetCart();
            var item = cart.FirstOrDefault(c => c.WatchId == request.WatchId);

            if (item != null)
            {
                cart.Remove(item);
                SaveCart(cart);
                _logger.LogInformation("Removed Watch {WatchId} from cart.", request.WatchId);
            }

            return Ok(new { success = true, count = cart.Sum(c => c.Quantity) });
        }
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            var cart = GetCart();
            var response = new CartSummaryResponse();
            var indiaCulture = new CultureInfo("hi-IN");
            decimal subtotal = 0;

            foreach (var item in cart)
            {
                var watch = await _catalogService.GetByIdAsync(item.WatchId);
                if (watch != null)
                {
                    var itemTotal = watch.RetailPrice * item.Quantity;
                    subtotal += itemTotal;

                    response.Items.Add(new CartItemDetailDto
                    {
                        WatchId = watch.Id,
                        Name = watch.Name,
                        Brand = watch.Brand,
                        Slug = watch.Slug,
                        Quantity = item.Quantity,
                        UnitPriceFormatted = watch.RetailPrice.ToString("C0", indiaCulture),
                        ItemTotalFormatted = itemTotal.ToString("C0", indiaCulture),
                        ImageUrl = watch.Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.Path ?? "/images/placeholder.webp"
                    });
                }
            }

            response.Count = cart.Sum(c => c.Quantity);
            response.SubtotalFormatted = subtotal.ToString("C0", indiaCulture);

            return Ok(response);
        }
    }

    // ---------------------------------------------------------
    // DTOs specific to the Cart API requests and responses
    // ---------------------------------------------------------

    public class AddToCartRequest
    {
        public Guid WatchId { get; set; }
        public int Quantity { get; set; } = 1;
    }

    public class RemoveFromCartRequest
    {
        public Guid WatchId { get; set; }
    }

    public class CartSummaryResponse
    {
        public List<CartItemDetailDto> Items { get; set; } = new();
        public string SubtotalFormatted { get; set; } = "₹0";
        public int Count { get; set; } = 0;
    }

    public class CartItemDetailDto
    {
        public Guid WatchId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string UnitPriceFormatted { get; set; } = string.Empty;
        public string ItemTotalFormatted { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
    }
}