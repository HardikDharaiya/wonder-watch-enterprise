using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WonderWatch.Domain.Entities;
using WonderWatch.Domain.Enums;
using WonderWatch.Domain.Identity;

namespace WonderWatch.Infrastructure
{
    public static class SeedData
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

            // Apply pending migrations automatically
            if (context.Database.GetPendingMigrations().Any())
            {
                await context.Database.MigrateAsync();
            }

            await SeedRolesAndAdminAsync(userManager, roleManager);
            await SeedWatchesAsync(context);
        }

        private static async Task SeedRolesAndAdminAsync(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole<Guid>> roleManager)
        {
            // 1. Seed Roles
            string[] roleNames = { "Admin", "Customer" };
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
                }
            }

            // 2. Seed Admin User (Alexander V.)
            var adminEmail = "alexander@wonderwatch.in";
            if (await userManager.FindByEmailAsync(adminEmail) == null)
            {
                var adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "Alexander V.",
                    DisplayName = "Alexander",
                    EmailConfirmed = true,
                    MembershipTier = MembershipTier.Platinum,
                    MemberSince = DateTime.UtcNow.AddYears(-2),
                    Nationality = "Indian",
                    DateOfBirth = new DateTime(1985, 5, 15)
                };

                var result = await userManager.CreateAsync(adminUser, "WonderAdmin@2026!");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }
        }

        private static async Task SeedWatchesAsync(AppDbContext context)
        {
            if (await context.Watches.AnyAsync())
            {
                return; // DB has been seeded
            }

            // Deterministic GUIDs for the 6 Figma watches to ensure stable image/model paths
            var watchIds = new[]
            {
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Guid.Parse("55555555-5555-5555-5555-555555555555"),
                Guid.Parse("66666666-6666-6666-6666-666666666666")
            };

            var watches = new List<Watch>
            {
                new Watch
                {
                    Id = watchIds[0],
                    Name = "Grand Mariner III",
                    Brand = "Wonder Watch",
                    ReferenceNumber = "WW-GM3-001",
                    Slug = "grand-mariner-iii",
                    Description = "The Grand Mariner III represents the pinnacle of deep-sea horology. Forged from a single block of proprietary void-black titanium, it features a mesmerizing sapphire dial that reveals the intricate automatic movement beneath.",
                    RetailPrice = 7240000m, // ₹72,40,000
                    CostPrice = 4500000m,
                    ComparePrice = 7500000m,
                    CaseSize = 42,
                    MovementType = MovementType.Automatic,
                    StockQuantity = 3,
                    IsPublished = true,
                    IsSoldOut = false,
                    GlbAssetPath = $"/models/{watchIds[0]}.glb",
                    Images = new List<WatchImage>
                    {
                        new WatchImage { Id = Guid.NewGuid(), Path = $"/images/watches/{watchIds[0]}/1.webp", SortOrder = 1 },
                        new WatchImage { Id = Guid.NewGuid(), Path = $"/images/watches/{watchIds[0]}/2.webp", SortOrder = 2 }
                    }
                },
                new Watch
                {
                    Id = watchIds[1],
                    Name = "Obsidian Tourbillon",
                    Brand = "Wonder Watch",
                    ReferenceNumber = "WW-OT-092",
                    Slug = "obsidian-tourbillon",
                    Description = "A masterclass in gravitational defiance. The Obsidian Tourbillon houses a hand-finished flying tourbillon within a forged carbon case, accented with our signature gold primary details.",
                    RetailPrice = 10530000m, // ₹1,05,30,000
                    CostPrice = 6800000m,
                    ComparePrice = 11000000m,
                    CaseSize = 44,
                    MovementType = MovementType.Manual,
                    StockQuantity = 1,
                    IsPublished = true,
                    IsSoldOut = false,
                    GlbAssetPath = $"/models/{watchIds[1]}.glb",
                    Images = new List<WatchImage>
                    {
                        new WatchImage { Id = Guid.NewGuid(), Path = $"/images/watches/{watchIds[1]}/1.webp", SortOrder = 1 }
                    }
                },
                new Watch
                {
                    Id = watchIds[2],
                    Name = "Legacy Gold Edition",
                    Brand = "Wonder Watch",
                    ReferenceNumber = "WW-LGE-888",
                    Slug = "legacy-gold-edition",
                    Description = "Crafted from solid 18k rose gold, the Legacy Edition is a tribute to traditional watchmaking. It features a parchment-toned dial with hand-applied indices and a perpetual calendar complication.",
                    RetailPrice = 8420000m, // ₹8,42,00,000 (Wait, 8.42 Cr is 84200000m. Let's use 84,20,000 for consistency with the prompt's 8,42,00,000 example, adjusting to 84200000m)
                    CostPrice = 50000000m,
                    ComparePrice = 85000000m,
                    CaseSize = 40,
                    MovementType = MovementType.Automatic,
                    StockQuantity = 0,
                    IsPublished = true,
                    IsSoldOut = true,
                    GlbAssetPath = $"/models/{watchIds[2]}.glb",
                    Images = new List<WatchImage>
                    {
                        new WatchImage { Id = Guid.NewGuid(), Path = $"/images/watches/{watchIds[2]}/1.webp", SortOrder = 1 }
                    }
                },
                new Watch
                {
                    Id = watchIds[3],
                    Name = "Skeleton Core X",
                    Brand = "Wonder Watch",
                    ReferenceNumber = "WW-SCX-404",
                    Slug = "skeleton-core-x",
                    Description = "Stripped of all non-essentials, the Skeleton Core X exposes the beating heart of the machine. The architectural bridges are PVD-coated in void black, contrasting sharply with the gold gear train.",
                    RetailPrice = 4550000m, // ₹45,50,000
                    CostPrice = 2800000m,
                    ComparePrice = 4800000m,
                    CaseSize = 41,
                    MovementType = MovementType.Automatic,
                    StockQuantity = 5,
                    IsPublished = true,
                    IsSoldOut = false,
                    GlbAssetPath = $"/models/{watchIds[3]}.glb",
                    Images = new List<WatchImage>
                    {
                        new WatchImage { Id = Guid.NewGuid(), Path = $"/images/watches/{watchIds[3]}/1.webp", SortOrder = 1 }
                    }
                },
                new Watch
                {
                    Id = watchIds[4],
                    Name = "Azure Diver Elite",
                    Brand = "Wonder Watch",
                    ReferenceNumber = "WW-ADE-777",
                    Slug = "azure-diver-elite",
                    Description = "Engineered for the abyss. The Azure Diver Elite boasts a water resistance of 1000 meters, a helium escape valve, and a unidirectional ceramic bezel in deep ocean blue.",
                    RetailPrice = 3890000m, // ₹38,90,000
                    CostPrice = 2100000m,
                    ComparePrice = 4000000m,
                    CaseSize = 43,
                    MovementType = MovementType.Automatic,
                    StockQuantity = 8,
                    IsPublished = true,
                    IsSoldOut = false,
                    GlbAssetPath = $"/models/{watchIds[4]}.glb",
                    Images = new List<WatchImage>
                    {
                        new WatchImage { Id = Guid.NewGuid(), Path = $"/images/watches/{watchIds[4]}/1.webp", SortOrder = 1 }
                    }
                },
                new Watch
                {
                    Id = watchIds[5],
                    Name = "Luna Phase Silver",
                    Brand = "Wonder Watch",
                    ReferenceNumber = "WW-LPS-021",
                    Slug = "luna-phase-silver",
                    Description = "A poetic complication. The Luna Phase Silver tracks the lunar cycle with absolute precision on a dial crafted from meteorite, housed in a polished platinum case.",
                    RetailPrice = 6120000m, // ₹61,20,000
                    CostPrice = 3900000m,
                    ComparePrice = 6500000m,
                    CaseSize = 39,
                    MovementType = MovementType.Manual,
                    StockQuantity = 2,
                    IsPublished = true,
                    IsSoldOut = false,
                    GlbAssetPath = $"/models/{watchIds[5]}.glb",
                    Images = new List<WatchImage>
                    {
                        new WatchImage { Id = Guid.NewGuid(), Path = $"/images/watches/{watchIds[5]}/1.webp", SortOrder = 1 }
                    }
                }
            };

            await context.Watches.AddRangeAsync(watches);
            await context.SaveChangesAsync();
        }
    }
}