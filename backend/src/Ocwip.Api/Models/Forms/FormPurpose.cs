namespace Ocwip.Api.Models.Forms;

/// <summary>
/// What a form definition is for (T-38, D16): the application an applicant
/// fills in, or one of the two evaluation cards. One contract and one
/// versioning mechanism for all three, so the creator grows out of one model
/// instead of two. Stored as text, like the competition status, so adding a
/// purpose (the report, B-04) never reinterprets a stored row.
/// </summary>
public enum FormPurpose
{
    /// <summary>First on purpose: every definition published before T-38
    /// is an application form, and so is one whose caller names nothing.</summary>
    Application,

    /// <summary>"Karta oceny formalnej": filled in by the operator's staff.</summary>
    FormalEvaluation,

    /// <summary>"Karta oceny merytorycznej": filled in by each expert.</summary>
    MeritEvaluation,

    /// <summary>
    /// "Sprawozdanie" (T-50a): filled in by the applicant after the project,
    /// with the application's values next to the execution.
    /// </summary>
    Report,
}

/// <summary>
/// What a document may carry given its purpose, checked once the document
/// parsed whole. Without this an evaluation card could be published with no
/// criterion to compute a result from, and an application form could carry a
/// score nobody reads.
/// </summary>
internal static class FormPurposeRules
{
    public static void Check(FormJsonReader reader, FormDocument document, FormPurpose purpose)
    {
        for (var s = 0; s < document.Sections.Count; s++)
        {
            var fields = document.Sections[s].Fields;

            for (var f = 0; f < fields.Count; f++)
            {
                CheckField(reader, fields[f], $"$.sections[{s}].fields[{f}]", purpose);
            }
        }

        if (purpose == FormPurpose.Report)
        {
            CheckReport(reader, document);
        }

        var roles = document.Sections.SelectMany(section => section.Fields).Select(field => field.Role).ToList();

        if (purpose == FormPurpose.FormalEvaluation && !roles.Contains(FormFieldRole.FormalCriterion))
        {
            reader.Add(
                "$.sections",
                "Karta oceny formalnej nie ma żadnego kryterium (pola tak albo nie z rolą "
                + "\"formalCriterion\"), więc nie da się z niej ustalić wyniku oceny.");
        }

        if (purpose == FormPurpose.MeritEvaluation && !roles.Contains(FormFieldRole.MeritScore))
        {
            reader.Add(
                "$.sections",
                "Karta oceny merytorycznej nie ma sumy punktów (pola wyliczanego z rolą "
                + "\"meritScore\"), więc wniosku nie da się umieścić na liście rankingowej.");
        }
    }

