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
}
