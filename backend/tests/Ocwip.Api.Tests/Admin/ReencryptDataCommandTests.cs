using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Ocwip.Api.Admin;
using Ocwip.Api.Tests.Data;
using Xunit;

namespace Ocwip.Api.Tests.Admin;

/// <summary>
/// reencrypt-data (T-47a): a row written before encryption, as plaintext,
/// comes out encrypted, still reads the same, and keeps its "dane
/// zaktualizowane".
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ReencryptDataCommandTests(PostgresDatabaseFixture database)
{
    private IConfiguration Configuration => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Postgres"] = database.ConnectionString })
        .Build();

    private async Task<string> RawAsync(string sql, Guid id)
    {
        await using var context = database.CreateContext();
        return await context.Database.SqlQueryRaw<string>(sql, id).SingleAsync();
    }

    [RequiresDatabaseFact]
    public async Task A_row_from_before_encryption_is_encrypted_and_keeps_its_update_time()
    {
        var chain = await TestApplicationChain.SeedAsync(database, "przeszyfrowanie");
        await using (var context = database.CreateContext())
        {
            // The way a row looked before T-47a: plaintext, set past the model.
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE entities SET address = 'ul. Dawna 5', representatives = {1}, " +
                "updated_at = '2026-01-01T00:00:00Z' WHERE id = {0}",
                chain.EntityId,
                """[{"firstName":"Ewa","lastName":"Dawna","function":"Prezeska"}]""");
        }

        await using var output = new StringWriter();
        var exit = await AdminCommandRunner.RunAsync([ReencryptDataCommand.Verb], Configuration, output);

        Assert.Equal(AdminCommandRunner.Success, exit);
        Assert.Contains("Rewrote with key 1", output.ToString());

        var stored = await RawAsync("SELECT address || '|' || representatives AS \"Value\" FROM entities WHERE id = {0}", chain.EntityId);
        Assert.DoesNotContain("Dawna", stored);
        Assert.StartsWith("enc:1:", stored);

        await using (var context = database.CreateContext())
        {
            var entity = await context.Entities.AsNoTracking().SingleAsync(x => x.Id == chain.EntityId);
            Assert.Equal("ul. Dawna 5", entity.Address);
            Assert.Equal("Ewa", entity.Representatives.Single().FirstName);
            Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), entity.UpdatedAt);
        }
    }

    [RequiresDatabaseFact]
    public async Task Options_are_refused()
    {
        await using var output = new StringWriter();

        var exit = await AdminCommandRunner.RunAsync([ReencryptDataCommand.Verb, "--all"], Configuration, output);

        Assert.Equal(AdminCommandRunner.Failure, exit);
    }
}
