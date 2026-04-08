using System;

namespace WonderWatch.Domain.Entities
{
    /// <summary>
    /// Represents a saved shipping address for a user.
    /// Supports CRUD operations from the Vault → Addresses module.
    /// </summary>
    public class UserAddress
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }

        /// <summary>"Home" | "Office" | "Other"</summary>
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
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
