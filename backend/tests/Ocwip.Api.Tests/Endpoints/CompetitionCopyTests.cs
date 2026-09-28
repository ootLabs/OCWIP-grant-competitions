using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Models.Forms;
using Ocwip.Api.Tests.Data;
using Ocwip.Api.Tests.Models.Forms;
using Xunit;
using static Ocwip.Api.Tests.Endpoints.ApplicationTestHost;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// "Skopiuj konkurs" (T-98): a draft with the previous edition's settings,
/// forms, cards, report form and contract template, ready to publish once
/// the dates are set, and independent of the original.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CompetitionCopyTests(OcwipWebApplicationFactory factory, PostgresDatabaseFixture database)
    : IClassFixture<OcwipWebApplicationFactory>
{
    private const string Template = "Umowa {{numer_umowy}} z {{nazwa_realizatora}}, konto {{numer_rachunku}}.";

    private static readonly DateTimeOffset NewStart = CompetitionTestHost.End.AddMonths(6);

    private async Task<(HttpClient Operator, CompetitionResponse Source)> SourceAsync()
    {
        var (host, _) = CompetitionTestHost.Create(factory, database);
        var source = await PublishedCompetitionWithFormAsync(host);
        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);

        await EvaluationScene.PrepareAsync(operatorClient, source.Id);
        (await operatorClient.PostAsJsonAsync($"/competitions/{source.Id}/report-form",
            new FormDefinitionRequest(ReportFormSamples.Report()))).EnsureSuccessStatusCode();
        (await operatorClient.PostAsJsonAsync($"/competitions/{source.Id}/contract-template",
            new DocumentTemplateRequest(Template))).EnsureSuccessStatusCode();

        return (operatorClient, source);
    }

    private static Task<HttpResponseMessage> CopyAsync(HttpClient client, Guid sourceId, string number, DateTimeOffset? start) =>
        client.PostAsJsonAsync($"/competitions/{sourceId}/copy",
            new CompetitionCopyRequest(number, "Konkurs 2027", start, start?.AddMonths(1)));

    private static string Unique() => $"K/{Guid.NewGuid():N}"[..20];

    [RequiresDatabaseFact]
    public async Task A_copy_carries_every_document_as_version_one_and_is_ready_to_publish_once_dated()
    {
        var (operatorClient, source) = await SourceAsync();
        var number = Unique();

        var response = await CopyAsync(operatorClient, source.Id, number, NewStart);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var copy = (await response.Content.ReadFromJsonAsync<CompetitionResponse>())!;

        Assert.Equal(CompetitionStatus.Draft, copy.Status);
        Assert.Equal(number, copy.Number);
        Assert.Equal("Konkurs 2027", copy.Title);
        Assert.Equal(NewStart, copy.StartDate);
        Assert.Empty(copy.PublicationGaps);

        await using var context = database.CreateContext();
        var original = await context.Competitions.AsNoTracking().SingleAsync(x => x.Id == source.Id);
        var copied = await context.Competitions.AsNoTracking().SingleAsync(x => x.Id == copy.Id);

        foreach (var (from, to) in new[]
        {
            (original.FormDefinitionId, copied.FormDefinitionId),
            (original.FormalCardDefinitionId, copied.FormalCardDefinitionId),
            (original.MeritCardDefinitionId, copied.MeritCardDefinitionId),
            (original.ReportFormDefinitionId, copied.ReportFormDefinitionId),
        })
        {
            var a = await context.FormDefinitions.AsNoTracking().SingleAsync(x => x.Id == from);
            var b = await context.FormDefinitions.AsNoTracking().SingleAsync(x => x.Id == to);
            Assert.Equal(copy.Id, b.CompetitionId);
            Assert.Equal(1, b.VersionNumber);
            Assert.Equal(a.Purpose, b.Purpose);
            Assert.True(JsonElement.DeepEquals(a.Definition, b.Definition));
        }

        Assert.Equal(original.MeritThreshold, copied.MeritThreshold);
        Assert.Equal(original.EvaluatorsPerApplication, copied.EvaluatorsPerApplication);
        Assert.Null(copied.ResultsApprovedAt);
        Assert.Null(copied.PublishedAt);

        var template = await context.DocumentTemplates.AsNoTracking().SingleAsync(x => x.CompetitionId == copy.Id);
        Assert.Equal((1, Template), (template.VersionNumber, template.Body));
        Assert.False(await context.Applications.AnyAsync(x => x.CompetitionId == copy.Id));
    }

    [RequiresDatabaseFact]
    public async Task A_change_in_the_copy_leaves_the_original_as_it_was()
    {
        var (operatorClient, source) = await SourceAsync();
        var copy = (await (await CopyAsync(operatorClient, source.Id, Unique(), NewStart)).Content.ReadFromJsonAsync<CompetitionResponse>())!;

        await PublishFormAsync(operatorClient, copy.Id, FormDefinitionSamples.WithFields(
            FormDefinitionSamples.Field("inne", "shortText", "\"maxLength\": 50")));
        (await operatorClient.PostAsJsonAsync($"/competitions/{copy.Id}/contract-template",
            new DocumentTemplateRequest("Nowa umowa {{numer_umowy}}."))).EnsureSuccessStatusCode();

        await using var context = database.CreateContext();
        var original = await context.Competitions.AsNoTracking().SingleAsync(x => x.Id == source.Id);
        Assert.Equal(source.FormDefinitionId, original.FormDefinitionId);
        Assert.Equal(1, await context.FormDefinitions.CountAsync(x => x.CompetitionId == source.Id && x.Purpose == FormPurpose.Application));
        Assert.Equal(Template, (await context.DocumentTemplates.AsNoTracking()
            .Where(x => x.CompetitionId == source.Id).OrderByDescending(x => x.VersionNumber).FirstAsync()).Body);
    }

    [RequiresDatabaseFact]
    public async Task A_copy_needs_a_free_number_a_start_date_and_an_operator()
    {
        var (operatorClient, source) = await SourceAsync();

        Assert.Equal(HttpStatusCode.Conflict, (await CopyAsync(operatorClient, source.Id, source.Number, NewStart)).StatusCode);

        var undated = await CopyAsync(operatorClient, source.Id, Unique(), null);
        Assert.Equal(HttpStatusCode.BadRequest, undated.StatusCode);
        Assert.Contains("startDate", await undated.Content.ReadAsStringAsync());

        var (host, _) = CompetitionTestHost.Create(factory, database);
        var (applicant, _, _) = await SeedApplicantAsync(host, database);
        Assert.Equal(HttpStatusCode.Forbidden, (await CopyAsync(applicant, source.Id, Unique(), NewStart)).StatusCode);
    }
}
