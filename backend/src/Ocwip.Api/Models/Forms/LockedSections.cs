using System.Text.Json;

namespace Ocwip.Api.Models.Forms;

/// <summary>
/// The server side of "only the unlocked sections" (T-103): an answer in a
/// section the return did not unlock must come back exactly as it was stored.
/// The screen greys those fields out, but a lock only on the screen is a
/// suggestion (the same rule as the read only fields of a report, T-50a).
///
/// Calculated fields are left out: they follow from other answers, so an
/// unlocked cost changes a locked total without anybody typing into it.
/// A missing answer and a null one are the same answer.
/// </summary>
public static class LockedSections
{
    public const string Refusal = "Tej części wniosku nie odblokowano do poprawy. Zmienić można tylko sekcje wskazane w zwrocie.";

    public static IReadOnlyDictionary<string, string[]> Changes(
        FormDocument form, JsonElement stored, JsonElement proposed, Func<string, bool> unlocked)
    {
        var changes = new Dictionary<string, string[]>(StringComparer.Ordinal);

        foreach (var section in form.Sections.Where(x => !unlocked(x.Key)))
        {
            foreach (var field in section.Fields.Where(x => x.Type != FormFieldType.Calculated))
            {
                if (!Same(Answer(stored, field.Key), Answer(proposed, field.Key)))
                {
                    changes[field.Key] = [Refusal];
                }
            }
        }

        return changes;
    }

    private static bool Same(JsonElement? left, JsonElement? right) =>
        left is null || right is null ? left is null && right is null : JsonElement.DeepEquals(left.Value, right.Value);

    private static JsonElement? Answer(JsonElement answers, string key) =>
        answers.ValueKind == JsonValueKind.Object
            && answers.TryGetProperty(key, out var value)
            && value.ValueKind != JsonValueKind.Null
                ? value
                : null;
}
