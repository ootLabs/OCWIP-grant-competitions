using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Endpoints;
using Ocwip.Api.Tests.Data;
using Xunit;
using static Ocwip.Api.Tests.Endpoints.ApplicationTestHost;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// Reads of personal data land in personal_data_reads (T-47a): who, what,
/// through which endpoint. A refused read leaves no row, because it read
/// nothing.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class PersonalDataReadTests(OcwipWebApplicationFactory factory, PostgresDatabaseFixture database)
    : IClassFixture<OcwipWebApplicationFactory>
{
    private async Task<List<(Guid UserId, string Endpoint)>> ReadsOfAsync(Guid applicationId)
    {
        await using var context = database.CreateContext();
        return (await context.PersonalDataReads.AsNoTracking()
                .Where(x => x.Resource == "application" && x.ResourceId == applicationId)
                .Select(x => new { x.UserId, x.Endpoint })
                .ToListAsync())
            .Select(x => (x.UserId, x.Endpoint))
            .ToList();
    }

    private async Task<Guid> UserIdAsync(string email)
    {
        await using var context = database.CreateContext();
        return await context.Users.Where(x => x.Email == email).Select(x => x.Id).SingleAsync();
    }

    [RequiresDatabaseFact]
    public async Task Reading_an_application_is_logged_with_who_and_where()
    {
        var (host, clock) = CompetitionTestHost.Create(factory, database);
        var competition = await PublishedCompetitionWithFormAsync(host);
        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (applicant, _, email) = await SeedApplicantAsync(host, database);
        var draft = await CreateAsync(applicant, competition.Id);

        (await applicant.GetAsync($"/applications/{draft.Id}")).EnsureSuccessStatusCode();

        var read = Assert.Single(await ReadsOfAsync(draft.Id));
        Assert.Equal(await UserIdAsync(email), read.UserId);
        Assert.Equal("GET /applications/{id:guid}", read.Endpoint);
    }

    [RequiresDatabaseFact]
    public async Task A_refused_read_leaves_no_row()
    {
        var (host, clock) = CompetitionTestHost.Create(factory, database);
        var competition = await PublishedCompetitionWithFormAsync(host);
        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (owner, _, _) = await SeedApplicantAsync(host, database);
        var (stranger, _, _) = await SeedApplicantAsync(host, database);
        var draft = await CreateAsync(owner, competition.Id);

        var refused = await stranger.GetAsync($"/applications/{draft.Id}");

        Assert.True(refused.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound);
        Assert.Empty(await ReadsOfAsync(draft.Id));
    }

    /// <summary>
    /// The list of an application's attachments carries file names, which in
    /// practice carry surnames, and the same list is logged on the route
    /// beside it (S-36). Before this it was the cheaper way to the same data.
    /// </summary>
    [RequiresDatabaseFact]
    public async Task Listing_the_attachments_of_an_application_is_logged()
    {
        var (host, clock) = CompetitionTestHost.Create(factory, database);
        var competition = await PublishedCompetitionWithFormAsync(host);
        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (applicant, _, email) = await SeedApplicantAsync(host, database);
        var draft = await CreateAsync(applicant, competition.Id);

        (await applicant.GetAsync($"/applications/{draft.Id}/attachments")).EnsureSuccessStatusCode();

        var read = Assert.Single(await ReadsOfAsync(draft.Id));
        Assert.Equal(await UserIdAsync(email), read.UserId);
        Assert.Equal("GET /applications/{applicationId:guid}/attachments", read.Endpoint);
    }

    [Fact]
    public void Only_a_success_counts_as_a_read()
    {
        Assert.True(PersonalDataReadFilter.Succeeded(TypedResults.Ok("dane")));
        Assert.True(PersonalDataReadFilter.Succeeded(TypedResults.File([1], "application/pdf")));
        Assert.False(PersonalDataReadFilter.Succeeded(TypedResults.Problem("nie", statusCode: 403)));
        Assert.False(PersonalDataReadFilter.Succeeded(TypedResults.NotFound()));
        Assert.False(PersonalDataReadFilter.Succeeded(null));

        // No status code of their own, and none of them hands out the data.
        Assert.False(PersonalDataReadFilter.Succeeded(TypedResults.Forbid()));
        Assert.False(PersonalDataReadFilter.Succeeded(TypedResults.Challenge()));
        Assert.False(PersonalDataReadFilter.Succeeded(TypedResults.Redirect("/logowanie")));
    }

    /// <summary>
    /// Every endpoint that hands out an application, an attachment, a
    /// contract or a report is logged, read from the application's own
    /// endpoint table: dropping .LogsPersonalDataRead from one turns this red.
    /// </summary>
    [RequiresDatabaseFact]
    public void The_logged_reads_are_exactly_the_reviewed_list()
    {
        var host = SessionTestHost.Create(factory, database);

        var logged = host.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(x => x.Metadata.GetMetadata<PersonalDataReadMetadata>() is not null)
            .Select(x => $"{string.Join(",", x.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods)} {x.RoutePattern.RawText} "
                + x.Metadata.GetMetadata<PersonalDataReadMetadata>()!.Resource)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(Reviewed.Order(StringComparer.Ordinal).SequenceEqual(logged), "Logged reads now:\n" + string.Join("\n", logged));
    }

    /// <summary>
    /// Not here on purpose: the confirmation PDF (number and checksum, no
    /// answers), the list exports (no personal data) and the caller's own
    /// entity card (docs/architektura.md, T-47a).
    ///
    /// The six write routes are here because they answer with the same
    /// payload as the read beside them (S-36): drawing up a contract that
    /// already exists hands back its values without changing a row, so it
    /// was a way to read a PESEL and a bank account and leave no entry.
    /// </summary>
    private static readonly string[] Reviewed =
    [
        "GET /applications/{applicationId:guid}/attachments application",
        "GET /applications/{applicationId:guid}/contract application-contract",
        "GET /applications/{id:guid} application",
        "GET /applications/{id:guid}/pdf application",
        "GET /applications/{id:guid}/versions/{version:int} application",
        "GET /attachments/{id:guid} attachment",
        "GET /competitions/{competitionId:guid}/applications/{id:guid} application",
        "GET /contracts/{contractId:guid}/pdf contract",
        "GET /reports/{reportId:guid} report",
        "POST /applications/{applicationId:guid}/contract application-contract",
        "POST /contracts/{contractId:guid}/sign contract",
        "POST /reports/{reportId:guid}/accept report",
        "POST /reports/{reportId:guid}/return report",
        "PUT /contracts/{contractId:guid}/values contract",
        "PUT /reports/{reportId:guid}/cost-review report",
    ];

    [Fact]
    public void A_route_value_the_route_does_not_have_stops_the_application_at_startup()
    {
        var app = Microsoft.AspNetCore.Builder.WebApplication.CreateSlimBuilder().Build();
        app.MapGet("/x/{id:guid}", () => "dane").LogsPersonalDataRead("x", "identyfikator");

        var source = ((IEndpointRouteBuilder)app).DataSources.Single();

        var refusal = Assert.Throws<InvalidOperationException>(() => source.Endpoints);
        Assert.Contains("identyfikator", refusal.Message);
    }

    [RequiresDatabaseFact]
    public async Task A_read_whose_log_cannot_be_written_fails_instead_of_answering()
    {
        var (host, clock) = CompetitionTestHost.Create(factory, database);
        var competition = await PublishedCompetitionWithFormAsync(host);
        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (applicant, _, _) = await SeedApplicantAsync(host, database);
        var draft = await CreateAsync(applicant, competition.Id);

        await using var context = database.CreateContext();
        // The table out of reach for this one request; the collection runs one test at a time.
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE personal_data_reads RENAME TO personal_data_reads_away");
        HttpResponseMessage response;
        try
        {
            response = await applicant.GetAsync($"/applications/{draft.Id}");
        }
        finally
        {
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE personal_data_reads_away RENAME TO personal_data_reads");
        }

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("opis", await response.Content.ReadAsStringAsync());
    }
}
