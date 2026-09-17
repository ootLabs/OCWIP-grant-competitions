using System.Net;
using Ocwip.Api.Tests.Data;
using Xunit;

namespace Ocwip.Api.Tests.Authorization;

/// <summary>
/// Access refused exactly where it has to be refused (T-13.3), over real HTTP,
/// against real rows, with real sign ins.
///
/// T-13.2 proved the policies. It proved them on a route that is handed the
/// OWNER of the resource in its query string, which is the one shape that
/// cannot catch the mistake this card is about: a route that trusts the
/// identifier it was given passes every one of those tests and still serves
/// another organisation's application. The probe here is shaped like a product
/// route instead, reading the row by id before asking anything, so swapping
/// the identifier is a thing that can actually be attempted.
///
/// Every refusal below is paired with a caller who DOES get through on the
/// same route. A suite that refuses everything looks identical to a suite
/// whose endpoint is broken, and only one of those is worth keeping.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class PermissionDenialTests : IClassFixture<OcwipWebApplicationFactory>
{
    private readonly OcwipWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public PermissionDenialTests(
        OcwipWebApplicationFactory factory,
        PostgresDatabaseFixture database)
    {
        _factory = factory;
        _database = database;
    }

    private Task<PermissionScenario> ScenarioAsync() =>
        PermissionScenario.CreateAsync(_factory, _database);

    [RequiresDatabaseFact]
    public async Task Applicant_A_is_refused_applicant_B_s_application_and_still_reads_their_own()
    {
        // The rule the client stated in one sentence, and the reason this card
        // decides whether the system can be exposed anywhere at all.
        var scenario = await ScenarioAsync();
        var applicantA = await scenario.ApplicantOneAsync();

        var own = await applicantA.GetAsync(
            PolicyProbeEndpoints.ApplicationById(scenario.ApplicationOne));
        var somebodyElses = await applicantA.GetAsync(
            PolicyProbeEndpoints.ApplicationById(scenario.ApplicationTwo));

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, somebodyElses.StatusCode);

        // The status code is half the assertion. A 403 that ships the answers
        // in its body is still the leak this card exists to prevent, and the
        // answers are where an organisation's personal data sits.
        var refused = await somebodyElses.Content.ReadAsStringAsync();
        Assert.DoesNotContain(PermissionScenario.MarkerTwo, refused);

        var served = await own.Content.ReadAsStringAsync();
        Assert.Contains(PermissionScenario.MarkerOne, served);
        Assert.DoesNotContain(PermissionScenario.MarkerTwo, served);
    }

    [RequiresDatabaseFact]
    public async Task The_refusal_holds_in_both_directions()
    {
        // Asserted from the other side too, because the first organisation is
        // the one created first in the fixture: a rule that accidentally
        // depended on insert order, or on a stale row cached per request,
        // would pass the test above and fail here.
        var scenario = await ScenarioAsync();
        var applicantB = await scenario.ApplicantTwoAsync();

        var own = await applicantB.GetAsync(
            PolicyProbeEndpoints.ApplicationById(scenario.ApplicationTwo));
        var somebodyElses = await applicantB.GetAsync(
            PolicyProbeEndpoints.ApplicationById(scenario.ApplicationOne));

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, somebodyElses.StatusCode);
        Assert.DoesNotContain(
            PermissionScenario.MarkerOne,
            await somebodyElses.Content.ReadAsStringAsync());
    }

    [RequiresDatabaseFact]
    public async Task Swapping_the_identifier_in_the_url_never_yields_another_organisations_data()
    {
        // The card's headline case. Nothing in the interface links here, so
        // clicking around by hand never finds it: one signed in applicant
        // walks every application identifier in the database plus one that
        // belongs to nobody, and exactly one of them is theirs.
        var scenario = await ScenarioAsync();
        var applicantA = await scenario.ApplicantOneAsync();

        var served = new List<Guid>();

        foreach (var id in scenario.EveryApplicationIdPlusAStranger())
        {
            var response = await applicantA.GetAsync(
                PolicyProbeEndpoints.ApplicationById(id));
            var body = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == HttpStatusCode.OK)
            {
                served.Add(id);
            }
            else
            {
                // Refused rows answer 403, a row that is not there answers
                // 404. Both are acceptable answers to a swapped identifier;
                // a 200 and a 500 are not, and neither is a body carrying the
                // neighbour's data.
                Assert.Contains(
                    response.StatusCode,
                    new[] { HttpStatusCode.Forbidden, HttpStatusCode.NotFound });
            }

            Assert.DoesNotContain(PermissionScenario.MarkerTwo, body);
        }

        Assert.Equal(new[] { scenario.ApplicationOne }, served);
    }

    [RequiresDatabaseFact]
    public async Task An_applicant_is_refused_on_an_operator_route()
    {
        // Same role, same cookie, a route that is not theirs. Paired with the
        // operator on the same route, so the 403 cannot come from the route
        // being unreachable for everybody.
        var scenario = await ScenarioAsync();
        var applicant = await scenario.ApplicantOneAsync();
        var operatorClient = await scenario.OperatorAsync();

        var refused = await applicant.GetAsync(PolicyProbeEndpoints.OperatorOnly);
        var allowed = await operatorClient.GetAsync(PolicyProbeEndpoints.OperatorOnly);

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);

        // A refusal is a normal state of the application, so it is a problem
        // document in Polish rather than a 500 or a blank page.
        Assert.Equal(
            "application/problem+json",
            refused.Content.Headers.ContentType?.MediaType);
        Assert.Contains(
            "Nie masz dostępu",
            await refused.Content.ReadAsStringAsync());
    }

    [RequiresDatabaseFact]
    public async Task A_reviewer_is_refused_every_application_because_nothing_assigns_any()
    {
        // Refused deliberately, not by omission: a reviewer may see the
        // applications assigned to them and the assignment mechanism is T-37.
        // Until it exists the honest answer is no, for both applications, and
        // the alternative would be a reviewer reading every application in the
        // system in the window between the two cards.
        var scenario = await ScenarioAsync();
        var reviewer = await scenario.ReviewerAsync();

        foreach (var id in new[] { scenario.ApplicationOne, scenario.ApplicationTwo })
        {
            var response = await reviewer.GetAsync(
                PolicyProbeEndpoints.ApplicationById(id));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [RequiresDatabaseFact]
    public async Task An_anonymous_caller_gets_401_on_everything_protected()
    {
        // 401 and not 403, because the caller has not said who they are yet,
        // and not 302 either: the panels read a redirect to a login page as a
        // successful response and would render it into the data area. Asserted
        // across a product route and all three probes, including the one that
        // declares no rule at all, since that is the endpoint somebody forgets.
        var scenario = await ScenarioAsync();
        var anonymous = scenario.Anonymous();

        var paths = new[]
        {
            "/me",
            PolicyProbeEndpoints.OperatorOnly,
            PolicyProbeEndpoints.NoRuleAtAll,
            PolicyProbeEndpoints.ApplicationById(scenario.ApplicationOne),
        };

        foreach (var path in paths)
        {
            var response = await anonymous.GetAsync(path);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.DoesNotContain(
                PermissionScenario.MarkerOne,
                await response.Content.ReadAsStringAsync());
        }
    }

    [RequiresDatabaseFact]
    public async Task An_operator_reads_both_applications()
    {
        // "Operator widzi wszystko", said plainly by the client. This is also
        // what keeps the refusals above meaningful: if the probe were broken
        // rather than protective, this test is the one that would notice.
        var scenario = await ScenarioAsync();
        var operatorClient = await scenario.OperatorAsync();

        var first = await operatorClient.GetAsync(
            PolicyProbeEndpoints.ApplicationById(scenario.ApplicationOne));
        var second = await operatorClient.GetAsync(
            PolicyProbeEndpoints.ApplicationById(scenario.ApplicationTwo));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Contains(
            PermissionScenario.MarkerOne,
            await first.Content.ReadAsStringAsync());
        Assert.Contains(
            PermissionScenario.MarkerTwo,
            await second.Content.ReadAsStringAsync());
    }
}
