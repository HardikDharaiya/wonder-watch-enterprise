# Product Detail Page (PDP) Elevation & Feature Completeness

This plan details the steps required to align the Product Detail Page with the existing luxury aesthetic guidelines, while ensuring all relevant database attributes (`StrapMaterial`, `ComparePrice`, `StockQuantity`) are visible.

## User Review Required

> [!IMPORTANT]
> Please review the proposed changes below. The updates involve structural view model additions and UI enhancements for the Product Details page. Once approved, I will implement them and run verification.

## Proposed Changes

### Backend ViewModel Updates

#### [MODIFY] `CatalogController.cs` (file:///d:/Projects/Claude/WONDER_WATCH_MVC/WonderWatch.Web/Controllers/CatalogController.cs)
- Add missing properties to `WatchDetailViewModel`:
  - `public string StrapMaterial { get; set; }`
  - `public string ComparePriceFormatted { get; set; }`
  - `public int StockQuantity { get; set; }`
- Update the `Detail` action mapping to include:
  - `StrapMaterial = watch.StrapMaterial`
  - `StockQuantity = watch.StockQuantity`
  - Logic to format `ComparePrice` if it is greater than `RetailPrice`.

### Frontend Aesthetic & Functionality Updates

#### [MODIFY] `Detail.cshtml` (file:///d:/Projects/Claude/WONDER_WATCH_MVC/WonderWatch.Web/Views/Catalog/Detail.cshtml)
- **Data Binding:** 
  - Replace the hardcoded `Proprietary Alloy` with `@Model.StrapMaterial`.
- **E-commerce CRO (Urgency):** 
  - Add text indicating if stock is low (e.g., "Only 3 pieces remaining worldwide") underneath the price if `StockQuantity > 0` and `StockQuantity <= 4`.
- **Pricing:** 
  - Render `ComparePriceFormatted` elegantly as a struck-through price (`<del>`) when applicable to highlight value.
- **Aesthetic Refinements (Dark Luxury):**
  - Increase visual contrast of text data and utilize Tailwind transitions (`hover:border-gold` etc.).
  - Preserve `js-add-to-cart` and `js-toggle-wishlist` classes exactly as currently implemented so JavaScript connections remain unbroken.
  - Implement subtle fade/reveal structural styles around product details.

### Documentation Finalization
- Update `MEMORY.md`, `COMMANDS.md`, and `FILES.md` to map the changes securely onto the project timeline.

## Open Questions

None currently.

## Verification Plan

### Automated Verification
- Run `dotnet build WonderWatch.Web` to ensure zero compilation errors on the controller/view.

### Manual Verification
- Navigate to a product detail view (`/catalog/{slug}`) and visually confirm:
  1. No hardcoded attributes.
  2. Strikethrough pricing logic works.
  3. "Add to Cart" and "Wishlist" buttons remain operational.
