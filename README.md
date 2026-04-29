# Wonder Watch Enterprise

Welcome to the **Wonder Watch** source code repository. Wonder Watch is an enterprise-grade MVC web application designed as a sophisticated digital boutique for high-end, dark-luxury horology. 

## 🏗 Architecture & Stack
- **Framework:** ASP.NET Core MVC (.NET 8)
- **Styling:** Tailwind CSS (JIT compiling via Node)
- **Database:** Entity Framework Core (SQL Server)
- **Deployment:** Azure Web App CI/CD ready (`.github/workflows/ci-cd.yml`)
- **Assets:** 3D model processing (`.glb`) and WebP optimized photography

## 🚀 Local Setup Instructions

1. **Clone the Repository**

2. **Restore .NET Dependencies**
   Navigate to the root folder `WONDER_WATCH_MVC` and run:
   ```bash
   dotnet restore WonderWatch.sln
   ```

3. **Install NPM Packages & Build CSS**
   Navigate into the web project folder to generate the CSS assets:
   ```bash
   cd WonderWatch.Web
   npm install
   npm run build:css
   ```

4. **Database Configuration Setup**
   The application utilizes a rich Entity Framework Core integration. Ensure you have SQL Server LocalDB installed, or update the development connection string.
   - The design-time factory connection string is located at:
     `WonderWatch.Infrastructure/DesignTimeDbContextFactory.cs`
   - Data access patterns and DbSets are registered inside:
     `WonderWatch.Infrastructure/AppDbContext.cs`
   
5. **Run Migrations & Seeding**
   The database will automatically run pending migrations on startup. 
   **Note:** Default admin credentials and catalog items are seeded via `WonderWatch.Infrastructure/SeedData.cs`.
   If you ever need to reset the schema during development, an Idempotent Re-seed option exists at `/admin/reset` secured by the Admin Role.
   
   **Default Admin Identity:**
   - **User:** `alexander@wonderwatch.in`
   - **Password:** `WonderAdmin@2026!`

6. **Payment Gateway Setup (Razorpay)**
   The application uses Razorpay for secure payments. In development, credentials should be stored in the .NET User Secrets store:
   ```bash
   # Initialize secrets
   dotnet user-secrets init --project WonderWatch.Web

   # Set your Razorpay Test Keys
   dotnet user-secrets set "Razorpay:KeyId" "rzp_test_YOUR_KEY_ID" --project WonderWatch.Web
   dotnet user-secrets set "Razorpay:KeySecret" "YOUR_KEY_SECRET" --project WonderWatch.Web
   ```

7. **SMTP Configuration (OTP Emails)**
   The OTP verification system requires a configured SMTP server. Set up via Admin Panel (`/admin/settings`) or directly in `appsettings.json`:
   ```json
   "SmtpSettings": {
     "Host": "smtp.gmail.com",
     "Port": 587,
     "Username": "your-email@gmail.com",
     "Password": "your-app-password",
     "FromEmail": "noreply@wonderwatch.in",
     "FromName": "Wonder Watch"
   }
   ```

8. **Run the Project**
   ```bash
   cd WonderWatch.Web
   dotnet run
   ```
   Open the target localhost URL provided in your console to view the store.

## 🌟 Key Functional Features
- **3D Showrooms**: Dynamic Model loading via Three.js natively rendering luxury timepieces.
- **OTP Verification & Password Recovery**: Email-based 6-digit OTP for registration verification and secure password reset. Uses Identity's native `TwoFactorTokenAsync` — zero additional database tables.
- **Journal Subscriptions**: Complete Ajax-friendly Footer subscription catching marketing campaigns securely.
- **The Concierge**: Seamless fetch-API integrated support communications pipeline.
- **Pay on Delivery**: Robust fallback payment mechanisms supporting custom region rules logic.
- **Vault Memberships**: Admin-driven tier management supporting recurrent logic and billing features via Razorpay.

## 📁 Key File Contexts
- **`.github/workflows/ci-cd.yml`**: Contains the GitHub Actions scripts responsible for checking out, building .NET, building Tailwind, testing, and deploying zero-downtime releases to our Azure environment.
- **`WonderWatch.Infrastructure/Migrations/*`**: Contains all incremental snapshots for configuring the schema via Entity Framework.

> **Notice:** The `.vs` folder has purposefully been ignored for source control out of standard practice, preventing local `applicationhost.config` machine-port bindings from interfering with collaborative efforts.

