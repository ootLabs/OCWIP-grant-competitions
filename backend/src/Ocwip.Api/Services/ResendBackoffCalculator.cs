namespace Ocwip.Api.Services
{
    /// <summary>
    /// Pure math behind the resend cooldown's exponential backoff. Kept
    /// separate from EmailVerificationService so the formula can be unit
    /// tested without a cache, a database, or waiting on real time - see
    /// ResendBackoffCalculatorTests.
    /// </summary>
    internal static class ResendBackoffCalculator
    {
        /// <summary>
        /// The cooldown to apply after the given consecutive send attempt
        /// (1-based: 1 is the first email, 2 the first resend, and so on).
        /// Grows geometrically from <paramref name="baseCooldown"/> by
        /// <paramref name="multiplier"/> each attempt, capped at
        /// <paramref name="maxCooldown"/> so a determined flood eventually
        /// plateaus instead of growing without bound.
        /// </summary>
        public static TimeSpan CooldownFor(
            int attemptNumber,
            TimeSpan baseCooldown,
            TimeSpan maxCooldown,
            double multiplier)
        {
            if (attemptNumber < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(attemptNumber),
                    attemptNumber,
                    "Attempt number must be at least 1.");
            }

            // attempt 1 -> base, attempt 2 -> base * multiplier,
            // attempt 3 -> base * multiplier^2, ...
            var rawSeconds =
                baseCooldown.TotalSeconds * Math.Pow(multiplier, attemptNumber - 1);

            var seconds = Math.Min(rawSeconds, maxCooldown.TotalSeconds);

            return TimeSpan.FromSeconds(seconds);
        }
    }
}
