using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ocwip.Api.Data;

/// <summary>
/// Lets dotnet ef create the context without booting the web host.
/// Reads the same connection string the runtime uses; falls back to the
/// Docker Compose default so migrations can be authored offline.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=db;Port=5432;Database=ocwip;Username=ocwip;Password=ocwip";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(options);
    }
}
