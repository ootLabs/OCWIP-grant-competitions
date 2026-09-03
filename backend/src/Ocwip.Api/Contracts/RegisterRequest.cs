namespace Ocwip.Api.Contracts
{
    public record RegisterRequest(
     string Email,
     string Password,
     string FirstName,
     string LastName,
     string Pesel
 );
}
