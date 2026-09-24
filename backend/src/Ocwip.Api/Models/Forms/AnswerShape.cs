using System.Globalization;
using System.Text.Json;

namespace Ocwip.Api.Models.Forms;

/// <summary>
/// Whether one answer has the shape its field's kind takes (T-30): text for a
/// text field, a number for an amount, a value from the list for a choice.
///
/// Checked at both levels, a draft included, because the renderer can only
/// ever send these shapes (answer-types.ts). Anything else came from somebody
/// writing to the API by hand, and a draft that stores it would be read by the
/// print, the report and the agreement template as if the form had produced
/// it.
///
/// Tables are not here: a table is rows of these, and AnswerValidator walks
/// them.
/// </summary>
internal static class AnswerShape
{
    /// <summary>What the date input writes: "2026-10-25".</summary>
    private static readonly string[] DateFormats = ["yyyy-MM-dd"];

    /// <summary>
    /// What the datetime-local input writes, with or without seconds
    /// depending on the browser.
    /// </summary>
    private static readonly string[] DateTimeFormats =
        ["yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss"];

    /// <summary>
    /// The error for an answer that is present and not null, or null when
    /// its shape is right. Null itself is "not answered yet" for every kind
    /// and never reaches here.
    /// </summary>
    public static string? Check(FormField field, JsonElement value) =>
        field.Type switch
        {
            FormFieldType.ShortText or FormFieldType.LongText => Text(field, value),
            FormFieldType.Number or FormFieldType.Amount or FormFieldType.Percent =>
                value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out _)
                    ? null
                    : "Odpowiedź musi być liczbą.",
            FormFieldType.Date => Moment(value, DateFormats, "datą"),
            FormFieldType.DateTime => Moment(value, DateTimeFormats, "datą z godziną"),
            FormFieldType.YesNo or FormFieldType.Statement =>
                value.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? null
                    : "Odpowiedź musi być wyborem tak albo nie.",
            FormFieldType.SingleChoice => SingleChoice(field, value),
            FormFieldType.MultipleChoice => MultipleChoice(field, value),
            FormFieldType.File => File(value),
            // D11: a calculated value is computed from the answers, never
            // typed. Accepting one here would let a request carry its own
            // grant amount past every limit measured against it.
            FormFieldType.Calculated =>
                "To pole jest wyliczane i nie przyjmuje odpowiedzi.",
            _ => "Tego pola nie da się wypełnić.",
        };

    private static string? Text(FormField field, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            return "Odpowiedź musi być tekstem.";
        }

        // At both levels, unlike the minimum: the input itself stops at the
        // limit, so a longer text never came from the form.
        var length = value.GetString()!.Length;

        return field.MaxLength is { } max && length > max
            ? $"Przekroczono limit {max} znaków (jest {length})."
            : null;
    }

    private static string? Moment(JsonElement value, string[] formats, string named)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            return $"Odpowiedź musi być {named}.";
        }

        var text = value.GetString()!;

        // An empty string is what the input holds once it is cleared.
        if (text.Length == 0)
        {
            return null;
        }

        return DateTime.TryParseExact(
            text,
            formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _)
            ? null
            : $"Odpowiedź musi być {named}.";
    }

    private static string? SingleChoice(FormField field, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            return "Odpowiedź musi być jedną z opcji.";
        }

        var chosen = value.GetString()!;

        return chosen.Length == 0 || field.Options.Any(option => option.Value == chosen)
            ? null
            : "Tej odpowiedzi nie ma na liście wyboru.";
    }

    private static string? MultipleChoice(FormField field, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            return "Odpowiedź musi być listą zaznaczonych opcji.";
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String
                || !field.Options.Any(option => option.Value == item.GetString()))
            {
                return "Tej odpowiedzi nie ma na liście wyboru.";
            }

            if (!seen.Add(item.GetString()!))
            {
                return "Ta sama opcja jest zaznaczona dwa razy.";
            }
        }

        return null;
    }

    /// <summary>
    /// What the renderer keeps for a picked file until T-32 stores the file
    /// itself: its name and size, nothing more. The formats and the size
    /// ceiling are T-32's to check against the real upload, because a name
    /// and a number typed into a request prove nothing about a file.
    /// </summary>
    private static string? File(JsonElement value)
    {
        const string Wrong = "Załącznik musi mieć nazwę i rozmiar.";

        if (value.ValueKind != JsonValueKind.Object)
        {
            return Wrong;
        }

        var properties = value.EnumerateObject().ToList();

        if (properties.Count != 2
            || !value.TryGetProperty("name", out var name)
            || name.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(name.GetString())
            || !value.TryGetProperty("sizeBytes", out var size)
            || size.ValueKind != JsonValueKind.Number
            || !size.TryGetInt64(out var bytes)
            || bytes < 0)
        {
            return Wrong;
        }

        return null;
    }
}
