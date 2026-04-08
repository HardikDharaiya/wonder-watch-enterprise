using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;
using WonderWatch.Domain.Entities;
using WonderWatch.Domain.Enums;

namespace WonderWatch.Domain.Identity
{
    public class ApplicationUser : IdentityUser<Guid>
    {
        public string FullName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public MembershipTier MembershipTier { get; set; } = MembershipTier.Silver;
        public DateTime MemberSince { get; set; } = DateTime.UtcNow;
        public string Nationality { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }

        // Stored as JSON string to maintain pure POCO structure without complex relational mapping for dynamic preferences
        public string Preferences { get; set; } = "{}";

        // Navigation Properties
        public ICollection<Order> Orders { get; set; } = new List<Order>();
        public ICollection<Wishlist> Wishlist { get; set; } = new List<Wishlist>();
        public ICollection<Review> Reviews { get; set; } = new List<Review>();
        public ICollection<UserAddress> Addresses { get; set; } = new List<UserAddress>();
        public ICollection<UserNotification> Notifications { get; set; } = new List<UserNotification>();
    }
}