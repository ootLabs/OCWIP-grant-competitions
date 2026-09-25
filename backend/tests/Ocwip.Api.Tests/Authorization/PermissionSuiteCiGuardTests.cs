using System.Reflection;
using Xunit;

namespace Ocwip.Api.Tests.Authorization;

/// <summary>
/// The last criterion of T-13.3, and one T-36 repeats for the suite it adds:
/// these tests are wired into CI and a failure blocks the merge. Said plainly
/// by both cards, a test that can be skipped protects nothing, and a skipped
/// xUnit test is green enough to merge.
///
/// So the guard is here rather than in a reviewer's memory. Two things can
/// quietly disarm a permission suite without anything turning red: somebody
/// adds a plain [Fact] that passes without a database and therefore asserts
/// against a pipeline that was never built, or somebody drops the connection
/// string from the workflow and every fact in the suite reports Skipped.
/// </summary>
public sealed class PermissionSuiteCiGuardTests
{
    /// <summary>
    /// Every isolation suite this guard is responsible for. T-36's
    /// <see cref="ApplicantDataIsolationTests"/> joins T-13.3's
    /// <see cref="PermissionDenialTests"/> here rather than getting its own
    /// copy of this guard, so the two cards cannot drift into "the guard
    /// covers one but not the other" without anyone noticing.
    /// </summary>
    private static readonly Type[] GuardedSuites =
    [
        typeof(PermissionDenialTests),
        typeof(ApplicantDataIsolationTests),
    ];

    /// <summary>
    /// Every test in each guarded suite goes through one of the database
    /// gated attributes, so there is no test in it that can pass without a
    /// real PostgreSQL. A plain [Fact] added here would be the worst of both
    /// worlds: green without a database, and therefore green without the
    /// pipeline it claims to prove.
    ///
    /// Both attributes count, and TheoryAttribute derives from FactAttribute
    /// while RequiresDatabaseTheoryAttribute does not derive from the fact
    /// version (xUnit tells the two apart, so it cannot). Checking only for
    /// the fact attribute would therefore reject a perfectly gated
    /// [RequiresDatabaseTheory], which is the shape a table of denied roles
    /// naturally takes.
    /// </summary>
    [Fact]
    public void Every_permission_test_is_gated_on_a_real_database()
    {
        foreach (var suite in GuardedSuites)
        {
            var tests = suite
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => method
                    .GetCustomAttributes(inherit: true)
                    .OfType<FactAttribute>()
                    .Any())
                .ToList();

            // An empty list would satisfy the check below without meaning
            // anything, which is how this guard would rot if a suite were
            // ever renamed out from under it.
            Assert.NotEmpty(tests);

            var ungated = tests
                .Where(method => method
                    .GetCustomAttributes(inherit: true)
                    .OfType<FactAttribute>()
                    .Any(attribute => attribute
                        is not RequiresDatabaseFactAttribute
                        and not RequiresDatabaseTheoryAttribute))
                .Select(method => method.Name)
                .ToList();

            Assert.Empty(ungated);
        }
    }

    /// <summary>
    /// And when the guarded suites run in CI, they must not be skipping.
    /// Locally the assertion is satisfied by there being no CI, which is the
    /// point: the suites stay usable without a running stack, and the one
    /// place where Skipped would be mistaken for proof is the place that
    /// refuses it. One check, because <see cref="RequiresDatabaseFactAttribute"/>
    /// reads the same connection string for every suite in
    /// <see cref="GuardedSuites"/>.
    ///
    /// The variable is set explicitly by .github/workflows/ci.yml rather than
    /// left to the runner's own default. GitHub does set it, but a job moved
    /// into a container, which is how the smoke job already runs and how
    /// AGENTS.md runs the tests locally, does not inherit it, and this guard
    /// would then be vacuously true exactly where it has to bite.
    /// </summary>
    [Fact]
    public void In_ci_the_database_is_configured_so_nothing_reports_skipped()
    {
        var inCi = !string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable("CI"));

        var hasDatabase = !string.IsNullOrWhiteSpace(
            RequiresDatabaseFactAttribute.ConnectionString);

        Assert.True(
            !inCi || hasDatabase,
            $"Running in CI without {RequiresDatabaseFactAttribute.Variable}, so "
            + "every permission test would report Skipped and the merge would "
            + "not be blocked by anything.");
    }
}
