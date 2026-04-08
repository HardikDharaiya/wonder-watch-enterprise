using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using WonderWatch.Application.Interfaces;
using WonderWatch.Application.Services;
using WonderWatch.Domain.Identity;
using WonderWatch.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure Serilog (Structured Logging)
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

try
{
    Log.Information("Starting Wonder Watch Enterprise Web Host");

    // 2. Database Context
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Server=(localdb)\\mssqllocaldb;Database=WonderWatch_Dev;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(connectionString));

    // 3. ASP.NET Core Identity (NO OAuth, strictly local accounts)
    builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
    });

    // 4. Session State (For Cart functionality)
    builder.Services.AddDistributedMemoryCache();
    builder.Services.AddSession(options =>
    {
        options.IdleTimeout = TimeSpan.FromHours(2);
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
    });

    // 5. WebOptimizer (CSS/JS Minification & Bundling)
    builder.Services.AddWebOptimizer(pipeline =>
    {
        pipeline.MinifyCssFiles("css/**/*.css");
        pipeline.MinifyJsFiles("js/**/*.js");
    });

    // 6. Application Services DI Registration
    builder.Services.AddScoped<ICatalogService, CatalogService>();
    builder.Services.AddScoped<IOrderService, OrderService>();
    builder.Services.AddScoped<IPaymentProvider, PaymentService>();
    builder.Services.AddScoped<IWishlistService, WishlistService>();
    builder.Services.AddScoped<IAssetService, AssetService>();
    builder.Services.AddScoped<IAdminService, AdminService>();
    builder.Services.AddScoped<IEmailService, EmailService>();
    builder.Services.AddScoped<IAddressService, AddressService>();
    builder.Services.AddScoped<INotificationService, NotificationService>();

    // 7. MVC Controllers & Views
    builder.Services.AddControllersWithViews();

    var app = builder.Build();

    // ---------------------------------------------------------
    // STRICT MIDDLEWARE PIPELINE (Order is inviolable)
    // ---------------------------------------------------------

    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }
    else
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }

    app.UseStatusCodePagesWithReExecute("/Home/Error", "?statusCode={0}");

    app.UseHttpsRedirection();

    app.UseWebOptimizer(); // Must be before UseStaticFiles

    // ---------------------------------------------------------
    // FIXED: Configure MIME types to allow serving 3D Models (.glb)
    // ---------------------------------------------------------
    var provider = new FileExtensionContentTypeProvider();
    provider.Mappings[".glb"] = "model/gltf-binary";
    provider.Mappings[".gltf"] = "model/gltf+json";

    app.UseStaticFiles(new StaticFileOptions
    {
        ContentTypeProvider = provider
    });

    app.UseRouting();

    app.UseSession(); // Must be between UseRouting and UseAuthentication

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    // ---------------------------------------------------------
    // SEED DATA INVOCATION
    // ---------------------------------------------------------
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        try
        {
            await SeedData.InitializeAsync(services);
            Log.Information("Database seeded successfully.");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "An error occurred while seeding the database.");
        }
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}