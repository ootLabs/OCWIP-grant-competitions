using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;
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
    IAccountMailQueue mailQueue,
    IConfiguration configuration,
    IMemoryCache cache,
    ILogger<PasswordResetService> logger)
    : IPasswordResetService
{
    // The same growing cooldown /resend-verification has had since T-12.3
    // (EmailVerificationService), now on this route too (S-04). Without it
    // one caller turns a forgotten password into a mail flood at a chosen
    // address: the request limiter counts per client address, not per
    // mailbox, so the mailbox owner is the one who pays.
    private const int DefaultCooldownSeconds = 60;
    private const int DefaultCooldownMaxSeconds = 1800;
    private const double DefaultCooldownMultiplier = 5.0;
    private const int DefaultBackoffResetSeconds = 86400;

    public async Task RequestResetAsync(
        string? email,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Same one place as every other anonymous account route asks (S-31):
        // FindByEmailAsync refuses null with an argument exception.
        if (!AccountInput.IsAddress(email))
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

        // Still cooling down: nothing is sent, and the caller cannot tell,
        // because this route answers the same either way (rule 3).
        if (cache.TryGetValue(CooldownKey(user.Id), out _))
        {
            return;
        }

        cache.Set(CooldownKey(user.Id), true, CooldownFor(IncrementAttemptCount(user.Id)));

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

            Link jest ważny przez {PolishPlural.Hours(TokenLifetimeHours())}.

            Jeśli nie prosiłeś o reset hasła, zignoruj tę wiadomość - Twoje
            obecne hasło nadal działa.
            """);

        // Queued, not awaited: the relay's round trip would make a known
        // address answer slower than an unknown one, which is the one thing
        // the identical 200 is there to hide.
        mailQueue.Enqueue(message);

        logger.LogInformation(
            "Password reset email queued for user {UserId}",
            user.Id);
    }

    public async Task<PasswordResetResult> ResetAsync(
        string? userId,
        string? encodedToken,
        string? newPassword,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // A malformed link is exactly the kind of input a reset link
        // parameter has to survive; AccountInput says what that means and
        // every anonymous account route asks it the same way (S-31).
        if (!AccountInput.IsAccountId(userId)
            || !AccountInput.IsToken(encodedToken)
            || string.IsNullOrEmpty(newPassword))
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

    private TimeSpan CooldownFor(int attemptNumber)
    {
        var multiplier = configuration.GetValue<double?>("PasswordReset:CooldownMultiplier");

        return ResendBackoffCalculator.CooldownFor(
            attemptNumber,
            Seconds("PasswordReset:CooldownSeconds", DefaultCooldownSeconds),
            Seconds("PasswordReset:CooldownMaxSeconds", DefaultCooldownMaxSeconds),
            multiplier is > 1 ? multiplier.Value : DefaultCooldownMultiplier);
    }

    /// <summary>
    /// How many sends in a row this account has had, so the backoff keeps
    /// growing across separate requests instead of starting over every time
    /// the cooldown key expires. Refreshed on every send, so the reset
    /// window measures time since the last one.
    /// </summary>
    private int IncrementAttemptCount(Guid userId)
    {
        var key = AttemptKey(userId);
        var count = (cache.TryGetValue<int>(key, out var existing) ? existing : 0) + 1;
        cache.Set(key, count, Seconds("PasswordReset:CooldownResetSeconds", DefaultBackoffResetSeconds));

        return count;
    }

    private TimeSpan Seconds(string key, int fallback)
    {
        var value = configuration.GetValue<int?>(key);

        return TimeSpan.FromSeconds(value is > 0 ? value.Value : fallback);
    }

    // Keyed by account, not by the address typed in: an unknown address
    // sends nothing anyway, so the cache never holds one.
    private static string CooldownKey(Guid userId) => $"password-reset:{userId}";

    private static string AttemptKey(Guid userId) => $"password-reset-attempts:{userId}";
}
