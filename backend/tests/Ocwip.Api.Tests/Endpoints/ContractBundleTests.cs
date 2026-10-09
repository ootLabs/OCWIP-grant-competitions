using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Ocwip.Api.Tests.Services;
using Ocwip.Api.Tests.Services.Documents;
using Xunit;
using static Ocwip.Api.Tests.Endpoints.ApplicationTestHost;
using static Ocwip.Api.Tests.Endpoints.EvaluationScene;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// T-45b: the 2026 template published like any other, and every contract of
/// a competition at once: the complete ones in a ZIP, the one with a blank
/// named in braki.txt instead of stopping the rest. Operators only.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ContractBundleTests(OcwipWebApplicationFactory factory, PostgresDatabaseFixture database)
    : IClassFixture<OcwipWebApplicationFactory>
{
    [RequiresDatabaseFact]
    public async Task The_complete_contracts_come_in_one_zip_and_the_incomplete_one_is_listed()
    {
        var (host, clock) = CompetitionTestHost.Create(factory, database);
        var competition = await PublishedCompetitionWithFormAsync(host);
        clock.Now = CompetitionTestHost.Start.AddDays(1);

        var (applicant, complete, _) = await SubmittedAsync(host, database, competition.Id);
        var (_, blank, _) = await SubmittedAsync(host, database, competition.Id);
        var (rejectedApplicant, rejected, _) = await SubmittedAsync(host, database, competition.Id);
        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        await PrepareAsync(operatorClient, competition.Id);
        var (expert, expertId) = await SeedReviewerAsync(host);
        await AcceptDeclarationAsync(expert, competition.Id);
        foreach (var id in new[] { complete, blank })
        {
            await FormalAsync(operatorClient, id, passed: true);
            await ScoreAsync(operatorClient, expert, expertId, id, 18);
            (await operatorClient.PutAsJsonAsync(
                $"/applications/{id}/grant-decision", new GrantDecisionRequest(5000m, null))).EnsureSuccessStatusCode();
        }

        // Rejected at the formal stage: no grant, so no contract in the bundle.
        await FormalAsync(operatorClient, rejected, passed: false);
        var rejectedNumber = (await rejectedApplicant.GetFromJsonAsync<ApplicationResponse>($"/applications/{rejected}"))!.Number!;

        var bundle = $"/competitions/{competition.Id}/contracts/bundle";
        Assert.Equal(HttpStatusCode.Conflict, (await operatorClient.PostAsync(bundle, content: null)).StatusCode);

        (await operatorClient.PostAsJsonAsync($"/competitions/{competition.Id}/contract-template",
            new DocumentTemplateRequest(Contract2026TemplateTests.Template()))).EnsureSuccessStatusCode();
        await StartReviewAsync(operatorClient, competition.Id);
        (await operatorClient.PostAsync($"/competitions/{competition.Id}/results/approve", content: null))
            .EnsureSuccessStatusCode();

        // One contract drawn up and filled in by hand; the other is left to the bundle.
        var contract = (await (await operatorClient.PostAsync($"/applications/{complete}/contract", content: null))
            .Content.ReadFromJsonAsync<ContractResponse>())!;
        var values = contract.Fields.Where(x => !x.System).ToDictionary(x => x.Name, x => (string?)$"W-{x.Name}");
        (await operatorClient.PutAsJsonAsync($"/contracts/{contract.Id}/values", new ContractValuesRequest(values)))
            .EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Forbidden, (await applicant.PostAsync(bundle, content: null)).StatusCode);

        var response = await operatorClient.PostAsync(bundle, content: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType!.MediaType);

        using var zip = new ZipArchive(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
        var pdf = Assert.Single(zip.Entries, x => x.Name.EndsWith(".pdf", StringComparison.Ordinal));
        Assert.Equal($"umowa-{contract.ApplicationNumber!.Replace('/', '-')}.pdf", pdf.Name);

        await using (var stream = pdf.Open())
        {
            using var copy = new MemoryStream();
            await stream.CopyToAsync(copy);
            var text = PdfTextReader.Text(copy.ToArray());
            Assert.Contains("§20.", text);
            Assert.Contains("Opolskim Centrum Wspierania Inicjatyw Pozarządowych", text);
            Assert.Contains("pięć tysięcy złotych", text);
            Assert.Contains("W-numer_rachunku", text);
        }

        using var missing = new StreamReader(zip.GetEntry("braki.txt")!.Open());
        var list = await missing.ReadToEndAsync();
        var other = (await operatorClient.GetFromJsonAsync<ContractResponse>($"/applications/{blank}/contract"))!;
        // One line per contract, starting with its number: the entity names
        // carry a random GUID, which may contain the other number by chance.
        var lines = list.Split('\n');
        var missingLine = Assert.Single(lines, line => line.StartsWith($"{other.ApplicationNumber} ", StringComparison.Ordinal));
        // The account is not missing: the contract took it from the frozen
        // entity card (P4-20). What only the operator knows still is.
        Assert.DoesNotContain("Numer rachunku", missingLine);
        Assert.Contains("Numer umowy z NIW", missingLine);
        Assert.DoesNotContain(lines, line => line.StartsWith($"{contract.ApplicationNumber} ", StringComparison.Ordinal));

        // Only granted applications: the rejected one is in neither the files nor the list.
        Assert.DoesNotContain(zip.Entries, x => x.Name.Contains(rejectedNumber.Replace('/', '-'), StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.StartsWith($"{rejectedNumber} ", StringComparison.Ordinal));
    }
}
