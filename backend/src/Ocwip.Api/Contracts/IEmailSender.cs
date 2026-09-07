using Ocwip.Api.Models;

namespace Ocwip.Api.Contracts
{
    public interface IEmailSender
    {
        Task SendAsync(
            EmailMessage message,
            CancellationToken cancellationToken = default);
    }

}
