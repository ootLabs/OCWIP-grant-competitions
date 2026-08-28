using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Ocwip.Api.Data.Converters;

/// <summary>
/// Normalizes every <see cref="DateTimeOffset"/> to UTC on the way to
/// PostgreSQL and back.
///
/// Npgsql refuses to write a timestamptz from a value whose Offset is not zero,
/// so an operator submitting 2026-09-01T10:00:00+02:00 from a Polish browser
/// would hit "Cannot write DateTimeOffset with Offset=02:00:00" in SaveChanges.
/// A comment saying "UTC" is a wish; this converter is the contract.
///
/// Applied model wide in <c>AppDbContext.ConfigureConventions</c> so the rule is
/// one decision for every timestamp, including entities added later, instead of
/// a property setter that the next entity forgets to repeat.
/// </summary>
public sealed class UtcDateTimeOffsetConverter
    : ValueConverter<DateTimeOffset, DateTimeOffset>
{
    public UtcDateTimeOffsetConverter()
        : base(
            model => model.ToUniversalTime(),
            provider => provider.ToUniversalTime())
    {
    }
}
