namespace Ocwip.Api.Models.Forms;

/// <summary>
/// One reason a form definition was refused.
/// </summary>
/// <param name="Path">
/// Where in the document the problem sits, as a JSON path such as
/// "sections[1].fields[3].calculation". Technical on purpose: it is read by
/// the creator to put the operator back on the right field, not by a person.
/// </param>
/// <param name="Message">
/// What is wrong, in Polish, naming the field. The operator building a form is
/// the person who reads this, and a definition the renderer cannot draw is
/// refused at save time, which is the only moment somebody can still fix it.
/// </param>
public sealed record FormSchemaError(string Path, string Message);

/// <summary>
/// The answer of <see cref="FormSchemaValidator"/>: either a document that can
/// be rendered, or the complete list of what stops it.
///
/// The list is complete rather than first-failure, because an operator who
/// fixes one field and gets a new refusal for the next one gives up on the
/// fourth round.
/// </summary>
public sealed record FormSchemaValidationResult(
    FormDocument? Document,
    IReadOnlyList<FormSchemaError> Errors)
{
    public bool IsValid => Errors.Count == 0;

    public static FormSchemaValidationResult Valid(FormDocument document) =>
        new(document, []);

    public static FormSchemaValidationResult Invalid(
        IReadOnlyList<FormSchemaError> errors) =>
        new(null, errors);
}
