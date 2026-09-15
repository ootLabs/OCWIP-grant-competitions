using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Ocwip.Api.Tests.Endpoints;
using Xunit;

namespace Ocwip.Api.Tests.Services;

/// <summary>
/// The role column reaching the signed in principal.
///
/// Worth its own test rather than being implied by a green sign in: there is no
/// role table for Identity to read (AppDbContext derives from
/// IdentityUserContext on purpose), so nothing fails loudly when the claim goes
/// missing. Every policy written in T-13.2 would simply deny everything, and
/// the symptom would show up in that card rather than in this one.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class RoleClaimsPrincipalFactoryTests
    : IClassFixture<OcwipWebApplicationFactory>
{
    private readonly OcwipWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public RoleClaimsPrincipalFactoryTests(
        OcwipWebApplicationFactory factory,
        PostgresDatabaseFixture database)
    {
        _factory = factory;
        _database = database;
    }

    [RequiresDatabaseTheory]
    [InlineData(Role.Applicant)]
    [InlineData(Role.Operator)]
    [InlineData(Role.Reviewer)]
    public async Task The_role_column_becomes_a_role_claim(Role role)
    {
        // Arrange
        var host = SessionTestHost.Create(_factory, _database);
        var user = await SessionTestHost.CreateAccountAsync(
            host, SessionTestHost.Email($"rola-{role}"), role);

        using var scope = host.Services.CreateScope();
        var factory = scope.ServiceProvider
            .GetRequiredService<IUserClaimsPrincipalFactory<User>>();

        // Act
        var principal = await factory.CreateAsync(user);

        // Assert
        // The standard claim type, so IsInRole and the policies of T-13.2 work
        // without a translation layer.
        Assert.True(principal.IsInRole(role.ToString()));
        Assert.Equal(
            role.ToString(),
            principal.FindFirstValue(ClaimTypes.Role));

        // Exactly one. Two role claims is how an account ends up holding a role
        // somebody thought they had removed.
        Assert.Single(principal.FindAll(ClaimTypes.Role));
    }
}
