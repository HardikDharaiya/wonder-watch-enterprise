using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WonderWatch.Application.DTOs;
using WonderWatch.Application.Interfaces;
using WonderWatch.Domain.Enums;
using WonderWatch.Web.ViewModels;

namespace WonderWatch.Web.Controllers
{
    public class CatalogController : Controller
    {
        private readonly ICatalogService _catalogService;

        public CatalogController(ICatalogService catalogService)
        {
            _catalogService = catalogService;
        }
        [HttpGet]
        [Route("catalog")]
        public async Task<IActionResult> Index(
            [FromQuery] string? searchQuery,
            [FromQuery] decimal? minPrice,
            [FromQuery] decimal? maxPrice,
            [FromQuery] MovementType? movementType,
            [FromQuery] string[]? brands,
            [FromQuery] string? size,
            [FromQuery] string? sort,
            [FromQuery] string? strap, // Added to capture UI state, even if not filtered in DB yet
            [FromQuery] int page = 1)
        {
            // 1. Build the filter DTO
            int? caseSize = null;
            if (!string.IsNullOrEmpty(size) && int.TryParse(size.Trim().ToUpperInvariant().Replace("MM", ""), out int parsedSize))
            {
                caseSize = parsedSize;
            }

            var filter = new WatchFilterDto
            {
                SearchQuery = searchQuery,
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                MovementType = movementType,
                SortBy = sort ?? "newest",
                Brands = brands,
                CaseSize = caseSize,
                StrapMaterial = strap // Pass the strap material to the DTO
            };

            int pageSize = 12;
            page = Math.Max(1, page);

            // 2. Fetch matching watches from the service (Service now handles all filtering and pagination)
            var (pagedWatches, totalItems) = await _catalogService.GetAllAsync(filter, page, pageSize);

            // 3. Fetch dynamic filter options for the UI
            var availableBrands = await _catalogService.GetAvailableBrandsAsync();
            var availableSizes = await _catalogService.GetAvailableCaseSizesAsync();
            var filterConfig = await _catalogService.GetFilterConfigAsync();

            // 4. Pagination Logic
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            page = Math.Max(1, Math.Min(page, totalPages == 0 ? 1 : totalPages));

            var indiaCulture = new CultureInfo("hi-IN");

            // 5. Construct the ViewModel
            var viewModel = new CatalogIndexViewModel
            {
                CurrentFilters = filter,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalItems = totalItems,
                AvailableBrands = availableBrands,
                AvailableSizes = availableSizes,
                PriceMin = filterConfig.MinPrice,
                PriceMax = filterConfig.MaxPrice,
                Watches = pagedWatches.Select(w => new WatchCardDto
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

        [HttpGet]
        [Route("catalog/{slug}")]
        public async Task<IActionResult> Detail(string slug)
        {
            var watch = await _catalogService.GetBySlugAsync(slug);

            if (watch == null)
            {
                return NotFound();
            }

            var indiaCulture = new CultureInfo("hi-IN");

            var viewModel = new WatchDetailViewModel
            {
                Id = watch.Id,
                Name = watch.Name,
                Brand = watch.Brand,
                ReferenceNumber = watch.ReferenceNumber,
                Description = watch.Description,
                PriceFormatted = watch.RetailPrice.ToString("C0", indiaCulture),
                ComparePriceFormatted = watch.ComparePrice.ToString("C0", indiaCulture),
                HasDiscount = watch.ComparePrice > watch.RetailPrice,
                CaseSize = watch.CaseSize,
                MovementType = watch.MovementType.ToString(),
                StrapMaterial = watch.StrapMaterial,
                StockQuantity = watch.StockQuantity,
                IsSoldOut = watch.IsSoldOut,
                GlbAssetPath = watch.GlbAssetPath,
                ImageUrls = watch.Images.OrderBy(i => i.SortOrder).Select(i => i.Path).ToList()
            };

            return View(viewModel);
        }
    }
}

namespace WonderWatch.Web.ViewModels
{
    using System.Collections.Generic;
    using WonderWatch.Application.DTOs;

    public class CatalogIndexViewModel
    {
        public WatchFilterDto CurrentFilters { get; set; } = new();
        public List<WatchCardDto> Watches { get; set; } = new();

        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int TotalItems { get; set; } = 0;

        public List<string> AvailableBrands { get; set; } = new();
        public List<int> AvailableSizes { get; set; } = new();

        /// <summary>Admin-configured lower bound for price slider</summary>
        public decimal PriceMin { get; set; }
        /// <summary>Admin-configured upper bound for price slider</summary>
        public decimal PriceMax { get; set; }
    }

    public class WatchDetailViewModel
    {
        public System.Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string ReferenceNumber { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string PriceFormatted { get; set; } = string.Empty;
        public string ComparePriceFormatted { get; set; } = string.Empty;
        public int CaseSize { get; set; }
        public string MovementType { get; set; } = string.Empty;
        public string StrapMaterial { get; set; } = string.Empty;
        public int StockQuantity { get; set; }
        public bool IsSoldOut { get; set; }
        public bool HasDiscount { get; set; }
        public string GlbAssetPath { get; set; } = string.Empty;
        public List<string> ImageUrls { get; set; } = new();
    }
}