using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WonderWatch.Application.DTOs;
using WonderWatch.Application.Interfaces;
using WonderWatch.Web.ViewModels;

namespace WonderWatch.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ICatalogService _catalogService;
        private readonly ILogger<HomeController> _logger;

        public HomeController(ICatalogService catalogService, ILogger<HomeController> logger)
        {
            _catalogService = catalogService;
            _logger = logger;
        }[HttpGet]
        public async Task<IActionResult> Index()
        {
            var filter = new WatchFilterDto { SortBy = "newest" };
            var watches = await _catalogService.GetAllAsync(filter);
            var indiaCulture = new CultureInfo("hi-IN");
            
            var featuredWatches = watches.Take(3).Select(w => new WatchCardDto
            {
                Id = w.Id,
                Name = w.Name,
                Brand = w.Brand,
                Slug = w.Slug,
                PriceFormatted = w.RetailPrice.ToString("C0", indiaCulture),
                ImageUrl = w.Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.Path ?? "/images/placeholder.webp",
                IsSoldOut = w.IsSoldOut
            }).ToList();

            return View(featuredWatches);
        }

        [HttpGet]
        [Route("collections")]
        public IActionResult Collections()
        {
            return View();
        }

        [HttpGet]
        [Route("about")]
        public IActionResult About()
        {
            return View();
        }[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            _logger.LogError("An error occurred. RequestId: {RequestId}", Activity.Current?.Id ?? HttpContext.TraceIdentifier);
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}

namespace WonderWatch.Web.ViewModels
{
    public class ErrorViewModel
    {
        public string? RequestId { get; set; }
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}
