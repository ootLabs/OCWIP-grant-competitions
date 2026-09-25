using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Ocwip.Api.Tests.Models.Forms;
using Xunit;
using static Ocwip.Api.Tests.Endpoints.ApplicationTestHost;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// T-39 over real HTTP: the settings route and the ranking list read from
/// finished evaluations, with the sample cards of EvaluationCardSamples.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class RankingEndpointsTests : IClassFixture<OcwipWebApplicationFactory>
{
    private readonly OcwipWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public RankingEndpointsTests(OcwipWebApplicationFactory factory, PostgresDatabaseFixture database)
    {
        _factory = factory;
        _database = database;
    }

    [RequiresDatabaseFact]
    public async Task The_list_ranks_by_the_finished_cards_of_two_experts()
    {
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);
        clock.Now = CompetitionTestHost.Start.AddDays(1);

        var first = await SubmittedAsync(host, competition.Id);
        clock.Now = CompetitionTestHost.Start.AddDays(2);
        var second = await SubmittedAsync(host, competition.Id);

        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        await PublishCardAsync(operatorClient, competition.Id, "formal", EvaluationCardSamples.FormalCard());
        await PublishCardAsync(operatorClient, competition.Id, "merit", EvaluationCardSamples.MeritCard());

        var settings = await operatorClient.PutAsJsonAsync(
            $"/competitions/{competition.Id}/evaluation-settings",
            new EvaluationSettingsRequest(2, ScoreAggregation.Sum, 20m, false, 30m));
        Assert.Equal(HttpStatusCode.OK, settings.StatusCode);

        foreach (var application in new[] { first, second })
        {
            await FinishFormalAsync(operatorClient, application);
        }

        var (expertA, expertAId) = await SeedReviewerAsync(host);
        var (expertB, expertBId) = await SeedReviewerAsync(host);
        await AcceptDeclarationAsync(expertA, competition.Id);
        await AcceptDeclarationAsync(expertB, competition.Id);

        // The second application scores higher; the first has only one card.
        await ScoreAsync(operatorClient, expertA, expertAId, second, 18, 4);
        await ScoreAsync(operatorClient, expertB, expertBId, second, 16, 3);
        await ScoreAsync(operatorClient, expertA, expertAId, first, 10, 2);

        var ranking = (await operatorClient.GetFromJsonAsync<RankingResponse>(
            $"/competitions/{competition.Id}/ranking"))!;

        Assert.Equal(2, ranking.Rows.Count);
        var top = ranking.Rows[0];
        Assert.Equal(second, top.ApplicationId);
        Assert.Equal(1, top.Rank);
        Assert.Equal(41m, top.MeritScore);
        Assert.Equal(2m, top.StrategicScore);
        Assert.True(top.PassesThreshold);
        Assert.Equal(FormalStanding.Passed, top.Formal);

        var waiting = ranking.Rows[1];
        Assert.Equal(first, waiting.ApplicationId);
        Assert.Null(waiting.Rank);
        Assert.Equal(1, waiting.MeritCardsFinished);
        Assert.Equal(2, waiting.MeritCardsRequired);
    }

    [RequiresDatabaseFact]
    public async Task Settings_out_of_range_are_refused_with_the_field_named()
    {
        var (host, _) = CompetitionTestHost.Create(_factory, _database);
        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        var competition = await CompetitionTestHost.CreateAsync(operatorClient);

        var response = await operatorClient.PutAsJsonAsync(
            $"/competitions/{competition.Id}/evaluation-settings",
            new EvaluationSettingsRequest(0, ScoreAggregation.Sum, -1m, false, 120m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("evaluatorsPerApplication", body);
        Assert.Contains("meritThreshold", body);
        Assert.Contains("divergenceThresholdPercent", body);

        var stored = (await operatorClient.GetFromJsonAsync<EvaluationSettingsResponse>(
            $"/competitions/{competition.Id}/evaluation-settings"))!;
        Assert.Equal(2, stored.EvaluatorsPerApplication);
        Assert.Null(stored.MeritThreshold);
    }

    [RequiresDatabaseFact]
    public async Task Only_the_operator_sees_the_list_and_the_settings()
    {
        var (host, _) = CompetitionTestHost.Create(_factory, _database);
        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        var competition = await CompetitionTestHost.CreateAsync(operatorClient);
        var (reviewer, _) = await SeedReviewerAsync(host);
        var (applicant, _, _) = await SeedApplicantAsync(host, _database);

        foreach (var client in new[] { reviewer, applicant })
        {
            Assert.Equal(
                HttpStatusCode.Forbidden,
                (await client.GetAsync($"/competitions/{competition.Id}/ranking")).StatusCode);
            Assert.Equal(
                HttpStatusCode.Forbidden,
                (await client.GetAsync($"/competitions/{competition.Id}/evaluation-settings")).StatusCode);
        }

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await host.CreateClient().GetAsync($"/competitions/{competition.Id}/ranking")).StatusCode);
    }

    private async Task<Guid> SubmittedAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> host, Guid competitionId)
    {
        var (applicant, _, _) = await SeedApplicantAsync(host, _database);
        var draft = await CreateAsync(applicant, competitionId);
        await SaveAsync(applicant, draft.Id, FormDefinitionSamples.Parse("""{"opis":"projekt"}"""));
        (await applicant.PostAsync($"/applications/{draft.Id}/submit", content: null)).EnsureSuccessStatusCode();
        return draft.Id;
    }

    private static async Task PublishCardAsync(HttpClient client, Guid competitionId, string stage, System.Text.Json.JsonElement card) =>
        (await client.PostAsJsonAsync(
            $"/competitions/{competitionId}/evaluation-cards/{stage}",
            new FormDefinitionRequest(card))).EnsureSuccessStatusCode();

    private static async Task FinishFormalAsync(HttpClient operatorClient, Guid applicationId)
    {
        var card = (await (await operatorClient.PostAsync($"/applications/{applicationId}/evaluations/formal", content: null))
            .Content.ReadFromJsonAsync<EvaluationResponse>())!;
        (await operatorClient.PutAsJsonAsync($"/evaluations/{card.Id}",
            new { answers = new JsonObject { ["w_terminie"] = true, ["przychod"] = true } })).EnsureSuccessStatusCode();
        (await operatorClient.PostAsync($"/evaluations/{card.Id}/finish", content: null)).EnsureSuccessStatusCode();
    }

    private static async Task ScoreAsync(
        HttpClient operatorClient, HttpClient expert, Guid expertId, Guid applicationId, int idea, int budget)
    {
        (await operatorClient.PostAsJsonAsync(
            $"/applications/{applicationId}/assignments", new AssignReviewerRequest(expertId))).EnsureSuccessStatusCode();
        var card = (await (await expert.PostAsync($"/applications/{applicationId}/evaluations/merit", content: null))
            .Content.ReadFromJsonAsync<EvaluationResponse>())!;
        (await expert.PutAsJsonAsync($"/evaluations/{card.Id}", new
        {
            answers = new JsonObject
            {
                ["pomysl"] = idea,
                ["pomysl_uzasadnienie"] = "Uzasadnienie.",
                ["budzet"] = budget,
                ["biale_plamy"] = true,
            },
        })).EnsureSuccessStatusCode();
        (await expert.PostAsync($"/evaluations/{card.Id}/finish", content: null)).EnsureSuccessStatusCode();
    }
}
