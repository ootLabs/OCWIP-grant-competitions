namespace Ocwip.Api.Contracts;

/// <summary>
/// The body of POST /forgot-password. Answers the same whether or not the
/// address has an account (security rule 3), see PasswordResetService.
/// </summary>
public sealed record ForgotPasswordRequest(string? Email);
