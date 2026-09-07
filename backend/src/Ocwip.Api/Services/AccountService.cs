
using Microsoft.AspNetCore.Identity;
using Ocwip.Api.Models;
using Ocwip.Api.Contracts;

namespace Ocwip.Api.Services
{
    public class AccountService : IAccountService
    {
        private readonly UserManager<User> _userManager;
        private readonly IEmailVerificationService _emailVerificationService;

        public AccountService(
            UserManager<User> userManager,
            IEmailVerificationService emailVerificationService)
        {
            _userManager = userManager;
            _emailVerificationService = emailVerificationService;
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
                // No Pesel: registration must not collect it (Models/User.cs).
                // It is filled in later, at the agreement stage.
            };

            var result = await _userManager.CreateAsync(user, request.Password);

            if (result.Succeeded)
            {
                await _emailVerificationService.SendVerificationAsync(user);
            }

            return result;
        }



    }
}
