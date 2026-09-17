using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Ocwip.Api.Tests.Endpoints;
using Xunit;

namespace Ocwip.Api.Tests.Authorization;

/// <summary>
/// The fixture the negative permission tests need (T-13.3): one competition,
/// one version of its form, two different organisations, and one application
/// each. Two, because "applicant two reaches for applicant one's application"
/// is not a case a single application can express.
///
/// The card's specification says the identifiers from scripts/seed.py can be
/// quoted here. They cannot, for two reasons that are worth writing down
/// rather than rediscovering. The seeded accounts put a visible placeholder in
/// password_hash and therefore CANNOT sign in, while every test below goes
/// through a real POST /login. And this suite runs against
/// PostgresDatabaseFixture, the throwaway database shared by the postgres
/// collection, which never runs the seed script. So the seed's SHAPE is
/// reproduced here in code instead, deliberately identical to it: submitted
/// application with a number under the first organisation, draft under the
/// second.
/// </summary>
internal sealed class PermissionScenario
{
    /// <summary>
    /// Markers planted in the answers of each application. The answers are
    /// where the personal data of an organisation actually sits, so a leak is
    /// asserted as the ABSENCE of the other organisation's marker from a
    /// response body, not as a status code. A refusal that carried the data
    /// along with it would still be a leak.
    /// </summary>
    public const string MarkerOne = "Dane Podmiotu A, nie do pokazywania";

    public const string MarkerTwo = "Dane Podmiotu B, nie do pokazywania";

    private readonly WebApplicationFactory<Program> _host;
    private readonly PostgresDatabaseFixture _database;

    private PermissionScenario(
        WebApplicationFactory<Program> host,
        PostgresDatabaseFixture database,
        Guid applicationOne,
        Guid applicationTwo,
        Guid entityOne,
        Guid entityTwo)
    {
        _host = host;
        _database = database;
        ApplicationOne = applicationOne;
        ApplicationTwo = applicationTwo;
        EntityOne = entityOne;
        EntityTwo = entityTwo;
    }

    /// <summary>Submitted, owned by the first organisation, carries MarkerOne.</summary>
    public Guid ApplicationOne { get; }

    /// <summary>Draft, owned by the second organisation, carries MarkerTwo.</summary>
    public Guid ApplicationTwo { get; }

    public Guid EntityOne { get; }

    public Guid EntityTwo { get; }

    /// <summary>
    /// Every application identifier that exists, plus one that belongs to
    /// nobody. Read from the database rather than listed here, and that is the
    /// difference between a sweep and a reminder: a list written by hand only
    /// ever contains the cases somebody remembered, while the database also
    /// holds whatever the other classes in the postgres collection put there,
    /// every one of which belongs to an organisation that is not the caller's.
    ///
    /// The two scenario rows are yielded first and unconditionally, so the
    /// test still has its guaranteed floor if a future fixture change stops
    /// other classes from leaving rows behind.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> EveryApplicationIdPlusAStrangerAsync()
    {
        await using var context = _database.CreateContext();

        var stored = await context.Applications
            .AsNoTracking()
            .Select(application => application.Id)
            .ToListAsync();

        return new[] { ApplicationOne, ApplicationTwo }
            .Concat(stored)
            .Distinct()
            .Append(Guid.NewGuid())
            .ToList();
    }

    public static async Task<PermissionScenario> CreateAsync(
        OcwipWebApplicationFactory factory,
        PostgresDatabaseFixture database)
    {
        var host = SessionTestHost.Create(
            factory,
            database,
            settings: new Dictionary<string, string?>
            {
                // Four accounts sign in per test; the IP limit from T-12.5 is
                // not what any of these tests is about.
                ["RateLimiting:PermitLimit"] = "200",
            },
            services: services => services.AddPolicyProbes());

        await using var context = database.CreateContext();

        var competition = TestCompetition.New("Konkurs uprawnieniowy");
        var entityOne = TestEntity.New("Stowarzyszenie A");
        var entityTwo = TestEntity.New("Fundacja B");
        context.Competitions.Add(competition);
        context.Entities.AddRange(entityOne, entityTwo);
        await context.SaveChangesAsync();

        var definition = TestApplicationChain.NewFormDefinition(competition.Id);
        context.FormDefinitions.Add(definition);
        await context.SaveChangesAsync();

        // One competition and one form version for both, which is the real
        // shape: two organisations competing in the same call for proposals is
        // exactly when reaching for the neighbour's application is tempting.
        var chainOne = new ApplicationChain(competition.Id, definition.Id, entityOne.Id);
        var chainTwo = new ApplicationChain(competition.Id, definition.Id, entityTwo.Id);

        var applicationOne = TestApplication.Submitted(chainOne, "001", Answers(MarkerOne));
        var applicationTwo = TestApplication.Draft(chainTwo, Answers(MarkerTwo));
        context.Applications.AddRange(applicationOne, applicationTwo);
        await context.SaveChangesAsync();

        return new PermissionScenario(
            host,
            database,
            applicationOne.Id,
            applicationTwo.Id,
            entityOne.Id,
            entityTwo.Id);
    }

    public HttpClient Anonymous() => SessionTestHost.RawClient(_host);

    public Task<HttpClient> ApplicantOneAsync() =>
        SignedInAsync(Role.Applicant, EntityOne);

    public Task<HttpClient> ApplicantTwoAsync() =>
        SignedInAsync(Role.Applicant, EntityTwo);

    public Task<HttpClient> OperatorAsync() => SignedInAsync(Role.Operator, entityId: null);

    public Task<HttpClient> ReviewerAsync() => SignedInAsync(Role.Reviewer, entityId: null);

    /// <summary>
    /// Creates an account, attaches it to the given organisation and signs it
    /// in over real HTTP. The role is set through UserManager because it is
    /// never granted over HTTP (see Models/Role.cs), and the sign in is real
    /// because a forged principal would prove the handler works while proving
    /// nothing about the pipeline that has to reach it.
    /// </summary>
    private async Task<HttpClient> SignedInAsync(Role role, Guid? entityId)
    {
        var email = SessionTestHost.Email(role.ToString().ToLowerInvariant());
        var user = await SessionTestHost.CreateAccountAsync(_host, email, role);

        if (entityId is not null)
        {
            using var scope = _host.Services.CreateScope();
            var data = scope.ServiceProvider
                .GetRequiredService<Ocwip.Api.Data.AppDbContext>();

            var stored = await data.Users.SingleAsync(x => x.Id == user.Id);
            stored.EntityId = entityId;
            await data.SaveChangesAsync();
        }

        var client = _host.CreateClient();
        var login = await client.PostAsJsonAsync(
            "/login", new { email, password = SessionTestHost.Password });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        return client;
    }

    private static string Answers(string marker) =>
        $"{{\"strona-1\":{{\"nazwa-zadania\":\"{marker}\"}}}}";
}
