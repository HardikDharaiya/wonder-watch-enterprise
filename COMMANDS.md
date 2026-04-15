# COMMANDS.md — Wonder Watch Command Reference + Error Log
Last updated: 2026-04-15 (Session 16) | Location: India (IST)

## Essential Setup Commands (Run once on new machine)

### Environment Check
```powershell
dotnet --version
node --version
npm --version
```

### Project Scaffolding (N-Tier Architecture)
```powershell
# 1. Create Solution & Projects
dotnet new sln -n WonderWatch
dotnet new classlib -n WonderWatch.Domain
dotnet new classlib -n WonderWatch.Infrastructure
dotnet new classlib -n WonderWatch.Application
dotnet new mvc -n WonderWatch.Web
dotnet new xunit -n WonderWatch.Tests

# 2. Add to Solution
dotnet sln WonderWatch.sln add WonderWatch.Domain/WonderWatch.Domain.csproj
dotnet sln WonderWatch.sln add WonderWatch.Infrastructure/WonderWatch.Infrastructure.csproj
dotnet sln WonderWatch.sln add WonderWatch.Application/WonderWatch.Application.csproj
dotnet sln WonderWatch.sln add WonderWatch.Web/WonderWatch.Web.csproj
dotnet sln WonderWatch.sln add WonderWatch.Tests/WonderWatch.Tests.csproj

# 3. Establish Clean Architecture Dependencies
dotnet add WonderWatch.Application/WonderWatch.Application.csproj reference WonderWatch.Domain/WonderWatch.Domain.csproj
dotnet add WonderWatch.Infrastructure/WonderWatch.Infrastructure.csproj reference WonderWatch.Domain/WonderWatch.Domain.csproj
dotnet add WonderWatch.Infrastructure/WonderWatch.Infrastructure.csproj reference WonderWatch.Application/WonderWatch.Application.csproj
dotnet add WonderWatch.Web/WonderWatch.Web.csproj reference WonderWatch.Application/WonderWatch.Application.csproj
dotnet add WonderWatch.Web/WonderWatch.Web.csproj reference WonderWatch.Infrastructure/WonderWatch.Infrastructure.csproj
dotnet add WonderWatch.Tests/WonderWatch.Tests.csproj reference WonderWatch.Domain/WonderWatch.Domain.csproj
dotnet add WonderWatch.Tests/WonderWatch.Tests.csproj reference WonderWatch.Application/WonderWatch.Application.csproj
dotnet add WonderWatch.Tests/WonderWatch.Tests.csproj reference WonderWatch.Infrastructure/WonderWatch.Infrastructure.csproj
dotnet add WonderWatch.Tests/WonderWatch.Tests.csproj reference WonderWatch.Web/WonderWatch.Web.csproj
```

### Database & Entity Framework Core
```powershell
# Install EF Core CLI Tools globally (if not already installed)
dotnet tool install --global dotnet-ef

# Create a new migration (Run from solution root)
dotnet ef migrations add <MigrationName> --project WonderWatch.Infrastructure --startup-project WonderWatch.Infrastructure

# Apply migrations to the database
dotnet ef database update --project WonderWatch.Infrastructure --startup-project WonderWatch.Infrastructure

# Drop the database (Use with extreme caution)
dotnet ef database drop --project WonderWatch.Infrastructure --startup-project WonderWatch.Infrastructure
```

### Frontend Build (Tailwind CSS)
```powershell
# Navigate to the Web project
cd WonderWatch.Web

# Install Node dependencies (Tailwind, PostCSS, Autoprefixer)
npm install

# Compile Tailwind CSS (Run this every time you add new utility classes to .cshtml files)
npm run build:css

# Return to solution root
cd ..
```

### Development & Diagnostics
```powershell
# Build the entire solution
dotnet build WonderWatch.sln

# Run the Web Application
dotnet run --project WonderWatch.Web

# Run all xUnit Tests
dotnet test WonderWatch.Tests

# Count all files in the solution (Useful for workspace diagnostics)
powershell -NoProfile -Command "(Get-ChildItem -Recurse -File).Count"
```

### Secrets Management (Local Development)
```powershell
# Initialize User Secrets for the Web project
dotnet user-secrets init --project WonderWatch.Web

# Set Razorpay API Keys
dotnet user-secrets set "Razorpay:KeyId" "rzp_test_YOUR_KEY_ID" --project WonderWatch.Web
dotnet user-secrets set "Razorpay:KeySecret" "YOUR_KEY_SECRET" --project WonderWatch.Web

# List all configured secrets
dotnet user-secrets list --project WonderWatch.Web
```

