using System;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WonderWatch.Domain.Entities;
using WonderWatch.Domain.Identity;

namespace WonderWatch.Infrastructure
{
    public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Watch> Watches => Set<Watch>();
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderItem> OrderItems => Set<OrderItem>();
        public DbSet<Review> Reviews => Set<Review>();
        public DbSet<Wishlist> Wishlists => Set<Wishlist>();
        public DbSet<WatchImage> WatchImages => Set<WatchImage>();
        public DbSet<UserAddress> UserAddresses => Set<UserAddress>();
        public DbSet<UserNotification> UserNotifications => Set<UserNotification>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // ---------------------------------------------------------
            // IDENTITY CONFIGURATION
            // ---------------------------------------------------------
            builder.Entity<ApplicationUser>(b =>
            {
                b.HasMany(u => u.Orders)
                 .WithOne()
                 .HasForeignKey(o => o.UserId)
                 .OnDelete(DeleteBehavior.Restrict);

                b.HasMany(u => u.Wishlist)
                 .WithOne()
                 .HasForeignKey(w => w.UserId)
                 .OnDelete(DeleteBehavior.Cascade);

                b.HasMany(u => u.Reviews)
                 .WithOne()
                 .HasForeignKey(r => r.UserId)
                 .OnDelete(DeleteBehavior.Cascade);

                b.HasMany(u => u.Addresses)
                 .WithOne()
                 .HasForeignKey(a => a.UserId)
                 .OnDelete(DeleteBehavior.Cascade);

                b.HasMany(u => u.Notifications)
                 .WithOne()
                 .HasForeignKey(n => n.UserId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // ---------------------------------------------------------
            // WATCH CONFIGURATION
            // ---------------------------------------------------------
            builder.Entity<Watch>(b =>
            {
                b.HasKey(w => w.Id);
                b.HasIndex(w => w.Slug).IsUnique();
                b.HasIndex(w => w.ReferenceNumber).IsUnique();

                b.Property(w => w.Name).IsRequired().HasMaxLength(200);
                b.Property(w => w.Brand).IsRequired().HasMaxLength(100);
                b.Property(w => w.ReferenceNumber).IsRequired().HasMaxLength(50);
                b.Property(w => w.Slug).IsRequired().HasMaxLength(250);

                b.Property(w => w.RetailPrice).HasPrecision(18, 2);
                b.Property(w => w.CostPrice).HasPrecision(18, 2);
                b.Property(w => w.ComparePrice).HasPrecision(18, 2);

                b.HasMany(w => w.Images)
                 .WithOne(i => i.Watch)
                 .HasForeignKey(i => i.WatchId)
                 .OnDelete(DeleteBehavior.Cascade);

                b.HasMany(w => w.Reviews)
                 .WithOne(r => r.Watch)
                 .HasForeignKey(r => r.WatchId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // ---------------------------------------------------------
            // WATCH IMAGE CONFIGURATION
            // ---------------------------------------------------------
            builder.Entity<WatchImage>(b =>
            {
                b.HasKey(i => i.Id);
                b.Property(i => i.Path).IsRequired().HasMaxLength(500);
            });

            // ---------------------------------------------------------
            // ORDER CONFIGURATION
            // ---------------------------------------------------------
            builder.Entity<Order>(b =>
            {
                b.HasKey(o => o.Id);
                b.Property(o => o.TotalAmount).HasPrecision(18, 2);
                b.Property(o => o.RazorpayOrderId).HasMaxLength(100);
                b.Property(o => o.RazorpayPaymentId).HasMaxLength(100);

                // Owned Entity for Shipping Address
                b.OwnsOne(o => o.ShippingAddress, a =>
                {
                    a.Property(p => p.Line1).IsRequired().HasMaxLength(200);
                    a.Property(p => p.Line2).HasMaxLength(200);
                    a.Property(p => p.City).IsRequired().HasMaxLength(100);
                    a.Property(p => p.State).IsRequired().HasMaxLength(100);
                    a.Property(p => p.PinCode).IsRequired().HasMaxLength(20);
                    a.Property(p => p.Phone).IsRequired().HasMaxLength(20);
                });

                b.HasMany(o => o.Items)
                 .WithOne(i => i.Order)
                 .HasForeignKey(i => i.OrderId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // ---------------------------------------------------------
            // ORDER ITEM CONFIGURATION
            // ---------------------------------------------------------
            builder.Entity<OrderItem>(b =>
            {
                b.HasKey(i => i.Id);
                b.Property(i => i.UnitPrice).HasPrecision(18, 2);

                b.HasOne(i => i.Watch)
                 .WithMany()
                 .HasForeignKey(i => i.WatchId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ---------------------------------------------------------
            // WISHLIST CONFIGURATION
            // ---------------------------------------------------------
            builder.Entity<Wishlist>(b =>
            {
                b.HasKey(w => w.Id);

                // Prevent duplicate wishlisting of the same watch by the same user
                b.HasIndex(w => new { w.UserId, w.WatchId }).IsUnique();

                b.HasOne(w => w.Watch)
                 .WithMany()
                 .HasForeignKey(w => w.WatchId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // ---------------------------------------------------------
            // REVIEW CONFIGURATION
            // ---------------------------------------------------------
            builder.Entity<Review>(b =>
            {
                b.HasKey(r => r.Id);
                b.Property(r => r.Body).IsRequired().HasMaxLength(2000);
                b.Property(r => r.Rating).IsRequired();
            });

            // ---------------------------------------------------------
            // USER ADDRESS CONFIGURATION
            // ---------------------------------------------------------
            builder.Entity<UserAddress>(b =>
            {
                b.HasKey(a => a.Id);
                b.Property(a => a.Label).IsRequired().HasMaxLength(50);
                b.Property(a => a.FullName).IsRequired().HasMaxLength(200);
                b.Property(a => a.Line1).IsRequired().HasMaxLength(200);
                b.Property(a => a.Line2).HasMaxLength(200);
                b.Property(a => a.City).IsRequired().HasMaxLength(100);
                b.Property(a => a.State).IsRequired().HasMaxLength(100);
                b.Property(a => a.PinCode).IsRequired().HasMaxLength(20);
                b.Property(a => a.Country).IsRequired().HasMaxLength(100);
                b.Property(a => a.Phone).IsRequired().HasMaxLength(20);
                b.HasIndex(a => a.UserId);
            });

            // ---------------------------------------------------------
            // USER NOTIFICATION CONFIGURATION
            // ---------------------------------------------------------
            builder.Entity<UserNotification>(b =>
            {
                b.HasKey(n => n.Id);
                b.Property(n => n.Title).IsRequired().HasMaxLength(200);
                b.Property(n => n.Body).IsRequired().HasMaxLength(1000);
                b.HasIndex(n => n.UserId);
                b.HasIndex(n => new { n.UserId, n.IsRead });
            });
        }
    }
}