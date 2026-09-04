using Ocwip.Api.Models;

namespace Ocwip.Api.Tests.Data;

/// <summary>
/// An account that satisfies every constraint. The hash is an obvious
/// placeholder rather than a real one: nothing here should look like a
/// credential, in a test file just as much as in production code.
///
/// Fills the normalized columns by hand, because these tests write through EF
/// and never touch UserManager, which is what would normally set them. The
/// upper casing has to match Identity's ToUpperInvariant, and
/// NormalizedAddressTests is what keeps the two in step.
/// </summary>
internal static class TestUser
{
    public static User New(string email, Role role = Role.Applicant) =>
        new()
        {
            FirstName = "Adam",
            LastName = "Testowy",
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            PasswordHash = "placeholder-not-a-hash",
            Role = role,
            EmailConfirmed = true,
        };
}
