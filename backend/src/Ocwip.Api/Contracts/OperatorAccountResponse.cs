using Ocwip.Api.Models;

namespace Ocwip.Api.Contracts;

/// <summary>
/// One member of OCWIP staff, as the competition wizard offers them for step
/// 1.6 (T-22): "Osoby do kontaktu w konkursie: wybór z listy pracowników".
///
/// The wizard needs a name to show and an id to send back as one of
/// CompetitionRequest.ContactUserIds. Nothing else off the account travels
/// here, same reasoning as CompetitionContactResponse.
/// </summary>
public sealed record OperatorAccountResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email);

/// <summary>
/// One account of the OCWIP team (T-104): an operator or an expert, with its
/// role and whether it is active. Read only; roles are granted and accounts
/// deactivated by the server commands, never over HTTP.
/// </summary>
public sealed record TeamAccountResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    Role Role,
    bool IsActive);
