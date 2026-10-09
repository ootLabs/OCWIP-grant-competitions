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
    public const string EvaluationPurpose = "evaluations.answers";

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

        foreach (var field in Fields(definition))
        {
            if (field.TryGetProperty("key", out var key) && key.ValueKind == JsonValueKind.String
                && (Marked(field) || ColumnMarked(field)))
            {
                keys.Add(key.GetString()!);
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
    public static IReadOnlySet<string> ReportKeys(FormDocument? report, FormDocument? application) =>
        ReportKeys(report, Keys(application));

    /// <summary>The same, when the application's sensitive keys are already known.</summary>
    public static IReadOnlySet<string> ReportKeys(FormDocument? report, IReadOnlySet<string> fromApplication)
    {
        var keys = new HashSet<string>(Keys(report), StringComparer.Ordinal);

        foreach (var field in report?.Sections.SelectMany(x => x.Fields) ?? [])
        {
            // A cell of a sensitive table is as sensitive as the table.
            if (field.PrefillFrom is { } source
                && FormReportParts.ApplicationKey(source) is { } from
                && fromApplication.Contains(from))
            {
                keys.Add(field.Key);
            }
        }

        return keys;
    }

    /// <summary>
    /// The report's sensitive keys read straight from a stored definition,
    /// for one that no longer passes today's contract (R-34), the way
    /// <see cref="MarkedKeys"/> does it for an application. Without this the
    /// rotation would stop on such a report and leave it under the old key,
    /// which is the one a rotation is meant to retire.
    /// </summary>
    public static IReadOnlySet<string> MarkedReportKeys(JsonElement definition, IReadOnlySet<string> fromApplication)
    {
        var keys = new HashSet<string>(MarkedKeys(definition), StringComparer.Ordinal);

        foreach (var field in Fields(definition))
        {
            if (field.TryGetProperty("key", out var key) && key.ValueKind == JsonValueKind.String
                && field.TryGetProperty("prefillFrom", out var source) && source.ValueKind == JsonValueKind.String
                && fromApplication.Contains(source.GetString()!))
            {
                keys.Add(key.GetString()!);
            }
        }

        return keys;
    }

    private static IEnumerable<JsonElement> Fields(JsonElement definition)
    {
        if (definition.ValueKind != JsonValueKind.Object
            || !definition.TryGetProperty("sections", out var sections)
            || sections.ValueKind != JsonValueKind.Array)
        {
            yield break;
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
                if (field.ValueKind == JsonValueKind.Object)
                {
                    yield return field;
                }
            }
        }
    }

    /// <summary>Runs on every write, sensitive keys or none: see EncryptedDocument.Protect on text that looks encrypted.</summary>
    public static JsonElement Protect(JsonElement answers, IReadOnlySet<string> keys, string purpose = Purpose) =>
        EncryptedDocument.Protect(answers, keys.Contains, purpose);
}
