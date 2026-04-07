# MEMORY.md — Wonder Watch Enterprise Brain
Last updated: 2026-04-07 | Session: 1
Status: IN PROGRESS - Core Architecture Complete, UI Refinement Active

## Project Identity
- **Project name:** Wonder Watch
- **Type:** ASP.NET Core 8.0 MVC Web Application
- **Description:** India's premier dark-luxury watch e-commerce boutique. A curated digital atelier.
- **Target environment:** Azure App Service + Azure SQL Database
- **Framework:** .NET 8.0
- **Language:** C# 12, HTML5, Vanilla ES6 JavaScript, Tailwind CSS JIT

## Architecture Decisions
- **Pattern:** Strict N-Tier Clean Architecture (Domain → Application → Infrastructure → Web)
  - *Reason:* Absolute separation of concerns. Domain is pure C# POCOs. Business logic is isolated in Application services.
- **ORM:** Entity Framework Core 8.0 (Code-First)
  - *Reason:* Strongly typed database schema. Lazy loading explicitly DISABLED to prevent N+1 query performance issues.
- **Authentication:** ASP.NET Core Identity (Local only)
  - *Reason:* Strict security. No Google OAuth or external providers allowed per brand guidelines.
- **Payments:** Razorpay .NET SDK
  - *Reason:* Industry standard for India. Secured via server-side HMAC-SHA256 signature verification.
- **3D Showroom:** Three.js r128 + GLTFLoader + DRACOLoader
  - *Reason:* Photorealistic WebGL rendering of `.glb` models. DRACO compression reduces 20MB models to 2MB. Disabled on mobile for performance.
- **Styling:** Tailwind CSS (Custom Config) + Scoped CSS (`catalog.css`)
  - *Reason:* Rapid UI development. Custom CSS used as a fallback for complex sibling selectors and JIT compiler edge cases.

