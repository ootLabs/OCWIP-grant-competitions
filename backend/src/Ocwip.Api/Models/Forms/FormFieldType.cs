namespace Ocwip.Api.Models.Forms;

/// <summary>
/// Kinds of field a form definition can be built from (T-24).
///
/// Fifteen of them, taken from docs/runbook/pola.md, which derived the list by
/// taking the three real 2026 application templates apart. The card names
/// eight; the four missing money and time kinds plus the statement and the
/// calculated field are not cosmetic, because every one of them carries its
/// own validation and its own way of being printed.
///
/// There is deliberately no budget table kind. The budget is a
/// <see cref="RepeatableTable"/> whose value column is a
/// <see cref="Calculated"/> column, so nothing in the schema is stitched to
/// fit it.
/// </summary>
public enum FormFieldType
{
    /// <summary>
    /// Not a kind: the zero value of the enum, so a field nobody gave a kind
    /// cannot silently become a text box. AGENTS.md rule 1 applied to a struct.
    /// </summary>
    Unknown = 0,

    ShortText,
    LongText,
    Number,
    Amount,
    Percent,
    Date,
    DateTime,
    YesNo,
    SingleChoice,
    MultipleChoice,
    RepeatableTable,
    FixedTable,
    File,
    Statement,
    Calculated,
}

/// <summary>
/// The wire names of <see cref="FormFieldType"/> and the questions the rest of
/// the contract asks about a kind.
///
/// The names live here as a table rather than as serializer attributes,
/// because the parser has to answer "this kind does not exist" with a message
/// naming the field, and a converter can only throw. One table, so the name a
/// definition is written with and the name an error message lists cannot drift.
/// </summary>
public static class FormFieldTypes
{
    private static readonly Dictionary<string, FormFieldType> ByWireName =
        new(StringComparer.Ordinal)
        {
            ["shortText"] = FormFieldType.ShortText,
            ["longText"] = FormFieldType.LongText,
            ["number"] = FormFieldType.Number,
            ["amount"] = FormFieldType.Amount,
            ["percent"] = FormFieldType.Percent,
            ["date"] = FormFieldType.Date,
            ["dateTime"] = FormFieldType.DateTime,
            ["yesNo"] = FormFieldType.YesNo,
            ["singleChoice"] = FormFieldType.SingleChoice,
            ["multipleChoice"] = FormFieldType.MultipleChoice,
            ["repeatableTable"] = FormFieldType.RepeatableTable,
            ["fixedTable"] = FormFieldType.FixedTable,
            ["file"] = FormFieldType.File,
            ["statement"] = FormFieldType.Statement,
            ["calculated"] = FormFieldType.Calculated,
        };

    private static readonly Dictionary<FormFieldType, string> WireNames =
        ByWireName.ToDictionary(pair => pair.Value, pair => pair.Key);

    /// <summary>
    /// Kinds that carry a character limit, so the renderer can show a counter
    /// and the creator has something to set.
    /// </summary>
    private static readonly HashSet<FormFieldType> Textual =
    [
        FormFieldType.ShortText,
        FormFieldType.LongText,
    ];

    /// <summary>
    /// Kinds a calculation may read and a limit may be measured against. A sum
    /// over dates or over choices is not a mistake worth a message at fill-in
    /// time; it is a definition that must not reach the database.
    /// </summary>
    private static readonly HashSet<FormFieldType> Numeric =
    [
        FormFieldType.Number,
        FormFieldType.Amount,
        FormFieldType.Percent,
        FormFieldType.Calculated,
    ];

    private static readonly HashSet<FormFieldType> Choice =
    [
        FormFieldType.SingleChoice,
        FormFieldType.MultipleChoice,
    ];

    private static readonly HashSet<FormFieldType> Tables =
    [
        FormFieldType.RepeatableTable,
        FormFieldType.FixedTable,
    ];

    /// <summary>
    /// What a table column may be. No table inside a table and no attachment
    /// inside a row: both are shapes the renderer has no way to draw, and the
    /// place to refuse them is here rather than in the browser.
    /// </summary>
    private static readonly HashSet<FormFieldType> ColumnKinds =
    [
        FormFieldType.ShortText,
        FormFieldType.LongText,
        FormFieldType.Number,
        FormFieldType.Amount,
        FormFieldType.Percent,
        FormFieldType.Date,
        FormFieldType.DateTime,
        FormFieldType.YesNo,
        FormFieldType.SingleChoice,
        FormFieldType.MultipleChoice,
        FormFieldType.Calculated,
    ];

    public static bool TryParse(string name, out FormFieldType type) =>
        ByWireName.TryGetValue(name, out type);

    public static string WireName(FormFieldType type) =>
        WireNames.TryGetValue(type, out var name) ? name : "unknown";

    public static bool IsTextual(FormFieldType type) => Textual.Contains(type);

    public static bool IsNumeric(FormFieldType type) => Numeric.Contains(type);

    public static bool IsChoice(FormFieldType type) => Choice.Contains(type);

    public static bool IsTable(FormFieldType type) => Tables.Contains(type);

    public static bool IsAllowedInTable(FormFieldType type) =>
        ColumnKinds.Contains(type);

    /// <summary>
    /// Every kind in the order the documentation lists them, for tests and for
    /// the creator, which has to offer all of them.
    /// </summary>
    public static IReadOnlyCollection<FormFieldType> All => WireNames.Keys;
}
