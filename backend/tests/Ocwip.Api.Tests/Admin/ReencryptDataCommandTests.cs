using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Ocwip.Api.Admin;
using Ocwip.Api.Models;
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

    /// <summary>
    /// A form version that no longer passes the contract (a stricter rule
    /// since) still says which answers are sensitive: they come out
    /// encrypted, never written back in the clear.
    /// </summary>
    [RequiresDatabaseFact]
    public async Task An_answer_marked_sensitive_in_an_outdated_form_is_still_encrypted()
    {
        var chain = await TestApplicationChain.SeedAsync(database, "stary-formularz");
        Guid applicationId;
        await using (var context = database.CreateContext())
        {
            // No schemaVersion: refused by today's contract, marks as stored.
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE form_definitions SET definition = {1}::jsonb WHERE id = {0}",
                chain.FormDefinitionId,
                """{"sections":[{"key":"s","title":"S","fields":[{"key":"konto","type":"shortText","sensitive":true},{"key":"opis","type":"shortText"}]}]}""");
            var application = TestApplication.Draft(chain, """{"konto":"PL61109010140000071219812874","opis":"jawny"}""");
            context.Applications.Add(application);
            await context.SaveChangesAsync();
            applicationId = application.Id;
        }

        await using var output = new StringWriter();
        var exit = await AdminCommandRunner.RunAsync([ReencryptDataCommand.Verb], Configuration, output);

        Assert.Equal(AdminCommandRunner.Success, exit);
        var stored = await RawAsync("SELECT answers::text AS \"Value\" FROM applications WHERE id = {0}", applicationId);
        Assert.DoesNotContain("PL61109010140000071219812874", stored);
        Assert.Contains("jawny", stored);
    }

    [Theory]
    [InlineData("""{"sections":[]}""", "")]
    [InlineData("""{"sections":[{"fields":[{"key":"a","sensitive":true},{"key":"b"}]}]}""", "a")]
    [InlineData("""{"sections":[{"fields":[{"key":"t","table":{"columns":[{"key":"x"},{"key":"y","sensitive":true}]}}]}]}""", "t")]
    [InlineData("""{"sections":"nie tablica"}""", "")]
    public void Marks_are_read_from_a_stored_definition_as_they_are(string definition, string expected)
    {
        using var document = System.Text.Json.JsonDocument.Parse(definition);

        var keys = Ocwip.Api.Models.Forms.SensitiveAnswers.MarkedKeys(document.RootElement);

        Assert.Equal(expected, string.Join(",", keys.Order()));
    }

    [RequiresDatabaseFact]
    public async Task Options_are_refused()
    {
        await using var output = new StringWriter();

        var exit = await AdminCommandRunner.RunAsync([ReencryptDataCommand.Verb, "--all"], Configuration, output);

        Assert.Equal(AdminCommandRunner.Failure, exit);
    }

    /// <summary>
    /// The same for a report, which the rotation rewrites last: reading its
    /// form strictly stopped the run there and left the reports under the old
    /// key, which is the one the rotation exists to retire (S-37).
    /// </summary>
    [RequiresDatabaseFact]
    public async Task A_report_on_an_outdated_form_is_rewritten_instead_of_stopping_the_rotation()
    {
        var chain = await TestApplicationChain.SeedAsync(database, "stare-sprawozdanie");
        Guid reportId;
        await using (var context = database.CreateContext())
        {
            // No schemaVersion: refused by today's contract, marks as stored.
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE form_definitions SET definition = {1}::jsonb WHERE id = {0}",
                chain.FormDefinitionId,
                """{"sections":[{"key":"s","title":"S","fields":[{"key":"telefon","type":"shortText","sensitive":true},{"key":"kwota","type":"shortText"}]}]}""");

            var application = TestApplication.Submitted(chain, $"SR/{Guid.NewGuid():N}"[..20]);
            context.Applications.Add(application);
            await context.SaveChangesAsync();

            var report = new Report
            {
                ApplicationId = application.Id,
                CompetitionId = chain.CompetitionId,
                EntityId = chain.EntityId,
                FormDefinitionId = chain.FormDefinitionId,
                Answers = JsonDocument.Parse("""{"telefon":"600 999 888","kwota":"jawna"}""").RootElement,
                Prefill = JsonDocument.Parse("""{"telefon":"600 999 888"}""").RootElement,
            };
            context.Reports.Add(report);
            await context.SaveChangesAsync();
            reportId = report.Id;
        }

        await using var output = new StringWriter();
        var exit = await AdminCommandRunner.RunAsync([ReencryptDataCommand.Verb], Configuration, output);

        Assert.Equal(AdminCommandRunner.Success, exit);

        var stored = await RawAsync(
            "SELECT answers::text || '|' || prefill::text AS \"Value\" FROM reports WHERE id = {0}", reportId);
        Assert.DoesNotContain("600 999 888", stored);
        Assert.Contains("jawna", stored);
    }
}
