namespace Ocwip.Api.Services;

/// <summary>
/// What an anonymous account route may be handed (S-31). Three routes read an
/// account id and a token out of a link that a mail client, a copy and paste
/// or a probe can cut short, and <c>UserManager.FindByIdAsync</c> converts
/// that string straight to a Guid, throwing on anything else, all the way out
/// to an unhandled 500 (R-35).
///
/// One place, because the gap was fixed three times in three services and
/// missed in the fourth; a fifth route gets it by calling this.
/// </summary>
internal static class AccountInput
{
    /// <summary>Whether the value can be an account id at all. Refused the
    /// same way as an unknown one, so the answer never tells a mangled link
    /// apart from an account that does not exist (rule 3).</summary>
    public static bool IsAccountId(string? value) => Guid.TryParse(value, out _);

    /// <summary>Whether a token from a link is worth looking up at all.</summary>
    public static bool IsToken(string? value) => !string.IsNullOrWhiteSpace(value);

    /// <summary>Whether an address from a form is worth looking up at all.</summary>
    public static bool IsAddress(string? value) => !string.IsNullOrWhiteSpace(value);
}
