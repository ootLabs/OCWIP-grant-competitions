using Ocwip.Api.Data.Configurations;
using Ocwip.Api.Models;

namespace Ocwip.Api.Contracts;

/// <summary>
/// Shape of a competition request, checked at the API edge
/// (docs/konwencje.md) and answered in Polish, because a rejected form is text
/// somebody reads.
///
/// Only what the request can be judged on by itself. Whether the number is
/// already taken and whether the form version belongs to this competition need
/// the database and live in CompetitionService; whether the move through the
/// lifecycle is allowed needs the current state and lives in the transition
/// table.
///
/// The length limits repeat the column widths from CompetitionConfiguration on
/// purpose. Without them the answer to a too long title is a 500 out of
/// PostgreSQL, and the operator sees "coś poszło nie tak" instead of which
/// field to shorten.
/// </summary>
internal static class CompetitionRequestValidator
{
    /// <summary>Column widths, see CompetitionConfiguration.cs.</summary>
    private const int NumberLength = 50;

    private const int TitleLength = 200;

    private const int DescriptionLength = 10000;

    /// <summary>
    /// What numeric(18,2) holds, see CompetitionConfiguration.cs. Without this
    /// a larger figure reaches PostgreSQL as a numeric overflow and comes back
    /// as a 500, which is the failure the length limits above exist to avoid.
    /// </summary>
    private const decimal MaxAmount = 9_999_999_999_999_999.99m;

    /// <summary>
    /// The column keeps two decimal places and rounds the rest away silently,
    /// so 5000.005 would be answered as stored and read back as 5000.01.
    /// Money going into an agreement does not get rounded behind anybody's
    /// back. Tested by rounding rather than by counting digits, so a trailing
    /// 5000.000 typed by a spreadsheet passes and only a value that would
    /// actually CHANGE is refused.
    /// </summary>
    private const int AmountDecimals = 2;

    /// <summary>Same for numeric(5,2) on the percentage columns.</summary>
    private const int PercentDecimals = 2;

    /// <summary>
    /// Retention floor from AGENTS.md, security rule 5, in years.
    /// </summary>
    private const int RetentionYears = 5;

    /// <summary>
    /// Column widths of the wizard parameters, see CompetitionConfiguration
    /// and CompetitionAttachmentConfiguration.
    /// </summary>
    private const int ExpectedResultsLength =
        CompetitionConfiguration.ExpectedResultsLength;

    private const int UrlLength = CompetitionConfiguration.UrlLength;

    private const int MessageLength = CompetitionConfiguration.MessageLength;

    private const int PaperAddressLength =
        CompetitionConfiguration.PaperAddressLength;

    private const int AttachmentTitleLength =
        CompetitionAttachmentConfiguration.TitleLength;

    private const int AttachmentDescriptionLength =
        CompetitionAttachmentConfiguration.DescriptionLength;

    /// <summary>
    /// A ceiling on the list, because nothing else bounds it and a request
    /// carrying ten thousand attachments is a way to make one save write ten
    /// thousand rows. The 2026 competition asks for four.
    /// </summary>
    private const int MaxAttachments = 30;

    /// <summary>The same ceiling for the contact list, and for the same
    /// reason. Every one of them is checked against the accounts table.</summary>
    private const int MaxContacts = 20;

    /// <summary>
    /// Whitespace around a pasted number or title is somebody's clipboard, not
    /// their intent. It has to go before the uniqueness check reads the
    /// number, or " 1/2026" and "1/2026" become two competitions the unique
    /// index is happy with and no person can tell apart.
    /// </summary>
    public static CompetitionRequest Trim(CompetitionRequest request) => request with
    {
        Number = request.Number?.Trim() ?? string.Empty,
        Title = request.Title?.Trim() ?? string.Empty,
        Description = Text(request.Description),
        ExpectedResults = Text(request.ExpectedResults),
        RulesUrl = Text(request.RulesUrl),
        SubmissionNotice = Text(request.SubmissionNotice),
        SubmissionEmailBody = Text(request.SubmissionEmailBody),
        PaperSubmissionAddress = Text(request.PaperSubmissionAddress),
        Attachments = request.Attachments is null
            ? null
            : [.. request.Attachments.Select(attachment => attachment with
            {
                Title = attachment.Title?.Trim() ?? string.Empty,
                Description = Text(attachment.Description),
            })],
    };

