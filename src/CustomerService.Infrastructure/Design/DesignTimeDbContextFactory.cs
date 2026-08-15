using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using CustomerService.Infrastructure.Data;

namespace CustomerService.Infrastructure.Design
{
    // Design-time factory for EF Core tools.
    // Resolves the connection string the same way the running app does:
    // appsettings.json -> appsettings.Development.json -> user-secrets -> environment variables.
    // This means `dotnet ef` commands work without needing to manually set an env var each session.
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CustomerDbContext>
    {
        public CustomerDbContext CreateDbContext(string[] args)
        {
            var basePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "CustomerService.API");

            // Copied from CustomerService.API.csproj's <UserSecretsId> element.
            // Hardcoded here (instead of AddUserSecrets<Program>()) because referencing the
            // API project's Program type from Infrastructure would create a circular project
            // reference — API already references Infrastructure.
            const string userSecretsId = "1c30d0fb-891c-4328-af87-b3e6467e8e47";

            var configuration = new ConfigurationBuilder()
                .SetBasePath(Path.GetFullPath(basePath))
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddUserSecrets(userSecretsId)
                .AddEnvironmentVariables()
                .Build();

            var conn = configuration.GetConnectionString("CustDb");

            if (string.IsNullOrWhiteSpace(conn))
            {
                throw new InvalidOperationException("Connection string 'CustDb' not found. Checked appsettings.json, appsettings.Development.json, user-secrets, and environment variables.");
            }

            var optionsBuilder = new DbContextOptionsBuilder<CustomerDbContext>();
            optionsBuilder.UseSqlServer(conn, sql =>
            {
                sql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null);
            });

            return new CustomerDbContext(optionsBuilder.Options);
        }
    }
}