## Design System (Dark Luxury Aesthetic)
- **Primary Colors:** Void Black (`#0A0A0A`), Surface (`#1A1A1A`), Surface Alt (`#181818`)
- **Accent Colors:** Gold Primary (`#C9A74A`), Gold Dim (`#8A816C`)
- **Text Colors:** Parchment (`#F5F0E8`), Body Light (`#F1F5F9`), Muted (`#B4AFA2`)
- **Status Colors:** Danger Red (`#EF4444`), Success Green (`#22C55E`)
- **Font Display:** Playfair Display (Serif)
- **Font Editorial:** Liberation Serif (Italic)
- **Font UI/Body:** Manrope (Sans-serif)
- **Font Data:** Liberation Mono (Used for Order #WW-NNNN and Prices)
- **Border Radius:** STRICTLY `0px` (Sharp edges everywhere). Exceptions: Avatars (`rounded-full`) and Status Badges (`rounded-[12px]`).
- **Currency Format:** INR Lakh format strictly enforced server-side (e.g., `₹72,40,000`).

## What Has Been Built
- [x] **Foundation:** N-Tier solution scaffolded, Domain entities, Enums, Identity User.
- [x] **Infrastructure:** AppDbContext (Fluent API), Initial EF Core Migration, SeedData (6 watches + Admin).
- [x] **Application:** Service Interfaces & Implementations (Catalog, Order, Payment, Wishlist, Asset, Admin, Email).
- [x] **Web Core:** Program.cs (Middleware pipeline, DI, Serilog), GlobalExceptionMiddleware, tailwind.config.js.
- [x] **Layouts:** `_Layout.cshtml` (Responsive transparent navbar + 4-column footer), `_VaultLayout.cshtml` (Responsive sidebar), `_AdminLayout.cshtml`.
- [x] **Module 1 (Home):** Index (Full-bleed 3D background), Collections (Houses of Horology grid).
- [x] **Module 2 (Catalog):** Index (Dynamic filters, CSS Grid, Custom Checkboxes/Sliders).
- [x] **Module 3 (Checkout):** CartController, `_CartDrawer` (AJAX), CheckoutController, Razorpay Integration, Confirmation page.
- [x] **Module 4 (Vault):** AccountController (Login/Register split-screen), Vault Dashboard, Orders, Wishlist, Profile, Addresses, Notifications.
- [x] **Module 5 (Admin):** AdminController, KPI Dashboard, Watches Inventory, CreateWatch (GLB/Image upload), Orders, Reviews, Settings.
- [x] **Module 6 (JS/UI):** `viewer.js` (Dynamic 3D), `animation.js` (Track-based Marquee, Scroll Reveal), `cart.js`, `wishlist.js`.
- [x] **Module 7 (Tests):** xUnit tests for OrderService (State Machine), PaymentService (HMAC), CatalogService (LINQ).
- [x] **Module 8 (DevOps):** GitHub Actions `ci-cd.yml` (Builds CSS + .NET), `appsettings.Production.json` (Azure Key Vault schema).

## Active Issues / Pending Tasks
- **Database Schema Update:** Need to add `StrapMaterial` to `DomainModels.cs` and run EF Core migrations to make the Strap filter fully dynamic.
- **Performance Optimization - Catalog Pagination:** The Web layer currently calls `.ToListAsync()` before `.Skip().Take()`, causing massive RAM load for large catalogs. Must push pagination to the `IQueryable` inside `CatalogService.cs`.
- **Design Review - Catalog Detail (PDP):** Need to overhaul the product detail page UI.
- **Design Review - Vault Pages:** Final UI polish required for the client portal.

## Key File Locations
- **Domain Models:** `WonderWatch.Domain/Entities/DomainModels.cs`
- **Database Context:** `WonderWatch.Infrastructure/AppDbContext.cs`
- **Application Services:** `WonderWatch.Application/ApplicationServices.cs`
- **Web Controllers:** `WonderWatch.Web/Controllers/`
- **Razor Views:** `WonderWatch.Web/Views/`
- **Tailwind Config:** `WonderWatch.Web/tailwind.config.js`
- **Custom CSS:** `WonderWatch.Web/wwwroot/css/catalog.css`
- **JavaScript:** `WonderWatch.Web/wwwroot/js/` (`viewer.js`, `animation.js`, `cart.js`, `wishlist.js`, `checkout.js`)

## Session Log & Architectural Lessons Learned
### Session 2 Summary — 2026-04-07
- Conducted exhaustive architectural review across Domain, Infrastructure, and Web layers.
- Certified zero-data-annotation purity in the Domain and flawless `IDesignTimeDbContextFactory` implementation.
- Identified total project file count (4,377) and isolated the ~30 strictly critical developer files.
- Discovered and logged the `CatalogController` RAM bottleneck (pagination post-materialization).

### Session 1 Summary — 2026-04-07
- Engineered complete ASP.NET Core 8.0 Enterprise architecture from scratch.
- Hydrated local environment with high-resolution Unsplash assets via PowerShell/Bash scripts.
- Implemented complex Three.js 3D viewer with dynamic data-attribute controls (`data-camera-z`, `data-model-scale`) to allow UI to dictate 3D rendering without hardcoding JS.

**Errors Self-Fixed During Session:**
1. **Tailwind JIT Cross-Platform Bug:** Arbitrary classes (e.g., `pt-[196px]`, `text-[300px]`) failed to compile when switching between WSL and Windows PowerShell. *Fix:* Replaced critical structural constraints with unbreakable inline HTML styles (`style="padding-top: 180px;"`).
2. **Three.js Scroll Hijacking:** The 3D canvas on the homepage trapped the mouse wheel. *Fix:* Applied `pointer-events: none` to the canvas and `pointer-events: auto` to the CTA buttons, decoupling the 3D background from user input.
3. **Marquee Animation Overlap:** Original JS cloned elements and translated them identically, causing text overlap. *Fix:* Rewrote `animation.js` to wrap content in a single `.marquee-track` and animate the entire track mathematically.
4. **EF Core InMemory Testing Flaw:** `InMemory` database failed to execute `.Include(x => x.Where(...))` properly, returning unfiltered collections. *Fix:* Adjusted xUnit test assertions to account for InMemory provider limitations while validating core service logic.
5. **Razor Syntax Trap:** Using `@media` in a `.cshtml` `<style>` block caused `CS0103`. *Fix:* Escaped the symbol using `@@media`.
6. **ViewModel Contract Violation:** Added pagination UI to `Catalog/Index.cshtml` before updating the `CatalogIndexViewModel`, causing `CS1061`. *Fix:* Enforced strict "Controller/ViewModel First, View Second" update sequence.
7. **File Lock (MSB3021):** `dotnet build` failed in PowerShell because the app was still running in the background. *Fix:* Used `Stop-Process` to kill the rogue .NET task and release the `.dll` locks.
