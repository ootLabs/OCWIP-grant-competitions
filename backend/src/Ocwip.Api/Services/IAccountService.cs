using Ocwip.Api.Contracts;

namespace Ocwip.Api.Services;

/// <summary>
/// What happened, as far as the CALLER is allowed to know.
///
/// Two values, and the absence of a third is the whole design. Security rule 3
/// forbids answering differently for a taken and a free address, so "the account
/// was created" and "that address already has one" are the SAME value here.
/// There is no branch to get wrong, no code to compare against, and nothing an
/// endpoint could accidentally translate into 409.
///
/// This type exists because returning IdentityResult did not work: faking a
/// success meant returning IdentityResult.Failed with a made up code, which the
/// endpoint then read as a failure and answered 400 for a taken address and 201
/// for a free one. That is the leak this enum makes unrepresentable.
/// </summary>
internal enum RegistrationOutcome
{
    /// <summary>
    /// The request was accepted. Whether a row was written is deliberately not
    /// distinguishable from here.
    /// </summary>
    Accepted,

    /// <summary>
    /// The request itself cannot be registered: the password fails the policy.
    /// Says nothing about any existing account, which is why it may differ.
    /// </summary>
    Rejected,
}

internal sealed record RegistrationResult(
    RegistrationOutcome Outcome,
    IReadOnlyList<string> Errors)
{
    public static RegistrationResult Accepted { get; } =
        new(RegistrationOutcome.Accepted, []);

    public static RegistrationResult Rejected(IEnumerable<string> errors) =>
        new(RegistrationOutcome.Rejected, errors.ToList());
}

internal interface IAccountService
{
    Task<RegistrationResult> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default);
}
