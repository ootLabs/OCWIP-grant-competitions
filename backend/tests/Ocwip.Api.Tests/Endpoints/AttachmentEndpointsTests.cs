using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Ocwip.Api.Tests.Models.Forms;
using Xunit;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// Application attachments, over real HTTP and a real PostgreSQL (T-32): the
/// upload path, the format allow list, the two size limits, download under
/// the same permission check as the application, and replacement that never
/// hard deletes the file it replaces.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AttachmentEndpointsTests : IClassFixture<OcwipWebApplicationFactory>
{
    private readonly OcwipWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    private static readonly byte[] PdfBytes = "%PDF-1.4\ntresc pliku testowego"u8.ToArray();

    public AttachmentEndpointsTests(
        OcwipWebApplicationFactory factory,
        PostgresDatabaseFixture database)
    {
        _factory = factory;
        _database = database;
    }

    [RequiresDatabaseFact]
    public async Task An_applicant_uploads_downloads_and_replaces_their_own_attachment()
    {
        // Arrange
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);

        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (applicant, _, _) = await SeedApplicantAsync(host);
        var draft = await CreateDraftAsync(applicant, competition.Id);

        // Act: upload
        var uploadResponse = await UploadAsync(
            applicant,
            HttpMethod.Post,
            $"/applications/{draft.Id}/attachments",
            PdfBytes,
            "statut.pdf",
            "application/pdf");

        // Assert
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
        var uploaded = (await uploadResponse.Content.ReadFromJsonAsync<AttachmentResponse>())!;
        Assert.Equal("statut.pdf", uploaded.FileName);
        Assert.Equal(PdfBytes.Length, uploaded.SizeInBytes);
        Assert.Equal(draft.Id, uploaded.ApplicationId);

        // The row carries the applicant's own entity, not a zero guid or the
        // application's: EntityScopedHandler reads this column directly.
        await using (var context = _database.CreateContext())
        {
            var stored = await context.Attachments
                .AsNoTracking()
                .SingleAsync(x => x.Id == uploaded.Id);
            var application = await context.Applications
                .AsNoTracking()
                .SingleAsync(x => x.Id == draft.Id);
            Assert.Equal(application.EntityId, stored.EntityId);
            Assert.Equal(AllowedFileFormat.Pdf, stored.Format);
        }

        // Act: download
        var downloadResponse = await applicant.GetAsync($"/attachments/{uploaded.Id}");

        // Assert: the exact bytes, and a content type decided by the verified
        // format, not by whatever the upload declared.
        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        Assert.Equal(PdfBytes, await downloadResponse.Content.ReadAsByteArrayAsync());
        Assert.Equal("application/pdf", downloadResponse.Content.Headers.ContentType!.MediaType);
        Assert.Equal(
            "attachment", downloadResponse.Content.Headers.ContentDisposition!.DispositionType);

        // Act: replace
        var replacementBytes = "%PDF-1.4\nnowa tresc"u8.ToArray();
        var replaceResponse = await UploadAsync(
            applicant,
            HttpMethod.Put,
            $"/attachments/{uploaded.Id}",
            replacementBytes,
            "statut-poprawiony.pdf",
            "application/pdf");

        Assert.Equal(HttpStatusCode.OK, replaceResponse.StatusCode);
        var replacement = (await replaceResponse.Content.ReadFromJsonAsync<AttachmentResponse>())!;
        Assert.NotEqual(uploaded.Id, replacement.Id);

        // Assert: the old row is inactive, never deleted, and still readable
        // with its own original bytes (AGENTS.md rule 5, and the card's own
        // "poprzedni nie znika twardo").
        await using (var context = _database.CreateContext())
        {
            var old = await context.Attachments.AsNoTracking().SingleAsync(x => x.Id == uploaded.Id);
            Assert.False(old.IsActive);
            Assert.NotNull(old.DeactivatedAt);
        }

        var oldStillDownloads = await applicant.GetAsync($"/attachments/{uploaded.Id}");
        Assert.Equal(HttpStatusCode.OK, oldStillDownloads.StatusCode);
        Assert.Equal(PdfBytes, await oldStillDownloads.Content.ReadAsByteArrayAsync());

        var newDownloads = await applicant.GetAsync($"/attachments/{replacement.Id}");
        Assert.Equal(HttpStatusCode.OK, newDownloads.StatusCode);
        Assert.Equal(replacementBytes, await newDownloads.Content.ReadAsByteArrayAsync());
    }

    [RequiresDatabaseTheory]
    [InlineData("umowa.docx", new byte[] { 0x50, 0x4B, 0x03, 0x04 })]
    [InlineData("zdjecie.jpg", new byte[] { 0xFF, 0xD8, 0xFF })]
    public async Task Formats_on_the_allow_list_are_accepted(string fileName, byte[] header)
    {
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);

        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (applicant, _, _) = await SeedApplicantAsync(host);
        var draft = await CreateDraftAsync(applicant, competition.Id);

        var response = await UploadAsync(
            applicant,
            HttpMethod.Post,
            $"/applications/{draft.Id}/attachments",
            [.. header, .. "reszta pliku"u8.ToArray()],
            fileName,
            "application/octet-stream");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task A_format_outside_the_allow_list_is_refused_regardless_of_the_declared_content_type()
    {
        // Arrange
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);

        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (applicant, _, _) = await SeedApplicantAsync(host);
        var draft = await CreateDraftAsync(applicant, competition.Id);

        // Act: an executable, declared as a PDF. The lie is the point.
        var response = await UploadAsync(
            applicant,
            HttpMethod.Post,
            $"/applications/{draft.Id}/attachments",
            [0x4D, 0x5A, 0x90, 0x00],
            "wirus.exe",
            "application/pdf");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("format", await response.Content.ReadAsStringAsync());
    }

    [RequiresDatabaseFact]
    public async Task An_empty_file_is_refused()
    {
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);

        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (applicant, _, _) = await SeedApplicantAsync(host);
        var draft = await CreateDraftAsync(applicant, competition.Id);

        var response = await UploadAsync(
            applicant,
            HttpMethod.Post,
            $"/applications/{draft.Id}/attachments",
            [],
            "pusty.pdf",
            "application/pdf");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task A_file_over_the_competitions_per_file_limit_is_refused()
    {
        // Arrange: a limit smaller than a single upload, so one file alone
        // trips it.
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        var request = CompetitionTestHost.Request() with
        {
            MaxAttachmentSizeInBytes = 10,
            MaxApplicationSizeInBytes = 1000,
        };
        var competition = await PublishedCompetitionWithFormAsync(host, operatorClient, request);

        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (applicant, _, _) = await SeedApplicantAsync(host);
        var draft = await CreateDraftAsync(applicant, competition.Id);

        // Act
        var response = await UploadAsync(
            applicant,
            HttpMethod.Post,
            $"/applications/{draft.Id}/attachments",
            PdfBytes,
            "statut.pdf",
            "application/pdf");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("rozmiar", await response.Content.ReadAsStringAsync());
    }

    [RequiresDatabaseFact]
    public async Task A_second_file_pushing_the_application_over_its_total_limit_is_refused()
    {
        // Arrange: each file fits alone, but not both together.
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        var request = CompetitionTestHost.Request() with
        {
            MaxAttachmentSizeInBytes = PdfBytes.Length,
            MaxApplicationSizeInBytes = PdfBytes.Length + 5,
        };
        var competition = await PublishedCompetitionWithFormAsync(host, operatorClient, request);

        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (applicant, _, _) = await SeedApplicantAsync(host);
        var draft = await CreateDraftAsync(applicant, competition.Id);

        var first = await UploadAsync(
            applicant,
            HttpMethod.Post,
            $"/applications/{draft.Id}/attachments",
            PdfBytes,
            "pierwszy.pdf",
            "application/pdf");
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // Act
        var second = await UploadAsync(
            applicant,
            HttpMethod.Post,
            $"/applications/{draft.Id}/attachments",
            PdfBytes,
            "drugi.pdf",
            "application/pdf");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Replacing_an_already_replaced_attachment_is_refused()
    {
        // Arrange
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);

        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (applicant, _, _) = await SeedApplicantAsync(host);
        var draft = await CreateDraftAsync(applicant, competition.Id);

        var uploadResponse = await UploadAsync(
            applicant,
            HttpMethod.Post,
            $"/applications/{draft.Id}/attachments",
            PdfBytes,
            "statut.pdf",
            "application/pdf");
        var original = (await uploadResponse.Content.ReadFromJsonAsync<AttachmentResponse>())!;

        // One replace succeeds and deactivates the original.
        var firstReplace = await UploadAsync(
            applicant,
            HttpMethod.Put,
            $"/attachments/{original.Id}",
            "%PDF-1.4\ndruga wersja"u8.ToArray(),
            "v2.pdf",
            "application/pdf");
        Assert.Equal(HttpStatusCode.OK, firstReplace.StatusCode);

        // Act: a second request targeting the now-inactive original, as a
        // retried or replayed request would.
        var secondReplace = await UploadAsync(
            applicant,
            HttpMethod.Put,
            $"/attachments/{original.Id}",
            "%PDF-1.4\ntrzecia wersja"u8.ToArray(),
            "v3.pdf",
            "application/pdf");

        // Assert: refused, not a second active row for the same slot.
        Assert.Equal(HttpStatusCode.Conflict, secondReplace.StatusCode);

        await using var context = _database.CreateContext();
        var activeCount = await context.Attachments
            .CountAsync(x => x.ApplicationId == draft.Id && x.IsActive);
        Assert.Equal(1, activeCount);
    }

    [RequiresDatabaseFact]
    public async Task An_applicant_cannot_download_someone_elses_attachment_by_a_guessed_id()
    {
        // Arrange
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);

        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (owner, _, _) = await SeedApplicantAsync(host);
        var (stranger, _, _) = await SeedApplicantAsync(host);
        var draft = await CreateDraftAsync(owner, competition.Id);

        var uploadResponse = await UploadAsync(
            owner,
            HttpMethod.Post,
            $"/applications/{draft.Id}/attachments",
            PdfBytes,
            "poufne.pdf",
            "application/pdf");
        var uploaded = (await uploadResponse.Content.ReadFromJsonAsync<AttachmentResponse>())!;

        // Act: the id is not guessed at random here only because a real
        // attacker would enumerate GUIDs the same way, one real id at a time.
        var response = await stranger.GetAsync($"/attachments/{uploaded.Id}");

        // Assert: 403, and the body never carries the owner's file name
        // either (T-13.3's own standard: check the body, not only the status).
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain("poufne", await response.Content.ReadAsStringAsync());
    }

    [RequiresDatabaseFact]
    public async Task An_operator_may_download_any_attachment()
    {
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);

        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (applicant, _, _) = await SeedApplicantAsync(host);
        var draft = await CreateDraftAsync(applicant, competition.Id);

        var uploadResponse = await UploadAsync(
            applicant,
            HttpMethod.Post,
            $"/applications/{draft.Id}/attachments",
            PdfBytes,
            "statut.pdf",
            "application/pdf");
        var uploaded = (await uploadResponse.Content.ReadFromJsonAsync<AttachmentResponse>())!;

        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        var response = await operatorClient.GetAsync($"/attachments/{uploaded.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Uploading_after_the_intake_closes_is_refused_by_T21s_rule()
    {
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);

        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (applicant, _, email) = await SeedApplicantAsync(host);
        var draft = await CreateDraftAsync(applicant, competition.Id);

        // The clock jumps past the closing minute, so the earlier cookie
        // reads as issued a month before now: sign in again, same reasoning
        // as ApplicationDraftEndpointsTests.
        clock.Now = CompetitionTestHost.End.AddMinutes(1);
        applicant = await LoginAsync(host, email);

        var response = await UploadAsync(
            applicant,
            HttpMethod.Post,
            $"/applications/{draft.Id}/attachments",
            PdfBytes,
            "spozniony.pdf",
            "application/pdf");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Nabór został zamknięty", await response.Content.ReadAsStringAsync());
    }

    [RequiresDatabaseFact]
    public async Task Uploading_to_a_submitted_application_is_refused()
    {
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);

        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (applicant, _, _) = await SeedApplicantAsync(host);
        var draft = await CreateDraftAsync(applicant, competition.Id);

        // Submission is T-33, not built yet: the row is flipped by hand, the
        // same pattern ApplicationDraftEndpointsTests uses for the same reason.
        await using (var context = _database.CreateContext())
        {
            var application = await context.Applications.SingleAsync(x => x.Id == draft.Id);
            application.Status = ApplicationStatus.Submitted;
            application.SubmittedAt = clock.Now;
            application.Number = "001";
            await context.SaveChangesAsync();
        }

        var response = await UploadAsync(
            applicant,
            HttpMethod.Post,
            $"/applications/{draft.Id}/attachments",
            PdfBytes,
            "po-zlozeniu.pdf",
            "application/pdf");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Uploading_to_an_unknown_application_is_404()
    {
        var (host, _) = CompetitionTestHost.Create(_factory, _database);
        var (applicant, _, _) = await SeedApplicantAsync(host);

        var response = await UploadAsync(
            applicant,
            HttpMethod.Post,
            $"/applications/{Guid.NewGuid()}/attachments",
            PdfBytes,
            "statut.pdf",
            "application/pdf");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Downloading_or_replacing_an_unknown_attachment_is_404()
    {
        var (host, _) = CompetitionTestHost.Create(_factory, _database);
        var (applicant, _, _) = await SeedApplicantAsync(host);
        var missing = Guid.NewGuid();

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await applicant.GetAsync($"/attachments/{missing}")).StatusCode);

        var replaceResponse = await UploadAsync(
            applicant, HttpMethod.Put, $"/attachments/{missing}", PdfBytes, "x.pdf", "application/pdf");
        Assert.Equal(HttpStatusCode.NotFound, replaceResponse.StatusCode);
    }

    /// <summary>
    /// A competition published and taking applications, with a form already
    /// published against it and the given (or default) settings.
    /// </summary>
    private static async Task<CompetitionResponse> PublishedCompetitionWithFormAsync(
        WebApplicationFactory<Program> host,
        HttpClient? operatorClient = null,
        CompetitionRequest? request = null)
    {
        operatorClient ??= await CompetitionTestHost.SignedInAs(host, Role.Operator);
        var competition = await CompetitionTestHost.CreateAsync(operatorClient, request);

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

    private static async Task<ApplicationResponse> CreateDraftAsync(
        HttpClient client, Guid competitionId)
    {
        var response = await client.PostAsJsonAsync(
            $"/competitions/{competitionId}/applications", new { });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ApplicationResponse>())!;
    }

    private static async Task<HttpResponseMessage> UploadAsync(
        HttpClient client,
        HttpMethod method,
        string url,
        byte[] bytes,
        string fileName,
        string contentType)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", fileName);

        using var request = new HttpRequestMessage(method, url) { Content = content };
        return await client.SendAsync(request);
    }
}
