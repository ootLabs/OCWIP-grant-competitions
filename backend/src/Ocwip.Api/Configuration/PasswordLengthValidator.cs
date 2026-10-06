using Microsoft.AspNetCore.Identity;
using Ocwip.Api.Models;

namespace Ocwip.Api.Configuration;

/// <summary>
/// The upper bound Identity does not have (S-17). Hashing is deliberately
/// slow, so the length of what gets hashed is the caller's lever on the
/// server's work: a password of a megabyte costs the same hundred thousand
/// iterations as a good one, only over far more data, and nothing in Identity
/// says no.
///
/// A validator rather than a check in one request contract, because every
/// path that sets a password (registration, reset, change) goes through
/// Identity and would otherwise need its own copy of the rule.
/// </summary>
internal sealed class PasswordLengthValidator : IPasswordValidator<User>
{
    /// <summary>
    /// Long enough that no passphrase anybody writes runs into it, short
    /// enough that the hash is bounded work.
    /// </summary>
    public const int MaxLength = 128;

    public Task<IdentityResult> ValidateAsync(UserManager<User> manager, User user, string? password) =>
        Task.FromResult(password is { Length: > MaxLength }
            ? IdentityResult.Failed(new IdentityError
            {
                Code = "PasswordTooLong",
                Description = $"Hasło może mieć najwyżej {MaxLength} znaków.",
            })
            : IdentityResult.Success);
}