---

## ERROR LOG & TROUBLESHOOTING

### Error 001 — The File Lock (MSB3021 / MSB3026)
- **Trigger:** Running `dotnet build` or `dotnet run` while the application is already running in the background.
- **Symptoms:** `Could not copy "WonderWatch.Infrastructure.dll" to "bin\Debug\net8.0\WonderWatch.Infrastructure.dll". The file is locked by: "WonderWatch.Web (PID)"`
- **Fix:**
  ```powershell
  # Kill the rogue process holding the lock
  Stop-Process -Name "WonderWatch.Web" -Force -ErrorAction SilentlyContinue
  # Clean the build artifacts
  dotnet clean WonderWatch.sln
  # Re-run
  dotnet run --project WonderWatch.Web
  ```

### Error 002 — Tailwind JIT Compilation Failure (The "Squashed Layout" Bug)
- **Trigger:** Adding new arbitrary Tailwind classes (e.g., `pt-[196px]`, `text-[300px]`) to a `.cshtml` file without recompiling the CSS.
- **Symptoms:** The browser ignores the new classes, causing elements to overlap, lose padding, or render at the wrong size.
- **Fix:**
  1. Open terminal and navigate to `WonderWatch.Web`.
  2. Run `npm run build:css`.
  3. Refresh the browser.
- **Prevention:** For critical structural layout constraints (like hero section heights), use inline HTML styles (`style="padding-top: 180px;"`) or custom scoped CSS (`catalog.css`) to bypass the JIT compiler entirely.

### Error 003 — Razor Syntax Trap (CS0103)
- **Trigger:** Writing native CSS `@media` queries inside a `.cshtml` file's `<style>` block.
- **Symptoms:** `CS0103: The name 'media' does not exist in the current context`. The Razor engine tries to interpret `@media` as C# code.
- **Fix:** Escape the `@` symbol by doubling it: `@@media (max-width: 1024px)`.

### Error 004 — ViewModel Contract Violation (CS1061)
- **Trigger:** Adding new data requirements (like `@Model.TotalPages`) to a Razor View before updating the underlying C# ViewModel class.
- **Symptoms:** `CS1061: 'CatalogIndexViewModel' does not contain a definition for 'TotalPages'`.
- **Fix:** Always update the `ViewModel.cs` class and the `Controller.cs` action *before* referencing new properties in the `.cshtml` view.

### Error 005 — EF Core InMemory Testing Flaw
- **Trigger:** Running xUnit tests that rely on `.Include(x => x.Where(...))` against the `Microsoft.EntityFrameworkCore.InMemory` provider.
- **Symptoms:** The test fails because the InMemory provider returns the *entire* collection, ignoring the `.Where()` filter inside the `.Include()`.
- **Fix:** Acknowledge that the InMemory provider is not a true relational database. Adjust test assertions to validate the core service logic, or use SQLite In-Memory/Testcontainers for complex LINQ testing.

### Error 006 — 3D Model 404 Not Found (MIME Type Block)
- **Trigger:** Attempting to load a `.glb` file in the browser via `viewer.js`.
- **Symptoms:** Browser console shows `404 Not Found` for the `.glb` file, even though the file physically exists in `wwwroot/models/`.
- **Fix:** ASP.NET Core blocks unknown file extensions by default. Update `Program.cs` to explicitly map the `.glb` extension:
  ```csharp
  var provider = new FileExtensionContentTypeProvider();
  provider.Mappings[".glb"] = "model/gltf-binary";
  app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = provider });
  ```

### Error 007 — Dead Filters (Z-Index & Pointer Events)
- **Trigger:** Clicking custom-styled checkboxes or radio buttons in the Catalog sidebar does nothing.
- **Symptoms:** The visual state (gold border/tick) does not update because the hidden `<input>` is blocked by a higher `z-index` element or `pointer-events-none`.
- **Fix:** Ensure the `<label>` tag is the primary click target. Use inline JavaScript (`onchange="this.nextElementSibling.style.opacity = this.checked ? '1' : '0';"`) to guarantee the visual UI updates instantly, bypassing complex CSS sibling selectors that might fail to compile.


