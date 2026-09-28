using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Xunit;
using static Ocwip.Api.Tests.Endpoints.ApplicationTestHost;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// Templates of attachment requirements (T-102): the operator uploads,
/// replaces and withdraws; anybody downloads the one in force from a public
/// competition; the format comes from the bytes and the limit is 10 MB.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AttachmentTemplateTests(OcwipWebApplicationFactory factory, PostgresDatabaseFixture database)
    : IClassFixture<OcwipWebApplicationFactory>
{
    private static readonly byte[] Pdf = "%PDF-1.4\nwzor oswiadczenia"u8.ToArray();

    private async Task<(HttpClient Operator, HttpClient Anonymous, CompetitionResponse Competition, Guid Requirement)> SceneAsync(bool publish = true)
    {
        var (host, _) = CompetitionTestHost.Create(factory, database);
        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        var competition = await CompetitionTestHost.CreateAsync(operatorClient, CompetitionTestHost.Request() with
        {
            Attachments = [new CompetitionAttachmentRequest("Oświadczenie", null, AttachmentRequirement.Required, [AllowedFileFormat.Pdf])],
        });

        if (publish)
        {
            await PublishFormAsync(operatorClient, competition.Id, OneFieldForm());
            (await CompetitionTestHost.ChangeStatusAsync(operatorClient, competition.Id, CompetitionStatus.Published)).EnsureSuccessStatusCode();
        }

        return (operatorClient, host.CreateClient(), competition, competition.Attachments.Single().Id);
    }

    private static Task<HttpResponseMessage> UploadAsync(HttpClient client, Guid requirement, byte[] bytes, string name = "wzor.pdf")
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(file, "file", name);
        return client.PutAsync($"/competition-attachments/{requirement}/template", content);
    }

    [RequiresDatabaseFact]
    public async Task An_uploaded_template_is_linked_on_the_public_page_and_downloads_without_signing_in()
    {
        var (operatorClient, anonymous, competition, requirement) = await SceneAsync();

        var uploaded = await UploadAsync(operatorClient, requirement, Pdf);
        Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);

        var page = (await anonymous.GetFromJsonAsync<PublicCompetitionResponse>($"/public/competitions/{competition.Id}"))!;
        Assert.Equal("wzor.pdf", page.Attachments.Single().Template!.FileName);

        var download = await anonymous.GetAsync($"/public/attachment-templates/{requirement}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/pdf", download.Content.Headers.ContentType!.MediaType);
        Assert.Equal(Pdf, await download.Content.ReadAsByteArrayAsync());
    }

    [RequiresDatabaseFact]
    public async Task A_replacement_takes_over_and_a_withdrawal_ends_the_download_without_removing_rows()
    {
        var (operatorClient, anonymous, _, requirement) = await SceneAsync();
        (await UploadAsync(operatorClient, requirement, Pdf)).EnsureSuccessStatusCode();

        var second = "%PDF-1.4\nnowy wzor"u8.ToArray();
        (await UploadAsync(operatorClient, requirement, second, "nowy.pdf")).EnsureSuccessStatusCode();
        Assert.Equal(second, await anonymous.GetByteArrayAsync($"/public/attachment-templates/{requirement}"));

        Assert.Equal(HttpStatusCode.NoContent,
            (await operatorClient.PostAsync($"/competition-attachments/{requirement}/template/withdraw", content: null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/public/attachment-templates/{requirement}")).StatusCode);

        await using var context = database.CreateContext();
        var rows = await context.AttachmentTemplates.AsNoTracking().Where(x => x.CompetitionAttachmentId == requirement).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, x => Assert.False(x.IsActive));
    }

    [RequiresDatabaseFact]
    public async Task A_file_of_another_format_or_over_the_limit_is_refused()
    {
        var (operatorClient, _, _, requirement) = await SceneAsync();

        // Named .pdf, declared as PDF, and still not one: the bytes decide.
        var disguised = await UploadAsync(operatorClient, requirement, "to nie jest pdf"u8.ToArray());
        Assert.Equal(HttpStatusCode.BadRequest, disguised.StatusCode);

        var huge = new byte[Competition.DefaultMaxAttachmentSizeInBytes + 1];
        "%PDF-1.4\n"u8.CopyTo(huge);
        var tooLarge = await UploadAsync(operatorClient, requirement, huge);
        Assert.Equal(HttpStatusCode.BadRequest, tooLarge.StatusCode);
        Assert.Contains("10 MB", await tooLarge.Content.ReadAsStringAsync());

        await using var context = database.CreateContext();
        Assert.False(await context.AttachmentTemplates.AnyAsync(x => x.CompetitionAttachmentId == requirement));
    }

    [RequiresDatabaseFact]
    public async Task Only_an_operator_uploads_and_a_draft_competition_gives_nothing_away()
    {
        var (operatorClient, anonymous, _, requirement) = await SceneAsync(publish: false);
        (await UploadAsync(operatorClient, requirement, Pdf)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/public/attachment-templates/{requirement}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await UploadAsync(anonymous, requirement, Pdf)).StatusCode);
    }
}
