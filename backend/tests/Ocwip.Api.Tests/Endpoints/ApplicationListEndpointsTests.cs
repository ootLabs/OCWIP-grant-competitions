using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Ocwip.Api.Tests.Models.Forms;
using Xunit;
using static Ocwip.Api.Tests.Models.Forms.FormDefinitionSamples;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// The operator's list of the applications a competition received, one
/// submitted offer, and the exports (T-35), over real HTTP and PostgreSQL.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ApplicationListEndpointsTests : IClassFixture<OcwipWebApplicationFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly OcwipWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public ApplicationListEndpointsTests(
        OcwipWebApplicationFactory factory,
        PostgresDatabaseFixture database)
    {
        _factory = factory;
        _database = database;
    }

    private static JsonElement RoleForm() =>
        WithFields(
            Field("tytul", "shortText", "\"maxLength\": 200, \"role\": \"projectTitle\""),
            Field("koszt", "amount", "\"role\": \"totalCost\""),
            Field("wklad", "amount"),
            Field(
                "dotacja",
                "calculated",
                """
                "calculation": { "kind": "difference", "operands": ["koszt", "wklad"] },
                "role": "requestedGrant"
                """));

    private sealed record Scenario(
        WebApplicationFactory<Program> Host,
        HttpClient Operator,
        Guid CompetitionId,
        Guid FormDefinitionId);

    private async Task<Scenario> CompetitionAsync(decimal? pool)
    {
        var (host, _) = CompetitionTestHost.Create(_factory, _database);
        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        var competition = await CompetitionTestHost.CreateAsync(
            operatorClient, CompetitionTestHost.Request() with { TotalPoolAmount = pool });
        await ApplicationTestHost.PublishFormAsync(operatorClient, competition.Id, RoleForm());

        await using var context = _database.CreateContext();
        var definitionId = await context.FormDefinitions
            .Where(x => x.CompetitionId == competition.Id)
            .Select(x => x.Id)
            .SingleAsync();

        return new Scenario(host, operatorClient, competition.Id, definitionId);
    }

    /// <summary>Written through the context: 120 offers submitted over HTTP
    /// would test submission again, and T-33 already does.</summary>
    private async Task<List<Application>> SeedAsync(
        Scenario scenario,
        IEnumerable<(string? Number, string Answers, EntityType Type)> rows)
    {
        await using var context = _database.CreateContext();
        var applications = new List<Application>();

        foreach (var (number, answers, type) in rows)
        {
            var entity = TestEntity.New($"Podmiot {Guid.NewGuid():N}");
            entity.Type = type;
            context.Entities.Add(entity);

            var chain = new ApplicationChain(scenario.CompetitionId, scenario.FormDefinitionId, entity.Id);
            var application = number is null
                ? TestApplication.Draft(chain, answers)
                : TestApplication.Submitted(chain, number, answers);
            application.Entity = entity;
            context.Applications.Add(application);
            applications.Add(application);
        }

        await context.SaveChangesAsync();
        return applications;
    }

    [RequiresDatabaseFact]
    public async Task The_list_shows_submitted_offers_with_their_values_and_what_is_left_of_the_pool()
    {
        // Arrange
        var scenario = await CompetitionAsync(pool: 20000m);
        await SeedAsync(scenario,
        [
            ("002", """{"tytul":"Festyn","koszt":6000,"wklad":1000}""", EntityType.InformalGroup),
            ("001", """{"tytul":"Warsztaty","koszt":12000.5,"wklad":2000}""", EntityType.Organisation),
            (null, """{"tytul":"Nie złożony","koszt":99999,"wklad":0}""", EntityType.Organisation),
        ]);

        // Act
        var list = await scenario.Operator.GetFromJsonAsync<ApplicationListResponse>(
            $"/competitions/{scenario.CompetitionId}/applications", Json);

        // Assert: the draft is not there at all, the rest in number order.
        Assert.NotNull(list);
        Assert.Equal(["001", "002"], list.Applications.Select(x => x.Number));
        var first = list.Applications[0];
        Assert.Equal("Warsztaty", first.ProjectTitle);
        Assert.Equal(12000.5m, first.TotalCost);
        Assert.Equal(10000.5m, first.RequestedGrant);
        Assert.Equal(EntityType.Organisation, first.EntityType);
        Assert.Equal(ApplicationStatus.Submitted, first.Status);
        Assert.Equal(EntityType.InformalGroup, list.Applications[1].EntityType);
        Assert.Equal(15000.5m, list.RequestedTotal);
        Assert.Equal(20000m, list.TotalPoolAmount);
        Assert.Equal(4999.5m, list.PoolRemaining);
    }

    [RequiresDatabaseFact]
    public async Task The_kind_of_applicant_travels_as_text_for_the_screen_to_filter_on()
    {
        var scenario = await CompetitionAsync(pool: null);
        await SeedAsync(scenario, [("001", """{"tytul":"A"}""", EntityType.PatronInformalGroup)]);

        var body = await scenario.Operator.GetStringAsync(
            $"/competitions/{scenario.CompetitionId}/applications");

        Assert.Contains("\"entityType\":\"PatronInformalGroup\"", body);
        Assert.Contains("\"poolRemaining\":null", body);
    }

    [RequiresDatabaseFact]
    public async Task A_withdrawn_offer_and_another_competitions_offer_are_not_listed()
    {
        // Arrange
        var scenario = await CompetitionAsync(pool: null);
        var other = await CompetitionAsync(pool: null);
        var mine = await SeedAsync(scenario,
        [
            ("001", """{"tytul":"Zostaje"}""", EntityType.Organisation),
            ("002", """{"tytul":"Wycofany"}""", EntityType.Organisation),
        ]);
        await SeedAsync(other, [("001", """{"tytul":"Obcy"}""", EntityType.Organisation)]);

        await using (var context = _database.CreateContext())
        {
            var withdrawn = await context.Applications.SingleAsync(x => x.Id == mine[1].Id);
            withdrawn.IsActive = false;
            withdrawn.DeactivatedAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync();
        }

        // Act
        var list = await scenario.Operator.GetFromJsonAsync<ApplicationListResponse>(
            $"/competitions/{scenario.CompetitionId}/applications", Json);

        // Assert
        var only = Assert.Single(list!.Applications);
        Assert.Equal("Zostaje", only.ProjectTitle);
    }

    [RequiresDatabaseFact]
    public async Task A_hundred_and_twenty_offers_are_listed_whole_and_in_number_order()
    {
        var scenario = await CompetitionAsync(pool: 1000000m);
        await SeedAsync(scenario, Enumerable.Range(1, 120).Select(i =>
            ((string?)i.ToString("D3"), $$"""{"tytul":"Projekt {{i}}","koszt":1000,"wklad":0}""", EntityType.Organisation)));

        var list = await scenario.Operator.GetFromJsonAsync<ApplicationListResponse>(
            $"/competitions/{scenario.CompetitionId}/applications", Json);

        Assert.Equal(120, list!.Applications.Count);
        Assert.Equal("120", list.Applications[^1].Number);
        Assert.Equal(120000m, list.RequestedTotal);
    }

    [RequiresDatabaseFact]
    public async Task An_unknown_competition_answers_not_found()
    {
        var scenario = await CompetitionAsync(pool: null);

        var response = await scenario.Operator.GetAsync($"/competitions/{Guid.NewGuid()}/applications");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task One_offer_opens_with_its_form_version_and_its_active_attachments_only()
    {
        // Arrange
        var scenario = await CompetitionAsync(pool: null);
        var application = (await SeedAsync(
            scenario, [("001", """{"tytul":"Warsztaty"}""", EntityType.Organisation)]))[0];

        await using (var context = _database.CreateContext())
        {
            var kept = TestAttachment.New(application.Id, application.EntityId);
            kept.FileName = "statut.pdf";
            var replaced = TestAttachment.New(application.Id, application.EntityId);
            replaced.FileName = "stary.pdf";
            replaced.IsActive = false;
            replaced.DeactivatedAt = DateTimeOffset.UtcNow;
            context.Attachments.AddRange(kept, replaced);
            await context.SaveChangesAsync();
        }

        // Act
        var offer = await scenario.Operator.GetFromJsonAsync<SubmittedApplicationResponse>(
            $"/competitions/{scenario.CompetitionId}/applications/{application.Id}", Json);

        // Assert
        Assert.NotNull(offer);
        Assert.Equal("001", offer.Number);
        Assert.Equal(1, offer.FormVersion);
        Assert.Equal("Warsztaty", offer.Answers.GetProperty("tytul").GetString());
        Assert.Equal("tytul", offer.Definition.GetProperty("sections")[0].GetProperty("fields")[0]
            .GetProperty("key").GetString());
        Assert.Matches("^[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}$", offer.Checksum);
        Assert.Equal("statut.pdf", Assert.Single(offer.Attachments).FileName);
    }

    [RequiresDatabaseFact]
    public async Task A_draft_or_an_offer_of_another_competition_does_not_open_from_this_list()
    {
        var scenario = await CompetitionAsync(pool: null);
        var other = await CompetitionAsync(pool: null);
        var draft = (await SeedAsync(scenario, [(null, """{"tytul":"Szkic"}""", EntityType.Organisation)]))[0];
        var foreign = (await SeedAsync(other, [("001", """{"tytul":"Obcy"}""", EntityType.Organisation)]))[0];

        var draftResponse = await scenario.Operator.GetAsync(
            $"/competitions/{scenario.CompetitionId}/applications/{draft.Id}");
        var foreignResponse = await scenario.Operator.GetAsync(
            $"/competitions/{scenario.CompetitionId}/applications/{foreign.Id}");

        Assert.Equal(HttpStatusCode.NotFound, draftResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignResponse.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task The_exports_download_as_a_spreadsheet_and_a_pdf_named_after_the_competition()
    {
        var scenario = await CompetitionAsync(pool: 5000m);
        await SeedAsync(scenario, [("001", """{"tytul":"Warsztaty","koszt":1200,"wklad":200}""", EntityType.Organisation)]);

        var csv = await scenario.Operator.GetAsync(
            $"/competitions/{scenario.CompetitionId}/applications/export/csv");
        var pdf = await scenario.Operator.GetAsync(
            $"/competitions/{scenario.CompetitionId}/applications/export/pdf");

        Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
        Assert.Equal("text/csv", csv.Content.Headers.ContentType!.MediaType);
        Assert.EndsWith(".csv", csv.Content.Headers.ContentDisposition!.FileName!.Trim('"'));
        var text = Encoding.UTF8.GetString(await csv.Content.ReadAsByteArrayAsync());
        Assert.Contains(";Warsztaty;1200,00;1000,00;Złożony;", text);

        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType!.MediaType);
        Assert.Equal("%PDF-"u8.ToArray(), (await pdf.Content.ReadAsByteArrayAsync()).Take(5).ToArray());
    }

    private static readonly string[] Routes =
    [
        "/competitions/{0}/applications",
        "/competitions/{0}/applications/{1}",
        "/competitions/{0}/applications/export/csv",
        "/competitions/{0}/applications/export/pdf",
    ];

    [RequiresDatabaseTheory]
    [MemberData(nameof(RolesAndRoutes))]
    public async Task An_applicant_and_a_reviewer_are_refused_on_every_list_route(Role role, string route)
    {
        // Arrange: a real offer behind the route, so a 403 cannot be a 404 in disguise.
        var scenario = await CompetitionAsync(pool: null);
        var application = (await SeedAsync(scenario, [("001", """{"tytul":"A"}""", EntityType.Organisation)]))[0];
        var client = await CompetitionTestHost.SignedInAs(scenario.Host, role);

        // Act
        var response = await client.GetAsync(string.Format(route, scenario.CompetitionId, application.Id));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain("Podmiot", await response.Content.ReadAsStringAsync());
    }

    public static TheoryData<Role, string> RolesAndRoutes()
    {
        var data = new TheoryData<Role, string>();
        foreach (var route in Routes)
        {
            data.Add(Role.Applicant, route);
            data.Add(Role.Reviewer, route);
        }

        return data;
    }

    [RequiresDatabaseFact]
    public async Task An_anonymous_caller_is_asked_to_sign_in()
    {
        var scenario = await CompetitionAsync(pool: null);
        var anonymous = scenario.Host.CreateClient();

        foreach (var route in Routes)
        {
            var response = await anonymous.GetAsync(
                string.Format(route, scenario.CompetitionId, Guid.NewGuid()));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
