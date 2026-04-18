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
            await SeedBrandsAndFilterConfigAsync(context);
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
                    Brand = "Rolex",
                    ReferenceNumber = "WW-GM3-001",
                    Slug = "grand-mariner-iii",
                    Description = "The Grand Mariner III represents the pinnacle of deep-sea horology. Forged from a single block of proprietary void-black titanium, it features a mesmerizing sapphire dial that reveals the intricate automatic movement beneath.",
                    RetailPrice = 489000m, // ₹4,89,000 (reduced for Razorpay test-mode limit; real: ₹72,40,000)
                    CostPrice = 300000m,
                    ComparePrice = 499000m,
                    CaseSize = 42,
                    MovementType = MovementType.Automatic,
                    StockQuantity = 3,
                    IsPublished = true,
                    IsSoldOut = false,
                    StrapMaterial = "Steel",
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
                    Brand = "Audemars Piguet",
                    ReferenceNumber = "WW-OT-092",
                    Slug = "obsidian-tourbillon",
                    Description = "A masterclass in gravitational defiance. The Obsidian Tourbillon houses a hand-finished flying tourbillon within a forged carbon case, accented with our signature gold primary details.",
                    RetailPrice = 495000m, // ₹4,95,000 (reduced for Razorpay test-mode limit; real: ₹1,05,30,000)
                    CostPrice = 320000m,
                    ComparePrice = 499000m,
                    CaseSize = 44,
                    MovementType = MovementType.Manual,
                    StockQuantity = 1,
                    IsPublished = true,
                    IsSoldOut = false,
                    StrapMaterial = "Rubber",
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
                    Brand = "Patek Philippe",
                    ReferenceNumber = "WW-LGE-888",
                    Slug = "legacy-gold-edition",
                    Description = "Crafted from solid 18k rose gold, the Legacy Edition is a tribute to traditional watchmaking. It features a parchment-toned dial with hand-applied indices and a perpetual calendar complication.",
                    RetailPrice = 460000m, // ₹4,60,000 (reduced for Razorpay test-mode limit; real: ₹84,20,000)
                    CostPrice = 280000m,
                    ComparePrice = 485000m,
                    CaseSize = 40,
                    MovementType = MovementType.Automatic,
                    StockQuantity = 0,
                    IsPublished = true,
                    IsSoldOut = true,
                    StrapMaterial = "Leather",
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
                    Brand = "Richard Mille",
                    ReferenceNumber = "WW-SCX-404",
                    Slug = "skeleton-core-x",
                    Description = "Stripped of all non-essentials, the Skeleton Core X exposes the beating heart of the machine. The architectural bridges are PVD-coated in void black, contrasting sharply with the gold gear train.",
                    RetailPrice = 385000m, // ₹3,85,000 (reduced for Razorpay test-mode limit; real: ₹45,50,000)
                    CostPrice = 240000m,
                    ComparePrice = 420000m,
                    CaseSize = 41,
                    MovementType = MovementType.Automatic,
                    StockQuantity = 5,
                    IsPublished = true,
                    IsSoldOut = false,
                    StrapMaterial = "Rubber",
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
                    Brand = "Omega",
                    ReferenceNumber = "WW-ADE-777",
                    Slug = "azure-diver-elite",
                    Description = "Engineered for the abyss. The Azure Diver Elite boasts a water resistance of 1000 meters, a helium escape valve, and a unidirectional ceramic bezel in deep ocean blue.",
                    RetailPrice = 325000m, // ₹3,25,000 (reduced for Razorpay test-mode limit; real: ₹38,90,000)
                    CostPrice = 200000m,
                    ComparePrice = 360000m,
                    CaseSize = 43,
                    MovementType = MovementType.Automatic,
                    StockQuantity = 8,
                    IsPublished = true,
                    IsSoldOut = false,
                    StrapMaterial = "Steel",
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
                    Brand = "A. Lange & Söhne",
                    ReferenceNumber = "WW-LPS-021",
                    Slug = "luna-phase-silver",
                    Description = "A poetic complication. The Luna Phase Silver tracks the lunar cycle with absolute precision on a dial crafted from meteorite, housed in a polished platinum case.",
                    RetailPrice = 445000m, // ₹4,45,000 (reduced for Razorpay test-mode limit; real: ₹61,20,000)
                    CostPrice = 275000m,
                    ComparePrice = 475000m,
                    CaseSize = 39,
                    MovementType = MovementType.Manual,
                    StockQuantity = 2,
                    IsPublished = true,
                    IsSoldOut = false,
                    StrapMaterial = "Leather",
                    GlbAssetPath = $"/models/{watchIds[5]}.glb",
                    Images = new List<WatchImage>
                    {
                        new WatchImage { Id = Guid.NewGuid(), Path = $"/images/watches/{watchIds[5]}/1.webp", SortOrder = 1 }
                    }
                }
            };

            if (await context.Watches.AnyAsync())
            {
                // Sync prices if they differ to avoid dropping entire DB just for fixing Razorpay limits
                var existingWatches = await context.Watches.Where(w => watchIds.Contains(w.Id)).ToListAsync();
                bool updated = false;

                foreach (var seed in watches)
                {
                    var existing = existingWatches.FirstOrDefault(w => w.Id == seed.Id);
                    if (existing != null && (existing.RetailPrice != seed.RetailPrice || existing.ComparePrice != seed.ComparePrice))
                    {
                        existing.RetailPrice = seed.RetailPrice;
                        existing.ComparePrice = seed.ComparePrice;
                        updated = true;
                    }
                }

                if (updated)
                {
                    await context.SaveChangesAsync();
                }

                return; // DB has been seeded with structural data
            }

            await context.Watches.AddRangeAsync(watches);
            await context.SaveChangesAsync();
        }

        private static async Task SeedBrandsAndFilterConfigAsync(AppDbContext context)
        {
            // 1. Seed Brands from existing watch data if table is empty
            if (!await context.Brands.AnyAsync())
            {
                var distinctBrands = await context.Watches
                    .Select(w => w.Brand)
                    .Distinct()
                    .OrderBy(b => b)
                    .ToListAsync();

                int order = 0;
                foreach (var brandName in distinctBrands)
                {
                    context.Brands.Add(new Brand
                    {
                        Id = Guid.NewGuid(),
                        Name = brandName,
                        SortOrder = order++,
                        IsActive = true
                    });
                }

                await context.SaveChangesAsync();
            }

            // 2. Seed FilterConfig (single row) if table is empty, or update if bounds are stale
            var config = await context.FilterConfigs.FirstOrDefaultAsync();

            var minPrice = await context.Watches.AnyAsync() ? await context.Watches.MinAsync(w => w.RetailPrice) : 3800000m;
            var maxPrice = await context.Watches.AnyAsync() ? await context.Watches.MaxAsync(w => w.RetailPrice) : 10600000m;

            // Round down/up to nearest lakh for clean slider bounds
            decimal roundedMin = Math.Floor(minPrice / 100000m) * 100000m;
            decimal roundedMax = Math.Ceiling(maxPrice / 100000m) * 100000m;

            if (config == null)
            {
                context.FilterConfigs.Add(new FilterConfig
                {
                    Id = Guid.NewGuid(),
                    MinPrice = roundedMin,
                    MaxPrice = roundedMax
                });
                await context.SaveChangesAsync();
            }
            else if (config.MinPrice != roundedMin || config.MaxPrice != roundedMax)
            {
                config.MinPrice = roundedMin;
                config.MaxPrice = roundedMax;
                await context.SaveChangesAsync();
            }
        }
    }
}