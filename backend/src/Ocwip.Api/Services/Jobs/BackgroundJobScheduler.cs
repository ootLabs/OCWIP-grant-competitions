namespace Ocwip.Api.Services.Jobs;

/// <summary>
/// The one BackgroundService (T-105): every BackgroundJobs:IntervalSeconds
/// it runs each registered IBackgroundJob in a scope of its own. The jobs
/// are idempotent (JobRuns), so a tick that repeats the previous one does
/// nothing, and a tick that failed is simply the next tick's work.
///
/// Assumes a single API instance, like the resend cooldown of verification
/// mail (docs/architektura.md, "Jedna instancja API"). Two instances would
/// not send twice, because the claim is a conditional UPDATE, but they
/// would both look.
///
/// Off with BackgroundJobs:Enabled=false, which the tests set so that no
/// job runs behind a test's back; they call a job directly instead.
/// </summary>
internal sealed class BackgroundJobScheduler(
    IServiceScopeFactory scopes, IConfiguration configuration, ILogger<BackgroundJobScheduler> logger) : BackgroundService
{
    public const string Section = "BackgroundJobs";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue($"{Section}:Enabled", true))
        {
            logger.LogInformation("Background jobs are off (BackgroundJobs:Enabled=false).");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(5, configuration.GetValue($"{Section}:IntervalSeconds", 60)));
        using var timer = new PeriodicTimer(interval);

        do
        {
            await TickAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task TickAsync(CancellationToken stoppingToken)
    {
        await using var scope = scopes.CreateAsyncScope();

        foreach (var job in scope.ServiceProvider.GetServices<IBackgroundJob>())
        {
            try
            {
                var done = await job.RunAsync(stoppingToken);
                if (done > 0)
                {
                    logger.LogInformation("Background job {Job} did {Count} runs.", job.Name, done);
                }
            }
            // Only the host's own stopping ends the loop: any other
            // cancellation (an HTTP or SMTP timeout inside a job) is a failure
            // of that job like any other, not a reason to stop the API.
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                // The next tick tries again; one broken job does not stop the others.
                logger.LogError(exception, "Background job {Job} failed.", job.Name);
            }
        }
    }
}
