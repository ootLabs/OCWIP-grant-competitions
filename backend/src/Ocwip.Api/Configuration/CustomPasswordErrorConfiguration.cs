using Microsoft.AspNetCore.Identity;

namespace Ocwip.Api.Configuration
{
    public class CustomPasswordErrorConfiguration : IdentityErrorDescriber
    {
        public override IdentityError PasswordTooShort(int length)
        => new()
        {
            Code = nameof(PasswordTooShort),
            Description = $"Hasło musi zawierać co najmniej 8 znaków."
        };

        public override IdentityError PasswordRequiresNonAlphanumeric()
            => new()
            {
                Code = nameof(PasswordRequiresNonAlphanumeric),
                Description = "Hasło musi zawierać co najmniej jeden znak specjalny."
            };

        public override IdentityError PasswordRequiresDigit()
            => new()
            {
                Code = nameof(PasswordRequiresDigit),
                Description = "Hasło musi zawierać co najmniej jedną cyfrę."
            };

        public override IdentityError PasswordRequiresUpper()
            => new()
            {
                Code = nameof(PasswordRequiresUpper),
                Description = "Hasło musi zawierać co najmniej jedną wielką literę."
            };

        public override IdentityError PasswordRequiresLower()
            => new()
            {
                Code = nameof(PasswordRequiresLower),
                Description = "Hasło musi zawierać co najmniej jedną małą literę."
            };
    }
}
