using System.Text.Json;

namespace Ocwip.Api.Contracts;

/// <summary>
/// Publishing one version of the form of a competition (T-25).
///
/// The body carries the document and nothing else. The version number is not
/// here on purpose: it is a count of what has already been published, not a
/// value somebody types, and letting a caller name it would be the one way to
/// overwrite a version that applications already point at.
/// </summary>
/// <param name="Definition">
/// The form document, shaped by the contract in docs/kontrakt-formularza.md.
/// Kept as JsonElement rather than typed here, for the reason written on
/// FormDefinition.Definition: what the operator authored is stored as authored.
/// </param>
public sealed record FormDefinitionRequest(JsonElement Definition);
