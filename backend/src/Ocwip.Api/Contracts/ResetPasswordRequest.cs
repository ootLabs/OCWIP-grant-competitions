namespace Ocwip.Api.Contracts;

/// <summary>
/// The body of POST /reset-password: the link from the reset email plus the
/// chosen password.
/// </summary>
public sealed record ResetPasswordRequest(string? UserId, string? Token, string? NewPassword)
{
    /// <summary>
    /// Same reason as RegisterRequest.ToString: the generated ToString of a
    /// record prints every property, and this one carries a password.
    /// </summary>
    public override string ToString() => nameof(ResetPasswordRequest);
}
