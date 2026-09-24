using Ocwip.Api.Models;

namespace Ocwip.Api.Services
{
    public interface IEmailVerificationService
    {
        /// <summary>
        /// Sends the link. returnUrl goes into it only when it passes
        /// LoginLandingPath.SafeOrNull; anything else is left out, not repaired.
        /// </summary>
        Task SendVerificationAsync(
            User user,
            string? returnUrl = null,
            CancellationToken cancellationToken = default);

        Task<bool> VerifyAsync(
            string userId,
            string encodedToken,
            CancellationToken cancellationToken = default);

        Task<bool> ResendVerificationAsync(
            string email,
            string? returnUrl = null,
            CancellationToken cancellationToken = default);
    }
}