### Error 008 — Shell Mismatch (Bash vs. PowerShell)
- **Trigger:** Pasting PowerShell scripts (containing `Write-Host`, `$var = @()`) into an Ubuntu (WSL) Bash terminal.
- **Symptoms:** `command not found` and `syntax error` messages.
- **Fix:** Always match the script language to the terminal environment. Use `curl` and `mkdir -p` for Bash, and `Invoke-WebRequest` and `New-Item` for PowerShell.

### Error 009 — CSS Destruction from Wrong Tailwind Input File
- **Trigger:** Running `npx tailwindcss` manually with `./wwwroot/css/site.css` as input instead of `./Styles/app.css`.
- **Symptoms:** ALL Tailwind utility classes disappear. Pages render unstyled — white background, blue links, no layout.
- **Root Cause:** `site.css` is plain CSS (Bootstrap-era defaults + catalog components) with zero `@tailwind` directives. Compiling it overwrites `global.css` with only catalog CSS, destroying the utility layer.
- **Fix:** Always use `npm run build:css` (NEVER run `npx tailwindcss` directly with manual paths).
- **Prevention:** The `build:css` npm script in `package.json` is the **single source of truth** for input/output paths: `npx tailwindcss -i ./Styles/app.css -o ./wwwroot/css/global.css`.

---

## Command History (Auto-updated each session)
### Session 1 Final History — 2026-04-07
- `dotnet new sln` and project scaffolding ✅
- `dotnet add package` for EF Core, Identity, Razorpay, Serilog, FluentValidation ✅
- `dotnet ef migrations add InitialCreate` ✅
- `dotnet build WonderWatch.Application` (Fixed N-Tier dependency inversion) ✅
- `npm init -y` and `npm install -D tailwindcss` (Fixed WSL `.bin` generation bug) ✅
- `npm run build:css` ✅
- `dotnet build WonderWatch.Web` (Fixed `builder.App()` hallucination to `builder.Build()`) ✅
- `dotnet test WonderWatch.Tests` (Fixed InMemory `.Include` limitation) ✅
- Executed PowerShell Asset Hydration Script (Fixed Unsplash 404 dead link) ✅
- `dotnet run --project WonderWatch.Web` ✅ (Application rendering verified)

### Session 3 Final History — 2026-04-08
- Multi-replace executed on `_VaultLayout.cshtml` to map global navbar `mt-[96px]` buffer. ✅
- Authentication Screens updated to remove `pt-[131px]` offset and injected raw collection imagery instead of placeholder graphic. ✅
- Task artifact creation and systematic tracking enforced on `FILES.md`, `COMMANDS.md`, `MEMORY.md`. ✅

### Session 4 Final History — 2026-04-08
- `dotnet ef migrations add AddUserAvatarUrl --project WonderWatch.Infrastructure --startup-project WonderWatch.Infrastructure` ✅
- `dotnet ef database update --project WonderWatch.Infrastructure --startup-project WonderWatch.Infrastructure` ✅ (Migration applied)
- `dotnet build WonderWatch.Web` ✅ (0 Errors, 18 NuGet warnings)
- `npm run build:css` ✅ (Tailwind recompiled in 353ms)
- Files modified: `ApplicationUser.cs`, `VaultController.cs`, `_VaultLayout.cshtml`, `Index.cshtml` (Vault Dashboard)

### Session 5 Final History — 2026-04-09
- **CSS BUG FIX:** Rebuilt `global.css` via `npm run build:css` ✅ (9KB → 58KB, 418ms)
- Root cause: Session 5 build step accidentally used `site.css` as input instead of `Styles/app.css`
- `dotnet build WonderWatch.Web` ✅ (0 Errors, 18 NuGet warnings)
- Files modified: `ApplicationContracts.cs`, `VaultController.cs`, `Orders.cshtml`, `Wishlist.cshtml`, `_VaultLayout.cshtml`
- Files created: `Invoice.cshtml`, `DATABASE_SCHEMA.md`
- **Launch method:** Visual Studio 2026 ▶ button (IIS Express/Kestrel, `https://localhost:7105`)

