using System.Text.Json;
using Ocwip.Api.Data.Encryption;

namespace Ocwip.Api.Models.Forms;

/// <summary>
/// The answers of the fields a form marks <c>sensitive</c> (T-47a), encrypted
/// inside the answers document before it is written. Only the service that
/// holds the form knows which answers those are, so this runs there and the
/// column's converter only decrypts (Data/Encryption/EncryptedConverters.cs).
///
/// A top level field is sensitive when it or one of its table columns is
/// marked: an answer is stored per top level field, so a table with one
/// sensitive column is encrypted whole.
/// </summary>
public static class SensitiveAnswers
{
    public const string Purpose = "applications.answers";
    public const string ReportPurpose = "reports.answers";

    public static IReadOnlySet<string> Keys(FormDocument? form)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        if (form is null)
        {
            return keys;
        }

        foreach (var field in form.Sections.SelectMany(x => x.Fields))
        {
            if (field.Sensitive || field.Table?.Columns.Any(x => x.Sensitive) == true)
            {
                keys.Add(field.Key);
            }
        }

        return keys;
    }

    /// <summary>
    /// The same keys read straight from a stored definition, for one that no
    /// longer passes today's form contract (a stricter rule since, R-34):
    /// Keys(null) would say "nothing is sensitive", and whoever rewrites the
    /// answers would then write them back in the clear. Only the marks are
    /// read, the same two places Keys looks at.
    /// </summary>
    public static IReadOnlySet<string> MarkedKeys(JsonElement definition)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        if (definition.ValueKind != JsonValueKind.Object
            || !definition.TryGetProperty("sections", out var sections)
            || sections.ValueKind != JsonValueKind.Array)
        {
            return keys;
        }

        foreach (var section in sections.EnumerateArray())
        {
            if (section.ValueKind != JsonValueKind.Object
                || !section.TryGetProperty("fields", out var fields)
                || fields.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var field in fields.EnumerateArray())
            {
                if (field.ValueKind == JsonValueKind.Object
                    && field.TryGetProperty("key", out var key) && key.ValueKind == JsonValueKind.String
                    && (Marked(field) || ColumnMarked(field)))
                {
                    keys.Add(key.GetString()!);
                }
            }
        }

        return keys;

        static bool Marked(JsonElement element) =>
            element.TryGetProperty("sensitive", out var sensitive) && sensitive.ValueKind == JsonValueKind.True;

        static bool ColumnMarked(JsonElement field) =>
            field.TryGetProperty("table", out var table) && table.ValueKind == JsonValueKind.Object
            && table.TryGetProperty("columns", out var columns) && columns.ValueKind == JsonValueKind.Array
            && columns.EnumerateArray().Any(column => column.ValueKind == JsonValueKind.Object && Marked(column));
    }

    /// <summary>
    /// A report's sensitive answers: its own marked fields, and every field
    /// prefilled from a sensitive answer of the application, so the copy is
    /// not the plaintext way to the same data.
    /// </summary>
    public static IReadOnlySet<string> ReportKeys(FormDocument? report, FormDocument? application)
    {
        var fromApplication = Keys(application);
        var keys = new HashSet<string>(Keys(report), StringComparer.Ordinal);

        foreach (var field in report?.Sections.SelectMany(x => x.Fields) ?? [])
        {
            if (field.PrefillFrom is { } source && fromApplication.Contains(source))
            {
                keys.Add(field.Key);
            }
        }

        return keys;
    }

    /// <summary>Runs on every write, sensitive keys or none: see EncryptedDocument.Protect on text that looks encrypted.</summary>
    public static JsonElement Protect(JsonElement answers, IReadOnlySet<string> keys, string purpose = Purpose) =>
        EncryptedDocument.Protect(answers, keys.Contains, purpose);
}
