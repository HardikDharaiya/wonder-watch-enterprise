using System;
using System.Collections.Generic;
using WonderWatch.Domain.Enums;
using WonderWatch.Domain.Identity;

namespace WonderWatch.Domain.Entities
{
    public class MembershipPlan
    {
        public Guid Id { get; set; }
        public MembershipTier Tier { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string BillingCycle { get; set; } = string.Empty; // e.g. "Monthly", "Annual", "Lifetime"
        // Features stored as JSON array string of feature text items
        public string Features { get; set; } = "[]";
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property for users subscribed to this plan
        public ICollection<ApplicationUser> Subscribers { get; set; } = new List<ApplicationUser>();
    }
}
