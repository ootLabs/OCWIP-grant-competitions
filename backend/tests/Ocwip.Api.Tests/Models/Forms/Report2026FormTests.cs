using System.Text.Json;
using System.Text.Json.Nodes;
using Ocwip.Api.Models;
using Ocwip.Api.Models.Forms;
using Ocwip.Api.Services.Reports;
using Xunit;

namespace Ocwip.Api.Tests.Models.Forms;

/// <summary>
/// The report form of Kierunek NOWE FIO 2026 (T-95), read from
/// backend/seed/forms/report-2026.json: templates 4a, 4b and 4c in one
/// document through appliesTo, taking the planned values from the 2026
/// application (prefillFrom) next to the executed ones.
/// </summary>
public sealed class Report2026FormTests
{
    private static JsonElement Definition()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "seed", "forms", "report-2026.json");

            if (File.Exists(path))
            {
                return JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone();
            }
        }

        throw new FileNotFoundException($"seed/forms/report-2026.json not found above {AppContext.BaseDirectory}");
    }

    private static FormDocument Form()
    {
        var result = FormSchemaValidator.Validate(Definition(), FormPurpose.Report);
        Assert.Empty(result.Errors);
        return result.Document!;
    }

    private static JsonObject Prefill(EntityType kind) =>
        ReportPrefill.Build(
            Form(), Application2026FormTests.Form(), AnswerSamples.Element(Application2026FormTests.Answers(kind)), kind);

    /// <summary>A complete report of one kind: the prefill plus what the applicant writes.</summary>
    private static JsonObject Answers(EntityType kind)
    {
        var answers = Prefill(kind);
        answers["realizacja_od"] = "2026-06-01";
        answers["osiagniecie_celu"] = "Cel osiągnięty: ogród działa.";
        answers["opis_dzialan"] = new string('d', 1000);
        answers["zmiany"] = "Sąsiedzi spotykają się co tydzień.";
        answers["liczba_uczestnikow_osiagnieta"] = 45;
        answers["promocja"] = "Plakaty i profil w mediach społecznościowych.";

        foreach (var table in new[] { "rezultaty", "koszty_bezposrednie", "koszty_promocji", "koszty_posrednie" })
        {
            foreach (var row in answers[table]!.AsArray().Select(x => x!.AsObject()))
            {
                if (table == "rezultaty")
                {
                    row["osiagniety"] = "12 plakatów";
                    continue;
                }

                row["dokument"] = "FV 1/2026";
                row["wartosc_calkowita"] = row["planowana"]!.DeepClone();
                row["z_dotacji"] = row["planowana"]!.DeepClone();
            }
        }

        if (kind is not EntityType.InformalGroup)
        {
            answers["osoba_sporzadzajaca"] = "Anna Testowa";
            answers["osoba_telefon"] = "700 100 200";
            answers["osoba_email"] = "anna@example.org";
            answers["reprezentanci"] = new JsonArray(new JsonObject { ["imie_i_nazwisko"] = "Jan Testowy", ["funkcja"] = "Prezes" });
            answers["organizacja_wolontariusze"] = true;
            answers["organizacja_wzrost"] = false;
        }

        if (kind is not EntityType.Organisation)
        {
            answers["lider_imie_i_nazwisko"] = "Anna Testowa";
            answers["lider_telefon"] = "700 100 200";
            answers["lider_email"] = "anna@example.org";
            answers["grupa_dalsze_dzialanie"] = true;
            answers["grupa_wzrost"] = true;
        }

        if (kind is EntityType.InformalGroup)
        {
            answers["lider_adres"] = "ul. Testowa 1, Opole";
        }

        return answers;
    }

    private static List<string> Asked(EntityType kind)
    {
        var calculator = new AnswerCalculator(Form(), AnswerSamples.Element(Answers(kind)), kind);
        return [.. Form().Sections.SelectMany(section => section.Fields)
            .Where(field => calculator.IsApplicable(field) && calculator.IsVisible(field.VisibleWhen))
            .Select(field => field.Key)];
    }

    [Theory]
    [InlineData(EntityType.Organisation)]
    [InlineData(EntityType.PatronInformalGroup)]
    [InlineData(EntityType.InformalGroup)]
    public void A_complete_report_of_every_kind_passes_the_submission_check(EntityType kind)
    {
        var result = AnswerValidator.Validate(
            Form(), AnswerSamples.Element(Answers(kind)), Application2026FormTests.Bases, AnswerStrictness.Submission, kind);

        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Each_kind_gets_its_own_template()
    {
        // 4a: the organisation, its representatives and its two questions.
        Assert.Contains("reprezentanci", Asked(EntityType.Organisation));
        Assert.DoesNotContain("lider_imie_i_nazwisko", Asked(EntityType.Organisation));
        Assert.DoesNotContain("grupa_wzrost", Asked(EntityType.Organisation));

        // 4b: the patron's representatives and the leader, all four questions.
        Assert.Contains("reprezentanci", Asked(EntityType.PatronInformalGroup));
        Assert.Contains("lider_telefon", Asked(EntityType.PatronInformalGroup));
        Assert.DoesNotContain("lider_adres", Asked(EntityType.PatronInformalGroup));
        Assert.Contains("organizacja_wzrost", Asked(EntityType.PatronInformalGroup));
        Assert.Contains("grupa_wzrost", Asked(EntityType.PatronInformalGroup));

        // 4c: the leader with an address, no organisation at all.
        Assert.Contains("lider_adres", Asked(EntityType.InformalGroup));
        Assert.DoesNotContain("reprezentanci", Asked(EntityType.InformalGroup));
        Assert.DoesNotContain("organizacja_wolontariusze", Asked(EntityType.InformalGroup));
        Assert.DoesNotContain("osoba_sporzadzajaca", Asked(EntityType.InformalGroup));
    }

    [Fact]
    public void The_application_values_come_over_next_to_the_executed_ones()
    {
        var prefill = Prefill(EntityType.PatronInformalGroup);

        Assert.Equal("Ogród sąsiedzki przy szkole", (string?)prefill["tytul_projektu"]);
        Assert.Equal("Sąsiedzi z Zaodrza", (string?)prefill["nazwa_grupy"]);
        Assert.Equal("2026-11-30", (string?)prefill["realizacja_do"]);
        Assert.Equal("Lewin Brzeski", (string?)prefill["gmina_realizacji"]);
        Assert.Equal(40m, (decimal?)prefill["liczba_uczestnikow_planowana"]);
        Assert.Equal("10 plakatów", (string?)prefill["rezultaty"]![0]!["planowany"]);

        var direct = prefill["koszty_bezposrednie"]![0]!;
        Assert.Equal(("Sadzonki", 5000m), ((string?)direct["pozycja"], (decimal?)direct["planowana"]));
        Assert.Equal(200m, (decimal?)prefill["koszty_promocji"]![0]!["planowana"]);
        Assert.Equal(500m, (decimal?)prefill["koszty_posrednie"]![0]!["planowana"]);
    }

    [Fact]
    public void The_settlement_counts_all_three_parts_of_the_budget()
    {
        var settlement = ReportSettlement.Compute(
            Form(), AnswerSamples.Element(Answers(EntityType.Organisation)), EntityType.Organisation, 7000m, [])!;

        Assert.Equal(["koszty_bezposrednie", "koszty_promocji", "koszty_posrednie"], settlement.Rows.Select(x => x.Budget));
        Assert.Equal(5700m, settlement.GrantSpent);
        Assert.Equal(1300m, settlement.Refund);
    }

    /// <summary>
    /// Who writes the report and who signs it are natural persons, so the
    /// form marks those fields sensitive and the column stores them
    /// encrypted (S-08). The same classes of data are encrypted in the
    /// entity card, so leaving them plain here was the cheaper way to the
    /// same names, phone numbers and addresses.
    /// </summary>
    [Fact]
    public void The_fields_holding_personal_data_are_marked_sensitive()
    {
        var marked = SensitiveAnswers.Keys(Form());

        Assert.Contains("osoba_sporzadzajaca", marked);
        Assert.Contains("osoba_telefon", marked);
        Assert.Contains("osoba_email", marked);
        Assert.Contains("reprezentanci", marked);
        Assert.Contains("lider_imie_i_nazwisko", marked);
        Assert.Contains("lider_adres", marked);
        Assert.Contains("lider_telefon", marked);
        Assert.Contains("lider_email", marked);

        // The amounts and the description are the report, not personal data.
        Assert.DoesNotContain("tytul_projektu", marked);
    }
}
