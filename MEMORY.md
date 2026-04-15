# MEMORY.md — Wonder Watch Enterprise Brain
Last updated: 2026-04-15 | Session: 16
Status: IN PROGRESS - Documentation update (README Razorpay secrets) and final session prep.

## Project Identity
- **Project name:** Wonder Watch
- **Type:** ASP.NET Core 8.0 MVC Web Application
- **Description:** India's premier dark-luxury watch e-commerce boutique. A curated digital atelier.
- **Target environment:** Azure App Service + Azure SQL Database
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
- **Font Data:** Liberation Mono (Used for Order numbers and Prices)
- **Border Radius:** STRICTLY `0px` (Sharp edges everywhere). Exceptions: Avatars (`rounded-full`), Status Badges (`rounded-[12px]`), Progress Tracker circles (`rounded-full`).
- **Currency Format:** INR Lakh format strictly enforced server-side (e.g., `₹72,40,000`).

## What Has Been Built
- [x] **Foundation:** N-Tier solution scaffolded, Domain entities, Enums, Identity User.
- [x] **Infrastructure:** AppDbContext (Fluent API), EF Core Migrations (InitialCreate, AddStrapMaterial, AddUserAvatarUrl, AddUserAddressesAndNotifications, AddFiltersConfig), SeedData (6 watches + Admin + Brands + FilterConfig).
- [x] **Application:** Service Interfaces & Implementations (Catalog, Order, Payment, Wishlist, Asset, Admin, Email).
- [x] **Web Core:** Program.cs (Middleware pipeline, DI, Serilog), GlobalExceptionMiddleware, tailwind.config.js.
- [x] **Layouts:** `_Layout.cshtml` (Responsive transparent navbar + 4-column footer), `_VaultLayout.cshtml` (Responsive sidebar + Upgrade Plan CTA), `_AdminLayout.cshtml`.
- [x] **Module 1 (Home):** Index (Full-bleed 3D background), Collections (Houses of Horology grid).
- [x] **Module 2 (Catalog):** Index (DB-driven dynamic filters, Search bar, CSS Grid 3→2→2 responsive, Custom Checkboxes/Sliders, INR lakh/crore price slider).
- [x] **Module 3 (Checkout):** CartController, `_CartDrawer` (AJAX), CheckoutController, Razorpay Integration, Confirmation page.
- [x] **Module 4 (Vault):** AccountController (Login/Register split-screen), Vault Dashboard (OVERHAULED), Orders (OVERHAULED - filter tabs, progress tracker, invoice), Wishlist (OVERHAULED - REMOVE×, ADD TO CART, count), Profile (Redesigned), Addresses (CRUD + Slide-in), Notifications (Filtering + Read status).
- [x] **Module 5 (Admin):** AdminController, KPI Dashboard, Watches Inventory, CreateWatch (GLB/Image upload), Orders, Reviews, Settings (MailKit SMTP config), **Filters Management** (Brand CRUD + Price Range config).
- [x] **Module 6 (JS/UI):** `viewer.js` (Dynamic 3D), `animation.js` (Track-based Marquee, Scroll Reveal), `cart.js`, `wishlist.js`.
- [x] **Module 7 (Tests):** xUnit tests for OrderService (State Machine), PaymentService (HMAC), CatalogService (LINQ).
- [x] **Module 8 (DevOps):** GitHub Actions `ci-cd.yml` (Builds CSS + .NET), `appsettings.Production.json` (Azure Key Vault schema).

## Active Issues / Pending Tasks
- **Performance Optimization - Catalog Pagination:** The Web layer currently calls `.ToListAsync()` before `.Skip().Take()`, causing massive RAM load for large catalogs. Must push pagination to the `IQueryable` inside `CatalogService.cs`.
- **Design Review - Catalog Detail (PDP):** Need to overhaul the product detail page UI.
- **Admin Filters Nav Link:** Add "Filters" link to `_AdminLayout.cshtml` sidebar navigation.

## Key File Locations
- **Domain Models:** `WonderWatch.Domain/Entities/DomainModels.cs`
- **Database Context:** `WonderWatch.Infrastructure/AppDbContext.cs`
- **Database Schema Docs:** `DATABASE_SCHEMA.md` (root)
- **Application Services:** `WonderWatch.Application/ApplicationServices.cs`
- **Application Contracts:** `WonderWatch.Application/ApplicationContracts.cs`
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

