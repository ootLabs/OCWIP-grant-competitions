using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Ocwip.Api.Data.Converters;

/// <summary>
/// Normalizes to UTC like <see cref="UtcDateTimeOffsetConverter"/> and then drops
/// everything below the minute.
///
/// The competition deadline is a whole minute by requirement (T-11.3): the client
/// described it as "if somebody arrives at 12:05, they can no longer fill the
/// form in". Seconds and microseconds surviving in the column would make two
/// deadlines that render identically as 12:00 behave differently, and the one
/// that lost the race would have no way to see why.
///
/// Truncation goes down for both ends, so a stored deadline is never later than
/// the minute an operator typed.
///
/// Applied only to the competition window, not to audit timestamps: CreatedAt and
/// UpdatedAt keep full precision, because they answer "when exactly did this
/// happen" rather than "what did the operator promise".
/// </summary>
public sealed class WholeMinuteUtcConverter
    : ValueConverter<DateTimeOffset, DateTimeOffset>
{
    public WholeMinuteUtcConverter()
        : base(
            model => Truncate(model),
            provider => Truncate(provider))
    {
    }

    private static DateTimeOffset Truncate(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return utc.AddTicks(-(utc.Ticks % TimeSpan.TicksPerMinute));
    }
}
