using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ocwip.Api.Contracts;
using Ocwip.Api.Data;
using Ocwip.Api.Models;
using Ocwip.Api.Models.Forms;
using Ocwip.Api.Services.Export;
using Ocwip.Api.Services.Pdf;

namespace Ocwip.Api.Services.Documents;

internal enum ContractOutcome
{
    Succeeded,
    Created,
    NotFound,

    /// <summary>Only a granted application has a contract.</summary>
    NotGranted,

    /// <summary>The competition has no contract template yet.</summary>
    NoTemplate,

    /// <summary>Signed: the values are frozen.</summary>
    Signed,

    Invalid,
}

internal sealed record ContractResult(
    ContractOutcome Outcome,
    ContractResponse? Contract = null,
    IDictionary<string, string[]>? Errors = null,
    byte[]? Pdf = null,
    string? FileName = null);

internal sealed record TemplateResult(
    ContractOutcome Outcome,
    DocumentTemplateResponse? Template = null,
    IDictionary<string, string[]>? Errors = null);

/// <summary>Contract templates and contracts (T-45, model T-45.0).</summary>
internal interface IContractService
{
    Task<TemplateResult> PublishTemplateAsync(Guid competitionId, string? body, CancellationToken cancellationToken);

    /// <summary>The version in force, the highest; NotFound without any.</summary>
    Task<TemplateResult> TemplateAsync(Guid competitionId, CancellationToken cancellationToken);

    Task<ContractResult> StartAsync(Guid applicationId, CancellationToken cancellationToken);

    Task<ContractResult> GetAsync(Guid contractId, CancellationToken cancellationToken);

    Task<ContractResult> SaveValuesAsync(Guid contractId, IReadOnlyDictionary<string, string?> values, CancellationToken cancellationToken);

    Task<ContractResult> PdfAsync(Guid contractId, CancellationToken cancellationToken);

    Task<ContractResult> SignAsync(Guid contractId, Guid operatorId, DateOnly? signedOn, CancellationToken cancellationToken);

    Task<Contract?> FindForAuthorizationAsync(Guid contractId, CancellationToken cancellationToken);

    Task<Contract?> FindForApplicationAsync(Guid applicationId, CancellationToken cancellationToken);
}

internal sealed class ContractService(AppDbContext context, TimeProvider time) : IContractService
{
    internal const int BodyMaxLength = 100_000;
    internal const int ValueMaxLength = 2000;

    public async Task<TemplateResult> PublishTemplateAsync(Guid competitionId, string? body, CancellationToken cancellationToken)
    {
        if (!await context.Competitions.AnyAsync(x => x.Id == competitionId && x.IsActive, cancellationToken))
        {
            return new TemplateResult(ContractOutcome.NotFound);
        }

        var text = body?.Replace("\r\n", "\n").Trim();
        var problems = string.IsNullOrEmpty(text)
            ? ["Wzór umowy nie może być pusty."]
            : text.Length > BodyMaxLength
                ? [$"Wzór może mieć najwyżej {BodyMaxLength} znaków."]
                : TemplatePlaceholders.Problems(text);

        if (problems.Count > 0)
        {
            return new TemplateResult(ContractOutcome.Invalid, Errors: new Dictionary<string, string[]> { ["body"] = [.. problems] });
        }

        var next = await context.DocumentTemplates
            .Where(x => x.CompetitionId == competitionId && x.Kind == DocumentKind.Contract)
            .MaxAsync(x => (int?)x.VersionNumber, cancellationToken) ?? 0;

        var template = new DocumentTemplate
        {
            Id = Guid.NewGuid(),
            CompetitionId = competitionId,
            Kind = DocumentKind.Contract,
            VersionNumber = next + 1,
            Body = text!,
        };
        context.DocumentTemplates.Add(template);
        await context.SaveChangesAsync(cancellationToken);

        return new TemplateResult(ContractOutcome.Created, Response(template));
    }

    public async Task<TemplateResult> TemplateAsync(Guid competitionId, CancellationToken cancellationToken) =>
        await InForceAsync(competitionId, cancellationToken) is { } template
            ? new TemplateResult(ContractOutcome.Succeeded, Response(template))
            : new TemplateResult(ContractOutcome.NotFound);

