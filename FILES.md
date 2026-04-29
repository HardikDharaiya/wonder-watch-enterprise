# FILES.md — Wonder Watch Directory Atlas
Last updated: 2026-04-29 (Session 24)


## Project Root
D:\Projects\WONDER_WATCH_MVC\
*(Note: As of Session 2, there are 4,377 total files, primarily consisting of auto-generated binaries, NuGet packages, node_modules, and assets. Focus strictly on the tracked architectural structures below).*

## Top-Level Structure
```text
WONDER_WATCH_MVC/
├── MEMORY.md              ← Project brain (READ FIRST every session)
├── FILES.md               ← This file — directory atlas
├── WonderWatch.sln        ← Master .NET Solution File
├── setup.sh               ← Initial scaffolding script (F1)
├── hydrate.sh             ← Asset hydration script (Unsplash/GLB)
├── WonderWatch.Domain/    ← Pure C# POCOs (Zero external dependencies)
├── WonderWatch.Infrastructure/ ← EF Core, Migrations, Seed Data
├── WonderWatch.Application/    ← Business Logic, Services, DTOs, Interfaces
├── WonderWatch.Web/       ← ASP.NET Core MVC, Controllers, Views, wwwroot
└── WonderWatch.Tests/     ← xUnit Tests (State Machine, HMAC, LINQ)
```

## WonderWatch.Domain/ — Core Entities & Enums
```text
WonderWatch.Domain/
├── WonderWatch.Domain.csproj
├── Entities/
│   ├── DomainModels.cs         ← Watch, Order, OrderItem, Address, Review, Wishlist, WatchImage, Brand, FilterConfig, MembershipPlan
│   ├── JournalSubscription.cs  ← Newsletter email subscriptions
│   ├── UserAddress.cs          ← Vault saved address details
│   └── UserNotification.cs     ← Vault system/order notifications
├── Enums/
│   └── (Inside DomainModels.cs) ← MovementType, OrderStatus, ReviewStatus, MembershipTier
└── Identity/
    └── ApplicationUser.cs ← Inherits IdentityUser<Guid>, adds custom properties

```

## WonderWatch.Infrastructure/ — Data Access Layer
```text
WonderWatch.Infrastructure/
├── WonderWatch.Infrastructure.csproj
├── AppDbContext.cs        ← Full Fluent API configuration (No data annotations in Domain)
├── DesignTimeDbContextFactory.cs ← Used by EF Core CLI for migrations
├── SeedData.cs            ← Hydrates DB with 6 watches + Admin user + Brands + FilterConfig
└── Migrations/
    ├── 20260322062352_InitialCreate.cs
    ├── 20260407093131_AddStrapMaterial.cs
    ├── 20260408165906_AddUserAvatarUrl.cs
    ├── 20260409200000_AddUserAddressesAndNotifications.cs
    ├── 20260409160128_AddFiltersConfig.cs
    ├── 20260409170956_UpdateWatchesSeedData.cs
    ├── 20260416XXXXXX_AddMembershipPlans.cs
    ├── 20260416XXXXXX_AddJournalSubscriptions.cs
    ├── 20260419070443_AddPayOnDeliveryAndConfirmedStatus.cs
    └── AppDbContextModelSnapshot.cs

```

## WonderWatch.Application/ — Business Logic Layer
```text
WonderWatch.Application/
├── WonderWatch.Application.csproj
├── ApplicationContracts.cs ← ALL Interfaces (ICatalogService, IOrderService, IMembershipService, IJournalService, IEmailService + SendOtpAsync, etc.) and DTOs/ViewModels (VerifyOtpViewModel, ForgotPasswordViewModel, ResetPasswordViewModel)
└── ApplicationServices.cs  ← ALL Implementations (CatalogService, OrderService, MembershipService, JournalService, EmailService + branded OTP email template, etc.)

```

