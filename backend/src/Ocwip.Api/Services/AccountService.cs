
using Microsoft.AspNetCore.Identity;
using Ocwip.Api.Models;
using Ocwip.Api.Contracts;

namespace Ocwip.Api.Services
{
    public class AccountService : IAccountService
    {
        private readonly UserManager<User> _userManager;

        public AccountService(UserManager<User> userManager)
        {
            _userManager = userManager;
        }

        public async Task<IdentityResult> RegisterAsync(RegisterRequest request)
        {
            var existingUser = await _userManager.FindByEmailAsync(request.Email);

            if (existingUser != null)
            {
                return IdentityResult.Failed(new IdentityError
                {
                    Code = "Sukces",
                    Description = "Konto zostało utworzone." //Fake error for duplicate email.
                });
            }


            var user = new User
            {
                Email = request.Email.Trim().ToLowerInvariant(), //Email stores as lowercase to ensure case sensitive uniqueness.
                UserName = request.Email.Trim().ToLowerInvariant(),
                FirstName = request.FirstName,
                LastName = request.LastName,
                Role = Role.Applicant,
                Pesel = request.Pesel,
                IsVerified = false
            };

            var result = await _userManager.CreateAsync(user, request.Password);

            return result;
        }
        


    }
}
