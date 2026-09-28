using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Ocwip.Api.Services.Jobs;
using Xunit;

namespace Ocwip.Api.Tests.Services.Jobs;

/// <summary>The scheduler of T-105 on its own: off by a setting, and on it runs every job at once.</summary>
public sealed class BackgroundJobSchedulerTests
{
    private sealed class CountingJob : IBackgroundJob
    {
        public int Runs;

        public string Name => "counting";

        public Task<int> RunAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Runs);
            return Task.FromResult(0);
        }
    }

    private static (BackgroundJobScheduler Scheduler, CountingJob Job) Build(string enabled)
    {
        var job = new CountingJob();
        var services = new ServiceCollection().AddSingleton<IBackgroundJob>(job).BuildServiceProvider();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BackgroundJobs:Enabled"] = enabled,
                ["BackgroundJobs:IntervalSeconds"] = "3600",
            })
            .Build();

        return (new BackgroundJobScheduler(
            services.GetRequiredService<IServiceScopeFactory>(), configuration, NullLogger<BackgroundJobScheduler>.Instance), job);
    }

    [Fact]
    public async Task Turned_off_it_runs_nothing()
    {
        var (scheduler, job) = Build("false");

        await scheduler.StartAsync(CancellationToken.None);
        await scheduler.ExecuteTask!;

        Assert.Equal(0, job.Runs);
    }

    [Fact]
    public async Task Turned_on_it_runs_every_job_at_the_first_tick()
    {
        var (scheduler, job) = Build("true");

        await scheduler.StartAsync(CancellationToken.None);
        for (var i = 0; i < 50 && Volatile.Read(ref job.Runs) == 0; i++)
        {
            await Task.Delay(20);
        }

        await scheduler.StopAsync(CancellationToken.None);

        Assert.Equal(1, job.Runs);
    }

    [Fact]
    public void The_test_host_starts_with_the_scheduler_off()
    {
        using var factory = new OcwipWebApplicationFactory();

        Assert.False(factory.Services.GetRequiredService<IConfiguration>().GetValue("BackgroundJobs:Enabled", true));
    }
}
