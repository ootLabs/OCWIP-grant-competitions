using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Ocwip.Api.Tests.Models.Forms;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// What every test of an application over HTTP starts from: a competition
/// taking applications with a form published against it, and an applicant
/// with a Podmiot, signed in (T-29, T-30).
/// </summary>
internal static class ApplicationTestHost
{
    /// <summary>The form the draft tests fill in: one short text field.</summary>
    public static JsonElement OneFieldForm() =>
        FormDefinitionSamples.WithFields(
            FormDefinitionSamples.Field("opis", "shortText", "\"maxLength\": 500"));

    /// <summary>
    /// A competition published and taking applications, with a form already
    /// published against it. What CreateDraftAsync needs to succeed.
    /// </summary>
    public static async Task<CompetitionResponse> PublishedCompetitionWithFormAsync(
        WebApplicationFactory<Program> host,
        JsonElement? definition = null)
    {
        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        var competition = await CompetitionTestHost.CreateAsync(operatorClient);

        await CompetitionTestHost.ChangeStatusAsync(
            operatorClient, competition.Id, CompetitionStatus.Published);

        await PublishFormAsync(operatorClient, competition.Id, definition ?? OneFieldForm());

        var refreshed = await operatorClient.GetFromJsonAsync<CompetitionResponse>(
            $"/competitions/{competition.Id}");

        return refreshed!;
    }

    /// <summary>Publishes the next version of a competition's form.</summary>
    public static async Task PublishFormAsync(
        HttpClient operatorClient, Guid competitionId, JsonElement definition)
    {
        var response = await operatorClient.PostAsJsonAsync(
            $"/competitions/{competitionId}/form-definitions",
            new FormDefinitionRequest(definition));
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// An Applicant account wired to a fresh Podmiot, signed in. Written
    /// through the context because there is no endpoint that creates a
    /// Podmiot yet (B-09; "karta organizacji" is a separate, unbuilt path, see
    /// docs/runbook/proces.md, ścieżka 2).
    ///
    /// Signs in at whatever the clock reads right now: the cookie handler
    /// validates its ticket against the same FixedTimeProvider the test moves
    /// (see CompetitionLifecycleEndpointTests), so a caller that still needs
    /// the clock to jump forward after this returns has to sign in again with
    /// <see cref="LoginAsync"/> once it has, or the cookie this call issued
    /// reads as expired against the new time.
    /// </summary>
    public static async Task<(HttpClient Client, Guid EntityId, string Email)> SeedApplicantAsync(
        WebApplicationFactory<Program> host,
        PostgresDatabaseFixture database)
    {
        var entity = TestEntity.New($"Podmiot {Guid.NewGuid():N}");

        await using (var context = database.CreateContext())
        {
            context.Entities.Add(entity);
            await context.SaveChangesAsync();
        }

        var email = SessionTestHost.Email("wnioskodawca");
        await SessionTestHost.CreateAccountAsync(
            host, email, Role.Applicant, entityId: entity.Id);

        var client = await LoginAsync(host, email);

        return (client, entity.Id, email);
    }

    public static async Task<HttpClient> LoginAsync(
        WebApplicationFactory<Program> host, string email)
    {
        var client = host.CreateClient();
        var login = await client.PostAsJsonAsync(
            "/login", new { email, password = SessionTestHost.Password });
        login.EnsureSuccessStatusCode();

        return client;
    }

    public static async Task<ApplicationResponse> CreateAsync(
        HttpClient client, Guid competitionId)
    {
        var response = await client.PostAsJsonAsync(
            $"/competitions/{competitionId}/applications", new { });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ApplicationResponse>())!;
    }

    public static Task<HttpResponseMessage> PutAsync(
        HttpClient client, Guid id, JsonElement answers) =>
        client.PutAsJsonAsync($"/applications/{id}", new SaveApplicationDraftRequest(answers));

    public static async Task<ApplicationResponse> SaveAsync(
        HttpClient client, Guid id, JsonElement answers)
    {
        var response = await PutAsync(client, id, answers);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ApplicationResponse>())!;
    }

    public static async Task<ApplicationResponse> GetAsync(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<ApplicationResponse>($"/applications/{id}"))!;

    public static async Task<IReadOnlyList<ApplicationOverviewResponse>> ListMineAsync(
        HttpClient client) =>
        (await client.GetFromJsonAsync<IReadOnlyList<ApplicationOverviewResponse>>(
            "/applications"))!;
}
