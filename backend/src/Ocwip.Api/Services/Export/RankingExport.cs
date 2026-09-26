using System.Globalization;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;

namespace Ocwip.Api.Services.Export;

/// <summary>One cell of the exported ranking: text, or an amount or score the spreadsheet can count with.</summary>
internal sealed record ExportCell(string Text, decimal? Number = null)
{
    public static ExportCell Of(decimal? number, string text) => new(text, number);
}

/// <summary>
/// The ranking list as rows (T-42a), built once and written three ways
/// (CSV, XLSX, PDF), so the three files cannot disagree about a column.
/// </summary>
internal sealed record RankingExport(
    string CompetitionNumber,
    string CompetitionTitle,
    DateTimeOffset? ApprovedAt,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<ExportCell>> Rows,
    IReadOnlyList<(string Label, decimal? Amount)> Totals)
{
    public static readonly IReadOnlyList<string> Headings =
    [
        "Miejsce", "Numer", "Nazwa podmiotu", "Tytuł projektu", "Punkty razem",
        "Kwota wnioskowana", "Kwota rekomendowana", "Kwota przyznana", "Wynik",
    ];

    public static RankingExport From(string number, string title, RankingResponse ranking)
    {
        var rows = ranking.Rows.Select(row => (IReadOnlyList<ExportCell>)
        [
            new(row.Rank?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, row.Rank),
            new(row.Number ?? string.Empty),
            new(row.EntityName),
            new(row.ProjectTitle ?? string.Empty),
            Score(row.TotalScore),
            Money(row.RequestedGrant),
            Money(row.RecommendedGrant),
            Money(row.AwardedGrant),
            new(Result(row, ranking.ResultsApprovedAt is not null)),
        ]).ToList();

        var pool = ranking.TotalPool;
        return new RankingExport(
            number,
            title,
            ranking.ResultsApprovedAt,
            Headings,
            rows,
            [
                ("Suma przyznanych kwot", ranking.AwardedTotal),
                ("Pula konkursu", pool),
                ("Pozostało z puli", pool is null ? null : pool - ranking.AwardedTotal),
            ]);
    }

    /// <summary>Before approval the result column says what the draft would give, marked as such.</summary>
    private static string Result(RankingRow row, bool approved) =>
        approved
            ? row.Status is ApplicationStatus.Submitted ? string.Empty : ApplicationListLabels.Status(row.Status)
            : row.AwardedGrant is not null ? "roboczo: dofinansowanie" : string.Empty;

    private static ExportCell Money(decimal? amount) => ExportCell.Of(amount, ApplicationListLabels.Amount(amount));

    private static ExportCell Score(decimal? score) =>
        ExportCell.Of(score, score?.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',') ?? string.Empty);
}
