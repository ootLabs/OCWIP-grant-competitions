namespace Ocwip.Api.Services;

/// <summary>
/// Whether a request was sent by a person at a browser rather than by a
/// script: the Cloudflare Turnstile token the account forms send with
/// /login, /register, /forgot-password and /resend-verification.
///
/// Registered only when Turnstile:SecretKey is set
/// (Configuration/HumanCheckConfiguration.cs). Without it the forms are
/// guarded by the rate limit and the account lockout alone, which is how a
/// developer's machine and the test suite run; Production refuses to start
/// without the key (ProductionConfiguration).
/// </summary>
public interface IHumanCheck
{
    Task<HumanCheckOutcome> VerifyAsync(
        string? token, string? remoteIp, CancellationToken cancellationToken);
}

public enum HumanCheckOutcome
{
    Passed,

    /// <summary>No token, a forged one, an expired one or one already used.</summary>
    Refused,

    /// <summary>
    /// Cloudflare did not answer, or answered with its own error. The request
    /// is refused all the same: a check that lets everything through while
    /// it is down is a check a bot only has to wait out.
    /// </summary>
    Unavailable,
}
