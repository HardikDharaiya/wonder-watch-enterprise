using System;

namespace WonderWatch.Domain.Entities
{
    public class JournalSubscription
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public DateTime SubscribedAt { get; set; } = DateTime.UtcNow;
    }
}