    private static void CheckField(FormJsonReader reader, FormField field, string path, FormPurpose purpose)
    {
        var named = $"\"{field.Key}\"";

        if (purpose != FormPurpose.Report)
        {
            // Both only mean something where there is an application to
            // take the value from: a report (T-50a).
            foreach (var column in (field.Table?.Columns ?? []).Prepend(field))
            {
                if (column.ReadOnly)
                {
                    reader.Add($"{path}.readOnly", $"Pole {named}: \"readOnly\" wolno tylko we wzorze sprawozdania.");
                }

                if (column.PrefillFrom is not null)
                {
                    reader.Add($"{path}.prefillFrom", $"Pole {named}: \"prefillFrom\" wolno tylko we wzorze sprawozdania.");
                }
            }
        }
        else
        {
            // A report asks the applicant about the project, it does not
            // score anything: no role, no points. appliesTo stays, for the
            // three kinds of applicant (4a, 4b, 4c).
            if (field.Role is not FormFieldRole.None)
            {
                reader.Add($"{path}.role", $"Pole {named}: wzór sprawozdania nie nosi ról.");
            }

            if (field.Points is not null)
            {
                reader.Add($"{path}.points", $"Pole {named}: wzór sprawozdania nie przyznaje punktów.");
            }

            return;
        }

        if (purpose == FormPurpose.Application)
        {
            // The applicant's renderer does not read either property yet, so a
            // form carrying one would show the applicant a question the
            // server then treats as not asked. Lifted when the front learns
            // them (T-40 renders the cards with the same engine).
            if (field.AppliesTo is not null)
            {
                reader.Add($"{path}.appliesTo", $"Pole {named}: \"appliesTo\" wolno dziś tylko na karcie oceny.");
            }

            if (field.Points is not null)
            {
                reader.Add($"{path}.points", $"Pole {named}: punkty wolno dziś tylko na karcie oceny.");
            }

            if (field.Role is not FormFieldRole.None && !FormFieldRoles.IsApplicationRole(field.Role))
            {
                reader.Add(
                    $"{path}.role",
                    $"Pole {named}: rola \"{FormFieldRoles.WireName(field.Role)}\" należy do karty oceny, "
                    + "nie do formularza wniosku.");
            }

            return;
        }

        if (FormFieldRoles.IsApplicationRole(field.Role))
        {
            reader.Add(
                $"{path}.role",
                $"Pole {named}: rola \"{FormFieldRoles.WireName(field.Role)}\" należy do formularza "
                + "wniosku, nie do karty oceny.");
            return;
        }

        var wrongCard = purpose == FormPurpose.FormalEvaluation
            ? field.Role is FormFieldRole.MeritScore or FormFieldRole.StrategicScore or FormFieldRole.RecommendedGrant
            : field.Role is FormFieldRole.FormalCriterion;

        if (wrongCard)
        {
            reader.Add(
                $"{path}.role",
                $"Pole {named}: rola \"{FormFieldRoles.WireName(field.Role)}\" należy do drugiej "
                + "karty oceny, nie do tej.");
        }
    }

    /// <summary>
    /// "Było i jest" held together (T-50a): a value the applicant may not
    /// change has to come from somewhere, a computed field computes and
    /// copies nothing, and a column is taken from the application only
    /// inside a table that is.
    /// </summary>
    private static void CheckReport(FormJsonReader reader, FormDocument document)
    {
        for (var s = 0; s < document.Sections.Count; s++)
        {
            var fields = document.Sections[s].Fields;
            for (var f = 0; f < fields.Count; f++)
            {
                var field = fields[f];
                var path = $"$.sections[{s}].fields[{f}]";
                var named = $"\"{field.Key}\"";

                if (FormFieldTypes.IsTable(field.Type))
                {
                    if (field.ReadOnly)
                    {
                        reader.Add(
                            $"{path}.readOnly",
                            $"Tabela {named}: tylko do odczytu bywają kolumny, nie cała tabela.");
                    }

                    var columns = field.Table?.Columns ?? [];
                    for (var c = 0; c < columns.Count; c++)
                    {
                        CheckValueSource(reader, columns[c], $"{path}.table.columns[{c}]", field.PrefillFrom is not null);
                    }

                    continue;
                }

                CheckValueSource(reader, field, path, parentPrefilled: true);
            }
        }
    }

    private static void CheckValueSource(FormJsonReader reader, FormField field, string path, bool parentPrefilled)
    {
        var named = $"\"{field.Key}\"";

        if (field.Type == FormFieldType.Calculated && (field.ReadOnly || field.PrefillFrom is not null))
        {
            reader.Add(path, $"Pole {named} jest wyliczane, więc niczego nie przepisuje z wniosku.");
            return;
        }

        if (field.PrefillFrom is not null && !parentPrefilled)
        {
            reader.Add(
                $"{path}.prefillFrom",
                $"Kolumna {named}: przepisuje wartość z wniosku tylko w tabeli, która sama ma \"prefillFrom\".");
        }

        // Required and read only is a question the applicant cannot answer:
        // when the application left it empty, the report could never be
        // submitted.
        if (field.ReadOnly && field.Required)
        {
            reader.Add(
                $"{path}.required",
                $"Pole {named} jest tylko do odczytu, więc nie może być wymagane: wnioskodawca nie uzupełni go sam.");
        }

        if (field.ReadOnly && field.PrefillFrom is null)
        {
            reader.Add(
                $"{path}.readOnly",
                $"Pole {named} jest tylko do odczytu, ale nie ma \"prefillFrom\": nie miałoby skąd wziąć wartości.");
        }
    }
}
