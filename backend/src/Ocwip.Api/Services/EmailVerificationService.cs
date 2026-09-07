using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;
using Ocwip.Api.Configuration;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using System.Text;

namespace Ocwip.Api.Services
{
    public sealed class EmailVerificationService : IEmailVerificationService
    {
        // Falls back to the frontend's own local dev origin (matches the
        // Cors:Origins default in appsettings.json) so a missing config value
        // fails the same way in dev as it will everywhere else, instead of
        // pointing at the API itself.
        private const string DefaultFrontendBaseUrl = "http://localhost:3000";

        // A flood guard, not just a double-click guard: each consecutive
        // send (registration counts as the first) makes the next cooldown
        // longer, up to a cap, via ResendBackoffCalculator. It lives in this
        // instance's memory only, so it does not hold across app restarts or
        // multiple instances. If the API is ever scaled out, this needs to
        // move to a shared store (e.g. the database or a distributed cache)
        // to still be effective.
        private const int DefaultResendCooldownSeconds = 60;
        private const int DefaultResendCooldownMaxSeconds = 1800; // 30 minutes
        private const double DefaultResendCooldownMultiplier = 5.0;

        // How long a quiet period has to be before the backoff level resets
        // back to the base cooldown, so a one-off flood a week ago doesn't
        // still slow someone down today.
        private const int DefaultResendBackoffResetSeconds = 86400; // 24 hours

        private static readonly TimeSpan DefaultResendCooldown =
            TimeSpan.FromSeconds(DefaultResendCooldownSeconds);

        private static readonly TimeSpan DefaultResendCooldownMax =
            TimeSpan.FromSeconds(DefaultResendCooldownMaxSeconds);

        private static readonly TimeSpan DefaultResendBackoffResetWindow =
            TimeSpan.FromSeconds(DefaultResendBackoffResetSeconds);

        private readonly UserManager<User> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailVerificationService> _logger;