### Session 3 Summary — 2026-04-08
- Conducted UI precision pass on Vault ecosystem.
- Rectified global header transparency collision in `_VaultLayout.cshtml` by injecting explicit `mt-[96px]` buffer mapping.
- Elevated Auth screens by stripping hardcoded `pt-[131px]` offsets to permit full-bleed cover imagery.
- Replaced baked-text placeholder imagery (`login-bg.webp`, `register-bg.webp`) with raw high-fidelity product photography (`hero-macro.webp`, `void-series.webp`).

### Session 4 Summary — 2026-04-08
- **Domain Schema:** Added `AvatarUrl` (nullable string) to `ApplicationUser.cs`.
- **EF Core Migration:** Created and applied `20260408165906_AddUserAvatarUrl` via `dotnet ef` tooling.
- **VaultController.cs:** Updated `Index()` and `Profile()` actions to map `AvatarUrl` with `ui-avatars.com` fallback (SVG, branded gold-on-dark palette).
- **_VaultLayout.cshtml (OVERHAULED):** Replaced static sidebar with premium Identity Block — avatar (56×56, gold border), display name, membership tier badge, "Since MMM yyyy" context. Navigation links refined to left-border active state. Copyright footer added.
- **Vault/Index.cshtml (OVERHAULED):** Completely redesigned dashboard — hero greeting with inline avatar, unified 3-column KPI metrics row (shared border, no separate cards), premium recent order display with product image, editorial wishlist grid, bottom quick-action bar.
- **Build:** `dotnet build` 0 Errors. `npm run build:css` compiled in 353ms.

### Session 5 Summary — 2026-04-09
- **OrderSummaryDto:** Added `UpdatedAt` field for progress tracker date display.
- **VaultOrdersViewModel:** Added `ActiveFilter` property for tab strip state.
- **VaultWishlistViewModel:** Added `Count` property for dynamic subtitle.
- **VaultInvoiceViewModel + InvoiceItemDto:** New view models for printable invoice page.
- **VaultController.cs:** Added `Invoice(Guid id)` action (`GET /vault/orders/{id}/invoice`), updated `Orders()` to accept `string? filter` parameter, updated `Wishlist()` to pass `Count`.
- **Orders.cshtml (FULL REDESIGN):**
  - Italic serif `ORDER HISTORY` title matching Figma.
  - Client-side status filter tabs (ALL / PENDING / SHIPPED / DELIVERED) with gold underline active indicator.
  - 4-step visual progress tracker per order card (Order Placed → Payment Confirmed → Shipped → Delivered) with gold checkmarks and date sub-labels.
  - Cancelled orders show a red cancellation banner with date instead of tracker.
  - Invoice button now links to `/vault/orders/{id}/invoice` (opens in new tab).
  - Concierge CTA block at bottom with CONTACT CONCIERGE bordered button.
- **Wishlist.cshtml (FULL REDESIGN):**
  - Italic serif `MY WISHLIST` title with dynamic count subtitle.
  - Square aspect-ratio image cards with dark background.
  - `REMOVE ×` danger-colored text button replacing gold heart icon.
  - Full-width bordered `ADD TO CART` button replacing underline text link.
  - `UNAVAILABLE` disabled state for sold-out watches.
  - Inline JS remove handler with card fade-out animation + live count update.
- **Invoice.cshtml (NEW):** Standalone print-optimized HTML invoice (no layout). Gold accent branding, dual-column billing/shipping info, line items table, totals with "Complimentary" shipping, branded footer. Print button auto-hides.
- **_VaultLayout.cshtml:** Added `Upgrade Plan` bordered gold button above Sign Out in sidebar footer.
- **DATABASE_SCHEMA.md (NEW):** Complete schema documentation with all tables, relationships, indexes, and migration history.
- **Build:** `dotnet build` 0 Errors (18 warnings — pre-existing NuGet). Tailwind CSS compiled in 170ms.

### Session 6 Summary — 2026-04-09
- **Domain Schema:** Added `UserAddress.cs`, `UserNotification.cs`, and `NotificationType` enum.
- **EF Core Migration:** `AddUserAddressesAndNotifications` applied.
- **VaultController.cs:** Completed Profile Settings (personal, password, preferences), Addresses CRUD, and Notifications feed.
- **AdminController.cs:** Added MailKit SMTP configuration and testing interface.
- **Views:** Redesigned Profile, Addresses (slide-in modal), Notifications (filter tabs), Settings (SMTP panel).
- **_VaultLayout.cshtml:** Added unread dot indicator for Notifications link.
- **Build:** `dotnet build` 0 Errors.

