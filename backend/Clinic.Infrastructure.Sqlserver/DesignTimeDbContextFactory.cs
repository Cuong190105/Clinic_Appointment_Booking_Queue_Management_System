using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace Clinic.Infrastructure.Sqlserver.Persistence
{
    // Design-time factory so `dotnet ef` can create ApplicationDbContext.
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            // Try to load appsettings.json from current project then from Clinic.API
            var basePaths = new[]
            {
                Directory.GetCurrentDirectory(),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "Clinic.API"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "Clinic.API")
            };

            IConfigurationRoot? config = null;
            foreach (var p in basePaths)
            {
                var candidate = Path.Combine(p, "appsettings.json");
                if (File.Exists(candidate))
                {
                    var builder = new ConfigurationBuilder()
                        .SetBasePath(p)
                        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                        .AddEnvironmentVariables();
                    config = builder.Build();
                    break;
                }
            }

            if (config == null)
            {
                // fallback to environment variables only
                var builder = new ConfigurationBuilder().AddEnvironmentVariables();
                config = builder.Build();
            }

            var conn = config.GetConnectionString("DefaultConnection") ?? config["DefaultConnection"];
            if (string.IsNullOrEmpty(conn))
            {
                throw new InvalidOperationException("Could not find a connection string named 'DefaultConnection' in appsettings.json or environment variables.");
            }

            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseSqlServer(conn);

            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }
}
