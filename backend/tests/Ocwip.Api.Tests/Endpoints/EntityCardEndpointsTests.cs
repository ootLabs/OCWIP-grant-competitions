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
/// rewrites a submitted application, and nobody reaches a card they are not a
/// member of. Joining an existing card and choosing between several is
/// <see cref="EntityAccessEndpointsTests"/> (T-93a).
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

    /// <summary>
    /// A complete organisation card, typed the way a person types it. The NIP
    /// is fresh unless given, because an active card's NIP is unique (T-93a)
    /// and the tests share one database.
    /// </summary>
    public static EntityCardData OrganisationCard(
        EntityType type = EntityType.Organisation, string name = "Stowarzyszenie Karta Testowa", string? nip = null) =>
        new(
            type,
            name,
            LegalForm.Association,
            null,
            EntityRegister.Krs,
            "0000000001",
            Typed(nip ?? TestEntity.NewNip()),
            null,
            "ul. Testowa 1, 45-000 Opole",
            null,
            "+48 700 100 200",
            "biuro@example.org",
            "PL73 1111 1111 1111 1111 1111 1111",
            [new EntityRepresentative("Anna", "Testowa", "Prezeska")]);

    /// <summary>"123-456-78-90": the way a NIP is written on paper, which the card must accept.</summary>
    public static string Typed(string digits) =>
        $"{digits[..3]}-{digits[3..6]}-{digits[6..8]}-{digits[8..]}";

    public static async Task<EntityCardResponse> FoundAsync(HttpClient client, EntityCardData card)
    {
        var response = await client.PostAsJsonAsync("/me/entities", card);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<EntityCardResponse>())!;
    }

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

        Assert.Empty((await applicant.GetFromJsonAsync<List<EntityCardSummary>>("/me/entities"))!);

        var nip = TestEntity.NewNip();
        var card = type is EntityType.InformalGroup
            ? new EntityCardData(type, "Sąsiedzi z Zaodrza")
            : OrganisationCard(type, nip: nip);
        var created = await FoundAsync(applicant, card);
        Assert.True(created.IsFounder);

        var stored = (await applicant.GetFromJsonAsync<EntityCardResponse>($"/me/entities/{created.Id}"))!;
        Assert.Equal(type, stored.Card.Type);
        Assert.Single(stored.Members);
        if (type is not EntityType.InformalGroup)
        {
            // Stored as the digits the checksums were computed on.
            Assert.Equal(nip, stored.Card.Nip);
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
        var nip = TestEntity.NewNip();
        var founded = await FoundAsync(applicant, OrganisationCard(nip: nip));

        var draft = await CreateAsync(applicant, competition.Id);
        await SaveAsync(applicant, draft.Id, FormDefinitionSamples.Parse("""{"opis":"Nasz projekt"}"""));
        (await applicant.PostAsync($"/applications/{draft.Id}/submit", content: null)).EnsureSuccessStatusCode();

        var corrected = OrganisationCard(name: "Nowa nazwa po zmianie statutu", nip: nip) with { Address = "ul. Nowa 2, 45-000 Opole" };
        (await applicant.PutAsJsonAsync($"/me/entities/{founded.Id}", corrected)).EnsureSuccessStatusCode();

        var application = await GetAsync(applicant, draft.Id);
        Assert.Equal("Stowarzyszenie Karta Testowa", application.EntitySnapshot!.Name);
        Assert.Equal("ul. Testowa 1, 45-000 Opole", application.EntitySnapshot.Address);

        var card = (await applicant.GetFromJsonAsync<EntityCardResponse>($"/me/entities/{founded.Id}"))!;
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
    public async Task Nobody_reads_or_changes_a_card_they_are_not_a_member_of()
    {
        var (host, _) = CompetitionTestHost.Create(_factory, _database);
        var owner = await NewApplicantAsync(host);
        var founded = await FoundAsync(owner, OrganisationCard(name: "Cudza Fundacja"));
        var other = await NewApplicantAsync(host);

        var read = await other.GetAsync($"/me/entities/{founded.Id}");
        var change = await other.PutAsJsonAsync($"/me/entities/{founded.Id}", OrganisationCard(name: "Przejęta"));

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.DoesNotContain("Cudza Fundacja", await read.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, change.StatusCode);
        Assert.Empty((await other.GetFromJsonAsync<List<EntityCardSummary>>("/me/entities"))!);

        var ownerCard = (await owner.GetFromJsonAsync<EntityCardResponse>($"/me/entities/{founded.Id}"))!;
        Assert.Equal("Cudza Fundacja", ownerCard.Card.Name);

        // Other roles have no card of their own and no way to anybody's.
        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        Assert.Equal(HttpStatusCode.Forbidden, (await operatorClient.GetAsync("/me/entities")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await operatorClient.GetAsync($"/me/entities/{founded.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.CreateClient().GetAsync("/me/entities")).StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task A_card_with_wrong_numbers_is_refused_naming_each_field()
    {
        var (host, _) = CompetitionTestHost.Create(_factory, _database);
        var applicant = await NewApplicantAsync(host);

        var response = await applicant.PostAsJsonAsync(
            "/me/entities",
            OrganisationCard() with { Nip = "1234567890", BankAccount = "11 1111 1111", RegisterNumber = "123" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"nip\"", body);
        Assert.Contains("\"bankAccount\"", body);
        Assert.Contains("\"registerNumber\"", body);
        Assert.Empty((await applicant.GetFromJsonAsync<List<EntityCardSummary>>("/me/entities"))!);
    }

    [RequiresDatabaseFact]
    public async Task Two_people_founding_one_nip_at_once_get_one_card()
    {
        // T-93a: the second card with a NIP is refused even when both POSTs
        // pass the friendly check together; the index decides.
        var (host, _) = CompetitionTestHost.Create(_factory, _database);
        var first = await NewApplicantAsync(host);
        var second = await NewApplicantAsync(host);
        var nip = TestEntity.NewNip();

        var responses = await Task.WhenAll(
            first.PostAsJsonAsync("/me/entities", OrganisationCard(name: "Pierwsza", nip: nip)),
            second.PostAsJsonAsync("/me/entities", OrganisationCard(name: "Druga", nip: nip)));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        var refused = Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Contains("już zarejestrowana", await refused.Content.ReadAsStringAsync());

        await using var context = _database.CreateContext();
        Assert.Equal(1, await context.Entities.CountAsync(x => x.Nip == nip && x.IsActive));
    }

    [RequiresDatabaseFact]
    public async Task A_correction_cannot_take_another_card_s_nip()
    {
        var (host, _) = CompetitionTestHost.Create(_factory, _database);
        var someone = await NewApplicantAsync(host);
        var taken = TestEntity.NewNip();
        await FoundAsync(someone, OrganisationCard(name: "Zajęta", nip: taken));

        var applicant = await NewApplicantAsync(host);
        var own = await FoundAsync(applicant, OrganisationCard(name: "Własna"));

        var change = await applicant.PutAsJsonAsync($"/me/entities/{own.Id}", OrganisationCard(name: "Własna", nip: taken));

        Assert.Equal(HttpStatusCode.Conflict, change.StatusCode);
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
        var nip = TestEntity.NewNip();
        var founded = await FoundAsync(applicant, OrganisationCard(nip: nip));

        var draft = await CreateAsync(applicant, competition.Id);
        await SaveAsync(applicant, draft.Id, FormDefinitionSamples.Parse("""{"opis":"Nasz projekt"}"""));
        (await applicant.PostAsync($"/applications/{draft.Id}/submit", content: null)).EnsureSuccessStatusCode();

        var change = await applicant.PutAsJsonAsync(
            $"/me/entities/{founded.Id}", OrganisationCard(EntityType.PatronInformalGroup, nip: nip));

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