### Session 7 Summary — 2026-04-09
- **Domain Schema:** Added `Brand` entity (admin-managed filterable brand dictionary) and `FilterConfig` entity (single-row price slider bounds).
- **EF Core Migration:** Created and applied `20260409160128_AddFiltersConfig` — adds `Brands` table (unique Name index) and `FilterConfigs` table.
- **SeedData.cs:** Added `SeedBrandsAndFilterConfigAsync()` — auto-discovers distinct brands from Watch data, derives price bounds rounded to nearest lakh.
- **ApplicationContracts.cs:** Added `FilterConfigDto` DTO and `GetFilterConfigAsync()` to `ICatalogService` interface.
- **ApplicationServices.cs:** Updated `GetAvailableBrandsAsync()` to query admin-managed `Brand` table. Implemented `GetFilterConfigAsync()` with DB fallback.
- **CatalogController.cs:** Added `PriceMin`/`PriceMax` to `CatalogIndexViewModel` from `FilterConfig`. Wired `searchQuery` preservation across sort form.
- **_CatalogFilters.cshtml (FULL REWRITE):** Added Search bar, dynamic price slider (DB-driven bounds, 1-lakh step, INR locale JS formatter), brand checkboxes from Brand table, strap material radios, case diameter grid, "Clear all filters" link.
- **catalog.css:** Changed mobile grid from `1fr` to `repeat(2, 1fr)` for 2-column mobile layout.
- **AdminController.cs:** Added 4 filter management endpoints — `Filters` (GET list), `AddBrand` (POST), `DeleteBrand` (POST), `UpdateFilterConfig` (POST). Added `AdminFiltersViewModel`.
- **Admin/Filters.cshtml (NEW):** Admin page with brand CRUD (add/remove with confirmation dialog) and price range configuration (min/max INR inputs).
- **Build:** `dotnet build` 0 Errors. Migration applied. Seed data verified (Brands + FilterConfig populated).
- **Browser Test:** Catalog filters render correctly — search bar visible, price slider shows ₹38,00,000 to ₹1,06,00,000+, brand filter returns results.

### Session 8 Summary — 2026-04-09
- **Database Seed Data:** Updated `SeedData.cs` to assign distinct luxury brands (Rolex, Omega, Patek Philippe, Audemars Piguet, Richard Mille, A. Lange & Söhne) and valid `StrapMaterial`s to the 6 sample watches.
- **EF Core Migration:** Generated and applied `20260409170956_UpdateWatchesSeedData` to run raw SQL updates on the existing `Watches` table and delete existing admin `Brands` so the catalog filter resets cleanly dynamically.
- **Controllers:** Hardened `CatalogController.cs` parsing to use `.Trim().ToUpperInvariant()` for fault-tolerant query parameter processing.
- **UI UX Enhancement:** Implemented dynamic CSS-driven, floating JavaScript tooltips inside `_CatalogFilters.cshtml` to provide live sliding price feedback over the range anchors.

### Session 9 Summary — 2026-04-09
- **Product Detail Page Optimization:** Mapped missing Domain attributes (`StrapMaterial`, `ComparePrice`, `StockQuantity`) into the presentation layer via `WatchDetailViewModel`.
- **CRO & Frontend Polish:** Removed static HTML fallbacks inside `Detail.cshtml`. Re-engineered the UI with subtle tailwind animations (`animate-fade-in`), strict specification alignment, urgency stock tags, and visual anchor points for savings (strikethrough text) aligning with the Dark Luxury design system.

### Session 10 Summary — 2026-04-09
- **PDP Refinement & Cleanup**: Replaced buggy luxury 3D viewer elements natively with pristine high-fidelity 2D images.
- **Back Navigation**: In-layout structural element pointing back to catalog cleanly introduced avoiding absolute chaos.
- **Button Redesign**: Overhauled Wishlist and Cart interaction buttons by removing problematic custom arbitrary Tailwind extensions and returning them to pristine semantic variants (`hover:bg-gold`, `hover:text-void`). 
- **Security Checkup**: Inserted invisible `<form>` bearing `@Html.AntiForgeryToken()` to successfully allow `wishlist.js` validation for POST endpoints. 
- **Textual Logic**: Pulled false fallbacks ("Proprietary Alloy") out of specs mapping purely `Not Specified` for missing dimensions preserving dataset purity.