### Session 6 Final History — 2026-04-09
- `dotnet ef migrations add AddUserAddressesAndNotifications --project WonderWatch.Infrastructure --startup-project WonderWatch.Infrastructure` ✅
- `dotnet ef database update --project WonderWatch.Infrastructure --startup-project WonderWatch.Infrastructure` ✅
- `dotnet build WonderWatch.Web` ✅ (0 Errors)
- `npm run build:css` ✅ (Tailwind recompiled)
- Files modified: `ApplicationUser.cs`, `VaultController.cs`, `AdminController.cs`, `Profile.cshtml`, `Addresses.cshtml`, `Notifications.cshtml`, `Settings.cshtml`, `_VaultLayout.cshtml`, `appsettings.json`
- Files created: `UserAddress.cs`, `UserNotification.cs`, `vault.js`

### Session 7 Final History — 2026-04-09
- `Stop-Process -Name "WonderWatch.Web" -Force` ✅ (Killed running app to release DLL locks)
- `dotnet build WonderWatch.Web` ✅ (0 Errors, 18 NuGet warnings)
- `dotnet ef migrations add AddFiltersConfig --project WonderWatch.Infrastructure --startup-project WonderWatch.Web` ✅
- `dotnet ef database update --project WonderWatch.Infrastructure --startup-project WonderWatch.Web` ✅ (Migration `20260409160128_AddFiltersConfig` applied)
- `dotnet run --project WonderWatch.Web` ✅ (App started on http://localhost:5036. Seed data populated Brands + FilterConfigs tables.)
- Files modified: `DomainModels.cs`, `AppDbContext.cs`, `SeedData.cs`, `ApplicationContracts.cs`, `ApplicationServices.cs`, `CatalogController.cs`, `AdminController.cs`, `_CatalogFilters.cshtml`, `Index.cshtml` (Catalog), `catalog.css`
- Files created: `Admin/Filters.cshtml`, `Migrations/20260409160128_AddFiltersConfig.cs`

### Session 8 Final History — 2026-04-09
- `dotnet ef migrations add UpdateWatchesSeedData --project WonderWatch.Infrastructure --startup-project WonderWatch.Web` ✅
- `dotnet ef database update --project WonderWatch.Infrastructure --startup-project WonderWatch.Web` ✅ (Raw SQL Migration applied to synchronize watches/brands properties).
- `dotnet run --project WonderWatch.Web` ✅
- Files modified: `SeedData.cs`, `CatalogController.cs`, `_CatalogFilters.cshtml`
- Files created: `Migrations/20260409170956_UpdateWatchesSeedData.cs`

### Session 9 Final History — 2026-04-09
- **PDP Overhaul Execution:** Updated `CatalogController.cs` inside `WonderWatch.Web` to pass `StrapMaterial`, `StockQuantity`, and `ComparePriceFormatted` to the frontend via `WatchDetailViewModel`. ✅
- **View Enhancements:** Refactored `Detail.cshtml` to display a dynamic, database-driven specification sheet instead of a hardcoded mock UI. Added urgency indicator for low stock values (`<5`) and integrated strikethrough compare-at-price functionality. ✅
- `dotnet build WonderWatch.Web` ✅ (0 Errors, successful pipeline validation).

### Session 10 Final History — 2026-04-09
- **PDP Front-end Overhaul Execution:** Updated `Detail.cshtml` taking out buggy luxury 3D viewer replacing it with absolute perfect 2D static visual imagery array. Added back navigation. ✅
- **Wishlist Fixes:** Injected `@Html.AntiForgeryToken()` block into presentation layer allowing `wishlist.js` validation for POST endpoints. ✅
- **Interaction CSS Overhaul:** Swapped messy and unsupported `scale-x-100` JIT tailwind classes off wishlist and acquire timepiece buttons over to basic CSS structural color states. ✅
- `Stop-Process -Id 14824 -Force` to release locked binaries. ✅
- `dotnet build WonderWatch.Web` ✅ (0 Errors).

### Session 11 Final History — 2026-04-09
- **UI Bug fix Execution:** Switched `Detail.cshtml` CSS from `absolute` node injection on Back Navigation anchor to `inline-flex` ensuring it never squashes fluid typography inside vertical alignment calculations. ✅
- `dotnet build WonderWatch.Web` ✅ (0 Errors).

### Session 12 Final History — 2026-04-10
- **Wishlist Alarm Execution:** Updated `wishlist.js` `fetch` object to push `X-Requested-With` header preventing silent MVC redirect hijacks. Updated handling structure forcing JS `alert()` logic for unauthenticated 401s prior to safe login redirecting. ✅
- `dotnet build WonderWatch.Web` ✅ (0 Errors).

### Session 13 Final History — 2026-04-10
- **Mobile Rendering UI Fix:** Updated `_CartDrawer.cshtml` to rely upon `h-[100dvh]` rather than `100vh` to avoid address-bar clipping on iOS/Android. ✅
- **Catalog CSS Modification:** Appended `100dvh` structural container size logic to `.cat-filter-drawer` and `.cat-filter-panel` resolving unreachable filter UI cutoffs. ✅
- **Cart Button Optimization:** Increased touch-targets mathematically using explicit padding over the cart close icon and detached SVG inner bubbling with `pointer-events-none`. ✅
- **Drawer Animation Bug Fix:** Extracted cart drawer selector from scroll reveal observer in `animation.js` to unblock `translate-x` transformations on open/close. ✅
- `npm run build:css` ✅ (Recompiled `global.css` via Tailwind JIT)

### Session 14 Final History — 2026-04-11
- **Navbar Update Execution**: Adjusted `_Layout.cshtml` to inject `Home` URL paths resolving navigation loops. ✅
- **Navbar Vault Cleanup Execution**: Stripped overlapping "Vault" definitions out of both Desktop and Mobile Nav trees in `_Layout.cshtml` in favor of forcing native profile Account Icon clicks consistently across views. Removed "Authenticate" sub-menu item from the mobile view layout. ✅
- **Global Layout Enhancement**: Added `UserManager` and `INotificationService` directly into `_Layout.cshtml`. Moved wishlist icon to display on mobile beside cart. Inserted dynamic unread notification badge icon with routing to the Notifications page before the profile icon in all views. ✅
- **Cart Drawer Item Quantity**: Created new API endpoint `api/cart/update` inside `CartController.cs` to explicitly override a product's target quantity logic. Injected physical `+` / `-` visual keys into UI of `_CartDrawer.cshtml`, binding these event listeners directly back inside `cart.js`. ✅
- **Checkout Saved Addresses Overhaul**: Injected `IAddressService` into `CheckoutController` fetching explicitly assigned address objects mapped natively to newly designed selection `radio` containers in the Razor View logic blocks allowing dynamic POST payload modifications prior to Razorpay initializations. ✅
- **Checkout Bug Tracking Execution**: Fixed checkout page edge cases. Injected `< Back to Cart` logic. Appended Cart API proxy commands directly inside Javascript's event loop to allow line item deletions without reloading via HTTP POST handling. Swapped `disabled`/`pointer-events` toggles into JavaScript blocking HTML form access correctly. ✅
- **Checkout Build Error Fix**: Resolved `CS1061` by adding a missing `WatchId` property to `CheckoutItemViewModel` and updating `CheckoutController` to populate it in `Index` and `Confirmation` views. ✅
- `dotnet build WonderWatch.Web` ✅ (0 Errors).

### Session 15 Final History — 2026-04-12
- **Admin Settings — Save Config 404 Fix**: Changed outer `<form action="#">` → `action="/admin/settings/save"` in `Settings.cshtml`. Confirmed Save Configuration now posts correctly. ✅
- **Admin Settings — Silent Email False-Success Fix**: `EmailService.SendEmailAsync` previously did a silent `return` (no exception) when SMTP was not configured, so the Test Email button always showed a "success" toast. Fixed by throwing `InvalidOperationException` instead, ensuring the controller catches and displays a real error message. ✅
- **Admin Settings — Live Config Reload**: `AdminController` now casts `IConfiguration` to `IConfigurationRoot` and calls `.Reload()` after writing to `appsettings.json` in `SaveSettings()`. SMTP settings now take effect immediately without an app restart. ✅
- **Test Email Guard**: Added an explicit pre-check in `TestEmail()` — returns early with a user-friendly error if `SmtpSettings:Host` or `SmtpSettings:Username` is still empty, preventing cryptic MailKit exceptions. ✅
- `dotnet build WonderWatch.Web` ✅ (0 Errors, 9 pre-existing NuGet warnings)

### Session 16 Final History — 2026-04-15
- **Documentation Update**: Updated `README.md` to include explicitly documented `.NET User Secrets` commands for Razorpay setup (`KeyId`, `KeySecret`). ✅
- **Brain Sync**: Synchronized `MEMORY.md`, `FILES.md`, and `COMMANDS.md` with latest session history and date pointers. ✅
- **Git Repository Update**: Committed all pending documentation and asset changes to the main branch. ✅
