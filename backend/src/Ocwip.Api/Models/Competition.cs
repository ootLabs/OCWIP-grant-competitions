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

        /// <summary>
        /// The window in the shape this entity stores it. Public because the
        /// API edge has to compare the two dates AFTER truncation: 12:00:30 and
        /// 12:00:45 are a valid looking pair that both collapse to 12:00, and a
        /// validator comparing what was typed would wave them through into a
        /// check constraint violation, which reaches the operator as a 500.
        /// </summary>
        public static DateTimeOffset ToWholeMinuteUtc(DateTimeOffset value)
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

        /// <summary>
        /// The formal evaluation card in force (T-38), pointed at the same way
        /// as FormDefinitionId and for the same reason. Null until one is
        /// published: a competition can take applications before its cards
        /// exist, it only cannot be evaluated.
        /// </summary>
        public Guid? FormalCardDefinitionId { get; set; }

        /// <summary>The merit evaluation card in force (T-38).</summary>
        public Guid? MeritCardDefinitionId { get; set; }

        // Evaluation settings (T-39, report step 5.0), set apart from the
        // announcement wizard through their own route so a wizard save never
        // resets them. Defaults follow the 2026 regulations.

        /// <summary>How many experts score one application ("2 niezależnych członków").</summary>
        public int EvaluatorsPerApplication { get; set; } = 2;

        /// <summary>How the experts' cards combine: 2026 adds them up (maximum 100).</summary>
        public ScoreAggregation ScoreAggregation { get; set; } = ScoreAggregation.Sum;

        /// <summary>
        /// The merit score an application needs to be evaluated positively
        /// (50 in 2026), null for no threshold at all. Not defaulted: a
        /// competition created before T-39 has regulations we have not read.
        /// </summary>
        public decimal? MeritThreshold { get; set; }

        /// <summary>Whether the strategic points count toward the threshold (not in 2026).</summary>
        public bool ThresholdIncludesStrategic { get; set; }

        /// <summary>
        /// Percent of the merit scale two cards may differ by before the
        /// ranking warns (the report suggests 30), null for no warning. P2 on
        /// B-02 asks whether 2026 uses it at all.
        /// </summary>
        public decimal? DivergenceThresholdPercent { get; set; }

        // Steps 1.2 to 1.6 of the announcement wizard (T-20a). The wizard
        // splits them across screens; they are one row here, because a
        // half filled competition is a normal state (validation does not block
        // moving between steps, completeness is checked at publication) and a
        // row per step would turn that into five rows to keep in step.

        /// <summary>
        /// "Zakładane rezultaty konkursu" (step 1.2). Optional.
        /// </summary>
        public string? ExpectedResults { get; set; }

        /// <summary>
        /// "Adres strony z regulaminem" (step 1.2). A link out, not a document
        /// we host: the rules live wherever the organisation publishes them.
        /// </summary>
        public string? RulesUrl { get; set; }

        /// <summary>
        /// "Informacja pokazywana po złożeniu wniosku" (step 1.1), shown on the
        /// screen. A separate field from <see cref="SubmissionEmailBody"/> on
        /// purpose: the report asks for both and they say different things.
        /// </summary>
        public string? SubmissionNotice { get; set; }

        /// <summary>
        /// "Treść wiadomości e-mail wysyłanej po złożeniu wniosku" (step 1.6).
        /// Stored here, sent by the mail path (T-17 and M4). Nothing sends it
        /// yet.
        /// </summary>
        public string? SubmissionEmailBody { get; set; }

        /// <summary>
        /// "Czy wymagane jest złożenie dokumentów w wersji papierowej"
        /// (step 1.3), off by default. There is no separate paper workflow: we
        /// do not compare the two versions and do not hold the evaluation
        /// waiting for an envelope, see docs/runbook/pola.md.
        /// </summary>
        public bool RequiresPaperSubmission { get; set; }

        /// <summary>
        /// Deadline for the paper version, set exactly when
        /// <see cref="RequiresPaperSubmission"/> is on, paired with it by a
        /// check constraint. In UTC and truncated to a whole minute like the
        /// intake window, because it is the same kind of deadline and a
        /// deadline that keeps seconds argues with the one that does not.
        /// </summary>
        public DateTimeOffset? PaperSubmissionDeadline
        {
            get => _paperSubmissionDeadline;
            set => _paperSubmissionDeadline =
                value is null ? null : ToWholeMinuteUtc(value.Value);
        }

        private DateTimeOffset? _paperSubmissionDeadline;

        /// <summary>
        /// Where the paper version is handed in. Set exactly when
        /// <see cref="RequiresPaperSubmission"/> is on.
        /// </summary>
        public string? PaperSubmissionAddress { get; set; }

        /// <summary>
        /// "Termin realizacji zadań od, do" (step 1.4): the frame a project has
        /// to fit inside. Dates, not moments: the report asks for a day, and a
        /// project does not start at 08:00.
        /// </summary>
        public DateOnly? ProjectStartDate { get; set; }

        public DateOnly? ProjectEndDate { get; set; }

        /// <summary>
        /// "Całkowita kwota na realizację zadań": the pool of the competition,
        /// shown to applicants for information. Not a limit anything is checked
        /// against, unlike <see cref="MaxGrantAmount"/>.
        /// </summary>
        public decimal? TotalPoolAmount { get; set; }

        /// <summary>
        /// "Minimalna dotacja na jeden wniosek". Optional, and when it is set
        /// it may not exceed <see cref="MaxGrantAmount"/>.
        /// </summary>
        public decimal? MinGrantAmount { get; set; }

        /// <summary>
        /// "Maksymalny procent kosztów pośrednich z dotacji" (10 in the 2026
        /// template). Counted from <see cref="PercentageBasis"/>.
        /// </summary>
        public decimal? MaxIndirectCostPercent { get; set; }

        /// <summary>
        /// "Maksymalny procent kosztów rozwoju instytucjonalnego" (50 for a
        /// non governmental organisation in the 2026 template, 30 for both
        /// informal group variants).
        /// </summary>
        public decimal? MaxInstitutionalDevelopmentPercent { get; set; }

        /// <summary>
        /// What the two percentages above are counted from. Defaults to the
        /// grant amount because that is what the 2026 template says, and stays
        /// a switch because next year's might not.
        /// </summary>
        public PercentageBasis PercentageBasis { get; set; } = PercentageBasis.GrantAmount;

        /// <summary>
        /// "Maksymalny średni roczny przychód organizacji z trzech ostatnich
        /// zamkniętych lat" (200 000 zł in the template), the threshold that
        /// cuts large organisations off. Zero is a legal value and means
        /// something quite different from null, which is why this is nullable
        /// rather than defaulting to 0: null is "no threshold".
        /// </summary>
        public decimal? MaxAverageAnnualRevenue { get; set; }

        /// <summary>
        /// "Data, do której przetwarzane będą dane osobowe" (step 1.4). Goes
        /// into the GDPR clause. May not fall earlier than five years after the
        /// intake closes, which is checked at the API edge; see the validator
        /// for what that floor is when the intake never closes.
        ///
        /// Personal data: this date governs how long applicants' personal data
        /// stays readable, so it is the field to look at when encryption and
        /// erasure are built.
        /// </summary>
        public DateOnly? PersonalDataProcessedUntil { get; set; }

        /// <summary>
        /// Upload limits from step 1.5, kept as competition settings rather
        /// than constants because the report lists them as settings. Enforced
        /// by the upload path in T-32; nothing checks them yet.
        /// </summary>
        public long MaxAttachmentSizeInBytes { get; set; } = DefaultMaxAttachmentSizeInBytes;

        public long MaxApplicationSizeInBytes { get; set; } = DefaultMaxApplicationSizeInBytes;

        /// <summary>10 MB per file and 50 MB per application, as the report
        /// sets them for 2026.</summary>
        public const long DefaultMaxAttachmentSizeInBytes = 10L * 1024 * 1024;

        public const long DefaultMaxApplicationSizeInBytes = 50L * 1024 * 1024;

        public ICollection<CompetitionAttachment> Attachments { get; set; } = [];

        public ICollection<CompetitionContact> Contacts { get; set; } = [];

        /// <summary>
        /// The cost categories this competition allows. Presence is the
        /// setting, see CompetitionCostCategory.
        /// </summary>
        public ICollection<CompetitionCostCategory> CostCategories { get; set; } = [];

        public ICollection<FormDefinition> FormDefinitions { get; set; } = [];
        public ICollection<Application> Applications { get; set; } = [];
    }
}
