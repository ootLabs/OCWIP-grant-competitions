namespace Ocwip.Api.Models
{
    /// <summary>
    /// What a signed in account is allowed to see. Three roles, three different
    /// systems, see docs/reguly-biznesowe.md. The role is a column on the
    /// account, never something a view infers.
    ///
    /// Applicant is deliberately the first value, so it is also the CLR default.
    /// Code that forgets to set a role then produces the least privileged
    /// account instead of the most privileged one, and forgetting is exactly the
    /// failure this enum has to survive.
    ///
    /// Stored as text (UserConfiguration), so the order here means nothing in the
    /// database and a value can be moved without rewriting rows. The names are
    /// the contract: renaming one is a data migration.
    /// </summary>
    public enum Role
    {
        /// <summary>
        /// Files applications. Sees its own account and its own applications and
        /// nothing else. The role every registration produces.
        /// </summary>
        Applicant,

        /// <summary>
        /// An OCWIP employee running the competition. Sees everything: every
        /// competition, every application, every agreement, live during the
        /// intake.
        ///
        /// Never granted by the application: no screen, no endpoint, no flag on
        /// registration. The supported paths are Admin/GrantRoleCommand.cs and a
        /// statement against the database, and the reason is in
        /// docs/architektura.md.
        /// </summary>
        Operator,

        /// <summary>
        /// Scores applications, and sees only the ones an operator assigned to
        /// it. The assignment itself is T-37 and waits on the review sheet we do
        /// not have, so today this role exists in the model and nowhere else.
        /// </summary>
        Reviewer
    }
}
