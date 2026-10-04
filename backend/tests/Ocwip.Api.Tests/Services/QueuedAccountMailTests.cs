using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Services;
using Xunit;

namespace Ocwip.Api.Tests.Services;

/// <summary>
/// The queue behind the verification and reset mails: handing a mail over
/// must cost the same whatever the relay is doing, because the time a public
/// account endpoint takes may not tell a known address from an unknown one.
/// </summary>
public sealed class QueuedAccountMailTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private static EmailMessage Mail(string to) => new(to, "Resetowanie hasła", "Treść");

    private static async Task<QueuedAccountMail> StartAsync(IEmailSender sender)
    {
        var provider = new ServiceCollection()
            .AddSingleton(sender)
            .BuildServiceProvider();
        var queue = new QueuedAccountMail(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<QueuedAccountMail>.Instance)
        {
            RetryDelay = TimeSpan.FromMilliseconds(1),
        };

        await queue.StartAsync(CancellationToken.None);
        return queue;
    }

    [Fact]
    public async Task Handing_a_mail_over_does_not_wait_for_the_relay()
    {
        var release = new TaskCompletionSource();
        var sender = new ScriptedSender(_ => release.Task);
        var queue = await StartAsync(sender);

        queue.Enqueue(Mail("anna@example.org"));

        // The relay has not answered, and the caller is already back.
        Assert.Empty(sender.Delivered);

        release.SetResult();
        await sender.WaitForDeliveredAsync(1);
        Assert.Equal("anna@example.org", Assert.Single(sender.Delivered).To);

        await queue.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task A_refused_mail_is_tried_again_until_the_relay_takes_it()
    {
        var sender = new ScriptedSender(attempt => attempt < 3
            ? Task.FromException(new System.Net.Mail.SmtpException("The relay refused the mail."))
            : Task.CompletedTask);
        var queue = await StartAsync(sender);

        queue.Enqueue(Mail("anna@example.org"));

        await sender.WaitForDeliveredAsync(1);
        Assert.Equal(3, sender.Attempts);

        await queue.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task A_mail_the_relay_never_takes_is_given_up_and_the_next_one_still_goes_out()
    {
        var sender = new ScriptedSender(
            _ => Task.CompletedTask,
            refuses: message => message.To == "refused@example.org");
        var queue = await StartAsync(sender);

        queue.Enqueue(Mail("refused@example.org"));
        queue.Enqueue(Mail("bartek@example.org"));

        await sender.WaitForDeliveredAsync(1);
        Assert.Equal("bartek@example.org", Assert.Single(sender.Delivered).To);
        Assert.Equal(QueuedAccountMail.Attempts + 1, sender.Attempts);

        await queue.StopAsync(CancellationToken.None);
    }

    /// <summary>
    /// Records a mail only once the scripted answer for its attempt has
    /// completed without failing, like a relay that took it.
    /// </summary>
    private sealed class ScriptedSender(Func<int, Task> answer, Func<EmailMessage, bool>? refuses = null) : IEmailSender
    {
        private readonly ConcurrentQueue<EmailMessage> _delivered = new();
        private int _attempts;

        public IReadOnlyCollection<EmailMessage> Delivered => _delivered.ToArray();

        public int Attempts => Volatile.Read(ref _attempts);

        public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            if (refuses?.Invoke(message) == true)
            {
                Interlocked.Increment(ref _attempts);
                throw new System.Net.Mail.SmtpException("The relay refused the mail.");
            }

            await answer(Interlocked.Increment(ref _attempts));
            _delivered.Enqueue(message);
        }

        public async Task WaitForDeliveredAsync(int count)
        {
            var deadline = DateTime.UtcNow + Patience;
            while (_delivered.Count < count)
            {
                Assert.True(DateTime.UtcNow < deadline, "The queue did not deliver the mail in time.");
                await Task.Delay(5);
            }
        }
    }
}
