using System.Text;
using System.Text.RegularExpressions;
using Ocwip.Api.Contracts;

namespace Ocwip.Api.Services.Export;

/// <summary>
/// The list of applications as a spreadsheet (T-35): CSV that Excel and
/// LibreOffice open with Polish characters intact and amounts as numbers.
///
/// CSV rather than XLSX, decided with the user on 2026-09-25: no new
/// dependency for a table of 120 rows. The three choices that make a Polish
/// spreadsheet open it without an import dialog are the byte order mark
/// (otherwise Excel reads UTF-8 as a Windows code page), the semicolon (a
/// comma is the decimal separator in pl-PL, so Excel splits on semicolons)
/// and CRLF line ends.
/// </summary>
internal static partial class ApplicationListCsv
{
    private const char Separator = ';';

    public static byte[] Build(ApplicationListResponse list)
    {
        var builder = new StringBuilder();

        void Row(params string[] cells) =>
            builder.Append(string.Join(Separator, cells.Select(Cell))).Append("\r\n");

        Row([.. ApplicationListLabels.Columns]);

        for (var i = 0; i < list.Applications.Count; i++)
        {
            var item = list.Applications[i];
            Row(
                (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                item.Number,
                item.EntityName,
                ApplicationListLabels.EntityType(item.EntityType),
                item.ProjectTitle ?? string.Empty,
                ApplicationListLabels.Amount(item.TotalCost),
                ApplicationListLabels.Amount(item.RequestedGrant),
                ApplicationListLabels.Status(item.Status),
                ApplicationListLabels.Moment(item.SubmittedAt));
        }

        builder.Append("\r\n");
        Row("Suma wnioskowanych kwot", ApplicationListLabels.Amount(list.RequestedTotal));
        Row("Pula konkursu", ApplicationListLabels.Amount(list.TotalPoolAmount));
        Row("Pozostało z puli", ApplicationListLabels.Amount(list.PoolRemaining));
        Row($"Daty według {ApplicationListLabels.TimeLabel}");

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(builder.ToString())];
    }

    /// <summary>
    /// Quoted when the separator, a quote or a line break would split it, and
    /// defused when it starts like a formula: a project title an applicant
    /// typed as "=HYPERLINK(...)" must reach the operator's spreadsheet as
    /// text, not run in it (CSV injection). The apostrophe is what the
    /// spreadsheet itself uses to say "this is text". A plain number is left
    /// alone, so an overdrawn pool stays the number "-500,00".
    /// </summary>
    internal static string Cell(string value)
    {
        if (value.Length > 0
            && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r'
            && !PlainNumber().IsMatch(value))
        {
            value = "'" + value;
        }

        return value.IndexOfAny([Separator, '"', '\r', '\n']) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }

    [GeneratedRegex(@"^-?[0-9]+(,[0-9]+)?$")]
    private static partial Regex PlainNumber();
}
