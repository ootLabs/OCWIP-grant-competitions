using System.Reflection;
using Ocwip.Api.Services;
using Xunit;

namespace Ocwip.Api.Tests.Services;

/// <summary>
/// The clock the product hands a person. The confirmation mail and the
/// confirmation PDF printed UTC while every screen said "czasu polskiego", so
/// an application submitted at 16:24 came with a paper proof saying 14:24.
/// </summary>
public class ReaderTimeTests
{
    /// <summary>Summer time in Poland: UTC+2, so 14:24 UTC is 16:24 on the wall.</summary>
    private static readonly DateTimeOffset Summer =
        new(2026, 9, 30, 14, 24, 0, TimeSpan.Zero);

    private static bool HasPolishZone =>
        TimeZoneInfo.TryFindSystemTimeZoneById("Europe/Warsaw", out _);

    [Fact]
    public void Moment_reads_the_polish_wall_clock()
    {
        // Both branches assert, rather than skipping: an image without the
        // time zone database must still print an hour and name it UTC, which
        // is the fallback the whole product already takes.
        if (HasPolishZone)
        {
            Assert.Equal("2026-09-30 16:24", ReaderTime.Moment(Summer));
            Assert.Equal("czasu polskiego", ReaderTime.Label);
        }
        else
        {
            Assert.Equal("2026-09-30 14:24", ReaderTime.Moment(Summer));
            Assert.Equal("czasu UTC", ReaderTime.Label);
        }
    }

    [Fact]
    public void Day_is_the_day_of_that_same_clock()
    {
        // 23:30 UTC on the 30th is already the 1st in Poland, and the day a
        // deadline falls on is the whole point of naming a clock.
        var lateEvening = new DateTimeOffset(2026, 9, 30, 23, 30, 0, TimeSpan.Zero);
        var expected = HasPolishZone ? new DateOnly(2026, 10, 1) : new DateOnly(2026, 9, 30);

        Assert.Equal(expected, ReaderTime.Day(lateEvening));
    }

    [Fact]
    public void Label_always_names_the_clock_the_moment_was_read_on()
    {
        // Whatever the image carries, the two agree: a printed hour is never
        // left for the reader to guess at.
        Assert.Contains(ReaderTime.Label, new[] { "czasu polskiego", "czasu UTC" });
    }

    /// <summary>
    /// The two documents that used to print UTC. Checked in the source,
    /// because building either one needs a whole saved application, and what
    /// went wrong was the format string, not the plumbing.
    /// </summary>
    [Theory]
    [InlineData("Services/ApplicationSubmissionService.cs")]
    [InlineData("Services/Pdf/ApplicationConfirmationPdfBuilder.cs")]
    public void The_submission_confirmation_prints_no_bare_utc(string relativePath)
    {
        var source = File.ReadAllText(SourcePath(relativePath));

        Assert.Contains("ReaderTime.Moment(submittedAt)", source);
        Assert.DoesNotContain("submittedAt.UtcDateTime", source);
    }

    private static string SourcePath(string relativePath)
    {
        // The test binary sits under bin/<config>/<tfm>, the sources three
        // levels up and across in src/Ocwip.Api.
        var here = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var repository = Path.GetFullPath(Path.Combine(here, "..", "..", "..", "..", ".."));

        return Path.Combine(repository, "src", "Ocwip.Api", relativePath);
    }
}
