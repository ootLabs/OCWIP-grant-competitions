using System.Net;
using System.Net.Http.Json;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Ocwip.Api.Tests.Models.Forms;
using Xunit;
using static Ocwip.Api.Tests.Endpoints.ApplicationTestHost;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// T-41a: the operator reads every card of an application, formal first,
/// with who filled each in; an expert reads none of them through this route.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ApplicationEvaluationsTests : IClassFixture<OcwipWebApplicationFactory>
{
    private readonly OcwipWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public ApplicationEvaluationsTests(OcwipWebApplicationFactory factory, PostgresDatabaseFixture database)
    {
        _factory = factory;
        _database = database;
    }

    [RequiresDatabaseFact]
    public async Task The_operator_reads_the_formal_card_and_each_experts_card_with_the_name()
    {
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);
        clock.Now = CompetitionTestHost.Start.AddDays(1);

        var (applicant, _, _) = await SeedApplicantAsync(host, _database);
        var draft = await CreateAsync(applicant, competition.Id);
        await SaveAsync(applicant, draft.Id, FormDefinitionSamples.Parse("""{"opis":"projekt"}"""));
        (await applicant.PostAsync($"/applications/{draft.Id}/submit", content: null)).EnsureSuccessStatusCode();

        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        foreach (var (stage, card) in new[]
            { ("formal", EvaluationCardSamples.FormalCard()), ("merit", EvaluationCardSamples.MeritCard()) })
        {
            (await operatorClient.PostAsJsonAsync(
                $"/competitions/{competition.Id}/evaluation-cards/{stage}",
                new FormDefinitionRequest(card))).EnsureSuccessStatusCode();
        }

        var address = $"/applications/{draft.Id}/evaluations";
        Assert.Empty((await operatorClient.GetFromJsonAsync<List<ApplicationEvaluationItem>>(address))!);

        var (expert, expertId) = await SeedReviewerAsync(host);
        (await operatorClient.PostAsJsonAsync(
            $"/applications/{draft.Id}/assignments", new AssignReviewerRequest(expertId))).EnsureSuccessStatusCode();
        await AcceptDeclarationAsync(expert, competition.Id);

        var merit = (await (await expert.PostAsync($"/applications/{draft.Id}/evaluations/merit", content: null))
            .EnsureSuccessStatusCode().Content.ReadFromJsonAsync<EvaluationResponse>())!;
        (await expert.PutAsJsonAsync(
            $"/evaluations/{merit.Id}",
            new SaveEvaluationRequest(FormDefinitionSamples.Parse("""{"pomysl":15,"budzet":3}""")))).EnsureSuccessStatusCode();
        (await operatorClient.PostAsync($"/applications/{draft.Id}/evaluations/formal", content: null))
            .EnsureSuccessStatusCode();

        var items = (await operatorClient.GetFromJsonAsync<List<ApplicationEvaluationItem>>(address))!;

        Assert.Equal([EvaluationStage.Formal, EvaluationStage.Merit], items.Select(x => x.Evaluation.Stage));
        Assert.Equal("Ada Testowa", items[1].Author);
        Assert.Equal(18m, items[1].Evaluation.MeritScore);

        Assert.Equal(HttpStatusCode.Forbidden, (await expert.GetAsync(address)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await operatorClient.GetAsync($"/applications/{Guid.NewGuid()}/evaluations")).StatusCode);
    }
}