## WonderWatch.Web/ — Presentation Layer (MVC)
```text
WonderWatch.Web/
├── WonderWatch.Web.csproj
├── Program.cs             ← DI Container, Middleware Pipeline, Serilog, Identity, Quartz
├── tailwind.config.js     ← Strict design tokens (Zero border-radius, specific hex codes)
├── package.json           ← NPM scripts (build:css)
├── appsettings.json
├── appsettings.Development.json
├── appsettings.Production.json ← Azure Key Vault schema
├── Controllers/
│   ├── AccountController.cs    ← Login, Register, VerifyOtp, ResendOtp, ForgotPassword, ResetPassword
│   ├── AdminController.cs
│   ├── CartController.cs
│   ├── CatalogController.cs
│   ├── CheckoutController.cs
│   ├── HomeController.cs
│   ├── JournalController.cs      ← POST /api/journal/subscribe (newsletter)
│   ├── MembershipPlanController.cs ← Admin CRUD for membership plans
│   ├── VaultController.cs        ← Includes Membership, DeleteAccount actions
│   └── WishlistApiController.cs
├── Jobs/
│   └── InventoryAlertJob.cs      ← Quartz.NET background chron job for inventory alerts

├── Middleware/
│   └── GlobalExceptionMiddleware.cs ← Intercepts errors, returns JSON for AJAX or HTML for MVC
├── Styles/
│   └── app.css            ← Source Tailwind directives
├── Views/
│   ├── _ViewImports.cshtml
│   ├── _ViewStart.cshtml
│   ├── Account/
│   │   ├── ForgotPassword.cshtml  ← Email input form for password recovery
│   │   ├── Login.cshtml
│   │   ├── Register.cshtml
│   │   ├── ResetPassword.cshtml   ← New password form (post-OTP verification)
│   │   └── VerifyOtp.cshtml       ← 6-digit OTP entry with auto-advance + paste support
│   ├── Admin/
│   │   ├── CreateWatch.cshtml
│   │   ├── Filters.cshtml     ← Brand CRUD + Price range config
│   │   ├── Index.cshtml
│   │   ├── Orders.cshtml
│   │   ├── Reviews.cshtml
│   │   ├── Settings.cshtml
│   │   └── Watches.cshtml
│   ├── Catalog/
│   │   ├── _CatalogFilters.cshtml ← Partial: Search bar, brand checkboxes, price slider, strap/size filters
│   │   ├── Detail.cshtml
│   │   └── Index.cshtml
│   ├── Checkout/
│   │   ├── Confirmation.cshtml
│   │   └── Index.cshtml
│   ├── Home/
│   │   ├── About.cshtml
│   │   ├── Collections.cshtml
│   │   ├── Contact.cshtml      ─ Concierge enquiry page (relocated from Views/Contact/ in Session 22)
│   │   ├── Index.cshtml
│   │   ├── Privacy.cshtml
│   │   ├── Shipping.cshtml
│   │   └── Terms.cshtml
│   ├── Shared/
│   │   ├── _AdminLayout.cshtml
│   │   ├── _CartDrawer.cshtml
│   │   ├── _Layout.cshtml
│   │   ├── _ValidationScriptsPartial.cshtml
│   │   ├── _VaultLayout.cshtml
│   │   └── Error.cshtml
│   ├── MembershipPlan/
│   │   ├── Index.cshtml      ← Admin membership plan list
│   │   ├── Create.cshtml     ← Admin create plan form
│   │   └── Edit.cshtml       ← Admin edit plan form
│   └── Vault/
│       ├── Addresses.cshtml
│       ├── Entry.cshtml
│       ├── Index.cshtml
│       ├── Membership.cshtml  ← User-facing pricing cards + Razorpay
│       ├── Notifications.cshtml
│       ├── Orders.cshtml
│       ├── Profile.cshtml
│       └── Wishlist.cshtml

└── wwwroot/
    ├── css/
    │   ├── catalog.css    ← Scoped CSS for complex sibling selectors
    │   └── global.css     ← Compiled Tailwind output
    ├── js/
    │   ├── animation.js   ← IntersectionObserver, Marquee, Smooth Scroll
    │   ├── cart.js        ← AJAX Cart Drawer logic
    │   ├── checkout.js    ← Razorpay SDK integration
    │   ├── vault.js       ← Profile validation, address modals, notification tabs
    │   ├── viewer.js      ← Three.js 3D Canvas logic
    │   └── wishlist.js    ← AJAX Wishlist toggle
    ├── models/            ← .glb 3D files
    └── images/            ← Local WebP assets (brand, collections, about, watches)
```

## WonderWatch.Tests/ — Testing Layer
```text
WonderWatch.Tests/
├── WonderWatch.Tests.csproj
├── CatalogServiceTests.cs ← Tests LINQ filter chains (Brands, Sizes, Price)
├── OrderServiceTests.cs   ← Tests State Machine (Pending -> Paid -> Shipped)
└── PaymentServiceTests.cs ← Tests HMAC-SHA256 cryptographic verification
```

## .github/ — CI/CD Pipeline
```text
.github/
└── workflows/
    └── ci-cd.yml          ← Builds CSS, runs xUnit tests, publishes to Azure App Service
```

## Critical Rules
1. **NEVER** write C# business logic inside a Controller. It belongs in `ApplicationServices.cs`.
2. **NEVER** pass raw Domain entities to a Razor View. Always map to a ViewModel in the Controller.
3. **NEVER** use Tailwind arbitrary classes (e.g., `pt-[196px]`) for critical structural layout without immediately running `npm run build:css`. Use inline styles (`style="padding-top: 196px;"`) if bypassing the compiler.
4. **NEVER** hardcode Razorpay API keys. They must be injected via `IConfiguration` (User Secrets in Dev, Azure Key Vault in Prod).
5. **ALWAYS** ensure `viewer.js` checks `dataset.mobile === 'true'` on line 1 before initializing WebGL.
6. **ALWAYS** format prices using `CultureInfo("hi-IN")` to ensure the INR Lakh format (`₹72,40,000`).
