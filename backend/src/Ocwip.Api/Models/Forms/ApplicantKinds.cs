using System.Text.Json;

namespace Ocwip.Api.Models.Forms;

/// <summary>
/// "Rodzaj wnioskodawcy" at submission (T-94): read from the field with the
/// applicantType role, checked against the Podmiot's card, and frozen into
/// applications.applicant_type.
///
/// pola.md puts the kind in the application because one foundation applies
/// alone in one competition and as a group's patron in the next. The card
/// still narrows it: an organisation's card (Organisation or
/// PatronInformalGroup) applies as an organisation or as a patron, and an
/// informal group without a patron has no card to apply as anything else.
/// </summary>
internal static class ApplicantKinds
{
    /// <summary>
    /// The kind to store, or the field key and message when the answer does
    /// not fit the card. A form without the role gives the card's own type,
    /// which is how every application before T-94 was evaluated.
    /// </summary>
    public static (EntityType? Kind, string? FieldKey, string? Problem) Resolve(
        FormDocument form, JsonElement answers, EntityType cardType)
    {
        var field = form.Sections
            .SelectMany(section => section.Fields)
            .FirstOrDefault(candidate => candidate.Role == FormFieldRole.ApplicantType);

        if (field is null)
        {
            return (cardType, null, null);
        }

        // Required by the form itself, so the submission check has already
        // refused an empty one; this reads what passed.
        var answer = new AnswerCalculator(form, answers).Answer(field.Key);
        if (answer is not { ValueKind: JsonValueKind.String } text
            || !Enum.TryParse<EntityType>(text.GetString(), ignoreCase: false, out var kind))
        {
            return (null, field.Key, "Wybierz rodzaj wnioskodawcy.");
        }

        if (!Fits(cardType, kind))
        {
            return (null, field.Key, cardType is EntityType.InformalGroup
                ? "Dane wnioskodawcy opisują grupę nieformalną bez patrona, więc wniosek składa się jako grupa nieformalna. "
                    + "Organizacja albo patron grupy potrzebuje karty organizacji w zakładce \"Mój profil\"."
                : "Dane wnioskodawcy opisują organizację, więc wniosek składa się jako organizacja albo jako patron grupy nieformalnej.");
        }

        return (kind, null, null);
    }

    private static bool Fits(EntityType card, EntityType kind) =>
        card is EntityType.InformalGroup
            ? kind is EntityType.InformalGroup
            : kind is EntityType.Organisation or EntityType.PatronInformalGroup;
}