        public EmailVerificationService(
            UserManager<User> userManager,
            IEmailSender emailSender,
            IMemoryCache cache,
            IConfiguration configuration,
            ILogger<EmailVerificationService> logger)
        {
            _userManager = userManager;
            _emailSender = emailSender;
            _cache = cache;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendVerificationAsync(
            User user,
            CancellationToken cancellationToken = default)
        {
            if (user.EmailConfirmed)
            {
                return;
            }

            var token =
                await _userManager.GenerateEmailConfirmationTokenAsync(user);

            var encodedToken = WebEncoders.Base64UrlEncode(
                Encoding.UTF8.GetBytes(token));

            var link =
                 $"{FrontendBaseUrl()}/verify-email" +
                 $"?userId={user.Id}" +
                 $"&token={Uri.EscapeDataString(encodedToken)}";

            var message = new EmailMessage(
                user.Email!,
                "Potwierdzenie adresu e-mail",
                $"""
            Dziękujemy za rejestrację.

            Aby potwierdzić adres e-mail, kliknij poniższy link:

            {link}

            Link jest ważny przez {TokenLifetimeHours()} godzin.

            Jeśli nie zakładałeś konta, zignoruj tę wiadomość.
            """);

            await _emailSender.SendAsync(
                message,
                cancellationToken);

            // Counts and cools down regardless of who triggered the send
            // (registration or an explicit resend), so the backoff is
            // effective from the very first email, not only from the first
            // resend, and a rapid string of resends keeps growing the wait
            // instead of resetting every time someone clicks again.
            var attemptNumber = IncrementResendAttemptCount(user.Id);
            var cooldown = ResendCooldownFor(attemptNumber);

            _cache.Set(
                ResendCooldownCacheKey(user.Id),
                true,
                cooldown);

            _logger.LogInformation(
                "Verification email sent for user {UserId} (attempt {AttemptNumber}, next resend allowed in {Cooldown})",
                user.Id,
                attemptNumber,
                cooldown);
        }

        public async Task<bool> VerifyAsync(
            string userId,
            string encodedToken,
            CancellationToken cancellationToken = default)
        {
            var user = await _userManager.FindByIdAsync(userId);

            if (user is null)
            {
                return false;
            }

            if (user.EmailConfirmed)
            {
                return false;
            }

            string token;

            try
            {
                token = Encoding.UTF8.GetString(
                    WebEncoders.Base64UrlDecode(encodedToken));
            }
            catch (FormatException)
            {
                return false;
            }

            // Identity's own EmailConfirmed column is the single source of
            // truth for verification status; ConfirmEmailAsync persists it,
            // so there is nothing else to sync here.
            var result = await _userManager.ConfirmEmailAsync(
                user,
                token);

            return result.Succeeded;
        }

        public async Task<bool> ResendVerificationAsync(
            string email,
            CancellationToken cancellationToken = default)
        {
            var user = await _userManager.FindByEmailAsync(email);

            // Always reports success whether or not an account exists for
            // this address, and whether or not a new email was actually
            // sent (already confirmed, or still cooling down) - the caller
            // can never use the response to enumerate accounts or probe
            // verification state.
            if (user is null)
            {
                return true;
            }

            if (user.EmailConfirmed)
            {
                return true;
            }

            if (_cache.TryGetValue(ResendCooldownCacheKey(user.Id), out _))
            {
                return true;
            }

            await SendVerificationAsync(
                user,
                cancellationToken);

            return true;
        }

        private string FrontendBaseUrl() =>
            (_configuration["EmailVerification:FrontendBaseUrl"] is { Length: > 0 } configured
                ? configured
                : DefaultFrontendBaseUrl)
                .TrimEnd('/');

        private int TokenLifetimeHours() =>
            _configuration.GetValue<int?>("EmailVerification:TokenLifetimeHours")
                ?? IdentityConfiguration.DefaultTokenLifetimeHours;

        private TimeSpan BaseResendCooldown()
        {
            var seconds = _configuration.GetValue<int?>(
                "EmailVerification:ResendCooldownSeconds");

            return seconds is > 0
                ? TimeSpan.FromSeconds(seconds.Value)
                : DefaultResendCooldown;
        }

        private TimeSpan MaxResendCooldown()
        {
            var seconds = _configuration.GetValue<int?>(
                "EmailVerification:ResendCooldownMaxSeconds");

            return seconds is > 0
                ? TimeSpan.FromSeconds(seconds.Value)
                : DefaultResendCooldownMax;
        }

        private double ResendCooldownMultiplier()
        {
            var multiplier = _configuration.GetValue<double?>(
                "EmailVerification:ResendCooldownMultiplier");

            return multiplier is > 1
                ? multiplier.Value
                : DefaultResendCooldownMultiplier;
        }

        private TimeSpan ResendBackoffResetWindow()
        {
            var seconds = _configuration.GetValue<int?>(
                "EmailVerification:ResendCooldownResetSeconds");

            return seconds is > 0
                ? TimeSpan.FromSeconds(seconds.Value)
                : DefaultResendBackoffResetWindow;
        }

        private TimeSpan ResendCooldownFor(int attemptNumber) =>
            ResendBackoffCalculator.CooldownFor(
                attemptNumber,
                BaseResendCooldown(),
                MaxResendCooldown(),
                ResendCooldownMultiplier());

        // Tracks how many consecutive sends this user has had (registration
        // plus every resend), so the backoff keeps growing across separate
        // /resend-verification calls rather than resetting each time the
        // per-attempt cooldown key above expires. Refreshed on every send so
        // the reset window always measures time since the *last* send.
        private int IncrementResendAttemptCount(Guid userId)
        {
            var key = ResendAttemptCountCacheKey(userId);
            var count = _cache.TryGetValue<int>(key, out var existing)
                ? existing
                : 0;

            count++;
            _cache.Set(key, count, ResendBackoffResetWindow());

            return count;
        }

        private static string ResendCooldownCacheKey(Guid userId) =>
            $"email-verification-resend:{userId}";

        private static string ResendAttemptCountCacheKey(Guid userId) =>
            $"email-verification-resend-attempts:{userId}";
    }
}
