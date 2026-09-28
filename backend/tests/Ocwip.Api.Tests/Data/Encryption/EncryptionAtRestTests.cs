using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Models;
using Ocwip.Api.Models.Forms;
using Ocwip.Api.Services.EntityCards;
using Xunit;

namespace Ocwip.Api.Tests.Data.Encryption;

/// <summary>
/// The T-47a criterion "a dump of the database without the key is useless",
/// as a test: every sensitive value written through the model is read back
/// with SQL, the way pg_dump sees it, and none of it shows.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class EncryptionAtRestTests(PostgresDatabaseFixture database)
{
    // Distinctive, so a leak cannot hide behind a common word.
    private const string Street = "ul. Sekretna 17";
    private const string Phone = "600 999 888";
    private const string Email = "tajny@example.org";
    private const string Person = "Zofia Ukryta";
    private const string Pesel = "85010112345";
    private const string Member = "Jan Nieznany";

    private async Task<string> RawAsync(string sql, Guid id)
    {
        await using var context = database.CreateContext();
        return await context.Database.SqlQueryRaw<string>(sql, id).SingleAsync();
    }

    private async Task<(Guid EntityId, Guid ApplicationId, Guid UserId, Guid ContractId)> SeedAsync()
    {
        await using var context = database.CreateContext();

        var chain = await TestApplicationChain.SeedAsync(database, "szyfrowanie");
        var entity = await context.Entities.SingleAsync(x => x.Id == chain.EntityId);
        entity.Address = Street;
        entity.CorrespondenceAddress = Street;
        entity.Phone = Phone;
        entity.Email = Email;
        entity.Representatives = [new EntityRepresentative("Zofia", "Ukryta", "Prezeska")];

        var user = TestUser.New($"szyfrowanie-{Guid.NewGuid():N}@example.org", pesel: Pesel);
        context.Users.Add(user);

        var answers = JsonDocument.Parse($$"""{"tytul":"Jawny tytuł","czlonkowie":[{"imie":"{{Member}}"}]}""").RootElement;
        var application = TestApplication.Submitted(chain, $"SZ/{Guid.NewGuid():N}"[..20]);
        application.Answers = SensitiveAnswers.Protect(answers, new HashSet<string> { "czlonkowie" });
        application.EntitySnapshot = EntitySnapshots.Capture(EntitySnapshots.ToData(entity));
        context.Applications.Add(application);

        var template = new DocumentTemplate
        {
            CompetitionId = chain.CompetitionId, Kind = DocumentKind.Contract, VersionNumber = 1, Body = "{{pesel_skarbnika}}",
        };
        context.Add(template);
        await context.SaveChangesAsync();

        var contract = new Contract
        {
            ApplicationId = application.Id,
            CompetitionId = chain.CompetitionId,
            EntityId = chain.EntityId,
            TemplateId = template.Id,
            Values = JsonSerializer.SerializeToElement(new Dictionary<string, string> { ["pesel_skarbnika"] = Pesel }),
        };
        context.Contracts.Add(contract);
        await context.SaveChangesAsync();

        return (entity.Id, application.Id, user.Id, contract.Id);
    }

    [RequiresDatabaseFact]
    public async Task No_sensitive_value_shows_in_the_rows_a_dump_would_hold()
    {
        var (entityId, applicationId, userId, contractId) = await SeedAsync();

        var entity = await RawAsync(
            "SELECT concat_ws('|', address, correspondence_address, phone, email, bank_account, representatives) AS \"Value\" FROM entities WHERE id = {0}",
            entityId);
        var application = await RawAsync(
            "SELECT answers::text || '|' || entity_snapshot::text AS \"Value\" FROM applications WHERE id = {0}", applicationId);
        var pesel = await RawAsync("SELECT pesel AS \"Value\" FROM users WHERE id = {0}", userId);
        var contract = await RawAsync("SELECT values::text AS \"Value\" FROM contracts WHERE id = {0}", contractId);

        foreach (var secret in new[] { Street, Phone, Email, "Ukryta", Pesel, Member, TestEntity.BankAccount })
        {
            Assert.DoesNotContain(secret, entity);
            Assert.DoesNotContain(secret, application);
            Assert.DoesNotContain(secret, pesel);
            Assert.DoesNotContain(secret, contract);
        }

        // What is not sensitive stays readable, and searchable in jsonb.
        Assert.Contains("Jawny tytuł", application);
        Assert.Contains(TestEntity.Nip, application);
        Assert.Contains("pesel_skarbnika", contract);
    }

    [RequiresDatabaseFact]
    public async Task With_the_key_everything_reads_back_as_written()
    {
        var (entityId, applicationId, userId, contractId) = await SeedAsync();
        await using var context = database.CreateContext();

        var entity = await context.Entities.AsNoTracking().SingleAsync(x => x.Id == entityId);
        Assert.Equal(Street, entity.Address);
        Assert.Equal(Phone, entity.Phone);
        Assert.Equal(Email, entity.Email);
        Assert.Equal(TestEntity.BankAccount, entity.BankAccount);
        Assert.Equal(Person, $"{entity.Representatives[0].FirstName} {entity.Representatives[0].LastName}");

        var application = await context.Applications.AsNoTracking().SingleAsync(x => x.Id == applicationId);
        Assert.Equal(Member, application.Answers.GetProperty("czlonkowie")[0].GetProperty("imie").GetString());
        Assert.Equal(Street, EntitySnapshots.Read(application.EntitySnapshot)!.Address);

        Assert.Equal(Pesel, (await context.Users.AsNoTracking().SingleAsync(x => x.Id == userId)).Pesel);
        var contract = await context.Contracts.AsNoTracking().SingleAsync(x => x.Id == contractId);
        Assert.Equal(Pesel, contract.Values.GetProperty("pesel_skarbnika").GetString());
    }
}
