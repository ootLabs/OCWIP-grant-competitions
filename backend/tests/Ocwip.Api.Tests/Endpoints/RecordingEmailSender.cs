using System.Collections.Concurrent;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// Stands in for the real (log-only) EmailSenderService in tests, so a test can
/// assert on what would have been sent without a real mail transport.
///
/// Registered as a singleton per test host (see EmailVerificationEndpointsTests),
/// so it outlives the per-request scope the app resolves IEmailSender from.
/// Concurrent because ASP.NET Core can process requests against the same host
/// in parallel.
/// </summary>
internal sealed class RecordingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    public IReadOnlyCollection<EmailMessage> Sent => _sent.ToArray();

    /// <summary>While true, every mail is refused the way a relay refuses it, and nothing is recorded.</summary>
    public bool Refuse { get; set; }

    public Task SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default)
    {
        if (Refuse)
        {
            throw new System.Net.Mail.SmtpException("The relay refused the mail.");
        }

        _sent.Enqueue(message);
        return Task.CompletedTask;
    }
}
