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

        if (document is not null && !reader.HasErrors)
        {
            // Only once the outline parsed WHOLE. A reference check over a
            // half-read document invents errors about fields that were merely
            // unreadable, and its positions in the document are shifted by
            // every field that was dropped, so it would point the operator at
            // the wrong one.
            FormSchemaReferences.Check(reader, document);
        }

        return reader.HasErrors
            ? FormSchemaValidationResult.Invalid(reader.Errors)
            : FormSchemaValidationResult.Valid(document!);
    }
}
