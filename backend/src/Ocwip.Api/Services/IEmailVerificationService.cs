using Ocwip.Api.Models;

namespace Ocwip.Api.Services
{
    public interface IEmailVerificationService
    {
        Task SendVerificationAsync(
            User user,
            CancellationToken cancellationToken = default);

        Task<bool> VerifyAsync(
            string userId,
            string encodedToken,
            CancellationToken cancellationToken = default);

        Task<bool> ResendVerificationAsync(
            string email,
            CancellationToken cancellationToken = default);
    }
}
