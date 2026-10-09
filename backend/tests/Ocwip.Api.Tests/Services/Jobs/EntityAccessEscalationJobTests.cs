using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Services.Jobs;
using Ocwip.Api.Tests.Data;
using Ocwip.Api.Tests.Endpoints;
using Xunit;
using static Ocwip.Api.Tests.Endpoints.ApplicationTestHost;
using static Ocwip.Api.Tests.Endpoints.EntityCardEndpointsTests;

namespace Ocwip.Api.Tests.Services.Jobs;

/// <summary>
/// T-93a, report step 2.2: a request to join a card that nobody answered for
/// seven days reaches every operator, once, and not a day earlier.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class EntityAccessEscalationJobTests(OcwipWebApplicationFactory factory, PostgresDatabaseFixture database)
    : IClassFixture<OcwipWebApplicationFactory>
{
    private static async Task RunAsync(WebApplicationFactory<Program> host)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var job = scope.ServiceProvider.GetServices<IBackgroundJob>().OfType<EntityAccessEscalationJob>().Single();
        await job.RunAsync(CancellationToken.None);
    }

    [RequiresDatabaseFact]
    public async Task A_request_unanswered_for_seven_days_reaches_each_operator_once()
    {
        var emails = new RecordingEmailSender();
        var (host, clock) = CompetitionTestHost.Create(factory, database, s => s.AddSingleton<IEmailSender>(emails));
        clock.Now = DateTimeOffset.UtcNow;

        var founderEmail = SessionTestHost.Email("zalozycielka");
        await SessionTestHost.CreateAccountAsync(host, founderEmail);
        var founder = await LoginAsync(host, founderEmail);
        var nip = TestEntity.NewNip();
        var card = await FoundAsync(founder, OrganisationCard(nip: nip));

        var requesterEmail = SessionTestHost.Email("proszacy");
        await SessionTestHost.CreateAccountAsync(host, requesterEmail);
        var requester = await LoginAsync(host, requesterEmail);
        var request = (await (await requester.PostAsJsonAsync("/me/access-requests", new EntityAccessRequestBody(nip)))
            .EnsureSuccessStatusCode().Content.ReadFromJsonAsync<MyEntityAccessRequest>())!;

        var operatorEmail = SessionTestHost.Email("operator-eskalacji");
        var operatorAccount = await SessionTestHost.CreateAccountAsync(host, operatorEmail, Role.Operator);
        var subject = JobRuns.SubjectFor(request.Id, operatorAccount.Id);

        await using var context = database.CreateContext();

        // Six days and change: nothing for this request.
        clock.Now = DateTimeOffset.UtcNow + EntityAccessRequest.EscalationAge - TimeSpan.FromHours(1);
        await RunAsync(host);
        Assert.False(await context.ScheduledJobRuns.AnyAsync(x => x.Job == EntityAccessEscalationJob.JobName && x.SubjectId == subject));

        clock.Now = DateTimeOffset.UtcNow + EntityAccessRequest.EscalationAge + TimeSpan.FromMinutes(1);
        await RunAsync(host);
        // At least this request's mail: a request some earlier run left
        // unanswered in the shared database escalates to this operator too.
        var run = Assert.Single(
            await context.ScheduledJobRuns.Where(x => x.Job == EntityAccessEscalationJob.JobName && x.SubjectId == subject).ToListAsync());
        Assert.NotNull(run.CompletedAt);
        var sent = emails.Sent.Where(x => x.To == operatorEmail).ToList();
        Assert.NotEmpty(sent);
        Assert.All(sent, x => Assert.Contains("/panel/operator/access-requests", x.Body));

        // Nothing in the mail says who or which organisation: that is
        // behind the operator's login.
        Assert.All(sent, x => Assert.DoesNotContain(requesterEmail, x.Body));

        // However often the scheduler looks afterwards: once.
        await RunAsync(host);
        Assert.Equal(sent.Count, emails.Sent.Count(x => x.To == operatorEmail));

        // Answered, so the next run of this suite does not escalate it again.
        // A new sign in: the week moved past the session's lifetime.
        founder = await LoginAsync(host, founderEmail);
        (await founder.PostAsJsonAsync(
            $"/me/entities/{card.Id}/access-requests/{request.Id}/decision",
            new EntityAccessDecisionBody(true))).EnsureSuccessStatusCode();
    }
}