    public async Task<ContractResult> StartAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        var application = await context.Applications.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == applicationId && x.IsActive, cancellationToken);

        if (application is null)
        {
            return new ContractResult(ContractOutcome.NotFound);
        }

        if (await FindForApplicationAsync(applicationId, cancellationToken) is { } existing)
        {
            return await GetAsync(existing.Id, cancellationToken);
        }

        if (!ApplicationStatuses.IsGranted(application.Status))
        {
            return new ContractResult(ContractOutcome.NotGranted);
        }

        if (await InForceAsync(application.CompetitionId, cancellationToken) is not { } template)
        {
            return new ContractResult(ContractOutcome.NoTemplate);
        }

        var contract = new Contract
        {
            Id = Guid.NewGuid(),
            ApplicationId = application.Id,
            CompetitionId = application.CompetitionId,
            EntityId = application.EntityId,
            TemplateId = template.Id,
            Values = JsonSerializer.SerializeToElement(new Dictionary<string, string>()),
        };
        context.Contracts.Add(contract);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            context.ChangeTracker.Clear();
            var raced = await FindForApplicationAsync(applicationId, cancellationToken)
                ?? throw new InvalidOperationException($"No active contract of {applicationId} after a unique violation.");
            return await GetAsync(raced.Id, cancellationToken);
        }

        var created = await GetAsync(contract.Id, cancellationToken);
        return created with { Outcome = ContractOutcome.Created };
    }

    public async Task<ContractResult> GetAsync(Guid contractId, CancellationToken cancellationToken)
    {
        var loaded = await LoadAsync(contractId, cancellationToken);
        return loaded is null
            ? new ContractResult(ContractOutcome.NotFound)
            : new ContractResult(ContractOutcome.Succeeded, Response(loaded.Value.Contract, loaded.Value.Values));
    }

    public async Task<ContractResult> SaveValuesAsync(
        Guid contractId, IReadOnlyDictionary<string, string?> values, CancellationToken cancellationToken)
    {
        if (values is null)
        {
            return new ContractResult(ContractOutcome.Invalid, Errors: new Dictionary<string, string[]>
            {
                ["values"] = ["Podaj wartości pól umowy."],
            });
        }

        var loaded = await LoadAsync(contractId, cancellationToken);
        if (loaded is not { } found)
        {
            return new ContractResult(ContractOutcome.NotFound);
        }

        var contract = found.Contract;
        if (contract.Status == ContractStatus.Signed)
        {
            return new ContractResult(ContractOutcome.Signed);
        }

        // Only the blanks the template leaves to the operator, and only those
        // its text leaves for THIS kind of applicant: a system value is read
        // from the application and cannot be typed over, and a register
        // number is not something an informal group has.
        var manual = TemplatePlaceholders
            .In(contract.Template.Body, contract.Application.KindOfApplicant)
            .Where(x => !x.System)
            .Select(x => x.Name)
            .ToHashSet();
        var errors = new Dictionary<string, string[]>();
        var kept = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (name, value) in values)
        {
            if (!manual.Contains(name))
            {
                errors[name] = ["Tego pola wzór nie zostawia do wpisania."];
                continue;
            }

            var text = value?.Trim();

            // The masked PESEL coming back untouched from the screen means
            // "keep it", not "replace it with stars".
            if (TemplatePlaceholders.IsPesel(name)
                && found.Values.GetValueOrDefault(name) is { } stored
                && text == TemplatePlaceholders.Mask(stored))
            {
                text = stored;
            }
            else if (TemplatePlaceholders.IsPesel(name) && text?.Contains('*') == true)
            {
                // A mask edited by hand: the stars would be stored as the number.
                errors[name] = ["Wpisz pełny numer PESEL albo zostaw zamaskowaną wartość bez zmian."];
                continue;
            }

            if (text is { Length: > ValueMaxLength })
            {
                errors[name] = [$"Wartość może mieć najwyżej {ValueMaxLength} znaków."];
                continue;
            }

            if (!string.IsNullOrEmpty(text))
            {
                kept[name] = text;
            }
        }

        if (errors.Count > 0)
        {
            return new ContractResult(ContractOutcome.Invalid, Errors: errors);
        }

        contract.Values = JsonSerializer.SerializeToElement(kept);
        await context.SaveChangesAsync(cancellationToken);
        return await GetAsync(contract.Id, cancellationToken);
    }

    public async Task<ContractResult> PdfAsync(Guid contractId, CancellationToken cancellationToken)
    {
        var loaded = await LoadAsync(contractId, cancellationToken);
        if (loaded is not { } found)
        {
            return new ContractResult(ContractOutcome.NotFound);
        }

        var text = TemplatePlaceholders.Fill(
            found.Contract.Template.Body, found.Values, found.Contract.Application.KindOfApplicant);
        var number = found.Contract.Application.Number ?? found.Contract.Id.ToString("N")[..8];
        var room = PdfPageLayout.Portrait.Width - 2 * PdfPageLayout.Portrait.Margin;
        var lines = text.Split('\n')
            .SelectMany(line => ApplicationPdfBuilder.Wrap(
                PdfText.Printable(line), piece => SimplePdfDocument.Measure(piece, PdfPageLayout.Portrait), room))
            .ToList();

        var pdf = SimplePdfDocument.Create(lines, PdfPageLayout.Portrait, [$"Umowa nr {number}", string.Empty]);
        return new ContractResult(ContractOutcome.Succeeded, Pdf: pdf, FileName: $"umowa-{number.Replace('/', '-')}.pdf");
    }

    public async Task<ContractResult> SignAsync(
        Guid contractId, Guid operatorId, DateOnly? signedOn, CancellationToken cancellationToken)
    {
        var loaded = await LoadAsync(contractId, cancellationToken);
        if (loaded is not { } found)
        {
            return new ContractResult(ContractOutcome.NotFound);
        }

        var contract = found.Contract;
        if (contract.Status == ContractStatus.Signed)
        {
            return new ContractResult(ContractOutcome.Signed);
        }

        var errors = new Dictionary<string, string[]>();
        if (signedOn is not { } day)
        {
            errors["signedOn"] = ["Podaj datę podpisania umowy."];
        }
        else if (day > ApplicationListLabels.Day(time.GetUtcNow()))
        {
            errors["signedOn"] = ["Data podpisania nie może być z przyszłości."];
        }

        // A contract printed with a blank is not the one somebody signs. Only
        // the blanks this applicant's contract really has: a part marked for
        // another kind is not printed, so it has nothing to miss.
        foreach (var blank in TemplatePlaceholders.In(contract.Template.Body, contract.Application.KindOfApplicant)
            .Where(x => !x.System && string.IsNullOrWhiteSpace(found.Values.GetValueOrDefault(x.Name))))
        {
            errors[blank.Name] = [$"Brakuje wartości \"{blank.Label}\"."];
        }

        if (errors.Count > 0)
        {
            return new ContractResult(ContractOutcome.Invalid, Errors: errors);
        }

        var now = time.GetUtcNow();
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        // Claimed conditionally: two operators recording the signing at once
        // do not both win with two different days; the second gets Signed.
        // Active only: a resignation in the same moment withdraws the draft.
        var claimed = await context.Contracts
            .Where(x => x.Id == contract.Id && x.IsActive && x.Status == ContractStatus.Draft)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.Status, ContractStatus.Signed)
                    .SetProperty(x => x.SignedOn, signedOn)
                    .SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow),
                cancellationToken);

        if (claimed != 1)
        {
            return new ContractResult(ContractOutcome.Signed);
        }

        // The application moves to "umowa podpisana" outside SaveChanges, for
        // the checksum reason in GrantDecisionService: UpdatedAt is part of it.
        var moved = await context.Applications
            .Where(x => x.Id == contract.ApplicationId && x.Status == ApplicationStatus.Funded)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ApplicationStatus.ContractSigned), cancellationToken);

        // The application is no longer funded (resigned in the meantime): a
        // signed contract beside a grant given up is not recorded.
        if (moved != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new ContractResult(ContractOutcome.NotGranted);
        }

        context.ApplicationStatusHistory.Add(new ApplicationStatusHistory
        {
            Id = Guid.NewGuid(),
            ApplicationId = contract.ApplicationId,
            FromStatus = ApplicationStatus.Funded,
            ToStatus = ApplicationStatus.ContractSigned,
            ChangedAt = now,
            ChangedByUserId = operatorId,
        });

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // The tracked copy still says Draft; read the row as it is now.
        context.ChangeTracker.Clear();
        return await GetAsync(contract.Id, cancellationToken);
    }

    public Task<Contract?> FindForAuthorizationAsync(Guid contractId, CancellationToken cancellationToken) =>
        context.Contracts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == contractId && x.IsActive, cancellationToken);

    public Task<Contract?> FindForApplicationAsync(Guid applicationId, CancellationToken cancellationToken) =>
        context.Contracts.AsNoTracking().FirstOrDefaultAsync(x => x.ApplicationId == applicationId && x.IsActive, cancellationToken);

    private Task<DocumentTemplate?> InForceAsync(Guid competitionId, CancellationToken cancellationToken) =>
        context.DocumentTemplates.AsNoTracking()
            .Where(x => x.CompetitionId == competitionId && x.Kind == DocumentKind.Contract)
            .OrderByDescending(x => x.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>The contract with everything it prints: system values read now, the operator's from the row.</summary>
    private async Task<(Contract Contract, Dictionary<string, string?> Values)?> LoadAsync(
        Guid contractId, CancellationToken cancellationToken)
    {
        var contract = await context.Contracts
            .Include(x => x.Template)
            .Include(x => x.Application).ThenInclude(x => x.Entity)
            .Include(x => x.Application).ThenInclude(x => x.Competition)
            .Include(x => x.Application).ThenInclude(x => x.FormDefinition)
            .FirstOrDefaultAsync(x => x.Id == contractId && x.IsActive, cancellationToken);

        if (contract is null)
        {
            return null;
        }

        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var property in contract.Values.EnumerateObject())
        {
            values[property.Name] = property.Value.GetString();
        }

        foreach (var (name, value) in SystemValues(contract))
        {
            values[name] = value;
        }

        return (contract, values);
    }

    private static Dictionary<string, string?> SystemValues(Contract contract)
    {
        var application = contract.Application;
        var form = FormSchemaValidator.Validate(application.FormDefinition.Definition).Document;
        var roles = form is null ? new ApplicationRoleValues(null, null, null) : ApplicationRoleValues.Read(form, application.Answers);
        string? Money(decimal? amount) => amount is null ? null : $"{ApplicationListLabels.Amount(amount)} zł";

        // The party as submitted (T-93), not the card as it is today: the
        // applicant can still edit the card, and a signed contract has to
        // print the same party every time. The live card only for an
        // application submitted before snapshots existed.
        var party = EntityCards.EntitySnapshots.Read(application.EntitySnapshot);

        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["numer_umowy"] = application.Number,
            ["data_zawarcia"] = contract.SignedOn is { } day ? TemplatePlaceholders.DateInWords(day) : null,
            ["numer_wniosku"] = application.Number,
            ["data_zlozenia_wniosku"] = application.SubmittedAt is { } submitted
                ? TemplatePlaceholders.DateInWords(ApplicationListLabels.Day(submitted))
                : null,
            ["nazwa_realizatora"] = EntityCards.EntitySnapshots.NameOf(application.EntitySnapshot, application.Entity.Name),
            ["nip"] = party is null ? application.Entity.Nip : party.Nip,
            ["adres"] = party is null ? application.Entity.Address : party.Address,
            ["tytul_projektu"] = roles.ProjectTitle,
            ["koszt_calkowity"] = Money(roles.TotalCost),
            ["kwota_wnioskowana"] = Money(roles.RequestedGrant),
            ["kwota_dotacji"] = Money(application.AwardedGrant),
            ["kwota_dotacji_slownie"] = application.AwardedGrant is { } grant ? AmountInWords.Of(grant) : null,
            ["numer_konkursu"] = application.Competition.Number,
            ["tytul_konkursu"] = application.Competition.Title,
            ["czlonkowie_grupy"] = GroupMembersValue.Read(form, application.Answers, application.KindOfApplicant),
        };
    }

    private static DocumentTemplateResponse Response(DocumentTemplate template) =>
        new(template.Id, template.CompetitionId, template.VersionNumber, template.Body,
            TemplatePlaceholders.In(template.Body), template.CreatedAt);

    /// <summary>A PESEL leaves the server masked except inside the contract PDF (T-47a).</summary>
    private static string? Shown(TemplatePlaceholder placeholder, string? value) =>
        !placeholder.System && value is not null && TemplatePlaceholders.IsPesel(placeholder.Name)
            ? TemplatePlaceholders.Mask(value)
            : value;

    private static ContractResponse Response(Contract contract, IReadOnlyDictionary<string, string?> values) =>
        new(
            contract.Id,
            contract.ApplicationId,
            contract.Application.Number,
            EntityCards.EntitySnapshots.NameOf(contract.Application.EntitySnapshot, contract.Application.Entity.Name),
            contract.Template.VersionNumber,
            contract.Status,
            contract.SignedOn,
            TemplatePlaceholders.In(contract.Template.Body, contract.Application.KindOfApplicant)
                .Select(x => new ContractField(x.Name, x.Label, x.System, Shown(x, values.GetValueOrDefault(x.Name))))
                .ToList());
}
