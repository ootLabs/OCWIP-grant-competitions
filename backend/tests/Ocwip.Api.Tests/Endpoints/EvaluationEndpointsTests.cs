using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Ocwip.Api.Tests.Models.Forms;
using Xunit;
using static Ocwip.Api.Tests.Endpoints.ApplicationTestHost;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// T-38 over real HTTP and a real database: publishing the two cards,
/// filling them in, finishing a stage, the result read from the answers, and
/// who may do which. The applicant of the scene is an Organisation, so the
/// sample formal card's patron criterion is not asked of them.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class EvaluationEndpointsTests : IClassFixture<OcwipWebApplicationFactory>
{
    private readonly OcwipWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public EvaluationEndpointsTests(OcwipWebApplicationFactory factory, PostgresDatabaseFixture database)
    {
        _factory = factory;
        _database = database;
    }

    [RequiresDatabaseFact]
    public async Task The_operator_fills_in_and_finishes_the_formal_card()
    {
        var scene = await SceneAsync();

        var start = await scene.Operator.PostAsync(Formal(scene), content: null);
        Assert.Equal(HttpStatusCode.Created, start.StatusCode);
        var evaluation = (await start.Content.ReadFromJsonAsync<EvaluationResponse>())!;
        Assert.Equal(EvaluationStatus.Draft, evaluation.Status);
        Assert.Equal(1, evaluation.CardVersionNumber);

        // Opening it again hands back the same card, not a second one.
        var again = await scene.Operator.PostAsync(Formal(scene), content: null);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(evaluation.Id, (await again.Content.ReadFromJsonAsync<EvaluationResponse>())!.Id);

        await SaveAsync(scene.Operator, evaluation.Id, new JsonObject { ["w_terminie"] = true });
        var incomplete = await scene.Operator.PostAsync($"/evaluations/{evaluation.Id}/finish", content: null);
        Assert.Equal(HttpStatusCode.BadRequest, incomplete.StatusCode);
        Assert.Contains("przychod", await incomplete.Content.ReadAsStringAsync());

        // Complete for an organisation: the patron criterion is not asked.
        await SaveAsync(scene.Operator, evaluation.Id, new JsonObject { ["w_terminie"] = true, ["przychod"] = true });
        var finish = await scene.Operator.PostAsync($"/evaluations/{evaluation.Id}/finish", content: null);
        Assert.Equal(HttpStatusCode.OK, finish.StatusCode);
        var finished = (await finish.Content.ReadFromJsonAsync<EvaluationResponse>())!;
        Assert.Equal(EvaluationStatus.Finished, finished.Status);
        Assert.NotNull(finished.FinishedAt);
        Assert.True(finished.FormalPassed);

        var late = await scene.Operator.PutAsJsonAsync(
            $"/evaluations/{evaluation.Id}", new { answers = new JsonObject { ["w_terminie"] = false } });
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task An_expert_scores_only_an_assigned_application_and_the_score_is_read_from_the_answers()
    {
        var scene = await SceneAsync();

        var unassigned = await scene.Reviewer.PostAsync(Merit(scene), content: null);
        Assert.Equal(HttpStatusCode.Forbidden, unassigned.StatusCode);

        await AssignAsync(scene, scene.ReviewerId);
        var start = await scene.Reviewer.PostAsync(Merit(scene), content: null);
        Assert.Equal(HttpStatusCode.Created, start.StatusCode);
        var evaluation = (await start.Content.ReadFromJsonAsync<EvaluationResponse>())!;

        var saved = await SaveAsync(scene.Reviewer, evaluation.Id, new JsonObject
        {
            ["pomysl"] = 18,
            ["budzet"] = 4,
            ["biale_plamy"] = true,
            ["z_patronem"] = true,
            ["kwota"] = 6000,
        });

        Assert.Equal(22m, saved.MeritScore);
        // "z_patronem" is asked of informal groups only: worth nothing here.
        Assert.Equal(1m, saved.StrategicScore);
        Assert.Equal(6000m, saved.RecommendedGrant);

        // Nothing stored but the answers.
        await using var context = _database.CreateContext();
        var row = await context.Evaluations.AsNoTracking().SingleAsync(x => x.Id == evaluation.Id);
        Assert.Equal(scene.ReviewerId, row.AuthorUserId);
        Assert.Equal(scene.ReviewerId, row.EnteredByUserId);
    }

    [RequiresDatabaseFact]
    public async Task One_expert_never_sees_another_experts_card_and_the_applicant_sees_none()
    {
        var scene = await SceneAsync();
        var (second, secondId) = await SeedReviewerAsync(scene.Host);
        await AssignAsync(scene, scene.ReviewerId);
        await AssignAsync(scene, secondId);

        var first = (await (await scene.Reviewer.PostAsync(Merit(scene), content: null))
            .Content.ReadFromJsonAsync<EvaluationResponse>())!;

        Assert.Equal(HttpStatusCode.Forbidden, (await second.GetAsync($"/evaluations/{first.Id}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await second.PutAsJsonAsync($"/evaluations/{first.Id}", new { answers = new JsonObject() })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await scene.Applicant.GetAsync($"/evaluations/{first.Id}")).StatusCode);

        // The second expert's own card is a different one.
        var own = (await (await second.PostAsync(Merit(scene), content: null))
            .Content.ReadFromJsonAsync<EvaluationResponse>())!;
        Assert.NotEqual(first.Id, own.Id);
    }

    [RequiresDatabaseFact]
    public async Task The_operator_reads_a_merit_card_but_does_not_fill_it_in()
    {
        var scene = await SceneAsync();
        await AssignAsync(scene, scene.ReviewerId);
        var card = (await (await scene.Reviewer.PostAsync(Merit(scene), content: null))
            .Content.ReadFromJsonAsync<EvaluationResponse>())!;

        Assert.Equal(HttpStatusCode.OK, (await scene.Operator.GetAsync($"/evaluations/{card.Id}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await scene.Operator.PutAsJsonAsync($"/evaluations/{card.Id}", new { answers = new JsonObject() })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await scene.Operator.PostAsync(Merit(scene), content: null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await scene.Reviewer.PostAsync(Formal(scene), content: null)).StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Withdrawing_the_assignment_withdraws_the_card()
    {
        var scene = await SceneAsync();
        await AssignAsync(scene, scene.ReviewerId);
        var card = (await (await scene.Reviewer.PostAsync(Merit(scene), content: null))
            .Content.ReadFromJsonAsync<EvaluationResponse>())!;

        await scene.Operator.DeleteAsync($"/applications/{scene.Application.Id}/assignments/{scene.ReviewerId}");

        Assert.Equal(HttpStatusCode.Forbidden, (await scene.Reviewer.GetAsync($"/evaluations/{card.Id}")).StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task A_card_keeps_the_version_it_was_started_on()
    {
        var scene = await SceneAsync();
        await AssignAsync(scene, scene.ReviewerId);
        var card = (await (await scene.Reviewer.PostAsync(Merit(scene), content: null))
            .Content.ReadFromJsonAsync<EvaluationResponse>())!;

        await PublishCardAsync(scene.Operator, scene.CompetitionId, "merit", EvaluationCardSamples.MeritCard());

        var read = (await scene.Reviewer.GetFromJsonAsync<EvaluationResponse>($"/evaluations/{card.Id}"))!;
        Assert.Equal(1, read.CardVersionNumber);
    }

    [RequiresDatabaseFact]
    public async Task Nothing_is_evaluated_before_its_card_exists_or_before_the_application_is_submitted()
    {
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);
        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (applicant, _, _) = await SeedApplicantAsync(host, _database);
        var draft = await CreateAsync(applicant, competition.Id);
        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);

        var onDraft = await operatorClient.PostAsync($"/applications/{draft.Id}/evaluations/formal", content: null);
        Assert.Equal(HttpStatusCode.Conflict, onDraft.StatusCode);

        await ApplicationTestHost.SaveAsync(applicant, draft.Id, FormDefinitionSamples.Parse("""{"opis":"jeden"}"""));
        (await applicant.PostAsync($"/applications/{draft.Id}/submit", content: null)).EnsureSuccessStatusCode();

        var noCard = await operatorClient.PostAsync($"/applications/{draft.Id}/evaluations/formal", content: null);
        Assert.Equal(HttpStatusCode.Conflict, noCard.StatusCode);
        Assert.Contains("karty oceny", await noCard.Content.ReadAsStringAsync());
    }

    [RequiresDatabaseFact]
    public async Task A_card_is_checked_for_its_purpose_when_published()
    {
        var scene = await SceneAsync();

        // A formal card with no criterion.
        var noCriterion = await scene.Operator.PostAsJsonAsync(
            $"/competitions/{scene.CompetitionId}/evaluation-cards/formal",
            new FormDefinitionRequest(OneFieldForm()));
        var unknownStage = await scene.Operator.PostAsJsonAsync(
            $"/competitions/{scene.CompetitionId}/evaluation-cards/ranking",
            new FormDefinitionRequest(EvaluationCardSamples.MeritCard()));
        var byApplicant = await scene.Applicant.PostAsJsonAsync(
            $"/competitions/{scene.CompetitionId}/evaluation-cards/merit",
            new FormDefinitionRequest(EvaluationCardSamples.MeritCard()));

        Assert.Equal(HttpStatusCode.BadRequest, noCriterion.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownStage.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byApplicant.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task An_evaluation_card_cannot_be_made_the_application_form_of_a_competition()
    {
        // The competition's own form pointer used to accept any version of
        // that competition; once cards are versions too, applicants would be
        // handed an evaluation card to fill in (review of T-38).
        var (host, _) = CompetitionTestHost.Create(_factory, _database);
        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        var competition = await CompetitionTestHost.CreateAsync(operatorClient);
        var published = await operatorClient.PostAsJsonAsync(
            $"/competitions/{competition.Id}/evaluation-cards/merit",
            new FormDefinitionRequest(EvaluationCardSamples.MeritCard()));
        var card = (await published.Content.ReadFromJsonAsync<FormDefinitionResponse>())!;

        var response = await operatorClient.PutAsJsonAsync(
            $"/competitions/{competition.Id}",
            CompetitionTestHost.Request() with { FormDefinitionId = card.Id });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("nie należy do tego konkursu", await response.Content.ReadAsStringAsync());
    }

    [RequiresDatabaseFact]
    public async Task The_schema_holds_one_author_and_a_dated_finish_whatever_writes_the_row()
    {
        var scene = await SceneAsync();
        var card = (await (await scene.Operator.PostAsync(Formal(scene), content: null))
            .Content.ReadFromJsonAsync<EvaluationResponse>())!;

        await using var context = _database.CreateContext();

        var twoAuthors = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE evaluations SET author_name = 'Komisja' WHERE id = {card.Id}"));
        var undated = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE evaluations SET status = 'Finished' WHERE id = {card.Id}"));

        Assert.Equal("ck_evaluations_one_author", twoAuthors.ConstraintName);
        Assert.Equal("ck_evaluations_finished_at_matches_status", undated.ConstraintName);
    }

    private static string Formal(Scene scene) => $"/applications/{scene.Application.Id}/evaluations/formal";

    private static string Merit(Scene scene) => $"/applications/{scene.Application.Id}/evaluations/merit";

    private static async Task<EvaluationResponse> SaveAsync(HttpClient client, Guid id, JsonObject answers)
    {
        var response = await client.PutAsJsonAsync($"/evaluations/{id}", new { answers });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EvaluationResponse>())!;
    }

    private static async Task AssignAsync(Scene scene, Guid reviewerId) =>
        (await scene.Operator.PostAsJsonAsync(
            $"/applications/{scene.Application.Id}/assignments",
            new AssignReviewerRequest(reviewerId))).EnsureSuccessStatusCode();

    private static async Task PublishCardAsync(HttpClient client, Guid competitionId, string stage, System.Text.Json.JsonElement card) =>
        (await client.PostAsJsonAsync(
            $"/competitions/{competitionId}/evaluation-cards/{stage}",
            new FormDefinitionRequest(card))).EnsureSuccessStatusCode();

    /// <summary>
    /// One competition with both sample cards published, one submitted
    /// application of an Organisation, an operator and one expert.
    /// </summary>
    private async Task<Scene> SceneAsync()
    {
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);
        clock.Now = CompetitionTestHost.Start.AddDays(1);

        var (applicant, _, _) = await SeedApplicantAsync(host, _database);
        var draft = await CreateAsync(applicant, competition.Id);
        await ApplicationTestHost.SaveAsync(applicant, draft.Id, FormDefinitionSamples.Parse("""{"opis":"jeden"}"""));
        var submit = await applicant.PostAsync($"/applications/{draft.Id}/submit", content: null);
        submit.EnsureSuccessStatusCode();
        var application = (await submit.Content.ReadFromJsonAsync<ApplicationResponse>())!;

        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        await PublishCardAsync(operatorClient, competition.Id, "formal", EvaluationCardSamples.FormalCard());
        await PublishCardAsync(operatorClient, competition.Id, "merit", EvaluationCardSamples.MeritCard());

        var (reviewer, reviewerId) = await SeedReviewerAsync(host);

        return new Scene(host, operatorClient, applicant, reviewer, reviewerId, competition.Id, application);
    }

    private sealed record Scene(
        WebApplicationFactory<Program> Host,
        HttpClient Operator,
        HttpClient Applicant,
        HttpClient Reviewer,
        Guid ReviewerId,
        Guid CompetitionId,
        ApplicationResponse Application);
}
