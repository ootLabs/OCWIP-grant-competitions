using System.IO.Compression;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Data;
using Ocwip.Api.Models;

namespace Ocwip.Api.Services.Documents;

internal enum ContractBundleOutcome
{
    Succeeded,
    NotFound,

    /// <summary>The competition has no contract template yet.</summary>
    NoTemplate,

    /// <summary>No application of the competition is granted.</summary>
    NothingGranted,
}

internal sealed record ContractBundleResult(ContractBundleOutcome Outcome, byte[]? Zip = null, string? FileName = null);

/// <summary>All the contracts of a competition at once (T-45b).</summary>
internal interface IContractBundleService
{
    Task<ContractBundleResult> BuildAsync(Guid competitionId, CancellationToken cancellationToken);
}

/// <summary>
/// "Umowy jednym kliknięciem" (T-45b): draws up the missing contract of every
/// granted application of the competition, then packs the PDF of each one
/// with every operator value in place into one ZIP. A contract with a blank
/// left does not stop the rest: it stays out of the file and is named, with
/// what it lacks, in braki.txt inside it. Everything goes through
/// IContractService, the path of a single contract, so a contract in the ZIP
/// is byte for byte the one its own page prints.
///
/// Built in memory: at the scale of a competition (about 60 contracts of
/// about 140 KB each with the font subset of T-45c) that is under 10 MB,
/// which does not need a background job or a file on disk.
/// </summary>
internal sealed class ContractBundleService(AppDbContext context, IContractService contracts) : IContractBundleService
{
    internal const string MissingFile = "braki.txt";

    private static readonly ApplicationStatus[] Granted =
        [ApplicationStatus.Funded, ApplicationStatus.ContractSigned, ApplicationStatus.Settled];

    public async Task<ContractBundleResult> BuildAsync(Guid competitionId, CancellationToken cancellationToken)
    {
        var competition = await context.Competitions.AsNoTracking()
            .Where(x => x.Id == competitionId && x.IsActive)
            .Select(x => new { x.Number })
            .FirstOrDefaultAsync(cancellationToken);

        if (competition is null)
        {
            return new ContractBundleResult(ContractBundleOutcome.NotFound);
        }

        if ((await contracts.TemplateAsync(competitionId, cancellationToken)).Template is null)
        {
            return new ContractBundleResult(ContractBundleOutcome.NoTemplate);
        }

        var applications = await context.Applications.AsNoTracking()
            .Where(x => x.CompetitionId == competitionId && x.IsActive && Granted.Contains(x.Status))
            .OrderBy(x => x.Number)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (applications.Count == 0)
        {
            return new ContractBundleResult(ContractBundleOutcome.NothingGranted);
        }

        var missing = new List<string>();
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var applicationId in applications)
            {
                var contract = (await contracts.StartAsync(applicationId, cancellationToken)).Contract;
                if (contract is null)
                {
                    continue;
                }

                var blanks = contract.Fields
                    .Where(x => !x.System && string.IsNullOrWhiteSpace(x.Value))
                    .Select(x => x.Label)
                    .ToList();

                if (blanks.Count > 0)
                {
                    missing.Add($"{contract.ApplicationNumber} {contract.EntityName}: {string.Join(", ", blanks)}");
                    continue;
                }

                var pdf = await contracts.PdfAsync(contract.Id, cancellationToken);
                var entry = zip.CreateEntry(pdf.FileName!, CompressionLevel.Optimal);
                await using var stream = entry.Open();
                await stream.WriteAsync(pdf.Pdf, cancellationToken);
            }

            if (missing.Count > 0)
            {
                var entry = zip.CreateEntry(MissingFile, CompressionLevel.Optimal);
                await using var stream = entry.Open();
                var text = "Umowy bez kompletu pól do wpisania, których nie ma w tym pliku:\n\n" + string.Join("\n", missing) + "\n";
                await stream.WriteAsync(Encoding.UTF8.GetBytes(text), cancellationToken);
            }
        }

        return new ContractBundleResult(
            ContractBundleOutcome.Succeeded, buffer.ToArray(), $"umowy-{competition.Number.Replace('/', '-')}.zip");
    }
}
