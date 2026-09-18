namespace Ocwip.Api.Models
{
    public class Competition : IAuditedEntity
    {
        public Guid Id { get; set; }

        /// <summary>
        /// The number the organisation calls this competition by, in the shape
        /// "1/2026" (docs/runbook/pola.md, step 1.1). Unique, because it ends
        /// up on an agreement and in correspondence, where two competitions
        /// answering to one number is a problem nobody can sort out afterwards.
        /// Free text and not a generated sequence: the numbering is the
        /// organisation's, not ours.
        /// </summary>
        public string Number { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }

        private DateTimeOffset _startDate;
        private DateTimeOffset? _endDate;

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

        /// <summary>
        /// Null exactly when the intake is continuous, paired with
        /// <see cref="IsContinuousIntake"/> by a check constraint. Null here
        /// means "never closes", never "closed long ago": see the note in
        /// CompetitionLifecycle.
        /// </summary>
        public DateTimeOffset? EndDate
        {
            get => _endDate;
            set => _endDate = value is null ? null : ToWholeMinuteUtc(value.Value);
        }

        /// <summary>
        /// "Nabór ciągły" (docs/runbook/pola.md, step 1.1). Switches the
        /// closing date off rather than pushing it far into the future, because
        /// a far date is still a date and would eventually close the intake on
        /// its own.
        /// </summary>
        public bool IsContinuousIntake { get; set; }

        /// <summary>
        /// When an operator published this competition. Stamped by the publish
        /// transition, null while it is a draft, and never set by hand: it is a
        /// record of what happened, not a setting.
        ///
        /// The planned publication date from step 1.1 of the wizard is a
        /// different field and belongs to the rest of the wizard parameters
        /// (T-20a).
        /// </summary>
        public DateTimeOffset? PublishedAt { get; set; }

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

        /// <summary>
        /// The form definition version applicants fill in. One competition has
        /// many versions (they are versioned so that an application in progress
        /// does not break when the form is edited); this points at the one in
        /// force. Null while the competition is still being written.
        ///
        /// The foreign key is composite on (CompetitionId, FormDefinitionId),
        /// see CompetitionConfiguration: it stops a competition from pointing
        /// at another competition's form, which no single column key can.
        /// </summary>
        public Guid? FormDefinitionId { get; set; }

        public ICollection<FormDefinition> FormDefinitions { get; set; } = [];
        public ICollection<Application> Applications { get; set; } = [];
    }
}
