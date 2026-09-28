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
