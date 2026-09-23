using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Ocwip.Api.Tests.Models.Forms;
using Xunit;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// Draft applications, over real HTTP and a real PostgreSQL (T-29).
///
/// The card in one sentence: a draft autosaves, tells its owner when it was
/// last saved, refuses to be touched once the intake closes (T-21), and is
/// never hard deleted. Isolation between applicants is asserted here too,
/// because starting and touching a draft is the first product endpoint that
/// exercises the resource policy PermissionDenialTests (T-13.3) proved in
/// isolation.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ApplicationDraftEndpointsTests : IClassFixture<OcwipWebApplicationFactory>
{
    private readonly OcwipWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public ApplicationDraftEndpointsTests(
        OcwipWebApplicationFactory factory,
        PostgresDatabaseFixture database)
    {
        _factory = factory;
        _database = database;
    }

    [RequiresDatabaseFact]
    public async Task An_applicant_creates_saves_reads_and_deactivates_their_own_draft()
    {
        // Arrange
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);

        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (applicant, _, _) = await SeedApplicantAsync(host);

        // Act
        var created = await CreateAsync(applicant, competition.Id);

        // Assert: an empty draft, already carrying what the "informacje
        // techniczne" block needs before a single field is filled in (D15).
        Assert.Equal(ApplicationStatus.Draft, created.Status);
        Assert.Equal(JsonValueKind.Object, created.Answers.ValueKind);
        Assert.Empty(created.Answers.EnumerateObject());
        Assert.Null(created.Number);
        Assert.Null(created.SubmittedAt);
        Assert.True(created.IsActive);
        Assert.Matches("^[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}$", created.Checksum);

        // Act: autosave, as the form would call it after a field is filled.
        var saved = await SaveAsync(
            applicant, created.Id, FormDefinitionSamples.Parse("""{"opis":"Nasz projekt"}"""));

        // Assert: the answer is there, and the checksum moved because the
        // content did (D15's own example format).
        Assert.Equal("Nasz projekt", saved.Answers.GetProperty("opis").GetString());
        Assert.NotEqual(created.Checksum, saved.Checksum);
        Assert.True(saved.LastSavedAt >= created.LastSavedAt);

        // Act: read back, as a page reload would.
        var reread = await GetAsync(applicant, created.Id);

        // Assert: resumes in exactly the place it was left, D15's checksum
        // included, so a reload never shows a different "informacje
        // techniczne" block than the one just saved.
        Assert.Equal("Nasz projekt", reread.Answers.GetProperty("opis").GetString());
        Assert.Equal(saved.Checksum, reread.Checksum);

        // Act
        var deactivateResponse = await applicant.DeleteAsync($"/applications/{created.Id}");

        // Assert: 200 and IsActive false, not a vanished row (rule 5).
        deactivateResponse.EnsureSuccessStatusCode();

        var deactivated = await deactivateResponse.Content
            .ReadFromJsonAsync<ApplicationResponse>();
        Assert.False(deactivated!.IsActive);

        await using var context = _database.CreateContext();
        var stored = await context.Applications
            .AsNoTracking()
            .SingleAsync(x => x.Id == created.Id);
        Assert.False(stored.IsActive);
        Assert.NotNull(stored.DeactivatedAt);

        // A deactivated draft is still visible to its own applicant (the
        // card: "zostaje widoczna"), only gone from a LIST, which is T-34.
        var stillReadable = await applicant.GetAsync($"/applications/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, stillReadable.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Creating_a_draft_against_an_unknown_competition_is_404()
    {
        var (host, _) = CompetitionTestHost.Create(_factory, _database);
        var (applicant, _, _) = await SeedApplicantAsync(host);

        var response = await applicant.PostAsJsonAsync(
            $"/competitions/{Guid.NewGuid()}/applications", new { });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Creating_a_draft_before_a_form_is_published_is_refused()
    {
        // Arrange: published, taking applications, but nobody ever published
        // its form. Rare in practice, but nothing enforces the order today.
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        var competition = await CompetitionTestHost.CreateAsync(operatorClient);
        await CompetitionTestHost.ChangeStatusAsync(
            operatorClient, competition.Id, CompetitionStatus.Published);

        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (applicant, _, _) = await SeedApplicantAsync(host);

        // Act
        var response = await applicant.PostAsJsonAsync(
            $"/competitions/{competition.Id}/applications", new { });

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Creating_a_draft_outside_the_intake_window_is_refused_by_T21s_rule()
    {
        // Arrange: published and formed, but the clock has not reached the
        // start date yet.
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);
        var (applicant, _, _) = await SeedApplicantAsync(host);

        // Act
        var response = await applicant.PostAsJsonAsync(
            $"/competitions/{competition.Id}/applications", new { });

        // Assert: D12, the refusal names the moment rather than repeating
        // that it is refused.
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Nabór jeszcze się nie rozpoczął", body);
    }

    [RequiresDatabaseFact]
    public async Task Saving_a_draft_a_minute_after_the_intake_closes_is_refused()
    {
        // Arrange
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);

        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (applicant, _, email) = await SeedApplicantAsync(host);
        var draft = await CreateAsync(applicant, competition.Id);

        // Act: the exact case the card's checklist names. The clock moves a
        // month past where the earlier cookie was issued, so the applicant
        // has to sign in again for the cookie to read as current rather than
        // expired (see the note on SeedApplicantAsync).
        clock.Now = CompetitionTestHost.End.AddMinutes(1);
        applicant = await LoginAsync(host, email);

        var response = await applicant.PutAsJsonAsync(
            $"/applications/{draft.Id}",
            new SaveApplicationDraftRequest(FormDefinitionSamples.Parse("""{"opis":"za pozno"}""")));

        // Assert: an unambiguous refusal, not a silently accepted save and not
        // a 500. D12 again: the moment is named.
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Nabór został zamknięty", body);

        // The stored answers never moved.
        await using var context = _database.CreateContext();
        var stored = await context.Applications.AsNoTracking().SingleAsync(x => x.Id == draft.Id);
        Assert.Empty(stored.Answers.EnumerateObject());
    }

    [RequiresDatabaseFact]
    public async Task Only_an_applicant_may_start_a_draft()
    {
        // Arrange
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);
        clock.Now = CompetitionTestHost.Start.AddDays(1);

        // Act & assert
        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        var operatorResponse = await operatorClient.PostAsJsonAsync(
            $"/competitions/{competition.Id}/applications", new { });
        Assert.Equal(HttpStatusCode.Forbidden, operatorResponse.StatusCode);

        var reviewerClient = await CompetitionTestHost.SignedInAs(host, Role.Reviewer);
        var reviewerResponse = await reviewerClient.PostAsJsonAsync(
            $"/competitions/{competition.Id}/applications", new { });
        Assert.Equal(HttpStatusCode.Forbidden, reviewerResponse.StatusCode);

        var anonymous = host.CreateClient();
        var anonymousResponse = await anonymous.PostAsJsonAsync(
            $"/competitions/{competition.Id}/applications", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task An_applicant_with_no_podmiot_cannot_start_a_draft()
    {
        // Arrange: B-09's default state, every account before a Podmiot is
        // wired to it.
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);
        clock.Now = CompetitionTestHost.Start.AddDays(1);

        var applicant = await CompetitionTestHost.SignedInAs(host, Role.Applicant);

        // Act
        var response = await applicant.PostAsJsonAsync(
            $"/competitions/{competition.Id}/applications", new { });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task An_applicant_cannot_reach_someone_elses_draft()
    {
        // Arrange
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);

        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (owner, _, _) = await SeedApplicantAsync(host);
        var (stranger, _, _) = await SeedApplicantAsync(host);

        var draft = await CreateAsync(owner, competition.Id);

        // Act & assert: 403 on every verb, and the body never carries the
        // owner's answers (T-13.3's own standard: a refusal has to be checked
        // by its body, not only by its status).
        var getResponse = await stranger.GetAsync($"/applications/{draft.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, getResponse.StatusCode);
        Assert.DoesNotContain(draft.Id.ToString(), await getResponse.Content.ReadAsStringAsync());

        var putResponse = await stranger.PutAsJsonAsync(
            $"/applications/{draft.Id}",
            new SaveApplicationDraftRequest(FormDefinitionSamples.Parse("{}")));
        Assert.Equal(HttpStatusCode.Forbidden, putResponse.StatusCode);

        var deleteResponse = await stranger.DeleteAsync($"/applications/{draft.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);

        // The owner's draft is untouched.
        var stillThere = await GetAsync(owner, draft.Id);
        Assert.True(stillThere.IsActive);
    }

    [RequiresDatabaseFact]
    public async Task An_operator_may_read_a_draft_but_a_reviewer_may_not()
    {
        // Arrange
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);

        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (applicant, _, _) = await SeedApplicantAsync(host);
        var draft = await CreateAsync(applicant, competition.Id);

        // Act & assert: the operator sees everything (T-13.2's own rule), and
        // a reviewer sees nothing until T-37 assigns applications to one.
        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        var operatorResponse = await operatorClient.GetAsync($"/applications/{draft.Id}");
        Assert.Equal(HttpStatusCode.OK, operatorResponse.StatusCode);

        var reviewerClient = await CompetitionTestHost.SignedInAs(host, Role.Reviewer);
        var reviewerResponse = await reviewerClient.GetAsync($"/applications/{draft.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, reviewerResponse.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Reading_saving_or_deactivating_an_unknown_application_is_404()
    {
        var (host, _) = CompetitionTestHost.Create(_factory, _database);
        var (applicant, _, _) = await SeedApplicantAsync(host);
        var missing = Guid.NewGuid();

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await applicant.GetAsync($"/applications/{missing}")).StatusCode);

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await applicant.PutAsJsonAsync(
                $"/applications/{missing}",
                new SaveApplicationDraftRequest(FormDefinitionSamples.Parse("{}"))))
            .StatusCode);

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await applicant.DeleteAsync($"/applications/{missing}")).StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Saving_answers_that_are_not_an_object_or_an_array_is_refused()
    {
        // Arrange
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);

        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (applicant, _, _) = await SeedApplicantAsync(host);
        var draft = await CreateAsync(applicant, competition.Id);

        // Act: the shape the check constraint refuses at the database, caught
        // here so the caller gets a 400 rather than the endpoint's own 500.
        var response = await applicant.PutAsJsonAsync(
            $"/applications/{draft.Id}",
            new SaveApplicationDraftRequest(FormDefinitionSamples.Parse("\"tylko tekst\"")));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task A_submitted_application_can_no_longer_be_autosaved_or_deactivated()
    {
        // Arrange
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);

        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (applicant, _, _) = await SeedApplicantAsync(host);
        var draft = await CreateAsync(applicant, competition.Id);

        // Submission itself is T-33, not built yet: the row is flipped by
        // hand, exactly as FormDefinitionVersioningTests seeds an application
        // by hand because the endpoints under test do not own that write.
        await using (var context = _database.CreateContext())
        {
            var application = await context.Applications.SingleAsync(x => x.Id == draft.Id);
            application.Status = ApplicationStatus.Submitted;
            application.SubmittedAt = clock.Now;
            application.Number = "001";
            await context.SaveChangesAsync();
        }

        // Act & assert
        var putResponse = await applicant.PutAsJsonAsync(
            $"/applications/{draft.Id}",
            new SaveApplicationDraftRequest(FormDefinitionSamples.Parse("""{"opis":"po zlozeniu"}""")));
        Assert.Equal(HttpStatusCode.Conflict, putResponse.StatusCode);

        var deleteResponse = await applicant.DeleteAsync($"/applications/{draft.Id}");
        Assert.Equal(HttpStatusCode.Conflict, deleteResponse.StatusCode);

        // Reading it is still fine: a submitted application is not hidden.
        var getResponse = await applicant.GetAsync($"/applications/{draft.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    /// <summary>
    /// A competition published and taking applications, with a form already
    /// published against it. What CreateDraftAsync needs to succeed.
    /// </summary>
    private static async Task<CompetitionResponse> PublishedCompetitionWithFormAsync(
        WebApplicationFactory<Program> host)
    {
        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        var competition = await CompetitionTestHost.CreateAsync(operatorClient);

        await CompetitionTestHost.ChangeStatusAsync(
            operatorClient, competition.Id, CompetitionStatus.Published);

        var publishResponse = await operatorClient.PostAsJsonAsync(
            $"/competitions/{competition.Id}/form-definitions",
            new FormDefinitionRequest(
                FormDefinitionSamples.WithFields(
                    FormDefinitionSamples.Field("opis", "shortText", "\"maxLength\": 500"))));
        publishResponse.EnsureSuccessStatusCode();

        var refreshed = await operatorClient.GetFromJsonAsync<CompetitionResponse>(
            $"/competitions/{competition.Id}");

        return refreshed!;
    }

    /// <summary>
    /// An Applicant account wired to a fresh Podmiot, signed in. Written
    /// through the context because there is no endpoint that creates a
    /// Podmiot yet (B-09; "karta organizacji" is a separate, unbuilt path, see
    /// docs/runbook/proces.md, ścieżka 2).
    ///
    /// Signs in at whatever the clock reads right now: the cookie handler
    /// validates its ticket against the same FixedTimeProvider the test moves
    /// (see CompetitionLifecycleEndpointTests), so a caller that still needs
    /// the clock to jump forward after this returns has to sign in again with
    /// <see cref="LoginAsync"/> once it has, or the cookie this call issued
    /// reads as expired against the new time.
    /// </summary>
    private async Task<(HttpClient Client, Guid EntityId, string Email)> SeedApplicantAsync(
        WebApplicationFactory<Program> host)
    {
        var entity = TestEntity.New($"Podmiot {Guid.NewGuid():N}");

        await using (var context = _database.CreateContext())
        {
            context.Entities.Add(entity);
            await context.SaveChangesAsync();
        }

        var email = SessionTestHost.Email("wnioskodawca");
        await SessionTestHost.CreateAccountAsync(
            host, email, Role.Applicant, entityId: entity.Id);

        var client = await LoginAsync(host, email);

        return (client, entity.Id, email);
    }

    private static async Task<HttpClient> LoginAsync(
        WebApplicationFactory<Program> host, string email)
    {
        var client = host.CreateClient();
        var login = await client.PostAsJsonAsync(
            "/login", new { email, password = SessionTestHost.Password });
        login.EnsureSuccessStatusCode();

        return client;
    }

    private static async Task<ApplicationResponse> CreateAsync(
        HttpClient client, Guid competitionId)
    {
        var response = await client.PostAsJsonAsync(
            $"/competitions/{competitionId}/applications", new { });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ApplicationResponse>())!;
    }

    private static async Task<ApplicationResponse> SaveAsync(
        HttpClient client, Guid id, JsonElement answers)
    {
        var response = await client.PutAsJsonAsync(
            $"/applications/{id}", new SaveApplicationDraftRequest(answers));
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ApplicationResponse>())!;
    }

    private static async Task<ApplicationResponse> GetAsync(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<ApplicationResponse>($"/applications/{id}"))!;
}
