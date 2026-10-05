using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Services;
using Ocwip.Api.Tests.Data;
using Ocwip.Api.Tests.Models.Forms;
using Xunit;
using static Ocwip.Api.Tests.Endpoints.ApplicationTestHost;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// "Zwrot do poprawy" (T-103): the operator returns a submitted application
/// with sections, a note and a deadline; the applicant changes only those
/// sections, before the deadline, and submits a new version while the old one
/// stays readable.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ApplicationReturnTests(OcwipWebApplicationFactory factory, PostgresDatabaseFixture database)
    : IClassFixture<OcwipWebApplicationFactory>
{
    private static readonly DateTimeOffset InIntake = CompetitionTestHost.Start.AddDays(1);

    private static JsonElement Form() => FormDefinitionSamples.Parse($$"""
        {
          "schemaVersion": 1,
          "sections": [
            { "key": "dane", "title": "Dane projektu", "fields": [{{FormDefinitionSamples.Field("opis", "shortText", "\"maxLength\": 500")}}] },
            { "key": "budzet", "title": "Budżet", "fields": [{{FormDefinitionSamples.Field("kwota", "shortText", "\"maxLength\": 50, \"sensitive\": true")}}] }
          ]
        }
        """);

    private sealed record Scene(
        WebApplicationFactory<Program> Host,
        FixedTimeProvider Clock,
        RecordingEmailSender Emails,
        HttpClient Applicant,
        string Email,
        HttpClient Operator,
        ApplicationResponse Submitted);

    private async Task<Scene> SubmittedAsync()
    {
        var emails = new RecordingEmailSender();
        var (host, clock) = CompetitionTestHost.Create(factory, database, s => s.AddSingleton<IEmailSender>(emails));
        var competition = await PublishedCompetitionWithFormAsync(host, Form());
        clock.Now = InIntake;

        var (applicant, _, email) = await SeedApplicantAsync(host, database);
        var draft = await CreateAsync(applicant, competition.Id);
        await SaveAsync(applicant, draft.Id, FormDefinitionSamples.Parse("""{"opis":"Pierwszy opis","kwota":"4711"}"""));
        (await applicant.PostAsync($"/applications/{draft.Id}/submit", content: null)).EnsureSuccessStatusCode();
        var submitted = (await applicant.GetFromJsonAsync<ApplicationResponse>($"/applications/{draft.Id}"))!;

        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        return new Scene(host, clock, emails, applicant, email, operatorClient, submitted);
    }

    private static Task<HttpResponseMessage> ReturnAsync(
        HttpClient client, Guid id, DateTimeOffset deadline, bool attachments = false, params string[] sections) =>
        client.PostAsJsonAsync($"/applications/{id}/return",
            new ApplicationReturnRequest(sections.Length == 0 ? ["budzet"] : sections, attachments, "Popraw kwotę w budżecie.", deadline));

    [RequiresDatabaseFact]
    public async Task A_returned_application_is_corrected_in_its_sections_and_submitted_as_a_new_version()
    {
        var scene = await SubmittedAsync();
        var id = scene.Submitted.Id;

        var returned = await ReturnAsync(scene.Operator, id, InIntake.AddHours(1));
        Assert.Equal(HttpStatusCode.Created, returned.StatusCode);

        var mail = Assert.Single(scene.Emails.Sent, x => x.Subject.Contains("zwrócony do poprawy"));
        Assert.Equal(scene.Email, mail.To);
        Assert.Contains("Popraw kwotę w budżecie.", mail.Body);
        Assert.Contains("- Budżet", mail.Body);
        Assert.DoesNotContain("Dane projektu", mail.Body);

        Assert.Equal(ApplicationStatus.Returned,
            (await scene.Applicant.GetFromJsonAsync<ApplicationResponse>($"/applications/{id}"))!.Status);

        // A locked section is refused by the server, whatever the screen allows.
        var locked = await PutAsync(scene.Applicant, id, FormDefinitionSamples.Parse("""{"opis":"Zmieniony","kwota":"4711"}"""));
        Assert.Equal(HttpStatusCode.BadRequest, locked.StatusCode);
        Assert.Contains("\"opis\"", await locked.Content.ReadAsStringAsync());

        await SaveAsync(scene.Applicant, id, FormDefinitionSamples.Parse("""{"opis":"Pierwszy opis","kwota":"250"}"""));
        var resubmitted = await scene.Applicant.PostAsync($"/applications/{id}/submit", content: null);
        Assert.Equal(HttpStatusCode.OK, resubmitted.StatusCode);

        var current = (await scene.Applicant.GetFromJsonAsync<ApplicationResponse>($"/applications/{id}"))!;
        Assert.Equal(ApplicationStatus.Submitted, current.Status);
        Assert.Equal(scene.Submitted.Number, current.Number);
        Assert.NotEqual(scene.Submitted.Checksum, current.Checksum);

        var corrections = (await scene.Operator.GetFromJsonAsync<ApplicationCorrectionsResponse>($"/applications/{id}/corrections"))!;
        var version = Assert.Single(corrections.Versions);
        Assert.Equal(scene.Submitted.Checksum, version.Checksum);
        Assert.NotNull(Assert.Single(corrections.Returns).ResolvedAt);
        Assert.Equal(
            [(ApplicationStatus.Draft, ApplicationStatus.Submitted), (ApplicationStatus.Submitted, ApplicationStatus.Returned), (ApplicationStatus.Returned, ApplicationStatus.Submitted)],
            corrections.History.Select(x => (x.FromStatus, x.ToStatus)));

        // The version the operator returned stays readable as it was.
        var first = (await scene.Applicant.GetFromJsonAsync<ApplicationVersionResponse>($"/applications/{id}/versions/1"))!;
        Assert.Equal("4711", first.Answers.GetProperty("kwota").GetString());

        // A sensitive answer is encrypted in the kept version too (T-47a).
        await using var context = database.CreateContext();
        var stored = await context.Database
            .SqlQueryRaw<string>("SELECT answers::text AS \"Value\" FROM application_versions WHERE application_id = {0}", id)
            .SingleAsync();
        Assert.DoesNotContain("4711", stored);
        Assert.Contains("Pierwszy opis", stored);
        Assert.Equal(scene.Submitted.SubmittedAt, first.SubmittedAt);
        Assert.Equal(HttpStatusCode.NotFound, (await scene.Applicant.GetAsync($"/applications/{id}/versions/2")).StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task A_correction_closes_at_the_minute_of_its_deadline_even_after_the_intake()
    {
        var scene = await SubmittedAsync();
        var id = scene.Submitted.Id;
        var deadline = CompetitionTestHost.End.AddDays(2);
        (await ReturnAsync(scene.Operator, id, deadline)).EnsureSuccessStatusCode();

        // After the intake has closed, one second before the deadline: accepted.
        scene.Clock.Now = deadline.AddSeconds(-1);
        var applicant = await LoginAsync(scene.Host, scene.Email);
        (await PutAsync(applicant, id, FormDefinitionSamples.Parse("""{"opis":"Pierwszy opis","kwota":"300"}"""))).EnsureSuccessStatusCode();

        // At the deadline: refused, and so is the submission.
        scene.Clock.Now = deadline;
        applicant = await LoginAsync(scene.Host, scene.Email);
        var late = await PutAsync(applicant, id, FormDefinitionSamples.Parse("""{"opis":"Pierwszy opis","kwota":"400"}"""));
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
        Assert.Contains("Termin poprawy minął", await late.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, (await applicant.PostAsync($"/applications/{id}/submit", content: null)).StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Only_an_operator_returns_and_only_a_submitted_application_with_a_valid_request()
    {
        var scene = await SubmittedAsync();
        var id = scene.Submitted.Id;

        Assert.Equal(HttpStatusCode.Forbidden, (await ReturnAsync(scene.Applicant, id, InIntake.AddHours(1))).StatusCode);

        var invalid = await scene.Operator.PostAsJsonAsync($"/applications/{id}/return",
            new ApplicationReturnRequest(["nie_ma"], false, " ", InIntake.AddMinutes(-1)));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var body = await invalid.Content.ReadAsStringAsync();
        Assert.Contains("\"sections\"", body);
        Assert.Contains("\"message\"", body);
        Assert.Contains("\"deadline\"", body);

        Assert.Equal(HttpStatusCode.BadRequest, (await ReturnAsync(scene.Operator, id, InIntake.AddHours(1).AddSeconds(30))).StatusCode);

        (await ReturnAsync(scene.Operator, id, InIntake.AddHours(1))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await ReturnAsync(scene.Operator, id, InIntake.AddHours(1))).StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Evaluations_so_far_are_kept_inactive_and_nothing_is_evaluated_until_it_comes_back()
    {
        var scene = await SubmittedAsync();
        var id = scene.Submitted.Id;
        await EvaluationScene.PrepareAsync(scene.Operator, scene.Submitted.CompetitionId);
        await EvaluationScene.FormalAsync(scene.Operator, id, passed: true);

        (await ReturnAsync(scene.Operator, id, InIntake.AddHours(1))).EnsureSuccessStatusCode();

        await using (var context = database.CreateContext())
        {
            var formal = await context.Evaluations.AsNoTracking().SingleAsync(x => x.ApplicationId == id);
            Assert.False(formal.IsActive);
            Assert.Equal(EvaluationStatus.Finished, formal.Status);
        }

        Assert.Equal(HttpStatusCode.Conflict,
            (await scene.Operator.PostAsync($"/applications/{id}/evaluations/formal", content: null)).StatusCode);

        (await scene.Applicant.PostAsync($"/applications/{id}/submit", content: null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Created,
            (await scene.Operator.PostAsync($"/applications/{id}/evaluations/formal", content: null)).StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Attachments_change_only_when_the_return_unlocks_them()
    {
        var scene = await SubmittedAsync();
        var id = scene.Submitted.Id;
        (await ReturnAsync(scene.Operator, id, InIntake.AddHours(1))).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Conflict, (await UploadAsync(scene.Applicant, id)).StatusCode);

        (await scene.Applicant.PostAsync($"/applications/{id}/submit", content: null)).EnsureSuccessStatusCode();
        (await ReturnAsync(scene.Operator, id, InIntake.AddHours(1), attachments: true)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Created, (await UploadAsync(scene.Applicant, id)).StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task A_return_clears_the_draft_grant_decision_taken_on_the_old_version()
    {
        var scene = await SubmittedAsync();
        var id = scene.Submitted.Id;
        (await scene.Operator.PutAsJsonAsync($"/applications/{id}/grant-decision", new GrantDecisionRequest(10000m, "Na starą wersję.")))
            .EnsureSuccessStatusCode();

        (await ReturnAsync(scene.Operator, id, InIntake.AddHours(1))).EnsureSuccessStatusCode();

        await using var context = database.CreateContext();
        var stored = await context.Applications.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Null(stored.AwardedGrant);
        Assert.Null(stored.DecisionNote);
    }

    [RequiresDatabaseFact]
    public async Task A_correction_keeps_the_copy_of_the_entity_card_the_return_did_not_unlock()
    {
        var scene = await SubmittedAsync();
        var id = scene.Submitted.Id;
        var name = scene.Submitted.EntitySnapshot!.Name;
        (await ReturnAsync(scene.Operator, id, InIntake.AddHours(1))).EnsureSuccessStatusCode();

        await using (var context = database.CreateContext())
        {
            var entity = await context.Entities.SingleAsync(x => context.Applications.Any(a => a.Id == id && a.EntityId == x.Id));
            entity.Name = "Nazwa zmieniona po zwrocie";
            await context.SaveChangesAsync();
        }

        (await scene.Applicant.PostAsync($"/applications/{id}/submit", content: null)).EnsureSuccessStatusCode();

        var resubmitted = (await scene.Applicant.GetFromJsonAsync<ApplicationResponse>($"/applications/{id}"))!;
        Assert.Equal(ApplicationStatus.Submitted, resubmitted.Status);
        Assert.Equal(name, resubmitted.EntitySnapshot!.Name);
    }

    /// <summary>
    /// A correction keeps the kind of applicant the first submission settled
    /// (S-32), because the copy of the card beside it is kept too: the
    /// evaluation cards pick their criteria by that column and the formal
    /// standing is read through it, so letting the live card move it would
    /// reinterpret an evaluation already made.
    /// </summary>
    [RequiresDatabaseFact]
    public async Task A_correction_keeps_the_kind_of_applicant_the_first_submission_settled()
    {
        var scene = await SubmittedAsync();
        var id = scene.Submitted.Id;
        (await ReturnAsync(scene.Operator, id, InIntake.AddHours(1))).EnsureSuccessStatusCode();

        // The card changes outside the application, which PUT /me/entity
        // allows at any time, also after the return.
        (await scene.Applicant.PutAsJsonAsync(
            "/me/entity",
            EntityCardEndpointsTests.OrganisationCard(EntityType.PatronInformalGroup))).EnsureSuccessStatusCode();

        (await scene.Applicant.PostAsync($"/applications/{id}/submit", content: null)).EnsureSuccessStatusCode();

        await using var context = database.CreateContext();
        var application = await context.Applications.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal(EntityType.Organisation, application.ApplicantType);
        Assert.Equal(
            EntityType.Organisation,
            Ocwip.Api.Services.EntityCards.EntitySnapshots.Read(application.EntitySnapshot)!.Type);
    }

    /// <summary>
    /// The return is between the operator and the applicant (S-22): an
    /// assigned expert reads the application and its attachments (T-40), not
    /// the notes about correcting it, and not the earlier version those notes
    /// belong to.
    /// </summary>
    [RequiresDatabaseFact]
    public async Task An_assigned_expert_does_not_read_the_return_notes_or_the_earlier_version()
    {
        var scene = await SubmittedAsync();
        var id = scene.Submitted.Id;
        await EvaluationScene.PrepareAsync(scene.Operator, scene.Submitted.CompetitionId);
        var (expert, expertId) = await SeedReviewerAsync(scene.Host);
        await AcceptDeclarationAsync(expert, scene.Submitted.CompetitionId);
        (await scene.Operator.PostAsJsonAsync(
            $"/applications/{id}/assignments", new AssignReviewerRequest(expertId))).EnsureSuccessStatusCode();

        (await ReturnAsync(scene.Operator, id, InIntake.AddHours(1))).EnsureSuccessStatusCode();
        (await scene.Applicant.PostAsync($"/applications/{id}/submit", content: null)).EnsureSuccessStatusCode();

        // What T-40 gives them stays given.
        Assert.Equal(HttpStatusCode.OK, (await expert.GetAsync($"/applications/{id}")).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await expert.GetAsync($"/applications/{id}/corrections")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await expert.GetAsync($"/applications/{id}/versions/1")).StatusCode);

        // The two who are having the conversation still read it.
        Assert.Equal(HttpStatusCode.OK, (await scene.Operator.GetAsync($"/applications/{id}/corrections")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await scene.Applicant.GetAsync($"/applications/{id}/versions/1")).StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task A_card_opened_while_the_row_is_being_returned_waits_and_is_refused()
    {
        var scene = await SubmittedAsync();
        var id = scene.Submitted.Id;
        await EvaluationScene.PrepareAsync(scene.Operator, scene.Submitted.CompetitionId);

        // The row held the way a return holds it between its UPDATE and its commit.
        await using var connection = new Npgsql.NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var returning = await connection.BeginTransactionAsync();
        await using (var update = new Npgsql.NpgsqlCommand(
            "UPDATE applications SET status = 'Returned' WHERE id = @id", connection, returning))
        {
            update.Parameters.AddWithValue("id", id);
            Assert.Equal(1, await update.ExecuteNonQueryAsync());
        }

        var start = scene.Operator.PostAsync($"/applications/{id}/evaluations/formal", content: null);
        Assert.NotSame(start, await Task.WhenAny(start, Task.Delay(TimeSpan.FromSeconds(1))));

        await returning.CommitAsync();
        Assert.Equal(HttpStatusCode.Conflict, (await start).StatusCode);

        await using var context = database.CreateContext();
        Assert.False(await context.Evaluations.AnyAsync(x => x.ApplicationId == id && x.IsActive));
    }

    /// <summary>
    /// A returned application is editable again, but only by its applicant:
    /// the operator (who reads every application) writes nothing into it.
    /// </summary>
    [RequiresDatabaseFact]
    public async Task Only_the_applicant_writes_answers_or_files_into_a_returned_application()
    {
        var scene = await SubmittedAsync();
        var id = scene.Submitted.Id;
        (await ReturnAsync(scene.Operator, id, InIntake.AddHours(1), attachments: true)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Forbidden,
            (await PutAsync(scene.Operator, id, FormDefinitionSamples.Parse("""{"opis":"Pierwszy opis","kwota":"1"}"""))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await UploadAsync(scene.Operator, id)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await scene.Operator.DeleteAsync($"/applications/{id}")).StatusCode);

        (await PutAsync(scene.Applicant, id, FormDefinitionSamples.Parse("""{"opis":"Pierwszy opis","kwota":"1"}"""))).EnsureSuccessStatusCode();
    }

    [Fact]
    public void A_returned_application_is_not_granted()
    {
        Assert.False(ApplicationStatuses.IsGranted(ApplicationStatus.Returned));
    }

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, Guid applicationId)
    {
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent("%PDF-1.4\npoprawka"u8.ToArray());
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        content.Add(file, "file", "poprawka.pdf");
        return await client.PostAsync($"/applications/{applicationId}/attachments", content);
    }
}
