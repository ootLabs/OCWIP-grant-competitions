namespace Ocwip.Api.Contracts
{
    /// <summary>
    /// The body of POST /resend-verification. ReturnUrl as in RegisterRequest:
    /// the new link has to lead back to the same place the expired one did.
    /// </summary>
    public record ResendVerificationRequest(
        string Email,
        string? ReturnUrl = null
    );
}
