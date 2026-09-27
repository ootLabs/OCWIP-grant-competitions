using Ocwip.Api.Models;

namespace Ocwip.Api.Contracts;

/// <summary>
/// The Podmiot's card (T-93, docs/runbook/pola.md step 2.2): what the
/// applicant fills in once, at the first application, and corrects from then
/// on. The body of POST and PUT /me/entity, the data of GET /me/entity, and
/// the shape of the copy an application keeps from the moment it was
/// submitted (<see cref="ApplicationResponse.EntitySnapshot"/>).
///
/// Every field but <see cref="Type"/> and <see cref="Name"/> belongs to an
/// organisation card, which is also the patron's card of a group under
/// patronage. An informal group without a patron has no card (pola.md, type
/// 3): only its name is kept, and anything else sent for it is dropped.
/// </summary>
/// <param name="Type">
/// Fixed once an application of this Podmiot has been submitted, because the
/// evaluation cards read it (AnswerCalculator). T-94 moves the kind of
/// applicant into the application, where pola.md puts it.
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
public sealed record EntityCardResponse(
    Guid Id,
    DateTimeOffset UpdatedAt,
    EntityCardData Card);
