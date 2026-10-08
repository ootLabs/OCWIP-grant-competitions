using Ocwip.Api.Models;

namespace Ocwip.Api.Contracts;

/// <summary>
/// The Podmiot's card (T-93, docs/runbook/pola.md step 2.2): what the
/// applicant fills in once, at the first application, and corrects from then
/// on. The body of POST /me/entities and PUT /me/entities/{id}, the data of
/// GET /me/entities/{id}, and the shape of the copy an application keeps from
/// the moment it was submitted (<see cref="ApplicationResponse.EntitySnapshot"/>).
///
/// Every field but <see cref="Type"/> and <see cref="Name"/> belongs to an
/// organisation card, which is also the patron's card of a group under
/// patronage. An informal group without a patron has no card (pola.md, type
/// 3): only its name is kept, and anything else sent for it is dropped.
/// </summary>
/// <param name="Type">
/// Which card this is: an organisation's (Organisation, or
/// PatronInformalGroup for a patron) or an informal group's. It narrows the
/// kind of applicant an application may name, and the application's own
/// answer is what its evaluation reads (T-94, applications.applicant_type).
/// </param>
/// <param name="Nip">Ten digits with a valid checksum; spaces and hyphens are dropped.</param>
/// <param name="BankAccount">NRB, 26 digits with a valid checksum; spaces and a leading "PL" are dropped.</param>
public sealed record EntityCardData(
    EntityType Type,
    string Name,
    LegalForm? LegalForm = null,
    string? LegalFormOther = null,
    EntityRegister? Register = null,
    string? RegisterNumber = null,
    string? Nip = null,
    string? Regon = null,
    string? Address = null,
    string? CorrespondenceAddress = null,
    string? Phone = null,
    string? Email = null,
    string? BankAccount = null,
    IReadOnlyList<EntityRepresentative>? Representatives = null);

/// <param name="UpdatedAt">
/// "Dane zaktualizowane", shown next to "dane są aktualne" on every
/// application after the first (pola.md, part I).
/// </param>
/// <param name="IsFounder">Whether the caller founded the card and so decides who joins it (T-93a).</param>
/// <param name="Members">Everybody with access to the card, the caller included (T-93a).</param>
public sealed record EntityCardResponse(
    Guid Id,
    DateTimeOffset UpdatedAt,
    EntityCardData Card,
    bool IsFounder,
    IReadOnlyList<EntityMemberResponse> Members);

/// <summary>
/// One person with access to a card, as the other members see them: a name,
/// nothing to contact them by. The founder is marked, because that is whom a
/// request to join goes to.
/// </summary>
public sealed record EntityMemberResponse(
    string FirstName,
    string LastName,
    bool IsFounder,
    DateTimeOffset Since);

/// <summary>
/// One of the cards the caller acts for, as "Mój profil" and the start of an
/// application list them (T-93a): enough to choose, not the whole card.
/// </summary>
public sealed record EntityCardSummary(
    Guid Id,
    EntityType Type,
    string Name,
    bool IsFounder,
    DateTimeOffset UpdatedAt);
