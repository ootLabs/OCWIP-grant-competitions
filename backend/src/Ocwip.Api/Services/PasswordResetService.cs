using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Ocwip.Api.Configuration;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using System.Text;

namespace Ocwip.Api.Services;

/// <summary>
/// Password recovery (T-12.4): the "forgot my password" mail and the link it
/// sends. Separate from AccountService and SessionService: this path neither
/// creates an account nor signs anyone in, it only replaces a password
/// someone can no longer supply.
/// </summary>
internal sealed class PasswordResetService(
    UserManager<User> userManager,
    IEmailSender emailSender,
    IConfiguration configuration,
    ILogger<PasswordResetService> logger)
    : IPasswordResetService
{
    public async Task RequestResetAsync(
        string? email,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var user = await userManager.FindByEmailAsync(email.Trim());

        // Same shape as ResendVerificationAsync: an unknown address does
        // nothing, and the caller cannot tell that apart from a sent mail,
        // because the endpoint answers 200 either way.
        if (user is null)
        {
            return;
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);

        var encodedToken = WebEncoders.Base64UrlEncode(
            Encoding.UTF8.GetBytes(token));

        var link =
            $"{FrontendBaseUrl()}/reset-password" +
            $"?userId={user.Id}" +
            $"&token={Uri.EscapeDataString(encodedToken)}";

        var message = new EmailMessage(
            user.Email!,
            "Resetowanie hasła",
            $"""
            Otrzymaliśmy prośbę o zresetowanie hasła do Twojego konta.

            Aby ustawić nowe hasło, kliknij poniższy link:

            {link}

            Link jest ważny przez {TokenLifetimeHours()} godzin.

            Jeśli nie prosiłeś o reset hasła, zignoruj tę wiadomość - Twoje
            obecne hasło nadal działa.
            """);

        await emailSender.SendAsync(message, cancellationToken);

        logger.LogInformation(
            "Password reset email sent for user {UserId}",
            user.Id);
    }

    public async Task<PasswordResetResult> ResetAsync(
        string? userId,
        string? encodedToken,
        string? newPassword,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(userId)
            || string.IsNullOrWhiteSpace(encodedToken)
            || string.IsNullOrEmpty(newPassword))
        {
            return PasswordResetResult.InvalidToken;
        }

        // FindByIdAsync converts the string straight to a Guid and throws
        // FormatException on anything that is not one, uncaught, all the way
        // out to an unhandled 500 - the same gap EmailVerificationService has
        // for /verify-email, copied here into a second unauthenticated public
        // endpoint. A malformed link is exactly the kind of input a reset
        // link parameter has to survive.
        if (!Guid.TryParse(userId, out _))
        {
            return PasswordResetResult.InvalidToken;
        }

        var user = await userManager.FindByIdAsync(userId);

        if (user is null)
        {
            return PasswordResetResult.InvalidToken;
        }

        string token;

        try
        {
            token = Encoding.UTF8.GetString(
                WebEncoders.Base64UrlDecode(encodedToken));
        }
        catch (FormatException)
        {
            return PasswordResetResult.InvalidToken;
        }

        // ResetPasswordAsync verifies the token first (unknown/expired/
        // already used all fail here with the same InvalidToken code) and
        // only then validates the new password against the same policy
        // registration uses. On success it rotates the SecurityStamp itself
        // (UpdatePasswordHash does, same mechanism SessionService.LogoutAsync
        // uses), and ValidationInterval = Zero means every other cookie for
        // this account stops working on its very next request - see
        // AuthenticationConfiguration.
        var result = await userManager.ResetPasswordAsync(user, token, newPassword);

        if (result.Succeeded)
        {
            // T-12.5 meets T-12.4 here, and without these two lines the two
            // cards cancel each other out. Forgetting a password is the main
            // way somebody reaches the lockout in the first place (five wrong
            // guesses at their own account), and a reset that leaves the lock
            // standing means the one self-service way out of it hands back an
            // account that still answers 429 for the next fifteen minutes,
            // now with a password nobody can tell is correct. It is also what
            // makes a maliciously locked account recoverable by its owner
            // instead of only by waiting.
            await userManager.ResetAccessFailedCountAsync(user);
            await userManager.SetLockoutEndDateAsync(user, null);

            logger.LogInformation(
                "Password reset for user {UserId}",
                user.Id);

            return PasswordResetResult.Succeeded;
        }

        if (result.Errors.Any(IsInvalidToken))
        {
            return PasswordResetResult.InvalidToken;
        }

        return PasswordResetResult.PasswordRejected(
            result.Errors.Select(error => error.Description));
    }

    private static bool IsInvalidToken(IdentityError error) =>
        error.Code == nameof(IdentityErrorDescriber.InvalidToken);

    private string FrontendBaseUrl() =>
        (configuration["EmailVerification:FrontendBaseUrl"] is { Length: > 0 } configured
            ? configured
            : "http://localhost:3000")
            .TrimEnd('/');

    private int TokenLifetimeHours() =>
        configuration.GetValue<int?>("PasswordReset:TokenLifetimeHours")
            ?? IdentityConfiguration.DefaultPasswordResetTokenLifetimeHours;
}
