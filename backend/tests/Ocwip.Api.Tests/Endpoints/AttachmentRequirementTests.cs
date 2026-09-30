using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Ocwip.Api.Tests.Models.Forms;
using Xunit;
using static Ocwip.Api.Tests.Endpoints.ApplicationTestHost;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// A file answers a requirement of its competition (T-101, R-33), and a
/// submission without every required one is refused with the missing ones
/// named.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AttachmentRequirementTests(OcwipWebApplicationFactory factory, PostgresDatabaseFixture database)
    : IClassFixture<OcwipWebApplicationFactory>
{
    private static readonly byte[] PdfBytes = "%PDF-1.4\ntresc pliku testowego"u8.ToArray();

    private sealed record Scene(
        WebApplicationFactory<Program> Host,
        HttpClient Applicant,
        Guid ApplicationId,
        CompetitionResponse Competition);

    private async Task<Scene> SceneAsync(Entity? entity = null)
    {
        var (host, clock) = CompetitionTestHost.Create(factory, database);
        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        var competition = await CompetitionTestHost.CreateAsync(operatorClient, CompetitionTestHost.Request() with
        {
            Attachments =
            [
                new CompetitionAttachmentRequest("Statut", null, AttachmentRequirement.Required, [AllowedFileFormat.Pdf]),
                new CompetitionAttachmentRequest("Odpis z rejestru", null, AttachmentRequirement.RequiredOutsideKrs, [AllowedFileFormat.Pdf]),
                new CompetitionAttachmentRequest("Rekomendacje", null, AttachmentRequirement.Optional, [AllowedFileFormat.Pdf]),
            ],
        });
        await PublishFormAsync(operatorClient, competition.Id, OneFieldForm());
        (await CompetitionTestHost.ChangeStatusAsync(operatorClient, competition.Id, CompetitionStatus.Published)).EnsureSuccessStatusCode();
        competition = (await operatorClient.GetFromJsonAsync<CompetitionResponse>($"/competitions/{competition.Id}"))!;
        clock.Now = CompetitionTestHost.Start.AddDays(1);

        entity ??= TestEntity.New();
        await using (var context = database.CreateContext())
        {
            context.Entities.Add(entity);
            await context.SaveChangesAsync();
        }

        var email = SessionTestHost.Email("zalaczniki");
        await SessionTestHost.CreateAccountAsync(host, email, Role.Applicant, entityId: entity.Id);
        var applicant = await LoginAsync(host, email);
        var draft = await CreateAsync(applicant, competition.Id);
        await SaveAsync(applicant, draft.Id, FormDefinitionSamples.Parse("""{"opis":"Nasz projekt"}"""));

        return new Scene(host, applicant, draft.Id, competition);
    }

    private static Guid Requirement(Scene scene, string title) =>
        scene.Competition.Attachments.Single(x => x.Title == title).Id;

    private static async Task<HttpResponseMessage> UploadAsync(
        HttpClient client, Guid applicationId, Guid? requirementId, byte[]? bytes = null,
        string contentType = "application/pdf", string fileName = "plik.pdf")
    {
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes ?? PdfBytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(file, "file", fileName);
        if (requirementId is { } id)
        {
            content.Add(new StringContent(id.ToString()), "requirementId");
        }

        return await client.PostAsync($"/applications/{applicationId}/attachments", content);
    }

    [RequiresDatabaseFact]
    public async Task A_file_uploaded_for_a_requirement_is_linked_to_it_and_keeps_the_link_when_replaced()
    {
        var scene = await SceneAsync();
        var statut = Requirement(scene, "Statut");

        var upload = await UploadAsync(scene.Applicant, scene.ApplicationId, statut);

        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var uploaded = (await upload.Content.ReadFromJsonAsync<AttachmentResponse>())!;
        Assert.Equal(statut, uploaded.RequirementId);

        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent("%PDF-1.4\nnowa"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(file, "file", "nowy.pdf");
        var replaced = await scene.Applicant.PutAsync($"/attachments/{uploaded.Id}", content);

        Assert.Equal(statut, (await replaced.Content.ReadFromJsonAsync<AttachmentResponse>())!.RequirementId);
    }

    [RequiresDatabaseFact]
    public async Task A_requirement_of_another_competition_is_refused()
    {
        var scene = await SceneAsync();
        var other = await SceneAsync();

        var response = await UploadAsync(scene.Applicant, scene.ApplicationId, Requirement(other, "Statut"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("nie wymaga takiego załącznika", await response.Content.ReadAsStringAsync());
    }

    [RequiresDatabaseFact]
    public async Task A_format_the_requirement_does_not_take_is_refused_naming_what_it_takes()
    {
        var scene = await SceneAsync();
        // A JPG: allowed by the product, not by this requirement.
        byte[] jpg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1];

        var response = await UploadAsync(scene.Applicant, scene.ApplicationId, Requirement(scene, "Statut"), jpg, "image/jpeg", "zdjecie.jpg");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        // Refused by the requirement, not by the product's own allow list.
        var problem = (await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>())!;
        Assert.Equal("Załącznik \"Statut\" przyjmuje tylko: PDF.", problem.Detail);
    }

    /// <summary>
    /// The replacement answers the same requirement as the file it replaces,
    /// so it obeys the same format list: without this, the only way past the
    /// check on the first upload was to upload a PDF and then replace it.
    /// </summary>
    [RequiresDatabaseFact]
    public async Task A_replacement_in_a_format_the_requirement_does_not_take_is_refused()
    {
        var scene = await SceneAsync();
        var upload = await UploadAsync(scene.Applicant, scene.ApplicationId, Requirement(scene, "Statut"));
        var uploaded = (await upload.Content.ReadFromJsonAsync<AttachmentResponse>())!;

        byte[] jpg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1];
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(jpg);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(file, "file", "zdjecie.jpg");

        var response = await scene.Applicant.PutAsync($"/attachments/{uploaded.Id}", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = (await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>())!;
        Assert.Equal("Załącznik \"Statut\" przyjmuje tylko: PDF.", problem.Detail);

        // The file that answered the requirement is still the one stored.
        var listed = (await scene.Applicant.GetFromJsonAsync<IReadOnlyList<AttachmentResponse>>(
            $"/applications/{scene.ApplicationId}/attachments"))!;
        Assert.Equal(uploaded.Id, Assert.Single(listed).Id);
    }

    [RequiresDatabaseFact]
    public async Task Submitting_without_a_required_attachment_is_refused_with_each_missing_one_named()
    {
        // A card in another register than KRS, so the register extract is required too.
        var entity = TestEntity.New();
        entity.Register = EntityRegister.Other;
        entity.RegisterNumber = "Ewidencja starosty 12/2020";
        var scene = await SceneAsync(entity);

        var refused = await scene.Applicant.PostAsync($"/applications/{scene.ApplicationId}/submit", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        var body = await refused.Content.ReadAsStringAsync();
        Assert.Contains("Brakuje wymaganego załącznika: Statut.", body);
        Assert.Contains("Brakuje wymaganego załącznika: Odpis z rejestru.", body);
        Assert.DoesNotContain("Rekomendacje", body);

        (await UploadAsync(scene.Applicant, scene.ApplicationId, Requirement(scene, "Statut"))).EnsureSuccessStatusCode();
        (await UploadAsync(scene.Applicant, scene.ApplicationId, Requirement(scene, "Odpis z rejestru"))).EnsureSuccessStatusCode();

        var submitted = await scene.Applicant.PostAsync($"/applications/{scene.ApplicationId}/submit", content: null);
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task An_organisation_in_krs_is_not_asked_for_the_register_extract()
    {
        var scene = await SceneAsync();
        (await UploadAsync(scene.Applicant, scene.ApplicationId, Requirement(scene, "Statut"))).EnsureSuccessStatusCode();

        var submitted = await scene.Applicant.PostAsync($"/applications/{scene.ApplicationId}/submit", content: null);

        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
    }
}
