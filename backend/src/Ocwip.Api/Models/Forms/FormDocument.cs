namespace Ocwip.Api.Models.Forms;

/// <summary>
/// A parsed form definition: what the JSONB document in
/// form_definitions.definition means (T-24).
///
/// The root is an OBJECT, not an array of sections. The check constraint in
/// the database allows either, and the contract picks the object because the
/// document has to carry its own schema version: an array leaves nowhere to
/// put it, and the first change to the contract would then be read by guessing
/// from the shape.
///
/// These types are the READING side of the contract. Nothing here is written
/// to the database directly; the definition is stored as it was authored, so a
/// property this version does not know yet survives a round trip instead of
/// being dropped by a serializer.
/// </summary>
/// <param name="SchemaVersion">
/// Version of THIS contract, not of the form. The form version is a column
/// (T-25), and the two answer different questions: one says which fields the
/// applicant saw, the other says how to read the document at all.
/// </param>
public sealed record FormDocument(
    int SchemaVersion,
    IReadOnlyList<FormSection> Sections)
{
    /// <summary>
    /// The only contract version that exists. A document declaring anything
    /// else is refused rather than read optimistically.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// Every field in the document, table columns included, in the order they
    /// are declared. The order is what conditions are checked against: a field
    /// may only be shown by an answer given ABOVE it.
    /// </summary>
    public IEnumerable<FormFieldPath> AllFields()
    {
        for (var s = 0; s < Sections.Count; s++)
        {
            var section = Sections[s];

            for (var f = 0; f < section.Fields.Count; f++)
            {
                var field = section.Fields[f];
                var path = $"$.sections[{s}].fields[{f}]";

                yield return new FormFieldPath(field.Key, field, null, path);

                if (field.Table is null)
                {
                    continue;
                }

                for (var c = 0; c < field.Table.Columns.Count; c++)
                {
                    yield return new FormFieldPath(
                        $"{field.Key}.{field.Table.Columns[c].Key}",
                        field.Table.Columns[c],
                        field,
                        $"{path}.table.columns[{c}]");
                }
            }
        }
    }
}

/// <summary>
/// A field together with the key it is referenced by. A table column is
/// referenced as "table.column", because the budget value column of table A
/// and of table C are two different things and a bare column key would make
/// them one.
/// </summary>
/// <param name="Table">
/// The table this field is a column of, or null for a field standing on its
/// own in a section.
/// </param>
/// <param name="JsonPath">
/// Where the field sits in the document, so a refusal can put the creator back
/// on it. Valid only for a document that parsed whole, which is why the
/// reference checks run only then.
/// </param>
public sealed record FormFieldPath(
    string Key,
    FormField Field,
    FormField? Table,
    string JsonPath);

/// <param name="VisibleWhen">
/// A whole section can be conditional too: the report has the institutional
/// development section switched off together with its cost category.
/// </param>
public sealed record FormSection(
    string Key,
    string Title,
    string? Description,
    FormCondition? VisibleWhen,
    IReadOnlyList<FormField> Fields);

/// <summary>
/// One field. The first eight properties are the minimum from pola.md, without
/// which a form cannot be reproduced.
/// </summary>
/// <param name="Help">
/// The hint shown under the field, always and quietly. Separate from an error,
/// which appears only when there is one.
/// </param>
/// <param name="Printed">
/// Decision D14: whether the field goes on the printed offer. Technical fields
/// exist only to compute something or to tie two sections together; they are
/// visible in the interface and absent from the print. Added later, this flag
/// would mean walking every existing definition and guessing.
/// </param>
/// <param name="Role">
/// What the field means outside the form (T-35): which one holds the project
/// title, the total cost and the requested grant the operator's list shows.
/// See FormFieldRole.
/// </param>
/// <param name="Limits">
/// Ceilings the answer is measured against, stated declaratively so that the
/// engine can INVERT them (decision D12): the message has to say "you may
/// still enter 900 zl", which needs the rule and the basis, not a yes or no.
/// </param>
public sealed record FormField(
    string Key,
    FormFieldType Type,
    string Label,
    string? Help,
    bool Required,
    bool Printed,
    int? MaxLength,
    int? MinLength,
    decimal? MinValue,
    decimal? MaxValue,
    FormCondition? VisibleWhen,
    IReadOnlyList<FormOption> Options,
    FormTable? Table,
    FormCalculation? Calculation,
    IReadOnlyList<FormLimit> Limits,
    FormFileRules? File,
    string? StatementText,
    FormFieldRole Role = FormFieldRole.None);

public sealed record FormOption(string Value, string Label);

/// <param name="Rows">
/// The fixed rows of a table of fixed size, for example the three members of
/// an informal group. Empty for a table the applicant adds rows to.
/// </param>
public sealed record FormTable(
    IReadOnlyList<FormField> Columns,
    IReadOnlyList<FormTableRow> Rows,
    int? MinRows,
    int? MaxRows);

public sealed record FormTableRow(string Key, string Label);

/// <summary>
/// Shows the field when the answer to <paramref name="Field"/> is one of
/// <paramref name="EqualsAnyOf"/>. Chosen over an expression on purpose: the
/// creator (T-26) has to let an operator set this by picking a field and a
/// value, and an expression would have to be typed.
/// </summary>
public sealed record FormCondition(
    string Field,
    IReadOnlyList<string> EqualsAnyOf);

/// <summary>
/// Where a calculated field takes its value from. Declarative rather than a
/// formula in a string, because D11 and D12 both need the rule read backwards:
/// the grant is what is left after the contributions, and a limit message has
/// to name the amount that is still free.
/// </summary>
public sealed record FormCalculation(
    FormCalculationKind Kind,
    IReadOnlyList<string> Operands);

public enum FormCalculationKind
{
    Unknown = 0,

    /// <summary>One operand, a table column: the sum down that column.</summary>
    Sum,

    /// <summary>Two or more operands multiplied: units times unit price.</summary>
    Product,

    /// <summary>Numerator and denominator, as a percentage.</summary>
    Ratio,

    /// <summary>The first operand minus the rest. D11 lives here.</summary>
    Difference,
}

/// <param name="Basis">
/// What the ceiling is measured against: another field of the form, or a
/// competition parameter written as "competition.maxGrantAmount". Keeping the
/// parameters out of the document is the point, because the same form serves
/// competitions with different limits.
/// </param>
/// <param name="PercentFrom">
/// For a percentage ceiling, the competition setting the percentage comes
/// from ("competition.maxIndirectCostPercent") instead of a number written
/// into the form (T-31). The threshold of cost table C is 10% in every 2026
/// template and the one for table B differs between them; either way it is
/// the competition's to set, and a percentage copied into the form is the
/// number that is wrong next year. Exactly one of this and Percent is set.
/// </param>
public sealed record FormLimit(
    FormLimitKind Kind,
    decimal? Percent,
    string Basis,
    string? PercentFrom = null);

public enum FormLimitKind
{
    Unknown = 0,

    /// <summary>The value may not exceed the basis.</summary>
    MaxAmount,

    /// <summary>The value may not exceed a percentage of the basis.</summary>
    MaxPercentOf,
}

/// <param name="MaxSizeMegabytes">
/// Per file. The whole-application ceiling is a competition setting, not a
/// form property, because it is about the upload and not about the question.
/// </param>
public sealed record FormFileRules(
    IReadOnlyList<AllowedFileFormat> AllowedFormats,
    int? MaxSizeMegabytes);
