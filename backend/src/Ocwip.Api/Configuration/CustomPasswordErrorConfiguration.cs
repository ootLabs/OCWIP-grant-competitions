using Microsoft.AspNetCore.Identity;

namespace Ocwip.Api.Configuration;

/// <summary>
/// Polish wording for the password rules. Identity answers in English out of
/// the box, and docs/konwencje.md puts UI text in Polish: a rejected password
/// is text a person reads, not a diagnostic.
///
/// Only the password rules are translated. Everything else Identity can say is
/// either invisible to the caller or must not reach them at all, because
/// telling somebody that an address is taken is exactly what security rule 3
/// forbids.
/// </summary>
public sealed class CustomPasswordErrorConfiguration : IdentityErrorDescriber
{
    public override IdentityError PasswordTooShort(int length) => new()
    {
        Code = nameof(PasswordTooShort),
        // Taken from the option rather than written out, so changing
        // RequiredLength does not leave this message claiming the old number.
        Description = $"Hasło musi zawierać co najmniej {length} znaków.",
    };

    public override IdentityError PasswordRequiresDigit() => new()
    {
        Code = nameof(PasswordRequiresDigit),
        Description = "Hasło musi zawierać co najmniej jedną cyfrę.",
    };

    public override IdentityError PasswordRequiresUpper() => new()
    {
        Code = nameof(PasswordRequiresUpper),
        Description = "Hasło musi zawierać co najmniej jedną wielką literę.",
    };

    public override IdentityError PasswordRequiresLower() => new()
    {
        Code = nameof(PasswordRequiresLower),
        Description = "Hasło musi zawierać co najmniej jedną małą literę.",
    };

    public override IdentityError PasswordRequiresNonAlphanumeric() => new()
    {
        Code = nameof(PasswordRequiresNonAlphanumeric),
        Description = "Hasło musi zawierać co najmniej jeden znak specjalny.",
    };
}
