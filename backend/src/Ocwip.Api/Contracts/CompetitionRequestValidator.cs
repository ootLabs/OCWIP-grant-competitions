using Ocwip.Api.Models;

namespace Ocwip.Api.Contracts;

/// <summary>
/// Shape of a competition request, checked at the API edge
/// (docs/konwencje.md) and answered in Polish, because a rejected form is text
/// somebody reads.
///
/// Only what the request can be judged on by itself. Whether the number is
/// already taken and whether the form version belongs to this competition need
/// the database and live in CompetitionService; whether the move through the
/// lifecycle is allowed needs the current state and lives in the transition
/// table.
///
/// The length limits repeat the column widths from CompetitionConfiguration on
/// purpose. Without them the answer to a too long title is a 500 out of
/// PostgreSQL, and the operator sees "coś poszło nie tak" instead of which
/// field to shorten.
/// </summary>
internal static class CompetitionRequestValidator
{
    /// <summary>Column widths, see CompetitionConfiguration.cs.</summary>
    private const int NumberLength = 50;

    private const int TitleLength = 200;

    private const int DescriptionLength = 10000;

    /// <summary>
    /// What numeric(18,2) holds, see CompetitionConfiguration.cs. Without this
    /// a larger figure reaches PostgreSQL as a numeric overflow and comes back
    /// as a 500, which is the failure the length limits above exist to avoid.
    /// </summary>
    private const decimal MaxAmount = 9_999_999_999_999_999.99m;

    /// <summary>
    /// The column keeps two decimal places and rounds the rest away silently,
    /// so 5000.005 would be answered as stored and read back as 5000.01.
    /// Money going into an agreement does not get rounded behind anybody's
    /// back. Tested by rounding rather than by counting digits, so a trailing
    /// 5000.000 typed by a spreadsheet passes and only a value that would
    /// actually CHANGE is refused.
    /// </summary>
    private const int AmountDecimals = 2;

    /// <summary>
    /// Whitespace around a pasted number or title is somebody's clipboard, not
    /// their intent. It has to go before the uniqueness check reads the
    /// number, or " 1/2026" and "1/2026" become two competitions the unique
    /// index is happy with and no person can tell apart.
    /// </summary>
    public static CompetitionRequest Trim(CompetitionRequest request) => request with
    {
        Number = request.Number?.Trim() ?? string.Empty,
        Title = request.Title?.Trim() ?? string.Empty,
        Description = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim(),
    };

    public static Dictionary<string, string[]> Validate(CompetitionRequest request)
    {
        var problems = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Number))
        {
            problems["number"] = ["Numer konkursu jest wymagany."];
        }
        else if (request.Number.Length > NumberLength)
        {
            problems["number"] =
                [$"Numer konkursu nie może być dłuższy niż {NumberLength} znaków."];
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            problems["title"] = ["Tytuł konkursu jest wymagany."];
        }
        else if (request.Title.Length > TitleLength)
        {
            problems["title"] =
                [$"Tytuł konkursu nie może być dłuższy niż {TitleLength} znaków."];
        }

        if (request.Description is { Length: > DescriptionLength })
        {
            problems["description"] =
                [$"Opis konkursu nie może być dłuższy niż {DescriptionLength} znaków."];
        }

        if (request.MaxGrantAmount <= 0)
        {
            problems["maxGrantAmount"] =
                ["Maksymalna kwota dotacji musi być większa od zera."];
        }
        else if (request.MaxGrantAmount > MaxAmount)
        {
            problems["maxGrantAmount"] =
                [$"Maksymalna kwota dotacji nie może przekraczać {MaxAmount:N2} zł."];
        }
        else if (decimal.Round(request.MaxGrantAmount, AmountDecimals)
            != request.MaxGrantAmount)
        {
            problems["maxGrantAmount"] =
                ["Kwota dotacji ma najwyżej dwa miejsca po przecinku."];
        }

        // The two columns are paired by a check constraint, and reaching it
        // would answer with a 500. D12 also asks a validation message to name
        // the consequence rather than the rule, which is what both of these do.
        if (request.IsContinuousIntake && request.EndDate is not null)
        {
            problems["endDate"] =
            [
                "Nabór ciągły nie ma terminu zakończenia. "
                + "Usuń datę zakończenia albo wyłącz nabór ciągły.",
            ];
        }
        else if (!request.IsContinuousIntake && request.EndDate is null)
        {
            problems["endDate"] =
            [
                "Podaj termin zakończenia naboru albo zaznacz nabór ciągły. "
                + "Bez tego nabór nigdy się nie zamknie.",
            ];
        }
        else if (request.EndDate is { } endDate
            && Competition.ToWholeMinuteUtc(endDate)
                <= Competition.ToWholeMinuteUtc(request.StartDate))
        {
            // Compared after truncation, because that is what gets stored.
            // 12:00:30 and 12:00:45 look like a window and are not one: both
            // land on 12:00, and the check constraint would answer with a 500.
            problems["endDate"] =
                ["Nabór musi się kończyć później, niż się zaczyna, i liczy się pełna minuta."];
        }

        return problems;
    }
}
