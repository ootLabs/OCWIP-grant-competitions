using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Ocwip.Api.Tests.Models.Forms;
using Xunit;
using static Ocwip.Api.Tests.Endpoints.ApplicationTestHost;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// Submitting a draft application and downloading its confirmation PDF, over
/// real HTTP and a real PostgreSQL (T-33): full validation, the T-21 deadline
/// check, freezing the answers, the application number, the status history
/// entry, the confirmation e-mail and the concurrency safe numbering.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ApplicationSubmissionEndpointsTests : IClassFixture<OcwipWebApplicationFactory>
{
    private readonly OcwipWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public ApplicationSubmissionEndpointsTests(
        OcwipWebApplicationFactory factory,
        PostgresDatabaseFixture database)
    {
        _factory = factory;
        _database = database;
    }

    [RequiresDatabaseFact]
    public async Task Submitting_a_complete_draft_assigns_a_number_freezes_it_logs_history_and_mails_a_confirmation()
    {
        // Arrange
        var (host, clock, emails) = CreateHost(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);

        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (applicant, entityId, email) = await SeedApplicantAsync(host, _database);
        var draft = await CreateAsync(applicant, competition.Id);

        // Nothing has mailed anything yet: autosave must never trigger the
        // confirmation.
        var savedDraft = await SaveAsync(
            applicant, draft.Id, FormDefinitionSamples.Parse("""{"opis":"Nasz projekt"}"""));
        Assert.Empty(emails.Sent);

        // Act
        var submitResponse = await SubmitAsync(applicant, draft.Id);

        // Assert
        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);
        var submitted = (await submitResponse.Content.ReadFromJsonAsync<ApplicationResponse>())!;
        Assert.Equal(ApplicationStatus.Submitted, submitted.Status);
        Assert.Equal("001", submitted.Number);
        Assert.NotNull(submitted.SubmittedAt);

        // The number is assigned only now, never at draft creation or autosave
        // (docs/runbook/M4-wnioski.md's own acceptance criterion).
        Assert.Null(savedDraft.Number);

        // The status history entry: who, when, from what to what, never a
        // second row overwriting a first one.
        await using (var context = _database.CreateContext())
        {
            var history = await context.ApplicationStatusHistory
                .AsNoTracking()
                .Where(x => x.ApplicationId == draft.Id)
                .ToListAsync();

            var entry = Assert.Single(history);
            Assert.Equal(ApplicationStatus.Draft, entry.FromStatus);
            Assert.Equal(ApplicationStatus.Submitted, entry.ToStatus);

            var actingUser = await context.Users
                .AsNoTracking()
                .SingleAsync(x => x.Id == entry.ChangedByUserId);
            Assert.Equal(email, actingUser.Email);
        }

        // The confirmation e-mail, sent exactly once, only from submission.
        var sent = Assert.Single(emails.Sent);
        Assert.Equal(email, sent.To);
        Assert.Contains("001", sent.Body);

        // Frozen: PUT still refuses, exactly like a hand flipped row would
        // (ApplicationDraftEndpointsTests), but now reached through the real
        // submission path rather than a row edited directly in the test.
        var editAttempt = await PutAsync(
            applicant, draft.Id, FormDefinitionSamples.Parse("""{"opis":"po zlozeniu"}"""));
        Assert.Equal(HttpStatusCode.Conflict, editAttempt.StatusCode);

        // The applicant can download a confirmation PDF.
        var pdfResponse = await GetConfirmationAsync(applicant, draft.Id);
        Assert.Equal(HttpStatusCode.OK, pdfResponse.StatusCode);
        Assert.Equal("application/pdf", pdfResponse.Content.Headers.ContentType!.MediaType);
        var pdfBytes = await pdfResponse.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF-"u8.ToArray(), pdfBytes.Take(5).ToArray());
    }

    [RequiresDatabaseFact]
    public async Task Submitting_with_a_missing_required_field_is_refused_and_nothing_is_assigned()
    {
        // Arrange: never filled in, so the one required field is still empty.
        var (host, clock, emails) = CreateHost(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);

        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (applicant, _, _) = await SeedApplicantAsync(host, _database);
        var draft = await CreateAsync(applicant, competition.Id);

        // Act
        var response = await SubmitAsync(applicant, draft.Id);

        // Assert: a field level error, in the same shape T-30 already returns
        // for a rejected autosave.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("opis", body);
        Assert.Empty(emails.Sent);

        await using var context = _database.CreateContext();
        var stored = await context.Applications.AsNoTracking().SingleAsync(x => x.Id == draft.Id);
        Assert.Equal(ApplicationStatus.Draft, stored.Status);
        Assert.Null(stored.Number);
        Assert.Null(stored.SubmittedAt);
        Assert.Empty(await context.ApplicationStatusHistory
            .Where(x => x.ApplicationId == draft.Id).ToListAsync());
    }

    [RequiresDatabaseFact]
    public async Task Submitting_an_already_submitted_application_is_refused()
    {
        // Arrange
        var (host, clock, _) = CreateHost(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);

        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (applicant, _, _) = await SeedApplicantAsync(host, _database);
        var draft = await CreateAsync(applicant, competition.Id);
        await SaveAsync(applicant, draft.Id, FormDefinitionSamples.Parse("""{"opis":"Nasz projekt"}"""));

        var first = await SubmitAsync(applicant, draft.Id);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Act: the exact same request, again.
        var second = await SubmitAsync(applicant, draft.Id);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        await using var context = _database.CreateContext();
        Assert.Single(await context.ApplicationStatusHistory
            .Where(x => x.ApplicationId == draft.Id).ToListAsync());
    }

    [RequiresDatabaseFact]
    public async Task Submitting_after_the_intake_closes_is_refused_by_T21s_rule()
    {
        // Arrange
        var (host, clock, emails) = CreateHost(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);

        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (applicant, _, email) = await SeedApplicantAsync(host, _database);
        var draft = await CreateAsync(applicant, competition.Id);
        await SaveAsync(applicant, draft.Id, FormDefinitionSamples.Parse("""{"opis":"Nasz projekt"}"""));

        // The clock moves past the closing minute, so the earlier cookie has
        // to be replaced, same reasoning as every other T-21 test in this
        // suite (see ApplicationDraftEndpointsTests).
        clock.Now = CompetitionTestHost.End.AddMinutes(1);
        applicant = await LoginAsync(host, email);

        // Act
        var response = await SubmitAsync(applicant, draft.Id);

        // Assert: D12, the moment is named, and nothing was sent.
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Nabór został zamknięty", await response.Content.ReadAsStringAsync());
        Assert.Empty(emails.Sent);
    }

    [RequiresDatabaseFact]
    public async Task Only_the_owning_applicant_may_submit_their_own_application()
    {
        // Arrange
        var (host, clock, _) = CreateHost(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);

        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (owner, _, _) = await SeedApplicantAsync(host, _database);
        var (stranger, _, _) = await SeedApplicantAsync(host, _database);
        var draft = await CreateAsync(owner, competition.Id);
        await SaveAsync(owner, draft.Id, FormDefinitionSamples.Parse("""{"opis":"Nasz projekt"}"""));

        // Act & assert: a stranger applicant gets 403, and the row is left
        // exactly as the owner left it, checked by its body and not only its
        // status (T-13.3's own standard).
        var strangerResponse = await SubmitAsync(stranger, draft.Id);
        Assert.Equal(HttpStatusCode.Forbidden, strangerResponse.StatusCode);
        Assert.DoesNotContain(draft.Id.ToString(), await strangerResponse.Content.ReadAsStringAsync());

        // An operator can read every application, but submitting is the one
        // act this product has to attribute to the applicant themselves: the
        // role policy on the route refuses an operator before the row is even
        // loaded.
        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        var operatorResponse = await SubmitAsync(operatorClient, draft.Id);
        Assert.Equal(HttpStatusCode.Forbidden, operatorResponse.StatusCode);

        var reviewerClient = await CompetitionTestHost.SignedInAs(host, Role.Reviewer);
        var reviewerResponse = await SubmitAsync(reviewerClient, draft.Id);
        Assert.Equal(HttpStatusCode.Forbidden, reviewerResponse.StatusCode);

        var anonymous = host.CreateClient();
        var anonymousResponse = await SubmitAsync(anonymous, draft.Id);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        // The owner can still submit their own application afterwards.
        var ownerResponse = await SubmitAsync(owner, draft.Id);
        Assert.Equal(HttpStatusCode.OK, ownerResponse.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Submitting_an_unknown_application_is_404()
    {
        var (host, clock, _) = CreateHost(_factory, _database);
        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (applicant, _, _) = await SeedApplicantAsync(host, _database);

        var response = await SubmitAsync(applicant, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Two_simultaneous_submissions_of_the_same_application_submit_it_only_once()
    {
        // Arrange: the race a double click or a retried request produces,
        // two requests for the very same application rather than two
        // different ones. Both share the same competition's advisory lock,
        // so the second only reaches the write after the first has
        // committed; ApplicationNumberAssigner has to notice the row already
        // moved instead of trusting the copy this request loaded before
        // waiting for the lock.
        var (host, clock, emails) = CreateHost(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);

        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (applicant, _, _) = await SeedApplicantAsync(host, _database);
        var draft = await CreateAsync(applicant, competition.Id);
        await SaveAsync(applicant, draft.Id, FormDefinitionSamples.Parse("""{"opis":"Nasz projekt"}"""));

        // Act: two requests for the same application id, at once.
        var firstTask = SubmitAsync(applicant, draft.Id);
        var secondTask = SubmitAsync(applicant, draft.Id);
        await Task.WhenAll(firstTask, secondTask);

        // Assert: exactly one succeeds, the other is told it is already
        // submitted rather than silently producing a second number.
        var statusCodes = new[] { firstTask.Result.StatusCode, secondTask.Result.StatusCode };
        Assert.Contains(HttpStatusCode.OK, statusCodes);
        Assert.Contains(HttpStatusCode.Conflict, statusCodes);

        await using var context = _database.CreateContext();
        var stored = await context.Applications.AsNoTracking().SingleAsync(x => x.Id == draft.Id);
        Assert.Equal(ApplicationStatus.Submitted, stored.Status);
        Assert.Equal("001", stored.Number);

        // Exactly one status history entry, never two "Draft to Submitted"
        // rows for a transition that happened once.
        var history = await context.ApplicationStatusHistory
            .Where(x => x.ApplicationId == draft.Id)
            .ToListAsync();
        Assert.Single(history);

        // Exactly one confirmation e-mail, not one per request.
        Assert.Single(emails.Sent);
    }

    [RequiresDatabaseFact]
    public async Task Two_simultaneous_submissions_in_the_same_competition_get_distinct_numbers()
    {
        // Arrange: two applicants, two complete drafts, same competition.
        var (host, clock, _) = CreateHost(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);

        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (first, _, _) = await SeedApplicantAsync(host, _database);
        var (second, _, _) = await SeedApplicantAsync(host, _database);

        var firstDraft = await CreateAsync(first, competition.Id);
        await SaveAsync(first, firstDraft.Id, FormDefinitionSamples.Parse("""{"opis":"Projekt A"}"""));

        var secondDraft = await CreateAsync(second, competition.Id);
        await SaveAsync(second, secondDraft.Id, FormDefinitionSamples.Parse("""{"opis":"Projekt B"}"""));

        // Act: both submit at once, the exact race
        // docs/model-danych.md's "otwarte punkty implementacyjne" describes.
        var firstTask = SubmitAsync(first, firstDraft.Id);
        var secondTask = SubmitAsync(second, secondDraft.Id);
        await Task.WhenAll(firstTask, secondTask);

        // Assert: both succeed, neither loses the race with an error the
        // applicant could do nothing about, and the numbers differ.
        Assert.Equal(HttpStatusCode.OK, firstTask.Result.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondTask.Result.StatusCode);

        var firstSubmitted = (await firstTask.Result.Content.ReadFromJsonAsync<ApplicationResponse>())!;
        var secondSubmitted = (await secondTask.Result.Content.ReadFromJsonAsync<ApplicationResponse>())!;

        Assert.NotEqual(firstSubmitted.Number, secondSubmitted.Number);
        Assert.Equal(
            new[] { "001", "002" },
            new[] { firstSubmitted.Number, secondSubmitted.Number }.OrderBy(x => x, StringComparer.Ordinal));

        await using var context = _database.CreateContext();
        var numbers = await context.Applications
            .Where(x => x.CompetitionId == competition.Id)
            .Select(x => x.Number)
            .ToListAsync();
        Assert.Equal(2, numbers.Distinct().Count());
    }

    [RequiresDatabaseFact]
    public async Task Downloading_a_confirmation_for_a_draft_is_refused()
    {
        var (host, clock, _) = CreateHost(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);

        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (applicant, _, _) = await SeedApplicantAsync(host, _database);
        var draft = await CreateAsync(applicant, competition.Id);

        var response = await GetConfirmationAsync(applicant, draft.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Downloading_someone_elses_confirmation_is_forbidden_but_an_operator_may_read_it()
    {
        // Arrange
        var (host, clock, _) = CreateHost(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);

        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (owner, _, _) = await SeedApplicantAsync(host, _database);
        var (stranger, _, _) = await SeedApplicantAsync(host, _database);
        var draft = await CreateAsync(owner, competition.Id);
        await SaveAsync(owner, draft.Id, FormDefinitionSamples.Parse("""{"opis":"Nasz projekt"}"""));
        await SubmitAsync(owner, draft.Id);

        // Act & assert
        var strangerResponse = await GetConfirmationAsync(stranger, draft.Id);
        Assert.Equal(HttpStatusCode.Forbidden, strangerResponse.StatusCode);

        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        var operatorResponse = await GetConfirmationAsync(operatorClient, draft.Id);
        Assert.Equal(HttpStatusCode.OK, operatorResponse.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Downloading_a_confirmation_for_an_unknown_application_is_404()
    {
        var (host, clock, _) = CreateHost(_factory, _database);
        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (applicant, _, _) = await SeedApplicantAsync(host, _database);

        var response = await GetConfirmationAsync(applicant, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// The same clock owning host CompetitionTestHost.Create builds, plus a
    /// RecordingEmailSender in place of the real (log only) sender, so a test
    /// can assert on what would have been sent (EmailVerificationEndpointsTests
    /// uses the same substitution for the same reason).
    /// </summary>
    private static (WebApplicationFactory<Program> Host, FixedTimeProvider Clock, RecordingEmailSender Emails)
        CreateHost(OcwipWebApplicationFactory factory, PostgresDatabaseFixture database)
    {
        var clock = new FixedTimeProvider(CompetitionTestHost.Now);
        var emails = new RecordingEmailSender();

        var host = SessionTestHost.Create(
            factory,
            database,
            settings: new Dictionary<string, string?>
            {
                ["RateLimiting:PermitLimit"] = "200",
            },
            services: services =>
            {
                services.AddSingleton<TimeProvider>(clock);
                services.AddSingleton<IEmailSender>(emails);
            });

        return (host, clock, emails);
    }

    private static Task<HttpResponseMessage> SubmitAsync(HttpClient client, Guid id) =>
        client.PostAsync($"/applications/{id}/submit", content: null);

    private static Task<HttpResponseMessage> GetConfirmationAsync(HttpClient client, Guid id) =>
        client.GetAsync($"/applications/{id}/confirmation");
}
