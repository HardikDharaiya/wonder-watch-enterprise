using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WonderWatch.Application.Interfaces;
using WonderWatch.Domain.Identity;

namespace WonderWatch.Web.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/wishlist")]
    public class WishlistApiController : ControllerBase
    {
        private readonly IWishlistService _wishlistService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<WishlistApiController> _logger;

        public WishlistApiController(
            IWishlistService wishlistService,
            UserManager<ApplicationUser> userManager,
            ILogger<WishlistApiController> logger)
        {
            _wishlistService = wishlistService;
            _userManager = userManager;
            _logger = logger;
        }

        [HttpPost("toggle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle([FromBody] ToggleWishlistRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized(new { success = false, error = "Authentication required." });
            }

            try
            {
                var isWishlisted = await _wishlistService.ToggleAsync(user.Id, request.WatchId);

                _logger.LogInformation("User {UserId} toggled wishlist for Watch {WatchId}. New state: {State}",
                    user.Id, request.WatchId, isWishlisted ? "Added" : "Removed");

                return Ok(new { success = true, isWishlisted = isWishlisted });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling wishlist for User {UserId} and Watch {WatchId}", user.Id, request.WatchId);
                return StatusCode(500, new { success = false, error = "An error occurred while updating your wishlist." });
            }
        }
    }

    public class ToggleWishlistRequest
    {
        public Guid WatchId { get; set; }
    }
}