namespace Ocwip.Api.Models
{
    /// <summary>
    /// A login account. Not the same thing as the entity that files an
    /// application, see docs/slownik.md and <see cref="Entity"/>.
    ///
    /// No DataAnnotations here on purpose. Mapping lives in
    /// Data/Configurations/UserConfiguration.cs and input validation lives at
    /// the API edge (docs/konwencje.md), so annotations would be a second way of
    /// saying the same thing and the two would drift.
    /// </summary>
    public class User : IAuditedEntity
    {
        public Guid Id { get; set; }

        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;

        /// <summary>
        /// Unique, enforced by an index in the database and not by application
        /// code, because two accounts on one address break password reset.
        ///
        /// Uniqueness is currently case sensitive. Normalizing the address, and
        /// therefore deciding whether two spellings are one account, belongs to
        /// registration (T-12.1) and is listed as an open point in
        /// docs/model-danych.md.
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// A hash, never the password. AGENTS.md security rule 4: passwords and
        /// sensitive data never reach a log, so this field must not appear in
        /// any log message, error body or API response.
        /// </summary>
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// Which of the three systems this account sees, see
        /// <see cref="Models.Role"/>.
        ///
        /// Applicant by default, stated here and again as a column default in
        /// UserConfiguration.cs. That is not one decision written twice: an
        /// account can also be inserted by a statement that never reaches EF,
        /// and such an insert omitting the column has to land on the least
        /// privileged role rather than fail.
        ///
        /// Operator is never granted by the application. There is no screen and
        /// no endpoint for it, on purpose: an operator sees the personal data of
        /// every organisation, so a screen that hands out the role is also a way
        /// to obtain it through a mistake in the authorization rules. Granting
        /// goes through Admin/GrantRoleCommand.cs or straight through the
        /// database, see docs/architektura.md.
        /// </summary>
        public Role Role { get; set; } = Role.Applicant;

        /// <summary>
        /// Sensitive Information. In scope for encryption at rest in T-80.
        ///
        /// Nullable, because a PESEL only shows up at the agreement stage. A
        /// required column would force every account created before that point
        /// to carry a placeholder, and a placeholder in a PESEL column is the
        /// kind of value that survives every validation.
        ///
        /// The schema bounds the length and nothing more. Checking that the
        /// value is 11 digits with a valid checksum belongs to the write path
        /// that first accepts a PESEL, which is the agreement module, not here:
        /// docs/konwencje.md puts validation at the API edge.
        /// </summary>
        public string? Pesel { get; set; }

        public bool IsVerified { get; set; }

        /// <summary>
        /// Soft delete flag, see the same field on Competition. Accounts are
        /// never removed: retention is at least 5 years.
        /// </summary>
        public bool IsActive { get; set; } = true;

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }

        /// <summary>
        /// When the row was marked inactive. Null while it is active.
        /// </summary>
        public DateTimeOffset? DeactivatedAt { get; set; }

        /// <summary>
        /// One to one with <see cref="Entity"/>, and that is an ASSUMPTION to
        /// confirm, not a settled rule: we do not know whether several people in
        /// one organisation file applications from separate accounts. See the
        /// assumptions table in docs/model-danych.md.
        ///
        /// Nullable, because an operator and a reviewer are accounts without an
        /// entity. They work for OCWIP, they do not apply for a grant.
        /// </summary>
        public Guid? EntityId { get; set; }
        public Entity? Entity { get; set; }
    }
}
