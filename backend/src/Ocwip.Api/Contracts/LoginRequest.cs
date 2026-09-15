namespace Ocwip.Api.Contracts;

/// <summary>
/// The body of POST /login.
///
/// ReturnUrl is the report's rule from step 3.1: an applicant who clicked
/// "Wypełnij wniosek" on a competition page and had to sign in first comes back
/// to THAT competition, not to a generic dashboard. The caller proposes it and
/// the server decides whether it is safe, see
/// <see cref="Services.LoginLandingPath"/> - a value that arrives from the
/// browser and is echoed into a redirect is an open redirect unless something
/// refuses everything that is not a local path.
/// </summary>
public sealed record LoginRequest(
    string Email,
    string Password,
    string? ReturnUrl = null)
{
    /// <summary>
    /// Same guard as RegisterRequest, and for the same reason: a record's
    /// generated ToString prints every property, so the default one prints the
    /// password. Security rule 4 gets broken by an interpolated string in some
    /// later card, not by a line anybody writes on purpose.
    /// </summary>
    public override string ToString() => nameof(LoginRequest);
}
