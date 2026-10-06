using Microsoft.AspNetCore.Http;
using Ocwip.Api.Configuration;
using Xunit;

namespace Ocwip.Api.Tests.Configuration;

/// <summary>
/// The second barrier in front of a request that changes something (S-15).
/// Until it there was one: the session cookie's SameSite. CORS is not a
/// second one, because it decides who may read an answer, not who may send a
/// request, and a form submission needs no preflight at all.
/// </summary>
public sealed class CrossSiteRequestFilterTests
{
    private static readonly HashSet<string> Allowed =
        new(["https://ocwip.example"], StringComparer.OrdinalIgnoreCase);

    private static HttpContext Request(string method, string? origin = null, string? site = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;

        if (origin is not null)
        {
            context.Request.Headers.Origin = origin;
        }

        if (site is not null)
        {
            context.Request.Headers["Sec-Fetch-Site"] = site;
        }

        return context;
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public void A_request_that_changes_nothing_is_never_refused(string method) =>
        Assert.False(CrossSiteRequestFilter.IsCrossSite(
            Request(method, origin: "https://obcy.example", site: "cross-site"), Allowed));

    [Fact]
    public void A_form_on_someone_elses_page_is_refused() =>
        Assert.True(CrossSiteRequestFilter.IsCrossSite(
            Request("POST", origin: "https://obcy.example"), Allowed));

    [Fact]
    public void So_is_one_the_browser_labels_cross_site_without_an_origin() =>
        Assert.True(CrossSiteRequestFilter.IsCrossSite(Request("POST", site: "cross-site"), Allowed));

    [Theory]
    [InlineData("same-origin")]
    [InlineData("same-site")]
    // The address bar: a person typing or a bookmark, not a page acting for them.
    [InlineData("none")]
    public void The_product_talking_to_itself_is_not(string site) =>
        Assert.False(CrossSiteRequestFilter.IsCrossSite(
            Request("POST", origin: "https://ocwip.example", site: site), Allowed));

    /// <summary>
    /// A deployment that serves the frontend and the API from one origin needs
    /// no CORS origin at all, so the list this filter reads can be empty while
    /// the browser still sends Origin on every POST. Only the browser can say
    /// "same-origin", and nothing it says that about is a request made by
    /// somebody else's page.
    /// </summary>
    [Fact]
    public void Same_origin_passes_even_when_no_origin_is_listed() =>
        Assert.False(CrossSiteRequestFilter.IsCrossSite(
            Request("POST", origin: "https://konkursy.example", site: "same-origin"),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)));

    [Fact]
    public void An_allowed_origin_passes_whatever_its_trailing_slash() =>
        Assert.False(CrossSiteRequestFilter.IsCrossSite(
            Request("POST", origin: "https://ocwip.example/"), Allowed));

    /// <summary>
    /// A caller outside a browser (a script, a probe, the test host) sends
    /// neither header. Refusing those would break every such caller for no
    /// gain: this filter exists against a browser doing what a page told it.
    /// </summary>
    [Fact]
    public void A_caller_that_says_nothing_about_where_it_came_from_passes() =>
        Assert.False(CrossSiteRequestFilter.IsCrossSite(Request("POST"), Allowed));
}
