using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Ocwip.Api.Contracts;
using Ocwip.Api.Tests.Data;
using Ocwip.Api.Tests.Models.Forms;
using static Ocwip.Api.Tests.Endpoints.ApplicationTestHost;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// The steps from a submitted application to a finished evaluation, shared by
/// the tests of what happens after the evaluation (T-42, T-42a): the cards of
/// EvaluationCardSamples, a formal card passed or failed, one merit card.
/// </summary>
internal static class EvaluationScene
{
    public static async Task<(HttpClient Applicant, Guid Id)> SubmittedAsync(
        WebApplicationFactory<Program> host, PostgresDatabaseFixture database, Guid competitionId)
    {
        var (applicant, _, _) = await SeedApplicantAsync(host, database);
        var draft = await CreateAsync(applicant, competitionId);
        await SaveAsync(applicant, draft.Id, FormDefinitionSamples.Parse("""{"opis":"projekt"}"""));
        (await applicant.PostAsync($"/applications/{draft.Id}/submit", content: null)).EnsureSuccessStatusCode();
        return (applicant, draft.Id);
    }

    /// <summary>Both cards published, one expert per application, threshold 10.</summary>
    public static async Task PrepareAsync(HttpClient operatorClient, Guid competitionId)
    {
        foreach (var (stage, document) in new[]
            { ("formal", EvaluationCardSamples.FormalCard()), ("merit", EvaluationCardSamples.MeritCard()) })
        {
            (await operatorClient.PostAsJsonAsync(
                $"/competitions/{competitionId}/evaluation-cards/{stage}",
                new FormDefinitionRequest(document))).EnsureSuccessStatusCode();
        }

        (await operatorClient.PutAsJsonAsync(
            $"/competitions/{competitionId}/evaluation-settings",
            new EvaluationSettingsRequest(1, Ocwip.Api.Models.ScoreAggregation.Sum, 10m, false, null))).EnsureSuccessStatusCode();
    }

    public static async Task FormalAsync(HttpClient operatorClient, Guid applicationId, bool passed)
    {
        var card = (await (await operatorClient.PostAsync($"/applications/{applicationId}/evaluations/formal", content: null))
            .Content.ReadFromJsonAsync<EvaluationResponse>())!;
        (await operatorClient.PutAsJsonAsync($"/evaluations/{card.Id}",
            new { answers = new JsonObject { ["w_terminie"] = passed, ["przychod"] = true } })).EnsureSuccessStatusCode();
        (await operatorClient.PostAsync($"/evaluations/{card.Id}/finish", content: null)).EnsureSuccessStatusCode();
    }

    /// <summary>Assigns the expert and finishes their card: idea points plus one for the budget.</summary>
    public static async Task ScoreAsync(
        HttpClient operatorClient, HttpClient expert, Guid expertId, Guid applicationId, int idea)
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
                ["budzet"] = 1,
                ["biale_plamy"] = false,
            },
        })).EnsureSuccessStatusCode();
        (await expert.PostAsync($"/evaluations/{card.Id}/finish", content: null)).EnsureSuccessStatusCode();
    }
}
