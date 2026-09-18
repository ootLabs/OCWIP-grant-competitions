namespace Ocwip.Api.Contracts;

/// <summary>
/// A person to ask about the competition (step 1.6).
///
/// Name and address of a member of staff, shown publicly because that is what
/// the report asks for: the address is a work contact published on purpose.
/// Nothing else off the account travels with it, so the record cannot grow a
/// field that was never meant to leave the panel.
/// </summary>
public sealed record CompetitionContactResponse(
    Guid UserId,
    string Name,
    string Email);
