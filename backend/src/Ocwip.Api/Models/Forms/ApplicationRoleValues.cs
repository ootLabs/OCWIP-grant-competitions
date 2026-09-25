using System.Text.Json;

namespace Ocwip.Api.Models.Forms;

/// <summary>
/// The project title, total cost and requested grant of one application,
/// read through the role markers of the form version it was filled in on
/// (T-35). Null when the form marks no field with the role, when the field
/// or its section is hidden by a condition (the answer left behind is not
/// part of the offer, the same rule the submission check follows), or when
/// an optional amount was left empty.
/// </summary>
internal sealed record ApplicationRoleValues(
    string? ProjectTitle,
    decimal? TotalCost,
    decimal? RequestedGrant)
{
    public static ApplicationRoleValues Read(FormDocument document, JsonElement answers)
    {
        var calculator = new AnswerCalculator(document, answers);
        string? title = null;
        decimal? cost = null;
        decimal? grant = null;

        foreach (var section in document.Sections)
        {
            if (!calculator.IsVisible(section.VisibleWhen))
            {
                continue;
            }

            foreach (var field in section.Fields)
            {
                if (field.Role == FormFieldRole.None || !calculator.IsVisible(field.VisibleWhen))
                {
                    continue;
                }

                switch (field.Role)
                {
                    case FormFieldRole.ProjectTitle:
                        title = Text(calculator.Answer(field.Key));
                        break;
                    case FormFieldRole.TotalCost:
                        cost = Amount(calculator, field);
                        break;
                    case FormFieldRole.RequestedGrant:
                        grant = Amount(calculator, field);
                        break;
                }
            }
        }

        return new ApplicationRoleValues(title, cost, grant);
    }

    private static string? Text(JsonElement? value) =>
        value is { ValueKind: JsonValueKind.String } text
            && !string.IsNullOrWhiteSpace(text.GetString())
            ? text.GetString()!.Trim()
            : null;

    /// <summary>A calculated field always has a value; a typed one only
    /// when something was typed, because an empty optional amount is not a
    /// grant of zero.</summary>
    private static decimal? Amount(AnswerCalculator calculator, FormField field) =>
        field.Type != FormFieldType.Calculated && AnswerValues.IsEmpty(calculator.Answer(field.Key))
            ? null
            : calculator.Value(field);
}
