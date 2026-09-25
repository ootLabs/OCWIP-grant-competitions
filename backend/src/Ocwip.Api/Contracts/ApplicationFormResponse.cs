using System.Text.Json;

namespace Ocwip.Api.Contracts;

/// <summary>
/// The form document one application was started on, at the exact version
/// it was started on (T-30: "czyta definicję ze wskazanej wersji, nie z
/// najnowszej"). What the applicant's own renderer needs to draw the form
/// (T-34).
///
/// Kept off <see cref="ApplicationResponse"/> on purpose: that response is
/// also what autosave answers on every PUT, and repeating a document that
/// cannot change once the application exists on every field the applicant
/// types would be pure waste. This is read once, when the fill screen opens.
/// </summary>
public sealed record ApplicationFormResponse(int VersionNumber, JsonElement Definition);
