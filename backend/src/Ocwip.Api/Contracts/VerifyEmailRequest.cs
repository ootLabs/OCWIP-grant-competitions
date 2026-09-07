namespace Ocwip.Api.Contracts
{
    public record VerifyEmailRequest(
        string UserId,
        string Token
    );
}
