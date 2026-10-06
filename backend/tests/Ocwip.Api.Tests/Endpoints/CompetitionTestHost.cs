using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// Shared setup for the competition endpoints (T-20): a host whose clock the
/// test owns, and a signed in caller of a chosen role.
/// </summary>
internal static class CompetitionTestHost
{
    /// <summary>
    /// A moment before every date the requests below use, so a freshly
    /// published competition starts out not yet taking applications and each
    /// test moves the clock to the case it is about.
    /// </summary>
    public static readonly DateTimeOffset Now =
        new(2026, 8, 1, 9, 0, 0, TimeSpan.Zero);

    public static readonly DateTimeOffset Start =
        new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    public static readonly DateTimeOffset End =
        new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    public static (WebApplicationFactory<Program> Host, FixedTimeProvider Clock) Create(
        OcwipWebApplicationFactory factory,
        PostgresDatabaseFixture database,
        Action<IServiceCollection>? services = null,
        IDictionary<string, string?>? settings = null)
    {
        var clock = new FixedTimeProvider(Now);

        var configuration = new Dictionary<string, string?>
        {
            // These tests sign in repeatedly; the per-IP limit from T-12.5
            // is not what any of them is about.
            ["RateLimiting:PermitLimit"] = "200",
        };

        foreach (var setting in settings ?? new Dictionary<string, string?>())
        {
            configuration[setting.Key] = setting.Value;
        }

        var host = SessionTestHost.Create(
            factory,
            database,
            settings: configuration,
            // Registered after the application's own TimeProvider, so this one
            // is what gets resolved.
            services: collection =>
            {
                collection.AddSingleton<TimeProvider>(clock);
                services?.Invoke(collection);
            });

        return (host, clock);
    }

    public static async Task<HttpClient> SignedInAs(
        WebApplicationFactory<Program> host,
        Role role)
    {
        var email = SessionTestHost.Email(role.ToString().ToLowerInvariant());
        await SessionTestHost.CreateAccountAsync(host, email, role);

        var client = host.CreateClient();

        var login = await client.PostAsJsonAsync(
            "/login",
            new
            {
                email,
                password = SessionTestHost.Password
            });

        login.EnsureSuccessStatusCode();

        return client;
    }

    /// <summary>
    /// A request every validation rule is happy with, so a test only has to
    /// state the one field it is about. The number is unique per call, because
    /// the column is.
    /// </summary>
    public static CompetitionRequest Request(
        string title = "Konkurs testowy",
        bool continuous = false) =>
        new(
            Number: $"{Guid.NewGuid():N}",
            Title: title,
            Description: "Opis konkursu testowego.",
            StartDate: Start,
            EndDate: continuous ? null : End,
            IsContinuousIntake: continuous,
            MaxGrantAmount: 5000m,
            FormDefinitionId: null);

    public static async Task<CompetitionResponse> CreateAsync(
        HttpClient client,
        CompetitionRequest? request = null)
    {
        var response = await client.PostAsJsonAsync(
            "/competitions", request ?? Request());

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<CompetitionResponse>())!;
    }

    /// <summary>
    /// Moves a competition through the status route. Publishing first gives
    /// the competition what publication requires (T-97) where it is still
    /// missing: the one-field form and both sample evaluation cards. A test
    /// that is ABOUT a missing form or card calls <see cref="RawChangeStatusAsync"/>.
    /// </summary>
    public static async Task<HttpResponseMessage> ChangeStatusAsync(
        HttpClient client,
        Guid id,
        CompetitionStatus target)
    {
        if (target is CompetitionStatus.Published)
        {
            await PrepareForPublicationAsync(client, id);
        }

        return await RawChangeStatusAsync(client, id, target);
    }

    public static Task<HttpResponseMessage> RawChangeStatusAsync(
        HttpClient client,
        Guid id,
        CompetitionStatus target) =>
        client.PostAsJsonAsync(
            $"/competitions/{id}/status",
            new CompetitionStatusChangeRequest(target));

    public static async Task PrepareForPublicationAsync(HttpClient client, Guid id)
    {
        // A caller who may not read it (the access tests' other roles) gets
        // no help: the status request itself is what those tests are about.
        var read = await client.GetAsync($"/competitions/{id}");
        if (!read.IsSuccessStatusCode)
        {
            return;
        }

        var competition = await read.Content.ReadFromJsonAsync<CompetitionResponse>();
        if (competition is null || !competition.IsActive || competition.PublicationGaps.Count == 0)
        {
            return;
        }

        if (competition.FormDefinitionId is null)
        {
            (await client.PostAsJsonAsync(
                $"/competitions/{id}/form-definitions",
                new FormDefinitionRequest(ApplicationTestHost.OneFieldForm()))).EnsureSuccessStatusCode();
        }

        foreach (var (stage, document, gap) in new[]
        {
            ("formal", Models.Forms.EvaluationCardSamples.FormalCard(), "formalnej"),
            ("merit", Models.Forms.EvaluationCardSamples.MeritCard(), "merytorycznej"),
        })
        {
            if (competition.PublicationGaps.Any(text => text.Contains(gap)))
            {
                (await client.PostAsJsonAsync(
                    $"/competitions/{id}/evaluation-cards/{stage}",
                    new FormDefinitionRequest(document))).EnsureSuccessStatusCode();
            }
        }
    }
}
