using System;
using WonderWatch.Domain.Enums;

namespace WonderWatch.Domain.Entities
{
    /// <summary>
    /// Represents a single notification feed item for a user.
    /// Types: Order, Offer, System — filterable in the Vault UI.
    /// </summary>
    public class UserNotification
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public NotificationType Type { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