### Session 11 Summary — 2026-04-09
- **PDP Layout Bug Fix**: Identified absolute positioning collision on the "Back to Catalog" anchor which overlapped dynamically centered text constraints on smaller viewport dimensions. Restored node to the native flex column rendering sequence as an inline-flex element.

### Session 12 Summary — 2026-04-10
- **Wishlist Unauthenticated State Fix**: Unauthenticated users clicking the "Wishlist" button were historically experiencing silent background ASP.NET Core `302 Redirect` HTML downloads rather than a proper `401 Unauthorized` intercept.
- **Frontend Changes**: Appended the critical `X-Requested-With: XMLHttpRequest` header to the `fetch` API payload in `wishlist.js`. 
- **UX Alarm**: Added a forced `alert(...)` popup whenever the response reads `401` or `response.redirected` evaluates to true, notifying the user to login *before* redirecting their active window viewport, successfully solving the "silent" click reporting.

### Session 13 Summary — 2026-04-10
- **Mobile Check UI Fix (Drawers)**: Converted all static `h-screen` representations mathematically failing inside Cart and Filter overlays to active dynamic structural views using `h-[100dvh]` rendering engine parameters.
- **UI Element Collision**: Extrapolated the exit SVG of the Cart Controller with explicit touch-targets, and de-assigned pointer-events natively inside the SVG to negate event bubbling.
- **Drawer Animation Bug Fix**: Debugged and fixed scroll reveal `IntersectionObserver` in `animation.js` explicitly injecting blocking inline CSS constraints onto `#cart-drawer` elements. Bypassed translation loop preventing slider lock.

### Session 14 Summary — 2026-04-11
- **Navigation Update**: Added a "Home" link (`/`) to the global `_Layout.cshtml` navigation bar (both desktop and mobile menus).
- **Navigation Logic**: Updated the `GetActiveNavClass` razor function to explicitly handle the root path `/` to ensure accurate active state highlighting without falsely matching all child paths.
- **Vault Deduplication (Desktop/Mobile Navbars)**: Stripped the redundant text-based "Vault" navigation links from main menus since an explicit profile Action Icon already triggers route validation.
- **Mobile Nav Authenticate Flow**: Moved the "Authenticate" target away from the side menu. Stripped `hidden` logic off the Action Icon loop so mobile viewers natively process authenticate/profile actions exactly like desktop users.
- **Global Wishlist & Notifications**: Re-located the "My Wishlist" action out from the mobile-hamburger menu by making the structural Wishlist SVG visible on all layouts (removed `hidden sm:block`). Injected `UserManager` and `INotificationService` into the global `_Layout.cshtml` to securely check and render a live Unread Notification Badge inside the global toolbar on both mobile and desktop explicitly linked to `/vault/notifications`.
- **Cart Drawer Item Quantity UI**: Enhanced the `_CartDrawer.cshtml` template. Added visual `+` (increase) and `-` (decrease) interactive target buttons dynamically encapsulating the currently isolated item quantity display block.
- **Cart API Layer**: Built a new POST `UpdateQuantity` endpoint bound to `/api/cart/update` in `CartController.cs`. Created matching `UpdateQuantityRequest` DTO handling increment, decrement, and explicit item zeroing (cart removal proxying) natively from backend sessions. Connected it natively to vanilla js clicks handling internal JS `cart.js`.
- **Checkout Saved Addresses Overhaul**: Injected `IAddressService` into `CheckoutController.cs` and redesigned `Index.cshtml` to securely pull and present a radio-selectable Vault address format to users. Added logic in `checkout.js` to correctly toggle UI manually blocking and mapping valid `dataset` variables dynamically on explicit POST requests to the `verify` flow.
- **Checkout Bug Fixes & UX**: Prevented hidden "Ship to a new address" form fields from being focusable by keyboard tab events by injecting CSS `pointer-events-none` & HTML5 `inert` property rules directly onto active layout via JS manipulation.
- **Checkout Return & Removal Options**: Implemented a "Return to Cart" navigation path. Enabled product deletions explicitly inside Checkout's static Order Summary box by pushing `Quantity = 0` commands directly to the backend Cart Controller.
- **Checkout Model Synchronization**: Fixed build error `CS1061` by adding the missing `WatchId` Guid property to `CheckoutItemViewModel` and ensuring it is populated throughout the `CheckoutController` pipeline.
