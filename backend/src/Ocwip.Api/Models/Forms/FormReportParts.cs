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

    /// <summary>The date the contract was signed, for "realizacja od" (O-17).</summary>
    public const string ContractSignedOn = "contract.signedOn";

    /// <summary>
    /// The key of an application field (a table for a table, a column of the
    /// application's table for a column), one cell of an application's fixed
    /// table as "table.row.column" (the group leader's name, O-17), or
    /// <see cref="ContractSignedOn"/>. Checked for shape only: the
    /// application form may have another version by the time a report is
    /// started, and a key it no longer has leaves the value empty.
    /// </summary>
    public static string? PrefillFrom(FormJsonReader reader, JsonElement element, string path, string named)
    {
        var key = reader.StringProperty(element, "prefillFrom", path, required: false);

        if (key is not null && !IsSource(key))
        {
            reader.Add(
                $"{path}.prefillFrom",
                $"Pole {named}: \"prefillFrom\" musi być kluczem pola wniosku (małe litery, cyfry, podkreślenia), "
                + $"komórką tabeli wniosku \"tabela.wiersz.kolumna\" albo \"{ContractSignedOn}\".");
            return null;
        }

        return key;
    }

    /// <summary>The application field a source reads, the table for a cell; null for the contract's date.</summary>
    public static string? ApplicationKey(string source) =>
        source == ContractSignedOn ? null : source.Split('.')[0];

    private static bool IsSource(string key)
    {
        if (key == ContractSignedOn)
        {
            return true;
        }

        var parts = key.Split('.');
        return parts.Length is 1 or 3 && parts.All(FormJsonReader.IsValidKey);
    }
}
