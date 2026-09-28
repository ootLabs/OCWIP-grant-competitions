using System.Text.Json;
using Ocwip.Api.Models;
using Ocwip.Api.Models.Forms;

namespace Ocwip.Api.Services.Documents;

/// <summary>
/// The {{czlonkowie_grupy}} of a contract (T-45b, P17): the names of the
/// informal group's members, read from the table the application form marks
/// "groupMembers", its first short text column, in the order of the rows.
/// An organisation applying alone has no group, and the contract says so
/// instead of leaving a blank nobody can fill in.
/// </summary>
internal static class GroupMembersValue
{
    public const string NotApplicable = "nie dotyczy";

    public static string? Read(FormDocument? form, JsonElement answers, EntityType applicant)
    {
        if (applicant is EntityType.Organisation)
        {
            return NotApplicable;
        }

        if (form is null)
        {
            return null;
        }

        var calculator = new AnswerCalculator(form, answers, applicant);
        var table = form.Sections
            .Where(section => calculator.IsVisible(section.VisibleWhen))
            .SelectMany(section => section.Fields)
            .FirstOrDefault(field => field.Role == FormFieldRole.GroupMembers
                && calculator.IsApplicable(field) && calculator.IsVisible(field.VisibleWhen));
        var column = table?.Table?.Columns.FirstOrDefault(x => x.Type == FormFieldType.ShortText);

        if (table is null || column is null)
        {
            return null;
        }

        var names = calculator.Rows(table)
            .Select(row => row is { ValueKind: JsonValueKind.Object } cells
                && cells.TryGetProperty(column.Key, out var name)
                && name.ValueKind == JsonValueKind.String
                    ? name.GetString()?.Trim()
                    : null)
            .Where(name => !string.IsNullOrEmpty(name))
            .ToList();

        return names.Count > 0 ? string.Join(", ", names) : null;
    }
}
