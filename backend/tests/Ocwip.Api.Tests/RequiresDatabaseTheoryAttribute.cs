using Xunit;

namespace Ocwip.Api.Tests;

/// <summary>
/// The theory counterpart of <see cref="RequiresDatabaseFactAttribute"/>.
///
/// It cannot be derived from that attribute, because xUnit dispatches on
/// FactAttribute versus TheoryAttribute, and a theory carrying a fact attribute
/// loses its data rows. Without this type a data driven database test has no
/// gated option at all, so it silently falls back to a plain [Theory] and fails
/// instead of skipping when there is no database.
/// </summary>
public sealed class RequiresDatabaseTheoryAttribute : TheoryAttribute
{
    public RequiresDatabaseTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(RequiresDatabaseFactAttribute.ConnectionString))
        {
            Skip =
                $"{RequiresDatabaseFactAttribute.Variable} is not set, so there " +
                "is no database to query.";
        }
    }
}
