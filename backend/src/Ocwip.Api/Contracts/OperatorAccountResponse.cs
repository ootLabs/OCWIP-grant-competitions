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
