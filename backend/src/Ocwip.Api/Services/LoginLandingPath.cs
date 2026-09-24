using Ocwip.Api.Models;

namespace Ocwip.Api.Services;

/// <summary>
/// Where a caller goes after signing in. One table, one guard, one place to
/// change when a panel moves.
///
/// Why the SERVER answers this at all, given that the panels are Next.js routes
/// (T-15.2, T-15.3): the rule has two halves and the dangerous half is the
/// server's. The role half is trivial, but the report (step 3.1) also wants an
/// applicant who signed in from a competition page to land back on THAT page,
/// which means a destination proposed by the browser. A destination proposed by
/// the browser and followed without a check is an open redirect, and the one
/// thing that must never be skipped is the check. Keeping both halves together
/// means the check cannot be forgotten by a screen that only remembered the
/// easy half.
///
/// The paths are the frontend's, not the API's, and that is a deliberate
/// coupling of exactly one file to exactly one product, written down in
/// docs/architektura.md. Nothing checks that they match the routes the panels
/// register, because there are no panels yet: until T-15.2 and T-15.3 land,
/// every one of these is a promise rather than a route, and the cross check
/// belongs to whichever of those two cards lands first.
/// </summary>
internal static class LoginLandingPath
{
    /// <summary>
    /// Sees every competition, every application, every agreement. Built in
    /// T-15.3.
    /// </summary>
    public const string Operator = "/panel/operator";

    /// <summary>Its own applications and nothing else. Built in T-15.2.</summary>
    public const string Applicant = "/panel/applicant";

    /// <summary>
    /// Only the applications assigned to it. The assignment mechanism is T-37,
    /// so today this role signs in and finds a panel with nothing in it, which
    /// is still better than signing in and landing on the operator's screen.
    /// </summary>
    public const string Reviewer = "/panel/reviewer";

    /// <summary>
    /// The role's own panel, ignoring anything the caller proposed.
    /// </summary>
    public static string For(Role role) => role switch
    {
        Role.Operator => Operator,
        Role.Reviewer => Reviewer,
        // Applicant is the default arm rather than a case, for the same reason
        // it is the first value of the enum: a role added later must land on
        // the least privileged screen until someone decides otherwise, not on
        // whichever branch happens to be first.
        _ => Applicant,
    };

    /// <summary>
    /// The caller's proposed destination when it is safe, the role's panel
    /// otherwise.
    ///
    /// Safe means a path inside this application and nothing else. Everything
    /// that is not one relative path is refused rather than repaired: a
    /// sanitiser that tries to fix "//evil.example" or "https:/evil.example"
    /// is a list of tricks somebody has already thought of, and the attacker
    /// only needs the one nobody thought of.
    /// </summary>
    public static string Resolve(Role role, string? proposed) =>
        SafeOrNull(proposed) ?? For(role);

    /// <summary>
    /// The longest destination accepted, after trimming. See SafeOrNull.
    /// </summary>
    public const int MaxLength = 512;

    /// <summary>
    /// The proposed destination, trimmed, when it is one relative path inside
    /// this application; null for anything else, an empty value included.
    ///
    /// Public on its own for the verification mail (T-12.8), which carries the
    /// way back through the inbox: a link sent from this product's address
    /// must not carry somebody else's, even though signing in would refuse it
    /// again later.
    /// </summary>
    public static string? SafeOrNull(string? proposed)
    {
        var candidate = proposed?.Trim();

        if (string.IsNullOrEmpty(candidate))
        {
            return null;
        }

        // A competition page is a short path with an identifier. Anything far
        // longer is text riding along in the query string, and since this value
        // also goes into the verification mail, it would be text of somebody
        // else's choosing inside a mail sent from this product's address.
        if (candidate.Length > MaxLength)
        {
            return null;
        }

        // Must start with a single slash: "/panel/applicant" yes,
        // "//evil.example/x" no (a protocol relative URL leaves the site),
        // "panel/applicant" no (relative to wherever the browser happens to
        // be), "https://evil.example" no.
        if (candidate[0] is not '/' || candidate.StartsWith("//", StringComparison.Ordinal))
        {
            return null;
        }

        // Backslashes, because browsers have historically treated "/\evil" and
        // "\\evil" the way they treat "//evil", and a control character can cut
        // a header short or hide the rest of the value from a log.
        if (candidate.Contains('\\') || candidate.Any(char.IsControl))
        {
            return null;
        }

        return candidate;
    }
}
