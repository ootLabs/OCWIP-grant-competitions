using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ocwip.Api.Data;

/// <summary>
/// Lets dotnet ef create the context without booting the web host.
///
/// Reads ConnectionStrings:Postgres from the same sources as the running
/// application and throws when it is missing. A hardcoded default would be
/// worse than an error here: "db" is the compose service name in half the
/// projects on one laptop, so a guessed address lets database update rewrite
/// someone else's schema and exit with code 0.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) => Create(BuildConfiguration());

    internal static AppDbContext Create(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres");

        // Empty counts as missing: appsettings.json ships the key with no value.
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'Postgres' is not configured. Set the "
                + "ConnectionStrings__Postgres environment variable, or "
                + "ConnectionStrings:Postgres in appsettings or user secrets.");
        }

        var options = new DbContextOptionsBuilder<AppDbContext>();
        options.UseOcwipPostgres(connectionString);

        return new AppDbContext(options.Options);
    }

    /// <summary>
    /// Internal rather than private because the administrative command
    /// (Admin/AdminCommandRunner.cs) has to read the connection string from the
    /// same sources, and a second copy of this list would drift.
    /// </summary>
    internal static IConfiguration BuildConfiguration()
    {
        var environment =
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

        return new ConfigurationBuilder()
            // The build output, because that is where the SDK copies the
            // settings files and dotnet ef runs from the solution directory.
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddUserSecrets<AppDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();
    }
}
