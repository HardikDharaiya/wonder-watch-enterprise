using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using WonderWatch.Domain.Entities;
using WonderWatch.Domain.Enums;

namespace WonderWatch.Application.DTOs
{
    public class WatchFilterDto
    {
        public string? SearchQuery { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public MovementType? MovementType { get; set; }
        public string? SortBy { get; set; } // e.g., "price_asc", "price_desc", "newest"

        public string[]? Brands { get; set; }
        public int? CaseSize { get; set; }

        // FIXED: Added StrapMaterial to pass the filter from Controller to Service
        public string? StrapMaterial { get; set; }
    }

    public class CreateOrderDto
    {
        public string Line1 { get; set; } = string.Empty;
        public string Line2 { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string PinCode { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public List<CartItemDto> Items { get; set; } = new();
    }

    public class CartItemDto
    {
        public Guid WatchId { get; set; }
        public int Quantity { get; set; }
    }

    public class DashboardKpiDto
    {
        public decimal TotalRevenue { get; set; }
        public int OrdersPending { get; set; }
        public int ActiveUsers { get; set; }
        public int LowStockCount { get; set; }
    }

    public class WatchCardDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string PriceFormatted { get; set; } = string.Empty; // e.g., "₹72,40,000"
        public string ImageUrl { get; set; } = string.Empty;
        public bool IsSoldOut { get; set; }
    }

    public class OrderSummaryDto
    {
        public Guid Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty; // e.g., "#WW-8921"
        public DateTime Date { get; set; }
        public string TotalFormatted { get; set; } = string.Empty;
        public OrderStatus Status { get; set; }
        public int ItemCount { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string WatchName { get; set; } = string.Empty;
    }
}

namespace WonderWatch.Application.Interfaces
{
    using WonderWatch.Application.DTOs;

    public interface ICatalogService
    {
        Task<List<Watch>> GetAllAsync(WatchFilterDto filter);
        Task<Watch?> GetByIdAsync(Guid id);
        Task<Watch?> GetBySlugAsync(string slug);
        Task<List<Watch>> SearchAsync(string query);

        Task<List<string>> GetAvailableBrandsAsync();
        Task<List<int>> GetAvailableCaseSizesAsync();

        // FIXED: Added interface method for dynamic strap materials
        Task<List<string>> GetAvailableStrapMaterialsAsync();
    }

    public interface IOrderService
    {
        Task<Order> CreateOrderAsync(Guid userId, CreateOrderDto dto);
        Task TransitionStatusAsync(Guid orderId, OrderStatus status);
        Task BulkTransitionAsync(List<Guid> orderIds, OrderStatus status);
        Task<List<Order>> GetUserOrdersAsync(Guid userId);
    }

    public interface IPaymentProvider
    {
        Task<string> CreateRazorpayOrderAsync(decimal amount);
        bool VerifySignature(string orderId, string paymentId, string signature);
    }

    public interface IWishlistService
    {
        Task<bool> ToggleAsync(Guid userId, Guid watchId);
        Task<List<Watch>> GetUserWishlistAsync(Guid userId);
        Task<bool> IsWishlistedAsync(Guid userId, Guid watchId);
    }

    public interface IAssetService
    {
        Task<string> SaveGlbAsync(IFormFile file, Guid watchId);
        Task<List<string>> SaveImagesAsync(List<IFormFile> files, Guid watchId);
        Task DeleteAssetAsync(string path);
    }

    public interface IAdminService
    {
        Task<DashboardKpiDto> GetDashboardKPIsAsync();
        Task<List<Watch>> GetInventoryAlertsAsync(); // Returns watches where stock <= 4
        Task ModerateReviewAsync(Guid reviewId, ReviewStatus status);
    }

    public interface IEmailService
    {
        Task SendOrderConfirmationAsync(Order order);
        Task SendShippingUpdateAsync(Order order);
    }
}