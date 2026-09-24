using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Ocwip.Api.Tests.Models.Forms;
using Xunit;
using static Ocwip.Api.Tests.Endpoints.ApplicationTestHost;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// The draft level of T-30 over real HTTP and a real PostgreSQL: the autosave
/// takes gaps, and refuses what the form could never have sent, naming each
/// field the way the renderer names its inputs.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ApplicationAnswerValidationTests : IClassFixture<OcwipWebApplicationFactory>
{
    private readonly OcwipWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public ApplicationAnswerValidationTests(
        OcwipWebApplicationFactory factory,
        PostgresDatabaseFixture database)
    {
        _factory = factory;
        _database = database;
    }

    [RequiresDatabaseFact]
    public async Task Any_json_sent_straight_to_the_api_is_refused_and_nothing_is_stored()
    {
        // Arrange
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(
            host, FormDefinitionSamples.AllFieldKinds());

        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (applicant, _, _) = await ApplicationTestHost.SeedApplicantAsync(host, _database);
        var draft = await CreateAsync(applicant, competition.Id);

        // Act
        var response = await PutAsync(
            applicant,
            draft.Id,
            FormDefinitionSamples.Parse(
                """
                {
                  "tytul": { "$where": "1 == 1" },
                  "kwota": "dużo",
                  "czy_admin": true,
                  "rezultaty": [{ "rezultat": "Spotkania", "wartosc_docelowa": "sto" }]
                }
                """));

        // Assert: every problem at once, each under the key the form uses.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(
            ["czy_admin", "kwota", "rezultaty[0].wartosc_docelowa", "tytul"],
            problem!.Errors.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(["Formularz nie ma takiego pola."], problem.Errors["czy_admin"]);

        var stored = await GetAsync(applicant, draft.Id);
        Assert.Empty(stored.Answers.EnumerateObject());
    }

    [RequiresDatabaseFact]
    public async Task A_draft_with_gaps_saves_although_every_field_of_the_form_is_required()
    {
        // Arrange
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(
            host, FormDefinitionSamples.AllFieldKinds());

        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (applicant, _, _) = await ApplicationTestHost.SeedApplicantAsync(host, _database);
        var draft = await CreateAsync(applicant, competition.Id);

        // Act: two fields of fifteen, one of them still too short to submit.
        var saved = await SaveAsync(
            applicant,
            draft.Id,
            FormDefinitionSamples.Parse("""{ "tytul": "Warsztaty", "opis": "Na razie tyle" }"""));

        // Assert
        Assert.Equal("Warsztaty", saved.Answers.GetProperty("tytul").GetString());
    }

    [RequiresDatabaseFact]
    public async Task A_draft_is_checked_against_its_own_version_not_the_newest_one()
    {
        // Arrange: version 1 asks for "opis"; version 2, published after the
        // draft was started, replaces it with "cel".
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);

        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (applicant, _, _) = await ApplicationTestHost.SeedApplicantAsync(host, _database);
        var draft = await CreateAsync(applicant, competition.Id);

        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        await PublishFormAsync(
            operatorClient,
            competition.Id,
            FormDefinitionSamples.WithFields(
                FormDefinitionSamples.Field("cel", "shortText", "\"maxLength\": 500")));

        // Act
        var own = await PutAsync(
            applicant, draft.Id, FormDefinitionSamples.Parse("""{ "opis": "Nasz projekt" }"""));
        var newest = await PutAsync(
            applicant, draft.Id, FormDefinitionSamples.Parse("""{ "cel": "Nasz cel" }"""));

        // Assert
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, newest.StatusCode);

        var problem = await newest.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(["cel"], problem!.Errors.Keys);
    }

    [RequiresDatabaseFact]
    public async Task A_closed_intake_answers_before_the_answers_are_even_read()
    {
        // Arrange
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);

        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (applicant, _, email) = await ApplicationTestHost.SeedApplicantAsync(host, _database);
        var draft = await CreateAsync(applicant, competition.Id);

        clock.Now = CompetitionTestHost.End.AddMinutes(1);
        applicant = await LoginAsync(host, email);

        // Act: answers that are wrong as well; the deadline is the news.
        var response = await PutAsync(
            applicant, draft.Id, FormDefinitionSamples.Parse("""{ "nieznane": 1 }"""));

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
