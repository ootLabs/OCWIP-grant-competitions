using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Xunit;
using static Ocwip.Api.Tests.Endpoints.ApplicationTestHost;
using static Ocwip.Api.Tests.Endpoints.EvaluationScene;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// T-42a: three files of the ranking list for the operator only, and the
/// published results for everybody, but only once approved and without the
/// rejected applications.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class RankingPublicationTests : IClassFixture<OcwipWebApplicationFactory>
{
    private readonly OcwipWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public RankingPublicationTests(OcwipWebApplicationFactory factory, PostgresDatabaseFixture database)
    {
        _factory = factory;
        _database = database;
    }

    [RequiresDatabaseFact]
    public async Task The_results_are_published_after_approval_without_the_rejected()
    {
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);
        clock.Now = CompetitionTestHost.Start.AddDays(1);

        var (applicant, funded) = await SubmittedAsync(host, _database, competition.Id);
        var (_, reserve) = await SubmittedAsync(host, _database, competition.Id);
        var (_, rejected) = await SubmittedAsync(host, _database, competition.Id);

        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        await PrepareAsync(operatorClient, competition.Id);
        await FormalAsync(operatorClient, funded, passed: true);
        await FormalAsync(operatorClient, reserve, passed: true);
        await FormalAsync(operatorClient, rejected, passed: false);
        var (expert, expertId) = await SeedReviewerAsync(host);
        await AcceptDeclarationAsync(expert, competition.Id);
        await ScoreAsync(operatorClient, expert, expertId, funded, 18);
        await ScoreAsync(operatorClient, expert, expertId, reserve, 12);
        (await operatorClient.PutAsJsonAsync(
            $"/applications/{funded}/grant-decision", new GrantDecisionRequest(6500m, null))).EnsureSuccessStatusCode();

        var anonymous = host.CreateClient();
        var results = $"/public/competitions/{competition.Id}/results";

        // A draft decision is not guessable from the public answer.
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync(results)).StatusCode);

        foreach (var format in new[] { "csv", "xlsx", "pdf" })
        {
            var address = $"/competitions/{competition.Id}/ranking/export/{format}";
            Assert.Equal(HttpStatusCode.Forbidden, (await applicant.GetAsync(address)).StatusCode);
            var file = await operatorClient.GetAsync(address);
            Assert.Equal(HttpStatusCode.OK, file.StatusCode);
            Assert.EndsWith($".{format}", file.Content.Headers.ContentDisposition!.FileName!.Trim('"'));
        }

        (await operatorClient.PostAsync($"/competitions/{competition.Id}/results/approve", content: null))
            .EnsureSuccessStatusCode();

        var published = (await anonymous.GetFromJsonAsync<PublicResultsResponse>(results))!;
        Assert.Equal(
            [(ApplicationStatus.Funded, (decimal?)6500m), (ApplicationStatus.Reserve, null)],
            published.Rows.Select(row => (row.Status, row.AwardedGrant)));
        Assert.DoesNotContain(published.Rows, row => row.Status == ApplicationStatus.Rejected);

        var xlsx = await operatorClient.GetByteArrayAsync($"/competitions/{competition.Id}/ranking/export/xlsx");
        using var zip = new ZipArchive(new MemoryStream(xlsx));
        using var sheet = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        var xml = await sheet.ReadToEndAsync();
        Assert.Contains("<v>6500", xml);
        Assert.Contains("Dofinansowany, umowa niepodpisana", xml);
    }
}
