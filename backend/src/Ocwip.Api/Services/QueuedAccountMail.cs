using System.Threading.Channels;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;

namespace Ocwip.Api.Services;

/// <summary>
/// The in-process queue behind IAccountMailQueue: one background reader that
/// sends each mail through IEmailSender in a scope of its own, so a request
/// never waits for the relay.
///
/// A refused mail is tried again a few times and then given up on, with the
/// subject in the log and never the body, which carries a link that signs
/// somebody in (AGENTS.md, security, point 4). The person asks again from the
/// screen: a new reset request, or a resend once its cooldown has passed.
/// Mail still waiting when the API stops is lost, which the same single
/// instance assumption as the scheduler accepts (BackgroundJobScheduler).
/// </summary>
internal sealed class QueuedAccountMail(
    IServiceScopeFactory scopes, ILogger<QueuedAccountMail> logger) : BackgroundService, IAccountMailQueue
{
    internal const int Capacity = 1000;
    internal const int Attempts = 3;

    private readonly Channel<EmailMessage> _queue = Channel.CreateBounded<EmailMessage>(
        new BoundedChannelOptions(Capacity) { FullMode = BoundedChannelFullMode.Wait });

    /// <summary>The wait before another try of a refused mail, doubled each time.</summary>
    internal TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(10);

    public void Enqueue(EmailMessage message)
    {
        // A full queue means the relay has been refusing for a long time. The
        // mail is dropped rather than the request held, because holding it is
        // the very difference this queue exists to remove.
        if (!_queue.Writer.TryWrite(message))
        {
            logger.LogError("Account mail queue is full, mail dropped. Subject: {Subject}", message.Subject);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var message in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                await DeliverAsync(message, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task DeliverAsync(EmailMessage message, CancellationToken stoppingToken)
    {
        var delay = RetryDelay;

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IEmailSender>().SendAsync(message, stoppingToken);
                return;
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                if (attempt >= Attempts)
                {
                    logger.LogError(
                        exception, "Account mail not delivered after {Attempts} attempts. Subject: {Subject}",
                        Attempts, message.Subject);
                    return;
                }

                logger.LogWarning(
                    exception, "Account mail refused (attempt {Attempt}). Subject: {Subject}",
                    attempt, message.Subject);
                await Task.Delay(delay, stoppingToken);
                delay *= 2;
            }
        }
    }
}
