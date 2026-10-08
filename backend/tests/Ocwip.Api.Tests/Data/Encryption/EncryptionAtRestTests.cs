using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Data.Configurations;
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
        var nip = await RawAsync("SELECT nip AS \"Value\" FROM entities WHERE id = {0}", entityId);
        Assert.Contains(nip, application);
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

    /// <summary>
    /// The three tables the first test did not reach, and the reason S-08 and
    /// S-34 could sit in the code with CI green: a report, an earlier version
    /// of a returned application and an evaluation card each hold answers,
    /// each has a column of its own, and a dump must be as useless for them
    /// as it is for the application.
    /// </summary>
    [RequiresDatabaseFact]
    public async Task No_sensitive_value_shows_in_a_report_an_earlier_version_or_an_evaluation_card()
    {
        var (versionId, reportId, evaluationId, reportVersionId) = await SeedDownstreamAsync();

        var version = await RawAsync(
            "SELECT answers::text || '|' || entity_snapshot::text AS \"Value\" FROM application_versions WHERE id = {0}", versionId);
        var report = await RawAsync(
            "SELECT answers::text || '|' || prefill::text AS \"Value\" FROM reports WHERE id = {0}", reportId);
        var evaluation = await RawAsync("SELECT answers::text AS \"Value\" FROM evaluations WHERE id = {0}", evaluationId);
        var keptVersion = await RawAsync(
            "SELECT answers::text || '|' || prefill::text AS \"Value\" FROM report_versions WHERE id = {0}", reportVersionId);

        foreach (var secret in new[] { Street, Phone, Member, Person })
        {
            Assert.DoesNotContain(secret, version);
            Assert.DoesNotContain(secret, report);
            Assert.DoesNotContain(secret, evaluation);
            Assert.DoesNotContain(secret, keptVersion);
        }

        // What is not sensitive stays readable, so the encryption is targeted.
        Assert.Contains("Jawny tytuł", version);
        Assert.Contains("Jawna kwota", report);
        Assert.Contains("Jawna ocena", evaluation);
    }

    [RequiresDatabaseFact]
    public async Task With_the_key_a_report_a_version_and_a_card_read_back_as_written()
    {
        var (versionId, reportId, evaluationId, reportVersionId) = await SeedDownstreamAsync();
        await using var context = database.CreateContext();

        var version = await context.ApplicationVersions.AsNoTracking().SingleAsync(x => x.Id == versionId);
        Assert.Equal(Member, version.Answers.GetProperty("czlonkowie")[0].GetProperty("imie").GetString());
        Assert.Equal(Street, EntitySnapshots.Read(version.EntitySnapshot)!.Address);

        var report = await context.Reports.AsNoTracking().SingleAsync(x => x.Id == reportId);
        Assert.Equal(Phone, report.Answers.GetProperty("osoba_telefon").GetString());
        Assert.Equal(Phone, report.Prefill.GetProperty("osoba_telefon").GetString());

        var evaluation = await context.Evaluations.AsNoTracking().SingleAsync(x => x.Id == evaluationId);
        Assert.Equal(Person, evaluation.Answers.GetProperty("uzasadnienie").GetString());

        var kept = await context.ReportVersions.AsNoTracking().SingleAsync(x => x.Id == reportVersionId);
        Assert.Equal(Phone, kept.Answers.GetProperty("osoba_telefon").GetString());
        Assert.Equal(Phone, kept.Prefill.GetProperty("osoba_telefon").GetString());
    }

    private async Task<(Guid VersionId, Guid ReportId, Guid EvaluationId, Guid ReportVersionId)> SeedDownstreamAsync()
    {
        await using var context = database.CreateContext();

        var chain = await TestApplicationChain.SeedAsync(database, $"dalej-{Guid.NewGuid():N}"[..20]);
        var entity = await context.Entities.SingleAsync(x => x.Id == chain.EntityId);
        entity.Address = Street;
        entity.Phone = Phone;
        entity.Representatives = [new EntityRepresentative("Zofia", "Ukryta", "Prezeska")];

        var user = TestUser.New($"karta-{Guid.NewGuid():N}@example.org");
        context.Users.Add(user);

        var application = TestApplication.Submitted(chain, $"SD/{Guid.NewGuid():N}"[..20]);
        context.Applications.Add(application);
        await context.SaveChangesAsync();

        var members = JsonDocument.Parse($$"""{"tytul":"Jawny tytuł","czlonkowie":[{"imie":"{{Member}}"}]}""").RootElement;
        var version = new ApplicationVersion
        {
            ApplicationId = application.Id,
            FormDefinitionId = chain.FormDefinitionId,
            VersionNumber = 1,
            Answers = SensitiveAnswers.Protect(
                members, new HashSet<string> { "czlonkowie" }, ApplicationVersionConfiguration.AnswersPurpose),
            EntitySnapshot = EntitySnapshots.Capture(EntitySnapshots.ToData(entity)),
            Checksum = "a1b2c3d4e5f6",
            SubmittedAt = new DateTimeOffset(2026, 9, 15, 10, 30, 0, TimeSpan.Zero),
            SupersededAt = new DateTimeOffset(2026, 9, 20, 10, 30, 0, TimeSpan.Zero),
        };

        var reportKeys = new HashSet<string> { "osoba_telefon" };
        var reportAnswers = JsonDocument.Parse($$"""{"kwota":"Jawna kwota","osoba_telefon":"{{Phone}}"}""").RootElement;
        var report = new Report
        {
            ApplicationId = application.Id,
            CompetitionId = chain.CompetitionId,
            EntityId = chain.EntityId,
            FormDefinitionId = chain.FormDefinitionId,
            Answers = SensitiveAnswers.Protect(reportAnswers, reportKeys, SensitiveAnswers.ReportPurpose),
            Prefill = SensitiveAnswers.Protect(reportAnswers, reportKeys, SensitiveAnswers.ReportPurpose),
        };

        var cardAnswers = JsonDocument.Parse($$"""{"ocena":"Jawna ocena","uzasadnienie":"{{Person}}"}""").RootElement;
        var evaluation = new Evaluation
        {
            CompetitionId = chain.CompetitionId,
            ApplicationId = application.Id,
            FormDefinitionId = chain.FormDefinitionId,
            Stage = EvaluationStage.Formal,
            AuthorUserId = user.Id,
            EnteredByUserId = user.Id,
            Answers = SensitiveAnswers.Protect(
                cardAnswers, new HashSet<string> { "uzasadnienie" }, SensitiveAnswers.EvaluationPurpose),
            Status = EvaluationStatus.Draft,
        };

        var reportVersion = new ReportVersion
        {
            ReportId = report.Id,
            VersionNumber = 1,
            FormDefinitionId = chain.FormDefinitionId,
            Answers = SensitiveAnswers.Protect(reportAnswers, reportKeys, ReportVersionConfiguration.AnswersPurpose),
            Prefill = SensitiveAnswers.Protect(reportAnswers, reportKeys, ReportVersionConfiguration.PrefillPurpose),
            SubmittedAt = new DateTimeOffset(2026, 9, 15, 10, 30, 0, TimeSpan.Zero),
            SupersededAt = new DateTimeOffset(2026, 9, 20, 10, 30, 0, TimeSpan.Zero),
        };

        context.ApplicationVersions.Add(version);
        context.Reports.Add(report);
        context.Evaluations.Add(evaluation);
        await context.SaveChangesAsync();

        reportVersion.ReportId = report.Id;
        context.ReportVersions.Add(reportVersion);
        await context.SaveChangesAsync();

        return (version.Id, report.Id, evaluation.Id, reportVersion.Id);
    }
}
