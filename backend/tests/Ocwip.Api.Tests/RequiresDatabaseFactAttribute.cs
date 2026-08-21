using Xunit;

namespace Ocwip.Api.Tests;

/// <summary>
/// A fact that reports Skipped, not Passed, when there is no database to talk
/// to. The suite stays usable without a running stack, and CI always has a real
/// PostgreSQL (docs/testy.md).
///
/// The reason is decided in the constructor because xUnit 2 reads Skip while
/// discovering tests and has no dynamic skip: returning early from the body
/// would report a green test that asserted nothing, and Assert.Skip would fail
/// it. The address comes from the environment for the same reason, since no
/// host exists yet at discovery.
/// </summary>
public sealed class RequiresDatabaseFactAttribute : FactAttribute
{
    public const string Variable = "ConnectionStrings__Postgres";

    public static string? ConnectionString => Environment.GetEnvironmentVariable(Variable);

    public RequiresDatabaseFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            Skip = $"{Variable} is not set, so there is no database to migrate.";
        }
    }
}