    /// <summary>
    /// Blank and missing are the same thing for an optional text: a field the
    /// operator cleared arrives as "" or as a line of spaces, and storing that
    /// would make "is the rules link set" a question with three answers.
    /// </summary>
    private static string? Text(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static Dictionary<string, string[]> Validate(CompetitionRequest request)
    {
        var problems = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Number))
        {
            problems["number"] = ["Numer konkursu jest wymagany."];
        }
        else if (request.Number.Length > NumberLength)
        {
            problems["number"] =
                [$"Numer konkursu nie może być dłuższy niż {NumberLength} znaków."];
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            problems["title"] = ["Tytuł konkursu jest wymagany."];
        }
        else if (request.Title.Length > TitleLength)
        {
            problems["title"] =
                [$"Tytuł konkursu nie może być dłuższy niż {TitleLength} znaków."];
        }

        if (request.Description is { Length: > DescriptionLength })
        {
            problems["description"] =
                [$"Opis konkursu nie może być dłuższy niż {DescriptionLength} znaków."];
        }

        if (request.MaxGrantAmount <= 0)
        {
            problems["maxGrantAmount"] =
                ["Maksymalna kwota dotacji musi być większa od zera."];
        }
        else if (request.MaxGrantAmount > MaxAmount)
        {
            problems["maxGrantAmount"] =
                [$"Maksymalna kwota dotacji nie może przekraczać {MaxAmount:N2} zł."];
        }
        else if (decimal.Round(request.MaxGrantAmount, AmountDecimals)
            != request.MaxGrantAmount)
        {
            problems["maxGrantAmount"] =
                ["Kwota dotacji ma najwyżej dwa miejsca po przecinku."];
        }

        // The two columns are paired by a check constraint, and reaching it
        // would answer with a 500. D12 also asks a validation message to name
        // the consequence rather than the rule, which is what both of these do.
        if (request.IsContinuousIntake && request.EndDate is not null)
        {
            problems["endDate"] =
            [
                "Nabór ciągły nie ma terminu zakończenia. "
                + "Usuń datę zakończenia albo wyłącz nabór ciągły.",
            ];
        }
        else if (!request.IsContinuousIntake && request.EndDate is null)
        {
            problems["endDate"] =
            [
                "Podaj termin zakończenia naboru albo zaznacz nabór ciągły. "
                + "Bez tego nabór nigdy się nie zamknie.",
            ];
        }
        else if (request.EndDate is { } endDate
            && Competition.ToWholeMinuteUtc(endDate)
                <= Competition.ToWholeMinuteUtc(request.StartDate))
        {
            // Compared after truncation, because that is what gets stored.
            // 12:00:30 and 12:00:45 look like a window and are not one: both
            // land on 12:00, and the check constraint would answer with a 500.
            problems["endDate"] =
                ["Nabór musi się kończyć później, niż się zaczyna, i liczy się pełna minuta."];
        }

        ValidateWizardParameters(request, problems);

