using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Ocwip.Api.Tests.Models.Forms;
using Xunit;
using static Ocwip.Api.Tests.Endpoints.ApplicationTestHost;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// The Podmiot card over HTTP (T-93): a new account creates it at the first
/// application and submits without anybody's help, a correction never
/// rewrites a submitted application, and nobody reaches another account's card.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class EntityCardEndpointsTests : IClassFixture<OcwipWebApplicationFactory>
{
    private readonly OcwipWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public EntityCardEndpointsTests(OcwipWebApplicationFactory factory, PostgresDatabaseFixture database)
    {
        _factory = factory;
        _database = database;
    }

    public static EntityCardData OrganisationCard(
        EntityType type = EntityType.Organisation, string name = "Stowarzyszenie Karta Testowa") =>
        new(
            type,
            name,
            LegalForm.Association,
            null,
            EntityRegister.Krs,
            "0000000001",
            "111-111-11-11",
            null,
            "ul. Testowa 1, 45-000 Opole",
            null,
            "+48 700 100 200",
            "biuro@example.org",
            "PL73 1111 1111 1111 1111 1111 1111",
            [new EntityRepresentative("Anna", "Testowa", "Prezeska")]);

    [RequiresDatabaseTheory]
    [InlineData(EntityType.Organisation)]
    [InlineData(EntityType.PatronInformalGroup)]
    [InlineData(EntityType.InformalGroup)]
    public async Task A_new_account_creates_its_card_at_the_first_application_and_submits_it(EntityType type)
    {
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);
        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var applicant = await NewApplicantAsync(host);

        Assert.Equal(HttpStatusCode.NotFound, (await applicant.GetAsync("/me/entity")).StatusCode);

        var card = type is EntityType.InformalGroup
            ? new EntityCardData(type, "Sąsiedzi z Zaodrza")
            : OrganisationCard(type);
        var created = await applicant.PostAsJsonAsync("/me/entity", card);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var stored = (await applicant.GetFromJsonAsync<EntityCardResponse>("/me/entity"))!;
        Assert.Equal(type, stored.Card.Type);
        if (type is not EntityType.InformalGroup)
        {
            // Stored as the digits the checksums were computed on.
            Assert.Equal(TestEntity.Nip, stored.Card.Nip);
            Assert.Equal(TestEntity.BankAccount, stored.Card.BankAccount);
        }

        var draft = await CreateAsync(applicant, competition.Id);
        await SaveAsync(applicant, draft.Id, FormDefinitionSamples.Parse("""{"opis":"Nasz projekt"}"""));
        var submit = await applicant.PostAsync($"/applications/{draft.Id}/submit", content: null);

        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        var submitted = (await submit.Content.ReadFromJsonAsync<ApplicationResponse>())!;
        Assert.Equal(card.Name, submitted.EntitySnapshot!.Name);
        Assert.Equal(type, submitted.EntitySnapshot.Type);
    }

    [RequiresDatabaseFact]
    public async Task A_correction_of_the_card_leaves_the_submitted_application_as_it_was()
    {
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);
        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var applicant = await NewApplicantAsync(host);
        (await applicant.PostAsJsonAsync("/me/entity", OrganisationCard())).EnsureSuccessStatusCode();

        var draft = await CreateAsync(applicant, competition.Id);
        await SaveAsync(applicant, draft.Id, FormDefinitionSamples.Parse("""{"opis":"Nasz projekt"}"""));
        (await applicant.PostAsync($"/applications/{draft.Id}/submit", content: null)).EnsureSuccessStatusCode();

        var corrected = OrganisationCard(name: "Nowa nazwa po zmianie statutu") with { Address = "ul. Nowa 2, 45-000 Opole" };
        (await applicant.PutAsJsonAsync("/me/entity", corrected)).EnsureSuccessStatusCode();

        var application = await GetAsync(applicant, draft.Id);
        Assert.Equal("Stowarzyszenie Karta Testowa", application.EntitySnapshot!.Name);
        Assert.Equal("ul. Testowa 1, 45-000 Opole", application.EntitySnapshot.Address);

        var card = (await applicant.GetFromJsonAsync<EntityCardResponse>("/me/entity"))!;
        Assert.Equal("Nowa nazwa po zmianie statutu", card.Card.Name);

        // The organiser's copy too: the operator's view and the PDF.
        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        var seen = (await operatorClient.GetFromJsonAsync<SubmittedApplicationResponse>(
            $"/competitions/{competition.Id}/applications/{draft.Id}"))!;
        Assert.Equal("Stowarzyszenie Karta Testowa", seen.EntityName);
        Assert.Equal("ul. Testowa 1, 45-000 Opole", seen.EntityCard!.Address);

        // The working list and the file printed from it say the same (S-06).
        // They used to read the live card, so the two documents the organiser
        // runs one intake from disagreed about who filed the offer, and the
        // applicant decided which one changed.
        var list = (await operatorClient.GetFromJsonAsync<ApplicationListResponse>(
            $"/competitions/{competition.Id}/applications"))!;
        Assert.Equal("Stowarzyszenie Karta Testowa", Assert.Single(list.Applications).EntityName);

        var csv = await (await operatorClient.GetAsync($"/competitions/{competition.Id}/applications/export/csv"))
            .EnsureSuccessStatusCode().Content.ReadAsStringAsync();
        Assert.Contains("Stowarzyszenie Karta Testowa", csv);
        Assert.DoesNotContain("Nowa nazwa po zmianie statutu", csv);
    }

    [RequiresDatabaseFact]
    public async Task Nobody_reads_or_changes_another_account_s_card()
    {
        var (host, _) = CompetitionTestHost.Create(_factory, _database);
        var owner = await NewApplicantAsync(host);
        (await owner.PostAsJsonAsync("/me/entity", OrganisationCard(name: "Cudza Fundacja"))).EnsureSuccessStatusCode();
        var other = await NewApplicantAsync(host);

        var read = await other.GetAsync("/me/entity");
        var change = await other.PutAsJsonAsync("/me/entity", OrganisationCard(name: "Przejęta"));

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.DoesNotContain("Cudza Fundacja", await read.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, change.StatusCode);

        var ownerCard = (await owner.GetFromJsonAsync<EntityCardResponse>("/me/entity"))!;
        Assert.Equal("Cudza Fundacja", ownerCard.Card.Name);

        // Other roles have no card of their own and no way to anybody's.
        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        Assert.Equal(HttpStatusCode.Forbidden, (await operatorClient.GetAsync("/me/entity")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.CreateClient().GetAsync("/me/entity")).StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task A_card_with_wrong_numbers_is_refused_naming_each_field()
    {
        var (host, _) = CompetitionTestHost.Create(_factory, _database);
        var applicant = await NewApplicantAsync(host);

        var response = await applicant.PostAsJsonAsync(
            "/me/entity",
            OrganisationCard() with { Nip = "1234567890", BankAccount = "11 1111 1111", RegisterNumber = "123" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"nip\"", body);
        Assert.Contains("\"bankAccount\"", body);
        Assert.Contains("\"registerNumber\"", body);
        Assert.Equal(HttpStatusCode.NotFound, (await applicant.GetAsync("/me/entity")).StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task A_second_card_is_refused_even_when_both_requests_race()
    {
        var (host, _) = CompetitionTestHost.Create(_factory, _database);
        var email = SessionTestHost.Email("wyscig");
        var user = await SessionTestHost.CreateAccountAsync(host, email, Role.Applicant);
        var first = await LoginAsync(host, email);
        var second = await LoginAsync(host, email);

        var responses = await Task.WhenAll(
            first.PostAsJsonAsync("/me/entity", OrganisationCard(name: "Pierwsza")),
            second.PostAsJsonAsync("/me/entity", OrganisationCard(name: "Druga")));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);

        await using var context = _database.CreateContext();
        var entityId = await context.Users.Where(x => x.Id == user.Id).Select(x => x.EntityId).SingleAsync();
        var name = await context.Entities.Where(x => x.Id == entityId).Select(x => x.Name).SingleAsync();
        var card = (await first.GetFromJsonAsync<EntityCardResponse>("/me/entity"))!;
        Assert.Equal(name, card.Card.Name);
    }

    [RequiresDatabaseFact]
    public async Task Changing_the_card_s_type_later_leaves_a_submitted_application_as_submitted()
    {
        // Until T-94 the type was locked here, because the evaluation read
        // the live card. Now every submission freezes its own kind.
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);
        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var applicant = await NewApplicantAsync(host);
        (await applicant.PostAsJsonAsync("/me/entity", OrganisationCard())).EnsureSuccessStatusCode();

        var draft = await CreateAsync(applicant, competition.Id);
        await SaveAsync(applicant, draft.Id, FormDefinitionSamples.Parse("""{"opis":"Nasz projekt"}"""));
        (await applicant.PostAsync($"/applications/{draft.Id}/submit", content: null)).EnsureSuccessStatusCode();

        var change = await applicant.PutAsJsonAsync("/me/entity", OrganisationCard(EntityType.PatronInformalGroup));

        Assert.Equal(HttpStatusCode.OK, change.StatusCode);
        await using var context = _database.CreateContext();
        var kind = await context.Applications.Where(x => x.Id == draft.Id).Select(x => x.ApplicantType).SingleAsync();
        Assert.Equal(EntityType.Organisation, kind);
    }

    [RequiresDatabaseFact]
    public async Task An_incomplete_card_stops_the_submission_and_nothing_is_numbered()
    {
        // A card written before the rules existed, straight into the table.
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);
        clock.Now = CompetitionTestHost.Start.AddDays(1);

        var entity = new Entity { Type = EntityType.Organisation, Name = "Stara karta bez NIP" };
        await using (var context = _database.CreateContext())
        {
            context.Entities.Add(entity);
            await context.SaveChangesAsync();
        }

        var email = SessionTestHost.Email("niepelna");
        await SessionTestHost.CreateAccountAsync(host, email, Role.Applicant, entityId: entity.Id);
        var applicant = await LoginAsync(host, email);

        var draft = await CreateAsync(applicant, competition.Id);
        await SaveAsync(applicant, draft.Id, FormDefinitionSamples.Parse("""{"opis":"Nasz projekt"}"""));
        var submit = await applicant.PostAsync($"/applications/{draft.Id}/submit", content: null);

        Assert.Equal(HttpStatusCode.Conflict, submit.StatusCode);
        Assert.Contains("Mój profil", await submit.Content.ReadAsStringAsync());
        var application = await GetAsync(applicant, draft.Id);
        Assert.Equal(ApplicationStatus.Draft, application.Status);
        Assert.Null(application.Number);
        Assert.Null(application.EntitySnapshot);
    }

    [RequiresDatabaseFact]
    public async Task The_copy_holds_the_checked_card_not_what_else_the_row_still_carries()
    {
        // An informal group from before T-93: its contact_information, a
        // natural person's, was renamed into email by the migration.
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);
        clock.Now = CompetitionTestHost.Start.AddDays(1);

        var entity = new Entity
        {
            Type = EntityType.InformalGroup,
            Name = "  Sąsiedzi z Zaodrza ",
            Email = "sasiedzi@example.org, tel. 700 300 400",
        };
        await using (var context = _database.CreateContext())
        {
            context.Entities.Add(entity);
            await context.SaveChangesAsync();
        }

        var email = SessionTestHost.Email("stara-grupa");
        await SessionTestHost.CreateAccountAsync(host, email, Role.Applicant, entityId: entity.Id);
        var applicant = await LoginAsync(host, email);

        var draft = await CreateAsync(applicant, competition.Id);
        await SaveAsync(applicant, draft.Id, FormDefinitionSamples.Parse("""{"opis":"Nasz projekt"}"""));
        var submit = await applicant.PostAsync($"/applications/{draft.Id}/submit", content: null);

        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        var snapshot = (await submit.Content.ReadFromJsonAsync<ApplicationResponse>())!.EntitySnapshot!;
        Assert.Equal("Sąsiedzi z Zaodrza", snapshot.Name);
        Assert.Null(snapshot.Email);
    }

    private static async Task<HttpClient> NewApplicantAsync(WebApplicationFactory<Program> host)
    {
        var email = SessionTestHost.Email("nowy");
        await SessionTestHost.CreateAccountAsync(host, email, Role.Applicant);
        return await LoginAsync(host, email);
    }
}
