namespace Ocwip.Api.Contracts;

/// <summary>
/// The body of POST /register.
///
/// No PESEL, deliberately. A PESEL appears at the agreement stage
/// (docs/model-danych.md), the column is nullable for exactly that reason, and
/// AGENTS.md requires sensitive data to be collected where it is needed rather
/// than as early as possible.
///
/// No entity either. Registration creating a Podmiot alongside the account
/// waits on the one to one assumption in docs/model-danych.md, tracked by B-09:
/// building it on an unconfirmed assumption buys a migration, not a feature.
/// </summary>
public sealed record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName)
{
    /// <summary>
    /// The generated ToString of a record prints every property, so the default
    /// one prints the password. Security rule 4 in AGENTS.md says a password
    /// never reaches a log, and the way that rule gets broken is not a line
    /// somebody writes on purpose: it is one interpolated string in a later
    /// card, in a log message or an exception, where the object looks harmless.
    /// Nothing logs this object today, which is exactly why the guard belongs
    /// on the type rather than on the call sites that do not exist yet.
    /// </summary>
    public override string ToString() => nameof(RegisterRequest);
}
