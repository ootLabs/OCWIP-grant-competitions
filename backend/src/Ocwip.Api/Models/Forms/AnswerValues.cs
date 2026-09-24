using System.Text.Json;

namespace Ocwip.Api.Models.Forms;

/// <summary>
/// How one stored answer is read (T-30), in the renderer's terms
/// (frontend/lib/forms/answer-types.ts, evaluate.ts, validate.ts). Two readers
/// of the same document that disagree about what "empty" means would have the
/// form say "ready" while the server says "missing".
/// </summary>
internal static class AnswerValues
{
    /// <summary>
    /// Not answered: absent, null, blank text or an empty list. False is an
    /// answer, because "no" is a complete answer to a yes or no question; the
    /// statement is the one kind that treats it as missing, and says so where
    /// it is checked.
    /// </summary>
    public static bool IsEmpty(JsonElement? value) =>
        value is not { } element
        || element.ValueKind switch
        {
            JsonValueKind.Undefined or JsonValueKind.Null => true,
            JsonValueKind.String => string.IsNullOrWhiteSpace(element.GetString()),
            JsonValueKind.Array => element.GetArrayLength() == 0,
            _ => false,
        };

    /// <summary>
    /// The number an answer stands for in a calculation. Anything that is not
    /// a number counts as zero, like the renderer: the shape check refuses it
    /// separately, and a calculation is not the place to say so twice.
    /// </summary>
    public static decimal Number(JsonElement? value) =>
        value is { ValueKind: JsonValueKind.Number } element
        && element.TryGetDecimal(out var number)
            ? number
            : 0m;

    /// <summary>
    /// Whether an answer is one of the values a condition lists. A yes or no
    /// answer is compared as "true" or "false", a multiple choice as "any of
    /// the ticked values", the same rules as conditionAnswerMatches in the
    /// renderer.
    /// </summary>
    public static bool Matches(JsonElement? value, IReadOnlyList<string> equalsAnyOf)
    {
        if (value is not { } element)
        {
            return false;
        }

        return element.ValueKind switch
        {
            JsonValueKind.True => equalsAnyOf.Contains("true"),
            JsonValueKind.False => equalsAnyOf.Contains("false"),
            JsonValueKind.String => equalsAnyOf.Contains(element.GetString()!),
            JsonValueKind.Array => element.EnumerateArray().Any(item =>
                item.ValueKind == JsonValueKind.String
                && equalsAnyOf.Contains(item.GetString()!)),
            _ => false,
        };
    }

    /// <summary>
    /// A property of an answer object, or null when it is not there.
    /// </summary>
    public static JsonElement? Property(JsonElement? container, string key) =>
        container is { ValueKind: JsonValueKind.Object } element
        && element.TryGetProperty(key, out var value)
            ? value
            : null;
}
