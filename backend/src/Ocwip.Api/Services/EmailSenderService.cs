using Ocwip.Api.Contracts;
using Ocwip.Api.Models;

namespace Ocwip.Api.Services
{
    /// <summary>
    /// The stand in used while no SMTP relay is configured (T-43a): mail is
    /// logged, not sent. In Development the whole mail goes to the log, so a
    /// developer can follow a verification link. Anywhere else only the
    /// subject, with a warning: a body carries links that sign somebody in
    /// and personal data, and a log is the wrong place for either
    /// (AGENTS.md, security, point 4).
    /// </summary>
    public sealed class EmailSenderService : IEmailSender
    {
        private readonly ILogger<EmailSenderService> _logger;
        private readonly IHostEnvironment _environment;

        public EmailSenderService(ILogger<EmailSenderService> logger, IHostEnvironment environment)
        {
            _logger = logger;
            _environment = environment;
        }

        public Task SendAsync(
            EmailMessage message,
            CancellationToken cancellationToken = default)
        {
            if (!_environment.IsDevelopment())
            {
                _logger.LogWarning(
                    "E-mail not sent, no SMTP relay is configured (SMTP__HOST). Subject: {Subject}",
                    message.Subject);

                return Task.CompletedTask;
            }

            _logger.LogInformation(
                """
            ===== DEV EMAIL =====
            To: {To}
            Subject: {Subject}

            {Body}
            =====================
            """,
                message.To,
                message.Subject,
                message.Body);

            return Task.CompletedTask;
        }
    }
}
