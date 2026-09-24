namespace Ocwip.Api.Models.Forms;

/// <summary>
/// How much of a complete application the answers have to be (T-30).
/// </summary>
public enum AnswerStrictness
{
    /// <summary>
    /// An autosave. The form is filled in over weeks, so anything may still be
    /// missing or out of range; what is refused is only what the renderer
    /// could never have sent: a key the form does not have, a value of the
    /// wrong kind, a choice that is not on the list.
    /// </summary>
    Draft,

    /// <summary>
    /// Submission (T-33). Everything a draft is checked for, plus every
    /// visible required field answered, every range and every limit kept.
    /// </summary>
    Submission,
}

/// <summary>
/// One reason the answers were refused.
/// </summary>
/// <param name="Key">
/// The field it is about, in the renderer's own spelling: the field key, or
/// "table[row].column" for one cell (cellKey in
/// frontend/components/form-renderer/renderer-context.tsx). The same string on
/// both sides is what lets the form put the message under the right input
/// without guessing.
/// </param>
/// <param name="Message">What is wrong, in Polish, for the applicant.</param>
public sealed record AnswerError(string Key, string Message);

/// <summary>
/// The answer of <see cref="AnswerValidator"/>: at most one message per key,
/// the most useful one, and the complete list over the whole form.
/// </summary>
public sealed record AnswerValidationResult(IReadOnlyList<AnswerError> Errors)
{
    public bool IsValid => Errors.Count == 0;

    /// <summary>The shape a validation problem carries: key to messages.</summary>
    public IDictionary<string, string[]> ToProblemErrors() =>
        Errors.ToDictionary(
            error => error.Key,
            error => new[] { error.Message },
            StringComparer.Ordinal);
}
