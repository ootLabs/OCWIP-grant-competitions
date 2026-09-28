using System.Net;
using Microsoft.AspNetCore.Http;
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

    [Fact]
    public void Only_a_success_counts_as_a_read()
    {
        Assert.True(PersonalDataReadFilter.Succeeded(TypedResults.Ok("dane")));
        Assert.True(PersonalDataReadFilter.Succeeded(TypedResults.File([1], "application/pdf")));
        Assert.False(PersonalDataReadFilter.Succeeded(TypedResults.Problem("nie", statusCode: 403)));
        Assert.False(PersonalDataReadFilter.Succeeded(TypedResults.NotFound()));
        Assert.False(PersonalDataReadFilter.Succeeded(null));
    }
}
