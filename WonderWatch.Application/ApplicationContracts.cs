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
        public bool IsPayOnDelivery { get; set; } = false;
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
        public string OrderNumber { get; set; } = string.Empty; // e.g., "#1A2B3C4"
        public DateTime Date { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string TotalFormatted { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public OrderStatus Status { get; set; }
        public int ItemCount { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string WatchName { get; set; } = string.Empty;
        public bool IsPayOnDelivery { get; set; }
    }

    // ---------------------------------------------------------
    // ADDRESS DTOS
    // ---------------------------------------------------------
    public class UserAddressDto
    {
        public Guid Id { get; set; }
        public string Label { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Line1 { get; set; } = string.Empty;
        public string Line2 { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string PinCode { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateAddressDto
    {
        public string Label { get; set; } = "Home";
        public string FullName { get; set; } = string.Empty;
        public string Line1 { get; set; } = string.Empty;
        public string Line2 { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string PinCode { get; set; } = string.Empty;
        public string Country { get; set; } = "India";
        public string Phone { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
    }

    // ---------------------------------------------------------
    // NOTIFICATION DTOS
    // ---------------------------------------------------------
    public class NotificationDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class FilterConfigDto
    {
        public decimal MinPrice { get; set; }
        public decimal MaxPrice { get; set; }
    }

    // ---------------------------------------------------------
    // MEMBERSHIP PLAN DTOS
    // ---------------------------------------------------------
    public class MembershipPlanDto
    {
        public Guid Id { get; set; }
        public MembershipTier Tier { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string PriceFormatted { get; set; } = string.Empty;
        public string BillingCycle { get; set; } = string.Empty;
        public List<string> Features { get; set; } = new();
        public bool IsActive { get; set; }
        public int SubscriberCount { get; set; }
    }
    
    public class CreateMembershipPlanDto
    {
        public MembershipTier Tier { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string BillingCycle { get; set; } = "One-Time";
        public List<string> Features { get; set; } = new();
        public bool IsActive { get; set; } = true;
    }

    public class SubmitEnquiryDto
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}

namespace WonderWatch.Application.Interfaces
{
    using WonderWatch.Application.DTOs;

    public interface ICatalogService
    {
        Task<(List<Watch> Watches, int TotalCount)> GetAllAsync(WatchFilterDto filter, int page = 1, int pageSize = 12);
        Task<Watch?> GetByIdAsync(Guid id);
        Task<Watch?> GetBySlugAsync(string slug);
        Task<List<Watch>> SearchAsync(string query);

        Task<List<string>> GetAvailableBrandsAsync();
        Task<List<int>> GetAvailableCaseSizesAsync();

        // FIXED: Added interface method for dynamic strap materials
        Task<List<string>> GetAvailableStrapMaterialsAsync();

        Task<FilterConfigDto> GetFilterConfigAsync();
    }

    public interface IOrderService
    {
        Task<Order> CreateOrderAsync(Guid userId, CreateOrderDto dto);
        Task TransitionStatusAsync(Guid orderId, OrderStatus status);
        Task BulkTransitionAsync(List<Guid> orderIds, OrderStatus status);
        Task<List<Order>> GetUserOrdersAsync(Guid userId);
        Task<Order?> GetOrderByIdAsync(Guid orderId, Guid userId);
        Task PayPendingOrderAsync(Guid orderId, Guid userId, string razorpayPaymentId);
        Task ConfirmDeliveryAsync(Guid orderId, Guid userId);
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
        Task SendTestEmailAsync(string toEmail);
        Task SendOtpAsync(string toEmail, string otp, string purpose);
    }

    public interface IAddressService
    {
        Task<List<UserAddressDto>> GetByUserAsync(Guid userId);
        Task<UserAddressDto> AddAsync(Guid userId, CreateAddressDto dto);
        Task UpdateAsync(Guid addressId, Guid userId, CreateAddressDto dto);
        Task DeleteAsync(Guid addressId, Guid userId);
        Task SetDefaultAsync(Guid addressId, Guid userId);
    }

    public interface INotificationService
    {
        Task<List<NotificationDto>> GetByUserAsync(Guid userId);
        Task MarkAllReadAsync(Guid userId);
        Task MarkReadAsync(Guid notificationId, Guid userId);
        Task<int> GetUnreadCountAsync(Guid userId);
        Task CreateAsync(Guid userId, string title, string body, NotificationType type);
    }

    public interface IMembershipService
    {
        Task<List<MembershipPlanDto>> GetActivePlansAsync();
        Task<List<MembershipPlanDto>> GetAllPlansAsync();
        Task<MembershipPlanDto?> GetPlanByIdAsync(Guid id);
        Task<MembershipPlanDto> CreatePlanAsync(CreateMembershipPlanDto dto);
        Task UpdatePlanAsync(Guid id, CreateMembershipPlanDto dto);
        Task TogglePlanActiveAsync(Guid id);
        Task DeletePlanAsync(Guid id);
        Task UpgradeUserPlanAsync(Guid userId, Guid planId);
    }

    public interface IJournalService
    {
        Task<bool> SubscribeAsync(string email);
    }

    public interface IDatabaseManagementService
    {
        Task ResetDatabaseAsync();
    }

    public interface IEnquiryService
    {
        Task<bool> SubmitEnquiryAsync(SubmitEnquiryDto dto);
    }
}