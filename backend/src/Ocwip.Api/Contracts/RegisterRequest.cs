namespace Ocwip.Api.Contracts
{
    // Deliberately no Pesel here: Models/User.cs is explicit that a PESEL
    // only shows up at the agreement stage, and registration (T-12.1) must
    // not collect it.
    public record RegisterRequest(
     string Email,
     string Password,
     string FirstName,
     string LastName
 );
}
