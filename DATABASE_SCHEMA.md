# DATABASE_SCHEMA.md — Wonder Watch Enterprise

**Source:** Derived from `AppDbContext.cs`, `DomainModels.cs`, `ApplicationUser.cs`, and `.bacpac` export `WonderWatch_Dev`.
**Database:** SQL Server LocalDB (`(localdb)\MSSQLLocalDB`) → `WonderWatch_Dev`
**Last Updated:** 2026-04-09 | Session 8

---

## Tables Overview

| Table | Description | Key Type |
|---|---|---|
| `AspNetUsers` | Identity + Custom user profile | `Guid` PK |
| `AspNetRoles` | Identity roles | `Guid` PK |
| `AspNetUserRoles` | Many-to-many user↔role mapping | Composite |
| `AspNetUserClaims` | User claims | `int` PK |
| `AspNetUserLogins` | External login providers | Composite |
| `AspNetUserTokens` | Auth tokens | Composite |
| `AspNetRoleClaims` | Role-based claims | `int` PK |
| `Watches` | Luxury watch product catalog | `Guid` PK |
| `WatchImages` | Watch image paths (1→many per watch) | `Guid` PK |
| `Orders` | Customer purchase orders | `Guid` PK |
| `OrderItems` | Line items within an order | `Guid` PK |
| `Wishlists` | User wishlist items | `Guid` PK |
| `Reviews` | Product reviews (moderated) | `Guid` PK |
| `UserAddresses` | Saved user shipping addresses | `Guid` PK |
| `UserNotifications` | In-app user notifications | `Guid` PK |
| `Brands` | Admin-managed filterable brand dictionary | `Guid` PK |
| `FilterConfigs` | Single-row price slider configuration | `Guid` PK |
| `__EFMigrationsHistory` | EF Core migration log | `varchar` PK |

---

## Table: `AspNetUsers` (ApplicationUser)

Extends ASP.NET Core Identity's `IdentityUser<Guid>`.

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `Id` | `Guid` | NOT NULL | PK |
| `UserName` | `nvarchar(256)` | NULL | Identity field (email) |
| `NormalizedUserName` | `nvarchar(256)` | NULL | Indexed |
| `Email` | `nvarchar(256)` | NULL | Identity email |
| `NormalizedEmail` | `nvarchar(256)` | NULL | Indexed |
| `EmailConfirmed` | `bit` | NOT NULL | |
| `PasswordHash` | `nvarchar(max)` | NULL | BCrypt hash |
| `SecurityStamp` | `nvarchar(max)` | NULL | |
| `ConcurrencyStamp` | `nvarchar(max)` | NULL | |
| `PhoneNumber` | `nvarchar(max)` | NULL | |
| `PhoneNumberConfirmed` | `bit` | NOT NULL | |
| `TwoFactorEnabled` | `bit` | NOT NULL | |
| `LockoutEnd` | `datetimeoffset` | NULL | |
| `LockoutEnabled` | `bit` | NOT NULL | |
| `AccessFailedCount` | `int` | NOT NULL | |
| `FullName` | `nvarchar(max)` | NOT NULL | Custom: full legal name |
| `DisplayName` | `nvarchar(max)` | NOT NULL | Custom: shown in UI |
| `AvatarUrl` | `nvarchar(max)` | NULL | Custom: profile image URL (added in migration `AddUserAvatarUrl`) |
| `MembershipTier` | `int` | NOT NULL | Enum: 0=Silver, 1=Gold, 2=Platinum |
| `MemberSince` | `datetime2` | NOT NULL | Default: UTC now |
| `Nationality` | `nvarchar(max)` | NOT NULL | |
| `DateOfBirth` | `datetime2` | NOT NULL | |
| `Preferences` | `nvarchar(max)` | NOT NULL | JSON blob `{}` |

**Navigation Properties:**
- `Orders` → `Orders.UserId` (DeleteBehavior: Restrict)
- `Wishlist` → `Wishlists.UserId` (DeleteBehavior: Cascade)
- `Reviews` → `Reviews.UserId` (DeleteBehavior: Cascade)
- `Addresses` → `UserAddresses.UserId` (DeleteBehavior: Cascade)
- `Notifications` → `UserNotifications.UserId` (DeleteBehavior: Cascade)

---

