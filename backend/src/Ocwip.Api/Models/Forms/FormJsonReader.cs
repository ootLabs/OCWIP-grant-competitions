using System.Text.Json;

namespace Ocwip.Api.Models.Forms;

/// <summary>
/// Reads the primitives of a form definition and collects what was wrong
/// instead of throwing (T-24).
///
/// Throwing on the first bad property would turn a definition with four
/// mistakes into four save attempts, and the operator building the form is the
/// person who would make all four of them.
/// </summary>
internal sealed class FormJsonReader
{
    private readonly List<FormSchemaError> _errors = [];

    public IReadOnlyList<FormSchemaError> Errors => _errors;

    public bool HasErrors => _errors.Count > 0;

    public void Add(string path, string message) =>
        _errors.Add(new FormSchemaError(path, message));

    /// <summary>
    /// The shape of a key. Lower case with underscores, because keys end up in
    /// answer documents, in report columns and in document placeholders, and
    /// the one place they must never differ is between those three.
    /// </summary>
    public static bool IsValidKey(string key) =>
        key.Length is > 0 and <= 64
        && char.IsAsciiLetterLower(key[0])
        && key.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '_');

    /// <summary>
    /// Reads an enum written by NAME. Enum.TryParse also accepts the numbers
    /// behind the names, so "77" would come back as a defined-looking value
    /// nobody ever declared, and "0" would quietly become whichever member
    /// happens to be first.
    /// </summary>
    public static bool TryParseName<TEnum>(string? name, out TEnum value)
        where TEnum : struct, Enum
    {
        value = default;

        return name is not null
            && name.Length > 0
            && char.IsAsciiLetter(name[0])
            && Enum.TryParse(name, ignoreCase: true, out value)
            && Enum.IsDefined(value);
    }

    public JsonElement? ObjectProperty(
        JsonElement parent,
        string name,
        string path,
        bool required)
    {
        if (!parent.TryGetProperty(name, out var value)
            || value.ValueKind == JsonValueKind.Null)
        {
            if (required)
            {
                Add($"{path}.{name}", $"Brak wymaganej właściwości \"{name}\".");
            }

            return null;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            Add($"{path}.{name}", $"Właściwość \"{name}\" musi być obiektem.");
            return null;
        }

        return value;
    }

    public IReadOnlyList<JsonElement> ArrayProperty(
        JsonElement parent,
        string name,
        string path,
        bool required)
    {
        if (!parent.TryGetProperty(name, out var value)
            || value.ValueKind == JsonValueKind.Null)
        {
            if (required)
            {
                Add($"{path}.{name}", $"Brak wymaganej właściwości \"{name}\".");
            }

            return [];
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            Add($"{path}.{name}", $"Właściwość \"{name}\" musi być tablicą.");
            return [];
        }

        return [.. value.EnumerateArray()];
    }

    public string? StringProperty(
        JsonElement parent,
        string name,
        string path,
        bool required)
    {
        if (!parent.TryGetProperty(name, out var value)
            || value.ValueKind == JsonValueKind.Null)
        {
            if (required)
            {
                Add($"{path}.{name}", $"Brak wymaganej właściwości \"{name}\".");
            }

            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            Add($"{path}.{name}", $"Właściwość \"{name}\" musi być tekstem.");
            return null;
        }

        var text = value.GetString();

        if (required && string.IsNullOrWhiteSpace(text))
        {
            Add($"{path}.{name}", $"Właściwość \"{name}\" nie może być pusta.");
            return null;
        }

        return text;
    }

    public bool BooleanProperty(
        JsonElement parent,
        string name,
        string path,
        bool required)
    {
        if (!parent.TryGetProperty(name, out var value)
            || value.ValueKind == JsonValueKind.Null)
        {
            if (required)
            {
                Add($"{path}.{name}", $"Brak wymaganej właściwości \"{name}\".");
            }

            return false;
        }

        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return value.GetBoolean();
        }

        Add($"{path}.{name}", $"Właściwość \"{name}\" musi być prawdą albo fałszem.");
        return false;
    }

    public int? IntegerProperty(JsonElement parent, string name, string path)
    {
        if (!parent.TryGetProperty(name, out var value)
            || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number))
        {
            Add($"{path}.{name}", $"Właściwość \"{name}\" musi być liczbą całkowitą.");
            return null;
        }

        if (number < 0)
        {
            Add($"{path}.{name}", $"Właściwość \"{name}\" nie może być ujemna.");
            return null;
        }

        return number;
    }

    public decimal? DecimalProperty(JsonElement parent, string name, string path)
    {
        if (!parent.TryGetProperty(name, out var value)
            || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out var number))
        {
            Add($"{path}.{name}", $"Właściwość \"{name}\" musi być liczbą.");
            return null;
        }

        return number;
    }

    /// <summary>
    /// Values a condition compares against, read as text whatever they were
    /// written as. A yes or no answer and a choice value are compared the same
    /// way, and keeping two representations of "true" is how a condition stops
    /// matching after somebody retypes it.
    /// </summary>
    public IReadOnlyList<string> ComparableValues(
        IReadOnlyList<JsonElement> values,
        string path)
    {
        var result = new List<string>(values.Count);

        for (var index = 0; index < values.Count; index++)
        {
            var value = values[index];

            var text = value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Number => value.GetRawText(),
                _ => null,
            };

            if (text is null)
            {
                Add(
                    $"{path}[{index}]",
                    "Wartość warunku musi być tekstem, liczbą albo prawdą i fałszem.");
                continue;
            }

            result.Add(text);
        }

        return result;
    }
}
