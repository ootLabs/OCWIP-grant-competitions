using Ocwip.Api.Models;
using Ocwip.Api.Services;
using Xunit;

namespace Ocwip.Api.Tests.Services;

/// <summary>
/// Where a session lands, and everything it refuses to land on.
///
/// The role half of this is the card's acceptance criterion. The other half is
/// the report's rule from step 3.1, an applicant returning to the competition
/// page they came from, and that half is an open redirect the moment anything
/// echoes the caller's value back without looking at it.
/// </summary>
public sealed class LoginLandingPathTests
{
    [Theory]
    [InlineData(Role.Operator, LoginLandingPath.Operator)]
    [InlineData(Role.Applicant, LoginLandingPath.Applicant)]
    [InlineData(Role.Reviewer, LoginLandingPath.Reviewer)]
    public void Each_role_lands_on_its_own_panel(Role role, string expected) =>
        Assert.Equal(expected, LoginLandingPath.For(role));

    [Fact]
    public void An_unknown_role_lands_on_the_least_privileged_panel()
    {
        // A value outside the enum is what a future role looks like before
        // anybody remembers this file. It must not fall through to the
        // operator's screen, which is the one that shows every organisation's
        // personal data.
        Assert.Equal(LoginLandingPath.Applicant, LoginLandingPath.For((Role)42));
    }

    [Theory]
    [InlineData("/konkursy/17")]
    [InlineData("/konkursy/17?strona=2")]
    [InlineData("/")]
    public void A_local_path_proposed_by_the_caller_is_honoured(string proposed) =>
        Assert.Equal(
            proposed,
            LoginLandingPath.Resolve(Role.Applicant, proposed));

    [Theory]
    // Absolute, so it leaves the site outright.
    [InlineData("https://evil.example/konkursy")]
    [InlineData("http://evil.example")]
    // Protocol relative: reads like a path and is not one.
    [InlineData("//evil.example/konkursy")]
    // Browsers have historically treated a backslash like a slash here.
    [InlineData("/\\evil.example")]
    [InlineData("\\\\evil.example")]
    // Relative to wherever the browser happens to be, which is not a promise
    // anybody can keep.
    [InlineData("konkursy/17")]
    // A control character can cut a header short and hide the rest from a log.
    [InlineData("/konkursy\n/17")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Anything_that_is_not_a_local_path_falls_back_to_the_panel(
        string? proposed) =>
        Assert.Equal(
            LoginLandingPath.Applicant,
            LoginLandingPath.Resolve(Role.Applicant, proposed));

    [Fact]
    public void The_fallback_still_depends_on_the_role()
    {
        // The guard must not quietly demote an operator to the applicant panel
        // when it refuses a proposed destination.
        Assert.Equal(
            LoginLandingPath.Operator,
            LoginLandingPath.Resolve(Role.Operator, "https://evil.example"));
    }

    [Fact]
    public void The_same_check_answers_on_its_own_for_the_verification_mail()
    {
        // T-12.8: the mail carries the value SafeOrNull returns, so it has to
        // be the trimmed path, and nothing at all for a refused one.
        Assert.Equal("/konkursy/17", LoginLandingPath.SafeOrNull("  /konkursy/17 "));
        Assert.Null(LoginLandingPath.SafeOrNull("https://evil.example"));
        Assert.Null(LoginLandingPath.SafeOrNull(null));
    }

    [Fact]
    public void A_path_up_to_the_limit_is_honoured_and_a_longer_one_is_not()
    {
        var atLimit = "/" + new string('a', LoginLandingPath.MaxLength - 1);
        var overLimit = atLimit + "a";

        Assert.Equal(atLimit, LoginLandingPath.SafeOrNull(atLimit));
        Assert.Null(LoginLandingPath.SafeOrNull(overLimit));
        Assert.Equal(
            LoginLandingPath.Applicant,
            LoginLandingPath.Resolve(Role.Applicant, overLimit));
    }
}
