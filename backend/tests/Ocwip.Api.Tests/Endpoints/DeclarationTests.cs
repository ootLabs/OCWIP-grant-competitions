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
/// T-40a: an expert sees no application of a competition before accepting the
/// impartiality declaration there; a refusal carries its reason and excludes
/// them; the decision is made once; the operator sees every expert's state.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class DeclarationTests : IClassFixture<OcwipWebApplicationFactory>
{
    private readonly OcwipWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public DeclarationTests(OcwipWebApplicationFactory factory, PostgresDatabaseFixture database)
    {
        _factory = factory;
        _database = database;
    }

    [RequiresDatabaseFact]
    public async Task Nothing_of_an_application_shows_before_the_declaration_is_accepted()
    {
        var scene = await SceneAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await scene.Expert.GetAsync($"/applications/{scene.ApplicationId}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await scene.Expert.PostAsync($"/applications/{scene.ApplicationId}/evaluations/merit", content: null)).StatusCode);

        var before = (await scene.Expert.GetFromJsonAsync<ReviewerWorkResponse>("/reviewer/applications"))!;
        var waiting = Assert.Single(before.Competitions);
        Assert.Equal(DeclarationStatus.NotDecided, waiting.Declaration);
        Assert.Equal(1, waiting.AssignedCount);
        Assert.Empty(waiting.Applications);

        var declaration = (await scene.Expert.GetFromJsonAsync<DeclarationResponse>(
            $"/reviewer/competitions/{scene.CompetitionId}/declaration"))!;
        Assert.Contains("bezstronności", declaration.Text);

        await AcceptDeclarationAsync(scene.Expert, scene.CompetitionId);

        Assert.Equal(HttpStatusCode.OK, (await scene.Expert.GetAsync($"/applications/{scene.ApplicationId}")).StatusCode);
        var after = (await scene.Expert.GetFromJsonAsync<ReviewerWorkResponse>("/reviewer/applications"))!;
        Assert.Single(Assert.Single(after.Competitions).Applications);
    }

    [RequiresDatabaseFact]
    public async Task A_refusal_needs_a_reason_excludes_the_expert_and_is_not_taken_back_with_a_click()
    {
        var scene = await SceneAsync();
        var address = $"/reviewer/competitions/{scene.CompetitionId}/declaration";

        var noReason = await scene.Expert.PostAsJsonAsync(address, new DeclarationDecisionRequest(false, "  "));
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);

        var refusal = await scene.Expert.PostAsJsonAsync(
            address, new DeclarationDecisionRequest(false, "Jestem członkiem zarządu jednego z wnioskodawców."));
        Assert.Equal(HttpStatusCode.OK, refusal.StatusCode);

        var second = await scene.Expert.PostAsJsonAsync(address, new DeclarationDecisionRequest(true, null));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await scene.Expert.GetAsync($"/applications/{scene.ApplicationId}")).StatusCode);

        var rows = (await scene.Operator.GetFromJsonAsync<List<DeclarationRow>>(
            $"/competitions/{scene.CompetitionId}/declarations"))!;
        var row = Assert.Single(rows);
        Assert.Equal(DeclarationStatus.Refused, row.Status);
        Assert.Equal("Jestem członkiem zarządu jednego z wnioskodawców.", row.RefusalReason);
    }

    [RequiresDatabaseFact]
    public async Task Only_an_expert_declares_and_only_the_operator_sees_everybodys_state()
    {
        var scene = await SceneAsync();

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await scene.Operator.PostAsJsonAsync(
                $"/reviewer/competitions/{scene.CompetitionId}/declaration",
                new DeclarationDecisionRequest(true, null))).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await scene.Expert.GetAsync($"/competitions/{scene.CompetitionId}/declarations")).StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task The_operator_lists_the_experts_and_who_is_assigned_where_and_an_expert_does_not()
    {
        var scene = await SceneAsync();

        var reviewers = (await scene.Operator.GetFromJsonAsync<List<ReviewerSummary>>("/reviewers"))!;
        var assignments = (await scene.Operator.GetFromJsonAsync<List<CompetitionAssignment>>(
            $"/competitions/{scene.CompetitionId}/assignments"))!;

        var assignment = Assert.Single(assignments);
        Assert.Equal(scene.ApplicationId, assignment.ApplicationId);
        Assert.Contains(reviewers, reviewer => reviewer.Id == assignment.ReviewerId);

        Assert.Equal(HttpStatusCode.Forbidden, (await scene.Expert.GetAsync("/reviewers")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await scene.Expert.GetAsync($"/competitions/{scene.CompetitionId}/assignments")).StatusCode);
    }

    private async Task<Scene> SceneAsync()
    {
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);
        clock.Now = CompetitionTestHost.Start.AddDays(1);

        var (applicant, _, _) = await SeedApplicantAsync(host, _database);
        var draft = await CreateAsync(applicant, competition.Id);
        await SaveAsync(applicant, draft.Id, FormDefinitionSamples.Parse("""{"opis":"projekt"}"""));
        (await applicant.PostAsync($"/applications/{draft.Id}/submit", content: null)).EnsureSuccessStatusCode();

        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        (await operatorClient.PostAsJsonAsync(
            $"/competitions/{competition.Id}/evaluation-cards/merit",
            new FormDefinitionRequest(EvaluationCardSamples.MeritCard()))).EnsureSuccessStatusCode();

        var (expert, expertId) = await SeedReviewerAsync(host);
        (await operatorClient.PostAsJsonAsync(
            $"/applications/{draft.Id}/assignments", new AssignReviewerRequest(expertId))).EnsureSuccessStatusCode();

        return new Scene(operatorClient, expert, competition.Id, draft.Id);
    }

    private sealed record Scene(HttpClient Operator, HttpClient Expert, Guid CompetitionId, Guid ApplicationId);
}
