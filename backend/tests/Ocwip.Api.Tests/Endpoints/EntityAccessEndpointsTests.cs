using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Ocwip.Api.Tests.Models.Forms;
using Xunit;
using static Ocwip.Api.Tests.Endpoints.ApplicationTestHost;
using static Ocwip.Api.Tests.Endpoints.EntityCardEndpointsTests;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// Several people on one Podmiot card (T-93a, report step 2.2, decision 7):
/// a taken NIP leads to a request instead of a duplicate card, only the
/// founder lets somebody in, an operator only after seven days and with a
/// note, and access goes with the organisation, drafts included.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class EntityAccessEndpointsTests(OcwipWebApplicationFactory factory, PostgresDatabaseFixture database)
    : IClassFixture<OcwipWebApplicationFactory>
{
    private sealed record Person(HttpClient Client, string Email, Guid Id);

    private static async Task<Person> PersonAsync(WebApplicationFactory<Program> host, Role role = Role.Applicant)
    {
        var email = SessionTestHost.Email(role is Role.Operator ? "operator-dostepu" : "osoba");
        var user = await SessionTestHost.CreateAccountAsync(host, email, role);
        return new Person(await LoginAsync(host, email), email, user.Id);
    }

    private static async Task<MyEntityAccessRequest> AskAsync(Person person, string nip)
    {
        var response = await person.Client.PostAsJsonAsync("/me/access-requests", new EntityAccessRequestBody(Typed(nip)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MyEntityAccessRequest>())!;
    }

    private static Task<HttpResponseMessage> FounderDecidesAsync(Person founder, Guid entityId, Guid requestId, bool approve) =>
        founder.Client.PostAsJsonAsync(
            $"/me/entities/{entityId}/access-requests/{requestId}/decision", new EntityAccessDecisionBody(approve));

    [RequiresDatabaseFact]
    public async Task A_second_person_with_a_taken_nip_asks_and_after_approval_finishes_the_same_draft()
    {
        var emails = new RecordingEmailSender();
        var (host, clock) = CompetitionTestHost.Create(factory, database, s => s.AddSingleton<IEmailSender>(emails));
        var competition = await PublishedCompetitionWithFormAsync(host);
        clock.Now = CompetitionTestHost.Start.AddDays(1);

        var founder = await PersonAsync(host);
        var nip = TestEntity.NewNip();
        var card = await FoundAsync(founder.Client, OrganisationCard(name: "Fundacja Wspólna", nip: nip));
        var draft = await CreateAsync(founder.Client, competition.Id);
        await SaveAsync(founder.Client, draft.Id, FormDefinitionSamples.Parse("""{"opis":"Zaczęte przez prezeskę"}"""));

        // The colleague types the same NIP and is sent to ask, not to found.
        var colleague = await PersonAsync(host);
        var duplicate = await colleague.Client.PostAsJsonAsync("/me/entities", OrganisationCard(nip: nip));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var request = await AskAsync(colleague, nip);
        Assert.Equal(EntityAccessRequestStatus.Pending, request.Status);
        Assert.Equal("Fundacja Wspólna", request.EntityName);
        Assert.Equal(request.Id, (await AskAsync(colleague, nip)).Id);

        // The founder hears once, with whom to recognise.
        var asked = Assert.Single(emails.Sent, x => x.To == founder.Email);
        Assert.Contains(colleague.Email, asked.Body);

        // Before the founder says yes, nothing of the organisation.
        Assert.NotEqual(HttpStatusCode.OK, (await colleague.Client.GetAsync($"/applications/{draft.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await colleague.Client.GetAsync($"/me/entities/{card.Id}")).StatusCode);

        var pending = (await founder.Client.GetFromJsonAsync<List<PendingEntityAccessRequest>>(
            $"/me/entities/{card.Id}/access-requests"))!;
        Assert.Equal(colleague.Email, Assert.Single(pending).Email);

        Assert.Equal(HttpStatusCode.NoContent, (await FounderDecidesAsync(founder, card.Id, request.Id, approve: true)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await FounderDecidesAsync(founder, card.Id, request.Id, approve: true)).StatusCode);
        Assert.Single(emails.Sent, x => x.To == colleague.Email && x.Subject.StartsWith("Dostęp", StringComparison.Ordinal));

        // Access goes with the organisation: the card, the list and the
        // founder's own draft, which the colleague may finish.
        var cards = (await colleague.Client.GetFromJsonAsync<List<EntityCardSummary>>("/me/entities"))!;
        Assert.False(Assert.Single(cards).IsFounder);
        var shared = (await colleague.Client.GetFromJsonAsync<EntityCardResponse>($"/me/entities/{card.Id}"))!;
        Assert.Equal(2, shared.Members.Count);
        Assert.Contains(await ListMineAsync(colleague.Client), x => x.Id == draft.Id && x.EntityName == "Fundacja Wspólna");
        await SaveAsync(colleague.Client, draft.Id, FormDefinitionSamples.Parse("""{"opis":"Dokończone przez skarbnika"}"""));
        Assert.Equal("Dokończone przez skarbnika", (await GetAsync(founder.Client, draft.Id)).Answers.GetProperty("opis").GetString());

        // The request stays in the history with who decided it.
        await using var context = database.CreateContext();
        var stored = await context.EntityAccessRequests.SingleAsync(x => x.Id == request.Id);
        Assert.Equal(founder.Id, stored.DecidedById);
        Assert.False(stored.DecidedByOperator);
    }

    [RequiresDatabaseFact]
    public async Task Only_the_founder_decides_and_a_refusal_lets_nobody_in()
    {
        var (host, _) = CompetitionTestHost.Create(factory, database);
        var founder = await PersonAsync(host);
        var nip = TestEntity.NewNip();
        var card = await FoundAsync(founder.Client, OrganisationCard(nip: nip));

        var member = await PersonAsync(host);
        var joined = await AskAsync(member, nip);
        (await FounderDecidesAsync(founder, card.Id, joined.Id, approve: true)).EnsureSuccessStatusCode();

        var stranger = await PersonAsync(host);
        var refusedOne = await AskAsync(stranger, nip);

        // A member who did not found the card neither sees nor decides.
        Assert.Equal(HttpStatusCode.Forbidden, (await member.Client.GetAsync($"/me/entities/{card.Id}/access-requests")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await FounderDecidesAsync(member, card.Id, refusedOne.Id, approve: true)).StatusCode);

        // Somebody outside the card cannot even tell that it exists.
        var outsider = await PersonAsync(host);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.Client.GetAsync($"/me/entities/{card.Id}/access-requests")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await FounderDecidesAsync(outsider, card.Id, refusedOne.Id, approve: true)).StatusCode);

        // A request of another card is not this card's to decide.
        var otherCard = await FoundAsync(outsider.Client, OrganisationCard());
        Assert.Equal(HttpStatusCode.NotFound, (await FounderDecidesAsync(outsider, otherCard.Id, refusedOne.Id, approve: true)).StatusCode);

        // The founder sends no note; only an operator does.
        var withNote = await founder.Client.PostAsJsonAsync(
            $"/me/entities/{card.Id}/access-requests/{refusedOne.Id}/decision", new EntityAccessDecisionBody(true, "sprawdzone"));
        Assert.Equal(HttpStatusCode.BadRequest, withNote.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await FounderDecidesAsync(founder, card.Id, refusedOne.Id, approve: false)).StatusCode);

        var mine = (await stranger.Client.GetFromJsonAsync<List<MyEntityAccessRequest>>("/me/access-requests"))!;
        Assert.Equal(EntityAccessRequestStatus.Rejected, Assert.Single(mine).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Client.GetAsync($"/me/entities/{card.Id}")).StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Asking_for_a_nip_nobody_registered_or_for_one_s_own_card_is_refused()
    {
        var (host, _) = CompetitionTestHost.Create(factory, database);
        var person = await PersonAsync(host);
        var nip = TestEntity.NewNip();
        await FoundAsync(person.Client, OrganisationCard(nip: nip));

        var unknown = await person.Client.PostAsJsonAsync("/me/access-requests", new EntityAccessRequestBody(Typed(TestEntity.NewNip())));
        var own = await person.Client.PostAsJsonAsync("/me/access-requests", new EntityAccessRequestBody(nip));
        var invalid = await person.Client.PostAsJsonAsync("/me/access-requests", new EntityAccessRequestBody("1234567890"));

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, own.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task An_operator_steps_in_only_after_seven_days_and_says_how_it_was_checked()
    {
        var emails = new RecordingEmailSender();
        var (host, clock) = CompetitionTestHost.Create(factory, database, s => s.AddSingleton<IEmailSender>(emails));
        clock.Now = DateTimeOffset.UtcNow;

        var founder = await PersonAsync(host);
        var nip = TestEntity.NewNip();
        await FoundAsync(founder.Client, OrganisationCard(nip: nip));
        var requester = await PersonAsync(host);
        var request = await AskAsync(requester, nip);
        var operatorPerson = await PersonAsync(host, Role.Operator);

        var operatorClient = operatorPerson.Client;
        var decide = (EntityAccessDecisionBody body) =>
            operatorClient.PostAsJsonAsync($"/access-requests/{request.Id}/decision", body);

        // A founder who answers within the week is not overruled.
        Assert.Equal(HttpStatusCode.Conflict, (await decide(new EntityAccessDecisionBody(true, "telefon do prezeski"))).StatusCode);
        Assert.DoesNotContain(
            (await operatorClient.GetFromJsonAsync<List<EscalatedEntityAccessRequest>>("/access-requests/escalated"))!,
            x => x.Id == request.Id);

        // A week later every session of the week before has expired.
        clock.Now = DateTimeOffset.UtcNow + EntityAccessRequest.EscalationAge + TimeSpan.FromMinutes(1);
        operatorClient = await LoginAsync(host, operatorPerson.Email);
        var requesterClient = await LoginAsync(host, requester.Email);

        var escalated = (await operatorClient.GetFromJsonAsync<List<EscalatedEntityAccessRequest>>("/access-requests/escalated"))!;
        var listed = Assert.Single(escalated, x => x.Id == request.Id);
        Assert.Equal(requester.Email, listed.RequesterEmail);
        Assert.Equal(founder.Email, listed.FounderEmail);

        Assert.Equal(HttpStatusCode.BadRequest, (await decide(new EntityAccessDecisionBody(true))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await decide(new EntityAccessDecisionBody(true, "   "))).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await decide(new EntityAccessDecisionBody(true, "Odpis KRS, telefon do zarządu 7.10"))).StatusCode);

        await using var context = database.CreateContext();
        var stored = await context.EntityAccessRequests.SingleAsync(x => x.Id == request.Id);
        Assert.Equal(EntityAccessRequestStatus.Approved, stored.Status);
        Assert.True(stored.DecidedByOperator);
        Assert.Equal(operatorPerson.Id, stored.DecidedById);
        Assert.Equal("Odpis KRS, telefon do zarządu 7.10", stored.OperatorNote);
        Assert.True(await context.EntityMembers.AnyAsync(x => x.UserId == requester.Id && x.EntityId == stored.EntityId && x.IsActive));

        // Applicants have no business on the operator's list.
        Assert.Equal(HttpStatusCode.Forbidden, (await requesterClient.GetAsync("/access-requests/escalated")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await requesterClient.PostAsJsonAsync($"/access-requests/{request.Id}/decision", new EntityAccessDecisionBody(true, "ja"))).StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task An_account_with_two_cards_says_which_one_the_application_is_for()
    {
        var (host, clock) = CompetitionTestHost.Create(factory, database);
        var competition = await PublishedCompetitionWithFormAsync(host);
        clock.Now = CompetitionTestHost.Start.AddDays(1);

        var person = await PersonAsync(host);
        var foundation = await FoundAsync(person.Client, OrganisationCard(name: "Fundacja"));
        var group = await FoundAsync(person.Client, new EntityCardData(EntityType.InformalGroup, "Sąsiedzi"));

        var unsaid = await person.Client.PostAsJsonAsync($"/competitions/{competition.Id}/applications", new { });
        Assert.Equal(HttpStatusCode.BadRequest, unsaid.StatusCode);

        var forGroup = await person.Client.PostAsJsonAsync(
            $"/competitions/{competition.Id}/applications?entityId={group.Id}", new { });
        Assert.Equal(HttpStatusCode.Created, forGroup.StatusCode);
        var draft = (await forGroup.Content.ReadFromJsonAsync<ApplicationResponse>())!;

        await using (var context = database.CreateContext())
        {
            Assert.Equal(group.Id, await context.Applications.Where(x => x.Id == draft.Id).Select(x => x.EntityId).SingleAsync());
        }

        // Somebody else's card answers like no card at all.
        var other = await PersonAsync(host);
        var foreign = await other.Client.PostAsJsonAsync(
            $"/competitions/{competition.Id}/applications?entityId={foundation.Id}", new { });
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);

        // The header names a person who acts for several organisations.
        var me = (await person.Client.GetFromJsonAsync<CurrentUserResponse>("/me"))!;
        Assert.Null(me.EntityName);
    }
}
