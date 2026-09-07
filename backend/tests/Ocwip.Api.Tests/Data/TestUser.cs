using Ocwip.Api.Data;
using Ocwip.Api.Models;

namespace Ocwip.Api.Tests.Data;

/// <summary>
/// An account that satisfies every constraint. The hash is an obvious
/// placeholder rather than a real one: nothing here should look like a
/// credential, in a test file just as much as in production code.
///
/// Fills the normalized columns by hand, because these tests write through EF
/// and never touch UserManager, which is what would normally set them. Through
/// Identity's own normalizer (Data/EmailNormalizer.cs), so a seeded account is
/// never one UserManager would fail to find.
///
/// LockoutEnabled is stated rather than left implicit: a registered account has
/// it on, and an object that says nothing about it would hide the difference
/// between the value EF writes and the value the store defaults to.
/// </summary>
internal static class TestUser
{
    public static User New(string email, Role role = Role.Applicant) =>
        new()
        {
            FirstName = "Adam",
            LastName = "Testowy",
            Email = email,
            NormalizedEmail = EmailNormalizer.Normalize(email),
            UserName = email,
            NormalizedUserName = EmailNormalizer.Normalize(email),
            PasswordHash = "placeholder-not-a-hash",
            Role = role,
            EmailConfirmed = true,
            LockoutEnabled = true,
        };
}
