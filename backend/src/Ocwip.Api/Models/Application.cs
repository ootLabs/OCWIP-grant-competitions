using System.Text.Json;
using Ocwip.Api.Authorization;

namespace Ocwip.Api.Models
{
    /// <summary>
    /// <see cref="IEntityScoped"/> is what lets the authorization handler
    /// answer "whose application is this" without knowing this type
    /// (T-13.2). The EntityId it needs is the column that was already here.
    /// </summary>
    public class Application : IAuditedEntity, IEntityScoped
    {
        public Guid Id { get; set; }

        /// <summary>
        /// Kept alongside <see cref="FormDefinitionId"/> even though the form
        /// definition already belongs to a competition.
        ///
        /// Two reasons. Reading it is the common case: the submission deadline
        /// lives on the competition and is checked on every save, so routing
        /// that through the form definition would put a join on the hottest
        /// path. And the pair cannot drift in the database, because the foreign
        /// key to form_definitions is composite, see ApplicationConfiguration.
        ///
        /// That last guarantee is the database's alone, not EF's. Setting both
        /// navigation properties at once, Competition to A together with a
        /// FormDefinition belonging to B, does not throw: EF quietly aligns
        /// CompetitionId to B, so the competition the caller passed in is
        /// discarded rather than rejected. Checking that the pair agrees stays
        /// at the API edge, and T-29 and T-33 have to remember it, because there
        /// the competition comes from the route and the form definition from the
        /// payload, and this property is the one the deadline check reads.
        /// </summary>
        public Guid CompetitionId { get; set; }
        public Competition Competition { get; set; } = null!;

        /// <summary>
        /// The entity that filed the application, which is not the account that
        /// filled it in: docs/slownik.md separates entity from user.
        /// </summary>
        public Guid EntityId { get; set; }
        public Entity Entity { get; set; } = null!;

        /// <summary>
        /// Points at a specific version of the form definition, not at the
        /// competition, because an operator may edit the form while the
        /// competition is open. Without the version an application filled in
        /// last week could no longer be rendered against this week's structure.
        /// </summary>
        public Guid FormDefinitionId { get; set; }
        public FormDefinition FormDefinition { get; set; } = null!;

        /// <summary>
        /// Answers stored as PostgreSQL JSONB. Their shape follows
        /// <see cref="FormDefinition.Definition"/>, so it cannot be modelled as
        /// columns: the form has 5 to 6 pages and every competition may shape
        /// them differently. The contract is settled together with the
        /// definition contract in card T-20.
        ///
        /// Holds personal data of the applying organisation and, from the
        /// agreement stage on, of natural persons. The answers of the fields
        /// the form marks sensitive are encrypted inside the document
        /// (T-47a, Models/Forms/SensitiveAnswers.cs).
        /// </summary>
        public JsonElement Answers { get; set; }

        public ApplicationStatus Status { get; set; } = ApplicationStatus.Draft;

        /// <summary>
        /// Set exactly when the status is anything but Draft, paired with it
        /// by a check constraint. Null on a draft, because a draft has no submission
        /// instant and 0001-01-01 would look like data.
        /// </summary>
        public DateTimeOffset? SubmittedAt { get; set; }

        /// <summary>
        /// Sensitive Information: the Podmiot's card as it stood when this
        /// application was submitted (T-93, pola.md "Kopia danych w złożonym
        /// wniosku"), serialized <see cref="Contracts.EntityCardData"/>. Null
        /// on a draft. Written once, in the same transaction that numbers the
        /// application, so a later change of address on the card never
        /// rewrites what the organiser already holds. Holds the names of the
        /// people who represent the organisation, so the card's sensitive
        /// fields are encrypted inside it (T-47a).
        /// </summary>
        public JsonElement? EntitySnapshot { get; set; }

        /// <summary>
        /// "Rodzaj wnioskodawcy" as submitted (T-94): the answer of the field
        /// with the applicantType role, or the card's type when the form has
        /// none. Null exactly on a draft. The evaluation cards pick their
        /// criteria by it (appliesTo), so a later change of the card cannot
        /// change the criteria of an application already submitted. Read
        /// through <see cref="KindOfApplicant"/>.
        /// </summary>
        public EntityType? ApplicantType { get; set; }

        /// <summary>The kind as submitted, or the Podmiot's type for a draft.</summary>
        public EntityType KindOfApplicant => ApplicantType ?? Entity.Type;

        /// <summary>
        /// The grant the operator awards (T-42), null for none. Written on the
        /// ranking list while the results are a draft, invisible outside the
        /// operator panel until they are approved; entering an amount is what
        /// funds the application (report). May be lower than requested.
        /// </summary>
        public decimal? AwardedGrant { get; set; }

        /// <summary>The operator's note next to the decision (report: "kolumna uwag"), for manual entries.</summary>
        public string? DecisionNote { get; set; }

        /// <summary>
        /// The number the applicant quotes in correspondence. Assigned at
        /// submission, so it is null on a draft and paired with the status by a
        /// check constraint: a draft that is never submitted must not burn a
        /// number, otherwise the register has gaps nobody can explain.
        ///
        /// That timing is an ASSUMPTION, see docs/model-danych.md. So is the
        /// scope of its uniqueness, which is one competition and not the whole
        /// database.
        /// </summary>
        public string? Number { get; set; }

        /// <summary>
        /// Soft delete flag, see the same field on Competition. Applications are
        /// the reason the rule exists: AGENTS.md keeps a retention of at least
        /// 5 years and deactivating a competition must not take its
        /// applications with it.
        /// </summary>
        public bool IsActive { get; set; } = true;

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }

        /// <summary>
        /// When the row was marked inactive. Null while it is active.
        /// </summary>
        public DateTimeOffset? DeactivatedAt { get; set; }

        public ICollection<Attachment> Attachments { get; set; } = [];

        /// <summary>
        /// Every status change this application has ever gone through
        /// (T-33), oldest first once loaded in order. Append-only, see
        /// Models/ApplicationStatusHistory.cs.
        /// </summary>
        public ICollection<ApplicationStatusHistory> StatusHistory { get; set; } = [];

        /// <summary>
        /// Every reviewer ever assigned to this application, active or
        /// revoked (T-37). See Models/ApplicationAssignment.cs, which
        /// EntityScopedHandler reads to decide whether a Reviewer may see it.
        /// </summary>
        public ICollection<ApplicationAssignment> Assignments { get; set; } = [];
    }
}