## Table: `Watches`

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `Id` | `uniqueidentifier` | NOT NULL | PK |
| `Name` | `nvarchar(200)` | NOT NULL | Watch model name |
| `Brand` | `nvarchar(100)` | NOT NULL | e.g., "ROLEX", "PATEK PHILIPPE" |
| `ReferenceNumber` | `nvarchar(50)` | NOT NULL | Unique index |
| `Slug` | `nvarchar(250)` | NOT NULL | Unique index (URL key) |
| `Description` | `nvarchar(max)` | NOT NULL | Long editorial text |
| `RetailPrice` | `decimal(18,2)` | NOT NULL | INR |
| `CostPrice` | `decimal(18,2)` | NOT NULL | Internal |
| `ComparePrice` | `decimal(18,2)` | NOT NULL | Strike-through price |
| `CaseSize` | `int` | NOT NULL | In mm |
| `MovementType` | `int` | NOT NULL | Enum: 0=Automatic, 1=Manual |
| `StockQuantity` | `int` | NOT NULL | |
| `IsPublished` | `bit` | NOT NULL | Public visibility |
| `IsSoldOut` | `bit` | NOT NULL | Derived flag |
| `GlbAssetPath` | `nvarchar(max)` | NOT NULL | Path to `.glb` 3D model |
| `StrapMaterial` | `nvarchar(max)` | NOT NULL | e.g., "Leather", "Steel" (added in `AddStrapMaterial` migration) |

**Unique Indexes:** `Slug`, `ReferenceNumber`

---

## Table: `WatchImages`

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `Id` | `uniqueidentifier` | NOT NULL | PK |
| `WatchId` | `uniqueidentifier` | NOT NULL | FK → `Watches.Id` (CASCADE DELETE) |
| `Path` | `nvarchar(500)` | NOT NULL | e.g., `/images/watches/{guid}/1.webp` |
| `SortOrder` | `int` | NOT NULL | Display order (0 = primary) |

---

## Table: `Orders`

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `Id` | `uniqueidentifier` | NOT NULL | PK |
| `UserId` | `uniqueidentifier` | NOT NULL | FK → `AspNetUsers.Id` (RESTRICT DELETE) |
| `Status` | `int` | NOT NULL | Enum: 0=Pending, 1=Paid, 2=Processing, 3=Shipped, 4=Delivered, 5=Cancelled |
| `RazorpayOrderId` | `nvarchar(100)` | NOT NULL | Razorpay order ref |
| `RazorpayPaymentId` | `nvarchar(100)` | NOT NULL | Razorpay payment ref |
| `TotalAmount` | `decimal(18,2)` | NOT NULL | INR total |
| `CreatedAt` | `datetime2` | NOT NULL | UTC |
| `UpdatedAt` | `datetime2` | NOT NULL | UTC |
| `ShippingAddress_Line1` | `nvarchar(200)` | NOT NULL | Owned entity (flattened) |
| `ShippingAddress_Line2` | `nvarchar(200)` | NULL | |
| `ShippingAddress_City` | `nvarchar(100)` | NOT NULL | |
| `ShippingAddress_State` | `nvarchar(100)` | NOT NULL | |
| `ShippingAddress_PinCode` | `nvarchar(20)` | NOT NULL | |
| `ShippingAddress_Phone` | `nvarchar(20)` | NOT NULL | |
| `ShippingAddress_IsDefault` | `bit` | NOT NULL | Always false for order copies |

**State Machine (strict transitions):**
Pending → Paid → Processing → Shipped → Delivered
Pending → Cancelled | Paid → Cancelled

---

## Table: `OrderItems`

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `Id` | `uniqueidentifier` | NOT NULL | PK |
| `OrderId` | `uniqueidentifier` | NOT NULL | FK → `Orders.Id` (CASCADE DELETE) |
| `WatchId` | `uniqueidentifier` | NOT NULL | FK → `Watches.Id` (RESTRICT DELETE) |
| `Quantity` | `int` | NOT NULL | |
| `UnitPrice` | `decimal(18,2)` | NOT NULL | Price at time of purchase |

---

## Table: `Wishlists`

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `Id` | `uniqueidentifier` | NOT NULL | PK |
| `UserId` | `uniqueidentifier` | NOT NULL | FK → `AspNetUsers.Id` (CASCADE DELETE) |
| `WatchId` | `uniqueidentifier` | NOT NULL | FK → `Watches.Id` (CASCADE DELETE) |
| `AddedAt` | `datetime2` | NOT NULL | UTC timestamp of addition |

**Unique Index:** `(UserId, WatchId)` — prevents duplicate entries

---

## Table: `Reviews`

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `Id` | `uniqueidentifier` | NOT NULL | PK |
| `WatchId` | `uniqueidentifier` | NOT NULL | FK → `Watches.Id` (CASCADE DELETE) |
| `UserId` | `uniqueidentifier` | NOT NULL | FK → `AspNetUsers.Id` (CASCADE DELETE) |
| `Rating` | `int` | NOT NULL | 1–5 scale |
| `Body` | `nvarchar(2000)` | NOT NULL | Review text |
| `CreatedAt` | `datetime2` | NOT NULL | UTC |

---

