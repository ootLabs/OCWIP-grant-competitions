using Ocwip.Api.Services;
using Xunit;

namespace Ocwip.Api.Tests.Services;

/// <summary>
/// The verification mail said "ważny przez 24 godzin" and the reset mail
/// "przez 1 godzin" (P4-04), the first thing a new user reads from the system.
/// </summary>
public class PolishPluralTests
{
    [Theory]
    [InlineData(1, "1 godzinę")]
    [InlineData(2, "2 godziny")]
    [InlineData(4, "4 godziny")]
    [InlineData(5, "5 godzin")]
    [InlineData(12, "12 godzin")]
    [InlineData(14, "14 godzin")]
    [InlineData(21, "21 godzin")]
    [InlineData(22, "22 godziny")]
    [InlineData(24, "24 godziny")]
    [InlineData(48, "48 godzin")]
    public void Hours_takes_the_form_the_count_needs(int hours, string expected) =>
        Assert.Equal(expected, PolishPlural.Hours(hours));
}
