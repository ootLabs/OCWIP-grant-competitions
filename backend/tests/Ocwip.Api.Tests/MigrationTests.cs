using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Data;
using Ocwip.Api.Tests.Data;
using Xunit;

namespace Ocwip.Api.Tests;

/// <summary>
/// Proves that the migration chain applies to an empty database.
/// </summary>
[Collection(Data.PostgresCollection.Name)]
public class MigrationTests
{
    [RequiresDatabaseFact]
    public async Task Migrations_apply_to_a_clean_database()
    {
        await using var database = await ThrowawayDatabase.CreateAsync("mig");

        // The same configuration the application and dotnet ef use, so this
        // test cannot pass against settings the API never runs with.
        var options = new DbContextOptionsBuilder<AppDbContext>();
        options.UseOcwipPostgres(database.ConnectionString);

        await using var context = new AppDbContext(options.Options);
        await context.Database.MigrateAsync();

        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.NotEmpty(await context.Database.GetAppliedMigrationsAsync());
    }
}
