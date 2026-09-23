using System.Text.Json;

namespace Ocwip.Api.Contracts;

/// <summary>
/// One autosave of a draft application (T-29). The body carries the whole set
/// of answers gathered so far and replaces what was stored: the form itself
/// tracks which field just changed, not this endpoint.
/// </summary>
/// <param name="Answers">
/// Shaped by the form definition the application points at. Kept as
/// JsonElement rather than typed here, same reason as FormDefinitionRequest:
/// what the applicant has typed is stored as typed. A draft is explicitly
/// allowed to be incomplete; T-30 owns full validation, at submission.
/// </param>
public sealed record SaveApplicationDraftRequest(JsonElement Answers);
