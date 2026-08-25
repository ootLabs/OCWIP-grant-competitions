using Microsoft.EntityFrameworkCore;

namespace Ocwip.Api.Data;

/// <summary>
/// The only place that configures the provider and the naming convention, for
/// the application, for dotnet ef and for the tests.
///
/// The naming convention is part of the EF model, so it shapes both the
/// snapshot migrations are generated from and the SQL emitted at runtime. Set
/// in more than one place it eventually differs, and then a migration creates
/// created_at while the application asks for "CreatedAt". That drift surfaces
/// at the first query, not at migration time and not at startup.
/// </summary>
public static class PostgresDbContextOptions
{
    public static void UseOcwipPostgres(
        this DbContextOptionsBuilder options,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention();
    }
}
