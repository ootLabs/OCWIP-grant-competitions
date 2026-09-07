using Microsoft.AspNetCore.Identity;

namespace Ocwip.Api.Data;

/// <summary>
/// The normalized address, computed by the very implementation Identity uses.
///
/// Uniqueness stands on the normalized_email column (UserConfiguration.cs), so
/// anything looking an account up has to arrive at exactly the value UserManager
/// wrote. Spelling that out as ToUpperInvariant looks equivalent and is not:
/// Identity's normalizer runs string.Normalize() first, so for an address typed
/// with a decomposed accent the two produce different strings and the lookup
/// reports an account that plainly exists as unknown.
///
/// Hence one call site for the real normalizer rather than a copy of what it
/// does. Anything that has a DI container should resolve ILookupNormalizer
/// instead; this exists for the paths that have none, which today is the
/// administrative command (it runs before a web host) and the schema tests
/// (they write through EF and never touch UserManager).
///
/// This is NOT what scripts/seed.py and the T-12.0 migration compute: those use
/// SQL upper(), which does not normalize Unicode. NormalizedAddressTests pins
/// the two against each other and documents where they part ways.
/// </summary>
internal static class EmailNormalizer
{
    private static readonly ILookupNormalizer Normalizer = new UpperInvariantLookupNormalizer();

    /// <summary>
    /// Never null for a non null address: the normalizer only returns null when
    /// it is handed null.
    /// </summary>
    public static string Normalize(string email) => Normalizer.NormalizeEmail(email)!;
}
