using Ocwip.Api.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Ocwip.Api.Tests.Data.Configurations
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
                PasswordHash = "placeholder-not-a-hash",
                Role = role,
                Pesel = pesel,
                IsVerified = true,
            };
    }
}