        return problems;
    }

    /// <summary>
    /// Steps 1.2 to 1.6 (T-20a). Every one of them optional: the wizard lets an
    /// operator leave a step half done and checks completeness at publication,
    /// so what is checked here is only whether what WAS filled in makes sense.
    /// </summary>
    private static void ValidateWizardParameters(
        CompetitionRequest request,
        Dictionary<string, string[]> problems)
    {
        Length(problems, "expectedResults", request.ExpectedResults,
            ExpectedResultsLength, "Zakładane rezultaty");
        Length(problems, "rulesUrl", request.RulesUrl, UrlLength,
            "Adres strony z regulaminem");
        Length(problems, "submissionNotice", request.SubmissionNotice,
            MessageLength, "Informacja po złożeniu wniosku");
        Length(problems, "submissionEmailBody", request.SubmissionEmailBody,
            MessageLength, "Treść e-maila po złożeniu wniosku");
        Length(problems, "paperSubmissionAddress", request.PaperSubmissionAddress,
            PaperAddressLength, "Adres do złożenia wersji papierowej");

        // Absolute http or https only. A link the operator typed as
        // "www.ocwip.pl" is resolved by the browser against our own address and
        // sends the applicant to a page of ours that does not exist, and a
        // javascript: link on a page shown to guests is worse than a broken one.
        if (request.RulesUrl is { } rulesUrl
            && !(Uri.TryCreate(rulesUrl, UriKind.Absolute, out var parsed)
                && (parsed.Scheme == Uri.UriSchemeHttp
                    || parsed.Scheme == Uri.UriSchemeHttps)))
        {
            problems["rulesUrl"] =
                ["Adres regulaminu musi zaczynać się od http:// albo https://."];
        }

        ValidatePaperSubmission(request, problems);
        ValidateProjectFrame(request, problems);
        ValidateAmounts(request, problems);
        ValidatePercent(problems, "maxIndirectCostPercent",
            request.MaxIndirectCostPercent, "Procent kosztów pośrednich");
        ValidatePercent(problems, "maxInstitutionalDevelopmentPercent",
            request.MaxInstitutionalDevelopmentPercent,
            "Procent kosztów rozwoju instytucjonalnego");
        ValidatePersonalDataDate(request, problems);
        ValidateUploadLimits(request, problems);
        ValidateCostCategories(request, problems);
        ValidateAttachments(request, problems);
        ValidateContacts(request, problems);
    }

    private static void ValidatePaperSubmission(
        CompetitionRequest request,
        Dictionary<string, string[]> problems)
    {
        // Paired in both directions by a check constraint, so reaching the
        // database with half of it answers 500. D12: the message says what
        // happens, not which rule fired.
        if (request.RequiresPaperSubmission)
        {
            if (request.PaperSubmissionDeadline is null)
            {
                problems["paperSubmissionDeadline"] =
                [
                    "Podaj termin składania wersji papierowej albo wyłącz wymóg "
                    + "wersji papierowej.",
                ];
            }

            if (request.PaperSubmissionAddress is null)
            {
                problems["paperSubmissionAddress"] =
                [
                    "Podaj adres, pod który wnioskodawca ma wysłać papier. "
                    + "Bez niego nie ma dokąd go wysłać.",
                ];
            }
        }
        else
        {
            if (request.PaperSubmissionDeadline is not null)
            {
                problems["paperSubmissionDeadline"] =
                [
                    "Wersja papierowa nie jest wymagana, więc termin jej złożenia "
                    + "nie ma zastosowania. Usuń go albo włącz wymóg papieru.",
                ];
            }

            if (request.PaperSubmissionAddress is not null)
            {
                problems["paperSubmissionAddress"] =
                [
                    "Wersja papierowa nie jest wymagana, więc adres jej złożenia "
                    + "nie ma zastosowania. Usuń go albo włącz wymóg papieru.",
                ];
            }
        }
    }

    private static void ValidateProjectFrame(
        CompetitionRequest request,
        Dictionary<string, string[]> problems)
    {
        // Equal dates pass: a one day project is a real thing, unlike a zero
        // length intake.
        if (request.ProjectStartDate is { } from
            && request.ProjectEndDate is { } to
            && to < from)
        {
            problems["projectEndDate"] =
                ["Termin realizacji zadań nie może kończyć się przed rozpoczęciem."];
        }
    }

    private static void ValidateAmounts(
        CompetitionRequest request,
        Dictionary<string, string[]> problems)
    {
        Amount(problems, "totalPoolAmount", request.TotalPoolAmount,
            "Całkowita kwota na realizację zadań");
        Amount(problems, "minGrantAmount", request.MinGrantAmount,
            "Minimalna dotacja");

        // The threshold is the one amount where zero is a setting and not an
        // empty field: the report asks for a field that accepts 0.
        if (request.MaxAverageAnnualRevenue is { } revenue)
        {
            if (revenue < 0)
            {
                problems["maxAverageAnnualRevenue"] =
                    ["Próg przychodu nie może być ujemny."];
            }
            else if (revenue > MaxAmount)
            {
                problems["maxAverageAnnualRevenue"] =
                    [$"Próg przychodu nie może przekraczać {MaxAmount:N2} zł."];
            }
            else if (decimal.Round(revenue, AmountDecimals) != revenue)
            {
                problems["maxAverageAnnualRevenue"] =
                    ["Próg przychodu ma najwyżej dwa miejsca po przecinku."];
            }
        }

        if (!problems.ContainsKey("minGrantAmount")
            && !problems.ContainsKey("maxGrantAmount")
            && request.MinGrantAmount is { } minimum
            && minimum > request.MaxGrantAmount)
        {
            problems["minGrantAmount"] =
            [
                "Minimalna dotacja nie może być wyższa od maksymalnej "
                + $"({request.MaxGrantAmount:N2} zł).",
            ];
        }
    }

    private static void ValidatePersonalDataDate(
        CompetitionRequest request,
        Dictionary<string, string[]> problems)
    {
        if (request.PersonalDataProcessedUntil is not { } until)
        {
            return;
        }

        // Five years from the closing of the intake, which is the retention
        // floor of AGENTS.md security rule 5 written into a competition
        // setting.
        //
        // A continuous intake has no closing moment, so there is nothing to
        // count five years from. Refusing every date would be the literal
        // reading and would make the field impossible to fill in, so the floor
        // moves to the opening of the intake, which is the earliest moment the
        // competition can produce any personal data at all. It is never
        // stricter than the rule and never allows less than five years of
        // retention on data already collected. Written up in
        // docs/architektura.md.
        var closing = request.IsContinuousIntake
            ? request.StartDate
            : request.EndDate ?? request.StartDate;

        var floor = DateOnly.FromDateTime(
            closing.ToUniversalTime().UtcDateTime).AddYears(RetentionYears);

        if (until < floor)
        {
            problems["personalDataProcessedUntil"] =
            [
                "Dane osobowe trzymamy co najmniej pięć lat od zamknięcia naboru, "
                + $"czyli do {floor:dd.MM.yyyy} albo dłużej.",
            ];
        }
    }

    private static void ValidateUploadLimits(
        CompetitionRequest request,
        Dictionary<string, string[]> problems)
    {
        if (request.MaxAttachmentSizeInBytes is { } perFile && perFile <= 0)
        {
            problems["maxAttachmentSizeInBytes"] =
                ["Limit rozmiaru pliku musi być większy od zera."];
        }

        if (request.MaxApplicationSizeInBytes is { } perApplication
            && perApplication <= 0)
        {
            problems["maxApplicationSizeInBytes"] =
                ["Limit rozmiaru wniosku musi być większy od zera."];
        }

        // A per file limit above the per application one is a limit that can
        // never be used, and the applicant meets it as a file accepted by one
        // check and refused by the next.
        if (!problems.ContainsKey("maxAttachmentSizeInBytes")
            && !problems.ContainsKey("maxApplicationSizeInBytes")
            && (request.MaxAttachmentSizeInBytes
                ?? Competition.DefaultMaxAttachmentSizeInBytes)
                > (request.MaxApplicationSizeInBytes
                    ?? Competition.DefaultMaxApplicationSizeInBytes))
        {
            problems["maxAttachmentSizeInBytes"] =
            [
                "Limit pojedynczego pliku nie może być wyższy niż limit całego "
                + "wniosku.",
            ];
        }
    }

    private static void ValidateCostCategories(
        CompetitionRequest request,
        Dictionary<string, string[]> problems)
    {
        if (request.CostCategories is not { Count: > 0 } categories)
        {
            // Missing means the three the 2026 template names, applied in
            // CompetitionService. A budget with no categories at all is not a
            // setting anybody wants, so "none" is not expressible on purpose.
            return;
        }

        if (categories.Distinct().Count() != categories.Count)
        {
            problems["costCategories"] =
                ["Każda kategoria kosztów może wystąpić tylko raz."];
        }
        else if (categories.Any(category => !Enum.IsDefined(category)))
        {
            problems["costCategories"] = ["Nieznana kategoria kosztów."];
        }
    }

    private static void ValidateAttachments(
        CompetitionRequest request,
        Dictionary<string, string[]> problems)
    {
        if (request.Attachments is not { Count: > 0 } attachments)
        {
            return;
        }

        if (attachments.Count > MaxAttachments)
        {
            problems["attachments"] =
                [$"Konkurs może wymagać najwyżej {MaxAttachments} załączników."];
            return;
        }

        // Indexed, because "tytuł jest wymagany" on a list of eight rows does
        // not say which row, and the operator then hunts for it.
        for (var index = 0; index < attachments.Count; index++)
        {
            var attachment = attachments[index];
            var field = $"attachments[{index}]";

            if (string.IsNullOrWhiteSpace(attachment.Title))
            {
                problems[$"{field}.title"] = ["Tytuł załącznika jest wymagany."];
            }
            else if (attachment.Title.Length > AttachmentTitleLength)
            {
                problems[$"{field}.title"] =
                [
                    "Tytuł załącznika nie może być dłuższy niż "
                    + $"{AttachmentTitleLength} znaków.",
                ];
            }

            if (attachment.Description is { Length: > AttachmentDescriptionLength })
            {
                problems[$"{field}.description"] =
                [
                    "Opis załącznika nie może być dłuższy niż "
                    + $"{AttachmentDescriptionLength} znaków.",
                ];
            }

            if (!Enum.IsDefined(attachment.Requirement))
            {
                problems[$"{field}.requirement"] = ["Nieznana wymagalność załącznika."];
            }

            if (attachment.AllowedFormats is not { Count: > 0 } formats)
            {
                problems[$"{field}.allowedFormats"] =
                [
                    "Wskaż przynajmniej jeden dopuszczalny format pliku, "
                    + "inaczej wnioskodawca nie załączy niczego.",
                ];
            }
            else if (formats.Distinct().Count() != formats.Count)
            {
                problems[$"{field}.allowedFormats"] =
                    ["Każdy format pliku może wystąpić tylko raz."];
            }
            else if (formats.Any(format => !Enum.IsDefined(format)))
            {
                problems[$"{field}.allowedFormats"] = ["Nieznany format pliku."];
            }
        }
    }

    private static void ValidateContacts(
        CompetitionRequest request,
        Dictionary<string, string[]> problems)
    {
        if (request.ContactUserIds is not { Count: > 0 } contacts)
        {
            return;
        }

        if (contacts.Count > MaxContacts)
        {
            problems["contactUserIds"] =
                [$"Konkurs może mieć najwyżej {MaxContacts} osób kontaktowych."];
            return;
        }

        // Whether the account exists and is an operator needs the database and
        // is answered in CompetitionService. Repetition does not.
        if (contacts.Distinct().Count() != contacts.Count)
        {
            problems["contactUserIds"] =
                ["Ta sama osoba kontaktowa jest wskazana dwa razy."];
        }
    }

    private static void Length(
        Dictionary<string, string[]> problems,
        string field,
        string? value,
        int limit,
        string label)
    {
        if (value is { } text && text.Length > limit)
        {
            problems[field] = [$"{label} nie może być dłuższa niż {limit} znaków."];
        }
    }

    private static void Amount(
        Dictionary<string, string[]> problems,
        string field,
        decimal? value,
        string label)
    {
        if (value is not { } amount)
        {
            return;
        }

        if (amount <= 0)
        {
            problems[field] = [$"{label} musi być większa od zera."];
        }
        else if (amount > MaxAmount)
        {
            problems[field] = [$"{label} nie może przekraczać {MaxAmount:N2} zł."];
        }
        else if (decimal.Round(amount, AmountDecimals) != amount)
        {
            problems[field] = [$"{label} ma najwyżej dwa miejsca po przecinku."];
        }
    }

    private static void ValidatePercent(
        Dictionary<string, string[]> problems,
        string field,
        decimal? value,
        string label)
    {
        if (value is not { } percent)
        {
            return;
        }

        // Zero is legal: a competition that funds no indirect costs at all
        // says so with a 0, and null would say "nothing decided yet".
        if (percent < 0 || percent > 100)
        {
            problems[field] = [$"{label} musi mieścić się między 0 a 100."];
        }
        else if (decimal.Round(percent, PercentDecimals) != percent)
        {
            problems[field] = [$"{label} ma najwyżej dwa miejsca po przecinku."];
        }
    }
}
