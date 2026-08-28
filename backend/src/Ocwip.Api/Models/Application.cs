using System.Text.Json;

namespace Ocwip.Api.Models
{
    public class Application : IAuditedEntity
    {
        public Guid Id { get; set; }

        /// <summary>
        /// Kept alongside <see cref="FormDefinitionId"/> even though the form
        /// definition already belongs to a competition.
        ///
        /// Two reasons. Reading it is the common case: the submission deadline
        /// lives on the competition and is checked on every save, so routing
        /// that through the form definition would put a join on the hottest
        /// path. And the pair cannot drift, because the foreign key to
        /// form_definitions is composite, see ApplicationConfiguration.
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
        /// agreement stage on, of natural persons. Sensitive, so it is in scope
        /// for encryption at rest in T-80.
        /// </summary>
        public JsonElement Answers { get; set; }

        public ApplicationStatus Status { get; set; } = ApplicationStatus.Draft;

        /// <summary>
        /// Set exactly when the status is Submitted, paired with it by a check
        /// constraint. Null on a draft, because a draft has no submission
        /// instant and 0001-01-01 would look like data.
        /// </summary>
        public DateTimeOffset? SubmittedAt { get; set; }

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
    }
}
