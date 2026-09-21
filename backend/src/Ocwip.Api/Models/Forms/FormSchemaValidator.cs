using System.Text.Json;

namespace Ocwip.Api.Models.Forms;

/// <summary>
/// The gate of the form contract (T-24): a definition either passes here or
/// never reaches form_definitions.definition.
///
/// The rule this enforces is one sentence: nothing may be stored that the
/// renderer would not know how to draw. The creator (T-26) and the renderer
/// (T-28) are two sides of this contract, and without a written-down schema
/// checked in one place they drift apart within a week.
///
/// What this does NOT do is check an APPLICANT'S answers. That is T-30, and it
/// reads the same document from the other end.
/// </summary>
public static class FormSchemaValidator
{
    public static FormSchemaValidationResult Validate(JsonElement definition)
    {
        var reader = new FormJsonReader();
        var document = FormDocumentParser.Parse(reader, definition);

        if (document is not null)
        {
            // Only once the outline parsed: a reference check over a half-read
            // document invents errors about fields that were merely unreadable,
            // and the operator chases the second message instead of the first.
            FormSchemaReferences.Check(reader, document);
        }

        return reader.HasErrors
            ? FormSchemaValidationResult.Invalid(reader.Errors)
            : FormSchemaValidationResult.Valid(document!);
    }
}
