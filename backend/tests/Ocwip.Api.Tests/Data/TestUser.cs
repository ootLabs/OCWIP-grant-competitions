using Ocwip.Api.Models;

namespace Ocwip.Api.Tests.Data;

/// <summary>
/// An account that satisfies every constraint. The hash is an obvious
/// placeholder rather than a real one: nothing here should look like a
/// credential, in a test file just as much as in production code.
/// </summary>
internal static class TestUser
{
    public static User New(string email, Role role = Role.Applicant) =>
        new()
        {
            FirstName = "Adam",
            LastName = "Testowy",
            Email = email,
            PasswordHash = "placeholder-not-a-hash",
            Role = role,
            IsVerified = true,
        };
}
