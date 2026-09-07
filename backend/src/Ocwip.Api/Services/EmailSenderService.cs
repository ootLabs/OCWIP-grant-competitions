
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;

namespace Ocwip.Api.Services
{
    public sealed class EmailSenderService : IEmailSender
    {
        private readonly ILogger<EmailSenderService> _logger;

        public EmailSenderService(ILogger<EmailSenderService> logger)
        {
            _logger = logger;
        }

        public Task SendAsync(
            EmailMessage message,
            CancellationToken cancellationToken = default)
        {
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
