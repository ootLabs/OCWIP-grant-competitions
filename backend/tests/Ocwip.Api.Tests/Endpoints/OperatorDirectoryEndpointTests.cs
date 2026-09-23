using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Xunit;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// GET /accounts/operators (T-22, step 1.6): who a competition may name as a
/// contact.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class OperatorDirectoryEndpointTests : IClassFixture<OcwipWebApplicationFactory>
{
    private readonly OcwipWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public OperatorDirectoryEndpointTests(
        OcwipWebApplicationFactory factory,
        PostgresDatabaseFixture database)
    {
        _factory = factory;
        _database = database;
    }

    private WebApplicationFactory<Program> Host() =>
        SessionTestHost.Create(_factory, _database);

    [RequiresDatabaseFact]
    public async Task Lists_only_active_operator_accounts_sorted_by_name()
    {
        // Arrange
        var host = Host();

        await SessionTestHost.CreateAccountAsync(
            host, SessionTestHost.Email("op-zet"), Role.Operator,
            firstName: "Zofia", lastName: "Zielińska");
        await SessionTestHost.CreateAccountAsync(
            host, SessionTestHost.Email("op-abc"), Role.Operator,
            firstName: "Adam", lastName: "Abacki");
        // Not an operator: must not show up in the picker.
        await SessionTestHost.CreateAccountAsync(
            host, SessionTestHost.Email("applicant"), Role.Applicant,
            firstName: "Jan", lastName: "Kowalski");
        // Deactivated operator: excluded, same rule CompetitionService checks
        // a contact against.
        await SessionTestHost.CreateAccountAsync(
            host, SessionTestHost.Email("op-gone"), Role.Operator,
            firstName: "Ewa", lastName: "Była", active: false);

        var client = await CompetitionTestHost.SignedInAs(host, Role.Operator);

        // Act
        var response = await client.GetAsync("/accounts/operators");

        // Assert
        response.EnsureSuccessStatusCode();
        var operators = await response.Content
            .ReadFromJsonAsync<List<OperatorAccountResponse>>();

        Assert.NotNull(operators);
        // The signed in caller is itself an active operator account (role
        // Operator, created by SignedInAs), so it is in the list too.
        var names = operators!
            .Select(account => (account.FirstName, account.LastName))
            .ToList();

        Assert.Contains(("Adam", "Abacki"), names);
        Assert.Contains(("Zofia", "Zielińska"), names);
        Assert.DoesNotContain(("Jan", "Kowalski"), names);
        Assert.DoesNotContain(("Ewa", "Była"), names);

        // Sorted by last name, then first name: "Abacki" before "Zielińska".
        var abackiIndex = names.IndexOf(("Adam", "Abacki"));
        var zielinskaIndex = names.IndexOf(("Zofia", "Zielińska"));
        Assert.True(abackiIndex < zielinskaIndex);
    }

    [RequiresDatabaseTheory]
    [InlineData(Role.Applicant)]
    [InlineData(Role.Reviewer)]
    public async Task Refuses_everyone_but_an_operator(Role role)
    {
        // Arrange
        var host = Host();
        var client = await CompetitionTestHost.SignedInAs(host, role);

        // Act
        var response = await client.GetAsync("/accounts/operators");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Refuses_an_anonymous_caller()
    {
        // Arrange
        var host = Host();
        var client = SessionTestHost.RawClient(host);

        // Act
        var response = await client.GetAsync("/accounts/operators");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
