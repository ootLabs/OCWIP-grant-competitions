using Ocwip.Api.Models;

namespace Ocwip.Api.Tests.Data;

/// <summary>
/// A competition that satisfies every check constraint, so a test only has to
/// state the one field it is about. Shared, because every database test needs a
/// valid competition before it can say anything about form definitions.
/// </summary>
internal static class TestCompetition
{
    public static Competition New(string title = "Konkurs testowy") =>
        new()
        {
            Title = title,
            Description = "Opis konkursu testowego.",
            StartDate = new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2026, 9, 30, 8, 0, 0, TimeSpan.Zero),
            MaxGrantAmount = 5000m,
            Status = CompetitionStatus.Draft,
        };
}
