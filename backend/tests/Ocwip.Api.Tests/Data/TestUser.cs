using Ocwip.Api.Data;
using Ocwip.Api.Models;


namespace Ocwip.Api.Tests.Data
{
    internal static class TestUser
    {
        public static User New(
            string email,
            Role role = Role.Applicant,
            string pesel = "90010112345") =>
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
                Pesel = pesel,
                EmailConfirmed = true,
            LockoutEnabled = true,
            };
    }
}
