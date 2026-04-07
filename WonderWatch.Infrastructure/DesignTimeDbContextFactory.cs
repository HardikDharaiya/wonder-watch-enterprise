using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WonderWatch.Infrastructure
{
    /// <summary>
    /// Factory for instantiating AppDbContext during design time (e.g., running EF Core migrations).
    /// This bypasses the need for a fully configured Web project (Program.cs) during the initial scaffolding phase.
    /// </summary>
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var builder = new DbContextOptionsBuilder<AppDbContext>();

            // This connection string is strictly for design-time migration generation.
            // Runtime connection strings will be securely sourced from IConfiguration / Azure Key Vault in Program.cs.
            builder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=WonderWatch_Dev;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True");

            return new AppDbContext(builder.Options);
        }
    }
}