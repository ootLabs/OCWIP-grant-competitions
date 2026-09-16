namespace Ocwip.Api.Services;

/// <summary>
/// What happened, as far as the CALLER is allowed to know.
///
/// InvalidToken covers every reason the link itself did not work: unknown
/// account, malformed token, expired token, already used token. Collapsing
/// all four into one value is deliberate, the same way LoginOutcome collapses
/// "no such account" and "wrong password": telling them apart would let an
/// outsider learn which addresses have accounts, or when a link was clicked.
/// </summary>
internal enum PasswordResetOutcome
{
    Succeeded,
    InvalidToken,
    PasswordRejected,
}

internal sealed record PasswordResetResult(
    PasswordResetOutcome Outcome,
    IReadOnlyList<string> Errors)
{
    public static PasswordResetResult Succeeded { get; } =
        new(PasswordResetOutcome.Succeeded, []);

    public static PasswordResetResult InvalidToken { get; } =
        new(PasswordResetOutcome.InvalidToken, []);

    public static PasswordResetResult PasswordRejected(IEnumerable<string> errors) =>
        new(PasswordResetOutcome.PasswordRejected, errors.ToList());
}

internal interface IPasswordResetService
{
    /// <summary>
    /// Always succeeds from the caller's point of view, whether or not the
    /// address has an account (security rule 3). See ResendVerificationAsync
    /// for the same shape.
    /// </summary>
    Task RequestResetAsync(string? email, CancellationToken cancellationToken = default);

    Task<PasswordResetResult> ResetAsync(
        string? userId,
        string? encodedToken,
        string? newPassword,
        CancellationToken cancellationToken = default);
}
