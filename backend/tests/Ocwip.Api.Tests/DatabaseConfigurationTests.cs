using EFCore.NamingConventions.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Ocwip.Api.Data;
using Xunit;

namespace Ocwip.Api.Tests;

/// <summary>
/// Guards the single configuration path. Design time and runtime building the
/// EF model differently survives both migration and startup, and only surfaces
/// at the first query.
/// </summary>
public class DatabaseConfigurationTests
{
    [Fact]
    public void Design_time_factory_refuses_to_guess_a_missing_connection_string()
    {
        // Empty rather than absent, which is what appsettings.json ships.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = string.Empty,
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => AppDbContextFactory.Create(configuration));

        // The message has to say what to set, because there is no fallback.
        Assert.Contains(
            RequiresDatabaseFactAttribute.Variable,
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Use_ocwip_postgres_applies_npgsql_and_the_snake_case_convention()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        options.UseOcwipPostgres("Host=example.invalid;Database=ocwip;Username=ocwip;Password=ocwip");

        using var context = new AppDbContext(options.Options);

        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
        // Asserted on the options, not on a table name: the model has no
        // entities yet, so there is nothing whose name could be compared.
        Assert.NotNull(options.Options.FindExtension<NamingConventionsOptionsExtension>());
    }
}
