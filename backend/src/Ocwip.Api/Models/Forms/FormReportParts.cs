using System.Text.Json;

namespace Ocwip.Api.Models.Forms;

/// <summary>
/// The two properties a report form needs (T-50a, "było i jest"): where a
/// field takes its value from the application, and whether the applicant may
/// change it. Read here for every document; whether the document may carry
/// them at all is FormPurposeRules' question.
/// </summary>
internal static class FormReportParts
{
    public static bool ReadOnly(FormJsonReader reader, JsonElement element, string path) =>
        reader.BooleanProperty(element, "readOnly", path, required: false);

    /// <summary>
    /// The key of an application field (a table for a table, a column of the
    /// application's table for a column). Checked for shape only: the
    /// application form may have another version by the time a report is
    /// started, and a key it no longer has leaves the value empty.
    /// </summary>
    public static string? PrefillFrom(FormJsonReader reader, JsonElement element, string path, string named)
    {
        var key = reader.StringProperty(element, "prefillFrom", path, required: false);

        if (key is not null && !FormJsonReader.IsValidKey(key))
        {
            reader.Add(
                $"{path}.prefillFrom",
                $"Pole {named}: \"prefillFrom\" musi być kluczem pola wniosku (małe litery, cyfry, podkreślenia).");
            return null;
        }

        return key;
    }
}
