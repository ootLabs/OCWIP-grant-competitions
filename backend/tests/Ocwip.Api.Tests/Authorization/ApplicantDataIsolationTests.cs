using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Ocwip.Api.Tests.Endpoints;
using Ocwip.Api.Tests.Models.Forms;
using Xunit;
using static Ocwip.Api.Tests.Endpoints.ApplicationTestHost;

namespace Ocwip.Api.Tests.Authorization;

/// <summary>
/// T-36: the same rule T-13.3 proved on a synthetic probe
/// (<see cref="PermissionDenialTests"/>) and T-29/T-32/T-33 each proved once,
/// on their own single endpoint, as a side effect of building it. This suite
/// exists so the guarantee has ONE address instead of ten, on the real,
/// complete path: a real draft, real saved answers, a real uploaded
/// attachment and a real submission, never a row written by hand.
///
/// The two applicants below are seeded under two DIFFERENT competitions on
/// purpose. That is the card's own "dostęp do wniosku z konkursu, w którym
/// podmiot nie startował": every refusal below is therefore also proof that
/// an unrelated competition grants no accidental access, not only that a
/// different Podmiot does not.
///
/// R-01 (dostęp za organizacją, nie za osobą) is explicitly OUT of scope
/// here: the schema still binds one account to one Entity 1:1 (B-09), so
/// there is nothing yet to write that rule against. The card says to add the
/// expected test only once that decision lands.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ApplicantDataIsolationTests : IClassFixture<OcwipWebApplicationFactory>
{
    private readonly OcwipWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    private static readonly byte[] PdfBytes = "%PDF-1.4\ntresc pliku testowego"u8.ToArray();

    public ApplicantDataIsolationTests(
        OcwipWebApplicationFactory factory,
        PostgresDatabaseFixture database)
    {
        _factory = factory;
        _database = database;
    }

    [RequiresDatabaseFact]
    public async Task Swapping_the_application_id_never_reaches_the_other_applicants_draft()
    {
        var scene = await SceneAsync();

        var own = await scene.ApplicantA.GetAsync($"/applications/{scene.DraftA.Id}");
        var swapped = await scene.ApplicantA.GetAsync($"/applications/{scene.DraftB.Id}");

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Contains(Scene.MarkerA, await own.Content.ReadAsStringAsync());

        await AssertRefusedAsync(swapped, Scene.MarkerB);

        // And the other direction, because a rule that only held for the
        // applicant seeded first would pass a one directional test.
        var reverse = await scene.ApplicantB.GetAsync($"/applications/{scene.DraftA.Id}");
        await AssertRefusedAsync(reverse, Scene.MarkerA);
    }

    [RequiresDatabaseFact]
    public async Task Editing_someone_elses_draft_by_id_is_refused_and_leaves_the_answers_untouched()
    {
        var scene = await SceneAsync();

        var response = await PutAsync(
            scene.ApplicantA,
            scene.DraftB.Id,
            FormDefinitionSamples.Parse("""{"opis":"podmienione przez A"}"""));

        await AssertRefusedAsync(response, Scene.MarkerB);

        await using var context = _database.CreateContext();
        var stored = await context.Applications.AsNoTracking()
            .SingleAsync(x => x.Id == scene.DraftB.Id);
        Assert.Contains(Scene.MarkerB, stored.Answers.GetRawText());
    }

    [RequiresDatabaseFact]
    public async Task Deleting_someone_elses_draft_is_refused_and_it_stays_active()
    {
        var scene = await SceneAsync();

        var response = await scene.ApplicantA.DeleteAsync($"/applications/{scene.DraftB.Id}");

        await AssertRefusedAsync(response, Scene.MarkerB);

        await using var context = _database.CreateContext();
        var stored = await context.Applications.AsNoTracking()
            .SingleAsync(x => x.Id == scene.DraftB.Id);
        Assert.True(stored.IsActive);
    }

    [RequiresDatabaseFact]
    public async Task Reading_someone_elses_form_definition_through_their_application_id_is_refused()
    {
        var scene = await SceneAsync();

        var response = await scene.ApplicantA.GetAsync(
            $"/applications/{scene.DraftB.Id}/form-definition");

        await AssertRefusedAsync(response, Scene.MarkerB);
    }

    [RequiresDatabaseFact]
    public async Task Swapping_the_attachment_id_never_reaches_the_other_applicants_file()
    {
        var scene = await SceneAsync();

        var download = await scene.ApplicantA.GetAsync($"/attachments/{scene.AttachmentB.Id}");
        await AssertRefusedAsync(download, Scene.AttachmentBName);

        var list = await scene.ApplicantA.GetAsync($"/applications/{scene.DraftB.Id}/attachments");
        await AssertRefusedAsync(list, Scene.AttachmentBName);

        // Paired with the owner, who still reaches both: a suite that refuses
        // everything looks identical to one whose route is broken.
        var ownDownload = await scene.ApplicantB.GetAsync($"/attachments/{scene.AttachmentB.Id}");
        Assert.Equal(HttpStatusCode.OK, ownDownload.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Submitting_someone_elses_application_is_refused_and_it_stays_a_draft()
    {
        var scene = await SceneAsync();

        var response = await SubmitAsync(scene.ApplicantA, scene.DraftB.Id);

        await AssertRefusedAsync(response, Scene.MarkerB);

        await using var context = _database.CreateContext();
        var stored = await context.Applications.AsNoTracking()
            .SingleAsync(x => x.Id == scene.DraftB.Id);
        Assert.Equal(ApplicationStatus.Draft, stored.Status);
        Assert.Null(stored.Number);
        Assert.Null(stored.SubmittedAt);

        // The owner can still submit their own complete application: the
        // refusal above is the authorization rule, not a broken route.
        var ownSubmit = await SubmitAsync(scene.ApplicantB, scene.DraftB.Id);
        Assert.Equal(HttpStatusCode.OK, ownSubmit.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task An_attachments_storage_path_grants_no_access_outside_the_authorized_download_route()
    {
        var scene = await SceneAsync();

        string storagePath;
        await using (var context = _database.CreateContext())
        {
            var stored = await context.Attachments.AsNoTracking()
                .SingleAsync(x => x.Id == scene.AttachmentA.Id);
            storagePath = stored.StoragePath;
        }

        // The whole point of an opaque storage path: it shares nothing with
        // the public identifier the API hands out, so knowing one file's
        // storage name (a backup, a log line, a misconfigured server) buys
        // an attacker nothing.
        Assert.NotEqual(scene.AttachmentA.Id.ToString(), storagePath);

        var anonymous = scene.Host.CreateClient();

        var atRoot = await anonymous.GetAsync($"/{storagePath}");
        Assert.Equal(HttpStatusCode.NotFound, atRoot.StatusCode);

        // The storage path is not an attachment id, so the real download
        // route does not recognise it either, signed in or not.
        var throughDownloadRoute = await scene.ApplicantA.GetAsync($"/attachments/{storagePath}");
        Assert.Equal(HttpStatusCode.NotFound, throughDownloadRoute.StatusCode);

        var throughDownloadRouteAnonymous = await anonymous.GetAsync($"/attachments/{storagePath}");
        Assert.Equal(HttpStatusCode.Unauthorized, throughDownloadRouteAnonymous.StatusCode);

        // Paired with the one route that does work: the real id, by the
        // owner, still returns the exact bytes.
        var realDownload = await scene.ApplicantA.GetAsync($"/attachments/{scene.AttachmentA.Id}");
        Assert.Equal(HttpStatusCode.OK, realDownload.StatusCode);
        Assert.Equal(PdfBytes, await realDownload.Content.ReadAsByteArrayAsync());
    }

    /// <summary>
    /// Every refusal in this suite must look the same: 403, a Polish problem
    /// document, and a body that never carries the other applicant's marker.
    /// A 500 or an empty body are exactly the failures the card names by hand.
    /// </summary>
    private static async Task AssertRefusedAsync(HttpResponseMessage response, string forbiddenMarker)
    {
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.NotEmpty(body);
        Assert.DoesNotContain(forbiddenMarker, body);
    }

    /// <summary>
    /// Two applicants, two competitions, one real draft and one real
    /// attachment each, built once per test through the same endpoints an
    /// applicant's browser calls. Built fresh per test rather than shared,
    /// the same tradeoff PermissionScenario makes, so one test's assertions
    /// never depend on another test's leftovers in the same database.
    /// </summary>
    private async Task<Scene> SceneAsync()
    {
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);

        var competitionOne = await PublishedCompetitionWithFormAsync(host);
        var competitionTwo = await PublishedCompetitionWithFormAsync(host);

        clock.Now = CompetitionTestHost.Start.AddDays(1);

        var (applicantA, _, _) = await SeedApplicantAsync(host, _database);
        var (applicantB, _, _) = await SeedApplicantAsync(host, _database);

        var draftA = await CreateAsync(applicantA, competitionOne.Id);
        var draftB = await CreateAsync(applicantB, competitionTwo.Id);

        draftA = await SaveAsync(
            applicantA, draftA.Id, FormDefinitionSamples.Parse($$"""{"opis":"{{Scene.MarkerA}}"}"""));
        draftB = await SaveAsync(
            applicantB, draftB.Id, FormDefinitionSamples.Parse($$"""{"opis":"{{Scene.MarkerB}}"}"""));

        var attachmentA = await UploadAttachmentAsync(applicantA, draftA.Id, Scene.AttachmentAName);
        var attachmentB = await UploadAttachmentAsync(applicantB, draftB.Id, Scene.AttachmentBName);

        return new Scene(host, applicantA, applicantB, draftA, draftB, attachmentA, attachmentB);
    }

    private static Task<HttpResponseMessage> SubmitAsync(HttpClient client, Guid id) =>
        client.PostAsync($"/applications/{id}/submit", content: null);

    private static async Task<AttachmentResponse> UploadAttachmentAsync(
        HttpClient client, Guid applicationId, string fileName)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(PdfBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", fileName);

        var response = await client.PostAsync($"/applications/{applicationId}/attachments", content);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<AttachmentResponse>())!;
    }

    private sealed record Scene(
        WebApplicationFactory<Program> Host,
        HttpClient ApplicantA,
        HttpClient ApplicantB,
        ApplicationResponse DraftA,
        ApplicationResponse DraftB,
        AttachmentResponse AttachmentA,
        AttachmentResponse AttachmentB)
    {
        /// <summary>Never a real project name, so a leak is unmistakable in a body assertion.</summary>
        public const string MarkerA = "PROJEKT-A-C82D1A";

        public const string MarkerB = "PROJEKT-B-F3E997";

        public const string AttachmentAName = "zalacznik-a-poufny.pdf";

        public const string AttachmentBName = "zalacznik-b-poufny.pdf";
    }
}
