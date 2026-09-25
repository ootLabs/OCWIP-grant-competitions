using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Ocwip.Api.Tests.Endpoints;
using Ocwip.Api.Tests.Models.Forms;
using Xunit;
using static Ocwip.Api.Tests.Endpoints.ApplicationTestHost;

namespace Ocwip.Api.Tests.Authorization;

/// <summary>
/// T-37, on the real path: assigning a reviewer, revoking that assignment,
/// and the visibility rule this whole card exists for, all through the
/// product's own endpoints rather than the T-13.2 test probes.
///
/// AuthorizationLayerTests.A_reviewer_is_refused_until_assignment_exists
/// still covers the resource the probe uses, which is not an Application, so
/// that suite stays untouched: EntityScopedHandler only ever grants a
/// reviewer access through Models.ApplicationAssignment, and this file is
/// where that grant is exercised for real.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ReviewerAssignmentTests : IClassFixture<OcwipWebApplicationFactory>
{
    private readonly OcwipWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public ReviewerAssignmentTests(
        OcwipWebApplicationFactory factory,
        PostgresDatabaseFixture database)
    {
        _factory = factory;
        _database = database;
    }

    [RequiresDatabaseFact]
    public async Task An_unassigned_reviewer_is_refused_and_an_assigned_one_is_let_in()
    {
        var scene = await SceneAsync();

        var before = await scene.Reviewer.GetAsync($"/applications/{scene.ApplicationOne.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, before.StatusCode);

        var assign = await AssignAsync(
            scene.Operator, scene.ApplicationOne.Id, scene.ReviewerId);
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);

        var after = await scene.Reviewer.GetAsync($"/applications/{scene.ApplicationOne.Id}");
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Being_assigned_in_one_competition_grants_nothing_in_another()
    {
        var scene = await SceneAsync();

        await AssignAsync(scene.Operator, scene.ApplicationOne.Id, scene.ReviewerId);

        // ApplicationOne and ApplicationTwo sit in two different
        // competitions on purpose (see SceneAsync): assignment is per
        // application, not per competition, and this is the proof.
        var ownCompetition = await scene.Reviewer.GetAsync(
            $"/applications/{scene.ApplicationOne.Id}");
        var otherCompetition = await scene.Reviewer.GetAsync(
            $"/applications/{scene.ApplicationTwo.Id}");

        Assert.Equal(HttpStatusCode.OK, ownCompetition.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, otherCompetition.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Assignment_is_many_to_many()
    {
        var scene = await SceneAsync();
        var (secondReviewer, secondReviewerId) = await SeedReviewerAsync(scene.Host);

        // One application, two reviewers.
        await AssignAsync(scene.Operator, scene.ApplicationOne.Id, scene.ReviewerId);
        await AssignAsync(scene.Operator, scene.ApplicationOne.Id, secondReviewerId);

        // One reviewer, two applications (across the two competitions).
        await AssignAsync(scene.Operator, scene.ApplicationTwo.Id, scene.ReviewerId);

        var firstSeesOne = await scene.Reviewer.GetAsync(
            $"/applications/{scene.ApplicationOne.Id}");
        var firstSeesTwo = await scene.Reviewer.GetAsync(
            $"/applications/{scene.ApplicationTwo.Id}");
        var secondSeesOne = await secondReviewer.GetAsync(
            $"/applications/{scene.ApplicationOne.Id}");
        var secondSeesTwo = await secondReviewer.GetAsync(
            $"/applications/{scene.ApplicationTwo.Id}");

        Assert.Equal(HttpStatusCode.OK, firstSeesOne.StatusCode);
        Assert.Equal(HttpStatusCode.OK, firstSeesTwo.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondSeesOne.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, secondSeesTwo.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Revoking_an_assignment_deactivates_it_instead_of_deleting_it_and_refuses_further_access()
    {
        var scene = await SceneAsync();

        var assign = await AssignAsync(
            scene.Operator, scene.ApplicationOne.Id, scene.ReviewerId);
        var assigned = (await assign.Content.ReadFromJsonAsync<ApplicationAssignmentResponse>())!;

        var unassign = await scene.Operator.DeleteAsync(
            $"/applications/{scene.ApplicationOne.Id}/assignments/{scene.ReviewerId}");
        Assert.Equal(HttpStatusCode.OK, unassign.StatusCode);

        var afterRevoke = await scene.Reviewer.GetAsync(
            $"/applications/{scene.ApplicationOne.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, afterRevoke.StatusCode);

        await using var context = _database.CreateContext();
        var stored = await context.ApplicationAssignments.AsNoTracking()
            .SingleAsync(x => x.Id == assigned.Id);

        Assert.False(stored.IsActive);
        Assert.NotNull(stored.DeactivatedAt);
    }

    [RequiresDatabaseFact]
    public async Task Only_an_operator_may_assign_or_revoke()
    {
        var scene = await SceneAsync();

        var applicantAttempt = await AssignAsync(
            scene.Applicant, scene.ApplicationOne.Id, scene.ReviewerId);
        var reviewerAttempt = await AssignAsync(
            scene.Reviewer, scene.ApplicationOne.Id, scene.ReviewerId);
        var anonymousAttempt = await AssignAsync(
            scene.Host.CreateClient(), scene.ApplicationOne.Id, scene.ReviewerId);

        Assert.Equal(HttpStatusCode.Forbidden, applicantAttempt.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, reviewerAttempt.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousAttempt.StatusCode);

        // Nothing above created a row the operator's own assignment could
        // have relied on, so the reviewer is still refused.
        var stillRefused = await scene.Reviewer.GetAsync(
            $"/applications/{scene.ApplicationOne.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, stillRefused.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Assigning_an_account_that_is_not_an_active_reviewer_is_refused()
    {
        var scene = await SceneAsync();

        // An applicant's own account id is not a reviewer at all.
        await using (var context = _database.CreateContext())
        {
            var applicantUser = await context.Users
                .SingleAsync(x => x.EntityId == scene.ApplicantEntityId);

            var response = await AssignAsync(
                scene.Operator, scene.ApplicationOne.Id, applicantUser.Id);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        // An id that belongs to nobody.
        var unknown = await AssignAsync(
            scene.Operator, scene.ApplicationOne.Id, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    private static Task<HttpResponseMessage> AssignAsync(
        HttpClient client, Guid applicationId, Guid reviewerId) =>
        client.PostAsJsonAsync(
            $"/applications/{applicationId}/assignments",
            new AssignReviewerRequest(reviewerId));

    private static Task<HttpResponseMessage> SubmitAsync(HttpClient client, Guid id) =>
        client.PostAsync($"/applications/{id}/submit", content: null);

    /// <summary>
    /// Two competitions, one real submitted application in each, an
    /// operator, an applicant and one reviewer, all built through the same
    /// endpoints their own clients call, the same tradeoff
    /// ApplicantDataIsolationTests makes and for the same reason: fresh per
    /// test, so one test's assignments never leak into another's.
    /// </summary>
    private async Task<Scene> SceneAsync()
    {
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);

        var competitionOne = await PublishedCompetitionWithFormAsync(host);
        var competitionTwo = await PublishedCompetitionWithFormAsync(host);

        clock.Now = CompetitionTestHost.Start.AddDays(1);

        var (applicant, applicantEntityId, _) = await SeedApplicantAsync(host, _database);

        var draftOne = await CreateAsync(applicant, competitionOne.Id);
        await SaveAsync(applicant, draftOne.Id, FormDefinitionSamples.Parse("""{"opis":"jeden"}"""));
        var submitOne = await SubmitAsync(applicant, draftOne.Id);
        submitOne.EnsureSuccessStatusCode();
        var applicationOne = (await submitOne.Content.ReadFromJsonAsync<ApplicationResponse>())!;

        var draftTwo = await CreateAsync(applicant, competitionTwo.Id);
        await SaveAsync(applicant, draftTwo.Id, FormDefinitionSamples.Parse("""{"opis":"dwa"}"""));
        var submitTwo = await SubmitAsync(applicant, draftTwo.Id);
        submitTwo.EnsureSuccessStatusCode();
        var applicationTwo = (await submitTwo.Content.ReadFromJsonAsync<ApplicationResponse>())!;

        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        var (reviewer, reviewerId) = await SeedReviewerAsync(host);

        return new Scene(
            host,
            operatorClient,
            applicant,
            applicantEntityId,
            reviewer,
            reviewerId,
            applicationOne,
            applicationTwo);
    }

    private sealed record Scene(
        WebApplicationFactory<Program> Host,
        HttpClient Operator,
        HttpClient Applicant,
        Guid ApplicantEntityId,
        HttpClient Reviewer,
        Guid ReviewerId,
        ApplicationResponse ApplicationOne,
        ApplicationResponse ApplicationTwo);
}
