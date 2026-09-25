namespace Ocwip.Api.Models
{
    /// <summary>
    /// An expert's impartiality declaration for one competition (T-40a):
    /// accepted, or refused with a reason. Until it is accepted the expert
    /// sees no application of that competition at all (report, decision 11;
    /// regulamin komisji 2026, § 2), which EntityScopedHandler and
    /// EvaluationAccessHandler enforce. Decided once: a refusal excludes the
    /// expert, and changing it is the operator's matter, not a second click.
    /// </summary>
    public class ReviewerDeclaration : IAuditedEntity
    {
        public Guid Id { get; set; }
        public Guid CompetitionId { get; set; }
        public Guid ReviewerId { get; set; }

        public bool Accepted { get; set; }

        /// <summary>Why the expert refused, required with a refusal and absent with an acceptance.</summary>
        public string? RefusalReason { get; set; }

        /// <summary>
        /// The text the expert saw when deciding, kept with the decision: the
        /// wording may change between editions, and "what exactly did they
        /// declare" has to have an answer for five years.
        /// </summary>
        public string DeclarationText { get; set; } = string.Empty;

        public DateTimeOffset DecidedAt { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public DateTimeOffset? DeactivatedAt { get; set; }
    }
}
