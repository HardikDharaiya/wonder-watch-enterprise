using System;
using System.Collections.Generic;
using WonderWatch.Domain.Enums;

namespace WonderWatch.Domain.Enums
{
    public enum MovementType
    {
        Automatic,
        Manual
    }

    public enum OrderStatus
    {
        Pending,
        Paid,
        Processing,
        Shipped,
        Delivered,
        Cancelled
    }

    public enum ReviewStatus
    {
        Pending,
        Approved,
        Rejected
    }

    public enum NotificationType
    {
        Order,
        Offer,
        System
    }

    public enum MembershipTier
    {
        Silver,
        Gold,
        Platinum
    }
}

namespace WonderWatch.Domain.Entities
{
    public class Watch
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string ReferenceNumber { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal RetailPrice { get; set; }
        public decimal CostPrice { get; set; }
        public decimal ComparePrice { get; set; }
        public int CaseSize { get; set; }
        public MovementType MovementType { get; set; }
        public int StockQuantity { get; set; }
        public bool IsPublished { get; set; }
        public bool IsSoldOut { get; set; }
        public string GlbAssetPath { get; set; } = string.Empty;

        // FIXED: Added StrapMaterial to support dynamic UI filtering
        public string StrapMaterial { get; set; } = string.Empty;

        public List<WatchImage> Images { get; set; } = new();
        public List<Review> Reviews { get; set; } = new();
    }

    public class Order
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public List<OrderItem> Items { get; set; } = new();
        public OrderStatus Status { get; set; }
        public string RazorpayOrderId { get; set; } = string.Empty;
        public string RazorpayPaymentId { get; set; } = string.Empty;
        public Address ShippingAddress { get; set; } = null!;
        public decimal TotalAmount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class OrderItem
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public Guid WatchId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }

        public Order Order { get; set; } = null!;
        public Watch Watch { get; set; } = null!;
    }

    public class Address
    {
        public string Line1 { get; set; } = string.Empty;
        public string Line2 { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string PinCode { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
    }

    public class Review
    {
        public Guid Id { get; set; }
        public Guid WatchId { get; set; }
        public Guid UserId { get; set; }
        public int Rating { get; set; }
        public string Body { get; set; } = string.Empty;
        public ReviewStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }

        public Watch Watch { get; set; } = null!;
    }

    public class Wishlist
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid WatchId { get; set; }
        public DateTime AddedAt { get; set; }

        public Watch Watch { get; set; } = null!;
    }

    public class WatchImage
    {
        public Guid Id { get; set; }
        public Guid WatchId { get; set; }
        public string Path { get; set; } = string.Empty;
        public int SortOrder { get; set; }

        public Watch Watch { get; set; } = null!;
    }

    /// <summary>
    /// Admin-controlled dictionary of brands shown in the catalog filter sidebar.
    /// NOT a FK on Watch — keeps existing Watch.Brand string field intact.
    /// </summary>
    public class Brand
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// Single-row configuration table for catalog filter bounds (price slider, etc.).
    /// Managed by the admin from the Settings/Filters panel.
    /// </summary>
    public class FilterConfig
    {
        public Guid Id { get; set; }
        public decimal MinPrice { get; set; }
        public decimal MaxPrice { get; set; }
    }
}