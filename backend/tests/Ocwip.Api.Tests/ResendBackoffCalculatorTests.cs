using Ocwip.Api.Services;
using Xunit;

namespace Ocwip.Api.Tests;

/// <summary>
/// ResendBackoffCalculator is the pure formula behind the resend flood
/// guard, kept separate from EmailVerificationService precisely so its
/// growth and cap can be checked without a cache, a database, or waiting
/// on real time.
/// </summary>
public class ResendBackoffCalculatorTests
{
    private static readonly TimeSpan BaseCooldown = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan MaxCooldown = TimeSpan.FromMinutes(30);
    private const double Multiplier = 5.0;

    [Fact]
    public void First_attempt_uses_the_base_cooldown()
    {
        var cooldown = ResendBackoffCalculator.CooldownFor(
            attemptNumber: 1,
            BaseCooldown,
            MaxCooldown,
            Multiplier);

        Assert.Equal(BaseCooldown, cooldown);
    }

    [Fact]
    public void Cooldown_grows_by_the_multiplier_each_attempt()
    {
        var second = ResendBackoffCalculator.CooldownFor(
            attemptNumber: 2,
            BaseCooldown,
            MaxCooldown,
            Multiplier);

        var third = ResendBackoffCalculator.CooldownFor(
            attemptNumber: 3,
            BaseCooldown,
            MaxCooldown,
            Multiplier);

        Assert.Equal(TimeSpan.FromSeconds(300), second);
        Assert.Equal(TimeSpan.FromSeconds(1500), third);
    }

    [Fact]
    public void Cooldown_is_capped_at_the_configured_maximum()
    {
        // Attempt 4 would be 60 * 5^3 = 7500s uncapped - well past the cap.
        var cooldown = ResendBackoffCalculator.CooldownFor(
            attemptNumber: 4,
            BaseCooldown,
            MaxCooldown,
            Multiplier);

        Assert.Equal(MaxCooldown, cooldown);
    }

    [Fact]
    public void Cooldown_stays_capped_for_arbitrarily_many_further_attempts()
    {
        var cooldown = ResendBackoffCalculator.CooldownFor(
            attemptNumber: 100,
            BaseCooldown,
            MaxCooldown,
            Multiplier);

        Assert.Equal(MaxCooldown, cooldown);
    }

    [Fact]
    public void Rejects_an_attempt_number_below_one()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ResendBackoffCalculator.CooldownFor(
                attemptNumber: 0,
                BaseCooldown,
                MaxCooldown,
                Multiplier));
    }
}
