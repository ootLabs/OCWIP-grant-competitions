namespace Ocwip.Api.Models
{
    public class Competition : IAuditedEntity
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }

        private DateTimeOffset _startDate;
        private DateTimeOffset _endDate;

        /// <summary>
        /// Stored in UTC, truncated to a whole minute.
        ///
        /// Truncation happens here and not in a value converter on purpose. EF
        /// applies a property converter to the other side of a comparison too,
        /// so a converter would rewrite <c>EndDate &gt;= now</c> at 12:00:45 into
        /// <c>EndDate &gt;= 12:00:00</c> and a competition closing at 12:00 would
        /// keep matching for another 59 seconds. That is the exact rule the
        /// truncation exists to protect (T-11.3), so it belongs on the write path
        /// only. The database enforces the same shape with two check constraints,
        /// which also covers inserts that never pass through a setter.
        ///
        /// UTC normalization stays a model wide converter, because unlike
        /// truncation it preserves the instant and is therefore harmless in a
        /// predicate.
        /// </summary>
        public DateTimeOffset StartDate
        {
            get => _startDate;
            set => _startDate = ToWholeMinuteUtc(value);
        }

        public DateTimeOffset EndDate
        {
            get => _endDate;
            set => _endDate = ToWholeMinuteUtc(value);
        }

        private static DateTimeOffset ToWholeMinuteUtc(DateTimeOffset value)
        {
            var utc = value.ToUniversalTime();
            return utc.AddTicks(-(utc.Ticks % TimeSpan.TicksPerMinute));
        }

        public decimal MaxGrantAmount { get; set; }
        public CompetitionStatus Status { get; set; }

        /// <summary>
        /// Soft delete flag. Rows are never removed: AGENTS.md, security rule 5,
        /// keeps a minimum retention of 5 years, so deletion means marking the
        /// row inactive.
        /// </summary>
        public bool IsActive { get; set; } = true;

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }

        /// <summary>
        /// When the row was marked inactive. Null while it is active, so an
        /// active competition never carries a fake date.
        /// </summary>
        public DateTimeOffset? DeactivatedAt { get; set; }

        public ICollection<FormDefinition> FormDefinitions { get; set; } = [];
    }
}
