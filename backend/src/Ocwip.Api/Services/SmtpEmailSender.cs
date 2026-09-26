using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Options;
using Ocwip.Api.Configuration;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;

namespace Ocwip.Api.Services;

/// <summary>
/// Mail through an SMTP relay (T-43a, R-18). System.Net.Mail rather than a
/// library: plain text to one recipient over STARTTLS is all the system
/// sends, and that is what the framework's client does without a dependency.
///
/// A failure is thrown to the caller, never swallowed here: the result mails
/// (T-43) record it as a failed attempt and send again, and the account mails
/// answer the request with an error rather than pretending a link went out.
/// </summary>
internal sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var smtp = options.Value;

        using var mail = Compose(message, smtp);
        using var client = new SmtpClient(smtp.Host, smtp.Port)
        {
            EnableSsl = smtp.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Credentials = string.IsNullOrEmpty(smtp.User)
                ? null
                : new NetworkCredential(smtp.User, smtp.Password),
        };

        await client.SendMailAsync(mail, cancellationToken);
    }

    internal static MailMessage Compose(EmailMessage message, SmtpOptions smtp) =>
        new(new MailAddress(smtp.From!, smtp.FromName), new MailAddress(message.To))
        {
            Subject = message.Subject,
            SubjectEncoding = Encoding.UTF8,
            Body = message.Body,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = false,
        };
}