## Table: `UserAddresses`

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `Id` | `uniqueidentifier` | NOT NULL | PK |
| `UserId` | `uniqueidentifier` | NOT NULL | FK → `AspNetUsers.Id` (CASCADE DELETE) |
| `AddressType` | `int` | NOT NULL | Enum: 0=Shipping, 1=Billing |
| `Label` | `nvarchar(100)` | NOT NULL | e.g. Home, Office |
| `Line1` | `nvarchar(200)` | NOT NULL | |
| `Line2` | `nvarchar(200)` | NULL | |
| `City` | `nvarchar(100)` | NOT NULL | |
| `State` | `nvarchar(100)` | NOT NULL | |
| `PinCode` | `nvarchar(20)` | NOT NULL | |
| `Country` | `nvarchar(100)` | NOT NULL | Default "India" |
| `Phone` | `nvarchar(20)` | NOT NULL | |
| `IsDefault` | `bit` | NOT NULL | |
| `CreatedAt` | `datetime2` | NOT NULL | UTC |

---

## Table: `UserNotifications`

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `Id` | `uniqueidentifier` | NOT NULL | PK |
| `UserId` | `uniqueidentifier` | NOT NULL | FK → `AspNetUsers.Id` (CASCADE DELETE) |
| `Type` | `int` | NOT NULL | Enum: 0=OrderUpdate, 1=System, 2=Promotion |
| `Title` | `nvarchar(200)` | NOT NULL | |
| `Message` | `nvarchar(max)` | NOT NULL | |
| `ActionUrl` | `nvarchar(500)` | NULL | Optional link |
| `IsRead` | `bit` | NOT NULL | |
| `CreatedAt` | `datetime2` | NOT NULL | UTC |

---

## EF Core Migration History

| Migration | Applied | Description |
|---|---|---|
| `InitialCreate` | Session 1 | Full schema scaffold (all tables) |
| `AddStrapMaterial` | Session 2 | Added `StrapMaterial` column to `Watches` |
| `AddUserAvatarUrl` | Session 4 | Added `AvatarUrl` column to `AspNetUsers` |
| `AddUserAddressesAndNotifications` | Session 6 | Added UserAddress and UserNotification tables |
| `UpdateWatchesSeedData` | Session 8 | Data update for watch strings and wiping current Brands |

---

## Seed Data (from `SeedData.cs`)

- **Admin Role:** `Admin` (Guid-keyed)
- **Admin User:** `admin@wonderwatch.in` / `WonderWatch@Admin123!` — Role: Admin, Tier: Platinum
- **Sample Watches:** ~6 luxury timepieces (Rolex, Patek Philippe, AP, etc.) with images and GLB assets
- **Brands:** Auto-discovered from existing Watch.Brand values (seeded on first run)
- **FilterConfig:** Single row — MinPrice/MaxPrice derived from actual watch prices, rounded to nearest lakh

---

## Table: `Brands`

Admin-managed brand dictionary for catalog filter sidebar.

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `Id` | `Guid` | NOT NULL | PK |
| `Name` | `nvarchar(100)` | NOT NULL | Unique index |
| `SortOrder` | `int` | NOT NULL | Display order in filter UI |
| `IsActive` | `bit` | NOT NULL | Default: true. Admin can deactivate without deleting |

**Indexes:** `IX_Brands_Name` (Unique)

---

## Table: `FilterConfigs`

Single-row configuration for catalog price slider bounds.

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `Id` | `Guid` | NOT NULL | PK |
| `MinPrice` | `decimal(18,2)` | NOT NULL | Lower bound for price slider |
| `MaxPrice` | `decimal(18,2)` | NOT NULL | Upper bound for price slider |

---

## ERD Summary (Relationships)

```
AspNetUsers (1) ──── (*) Orders
AspNetUsers (1) ──── (*) Wishlists
AspNetUsers (1) ──── (*) Reviews
AspNetUsers (1) ──── (*) UserAddresses
AspNetUsers (1) ──── (*) UserNotifications

Orders (1) ──── (*) OrderItems
Watches (1) ──── (*) OrderItems
Watches (1) ──── (*) WatchImages
Watches (1) ──── (*) Reviews
Watches (1) ──── (*) Wishlists

Brands (standalone)  ← Admin-managed filter dictionary
FilterConfigs (standalone, single-row) ← Price slider bounds
```

---

## Migration History

| Migration | Date | Description |
|---|---|---|
| `InitialCreate` | 2026-03-22 | Core schema (Users, Watches, Orders, etc.) |
| `AddStrapMaterial` | 2026-04-07 | Added StrapMaterial column to Watches |
| `AddUserAvatarUrl` | 2026-04-08 | Added AvatarUrl to AspNetUsers |
| `AddUserAddressesAndNotifications` | 2026-04-09 | Added UserAddresses and UserNotifications tables |
| `AddFiltersConfig` | 2026-04-09 | Added Brands and FilterConfigs tables |
| `UpdateWatchesSeedData` | 2026-04-09 | Raw SQL fix for existing models missing straps and brands |
