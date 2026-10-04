using Ocwip.Api.Models;

namespace Ocwip.Api.Services;

/// <summary>
/// Where the mails of the public account endpoints (verification, password
/// reset) go instead of straight to the relay. Handing a mail over costs the
/// same whether or not it is ever sent, so the time a request takes does not
/// depend on whether an account exists for the address (AGENTS.md, security,
/// point 3). Awaiting the relay in the request told the two apart by the
/// latency of one SMTP round trip.
/// </summary>
public interface IAccountMailQueue
{
    /// <summary>Hands the mail over for delivery and returns at once.</summary>
    void Enqueue(EmailMessage message);
}
