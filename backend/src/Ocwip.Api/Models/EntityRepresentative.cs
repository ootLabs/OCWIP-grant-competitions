namespace Ocwip.Api.Models
{
    /// <summary>
    /// Sensitive Information: one person authorised to represent the
    /// organisation ("osoby uprawnione do reprezentowania", pola.md step
    /// 2.2). A natural person's name, so the column holding the list is in
    /// scope for encryption at rest in T-47a.
    /// </summary>
    public sealed record EntityRepresentative(string FirstName, string LastName, string Function);
}
