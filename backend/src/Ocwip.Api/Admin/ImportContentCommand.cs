using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Contracts;
using Ocwip.Api.Data;
using Ocwip.Api.Models;
using Ocwip.Api.Models.Forms;
using Ocwip.Api.Services;

namespace Ocwip.Api.Admin;

/// <summary>One document to publish: which part of the competition, and from which file.</summary>
internal sealed record ContentFile(FormPurpose Purpose, string Label, string Path);

internal sealed record ImportContentRequest(Guid CompetitionId, IReadOnlyList<ContentFile> Files);

/// <summary>
/// Puts the starting content of a competition in place on a server (T-96):
/// the application form, both evaluation cards and the report form, from the
/// JSON files in backend/seed/, which the production image carries.
///
/// Production content, not test data: it creates no account and no password,
/// which is the line docs/architektura.md draws around scripts/seed.py.
///
/// Three promises, each tested:
/// - every file passes the same contract gate as the API before anything is
///   written, so one bad file leaves the database as it was;
/// - a file identical to the version in force publishes nothing and says so,
///   so running the command twice is safe;
/// - what is published goes through FormDefinitionService, the path the
///   operator's screens use, in one transaction.
/// </summary>
internal static class ImportContentCommand
{
    public const string Verb = "import-content";

    public const string Usage = """
        Usage:
          dotnet Ocwip.Api.dll import-content --competition <id> \
            [--application <file>] [--formal <file>] [--merit <file>] [--report <file>]

        Publishes each given file as the next version of that part of the
        competition, unless it is identical to the version in force. Every
        file is checked against the form contract first; if one is refused,
        nothing is published.
        """;

    private static readonly Dictionary<string, (FormPurpose Purpose, string Label)> Options = new(StringComparer.Ordinal)
    {
        ["--application"] = (FormPurpose.Application, "application form"),
        ["--formal"] = (FormPurpose.FormalEvaluation, "formal evaluation card"),
        ["--merit"] = (FormPurpose.MeritEvaluation, "merit evaluation card"),
        ["--report"] = (FormPurpose.Report, "report form"),
    };

    public static ImportContentRequest? Parse(string[] args, out string error)
    {
        Guid? competition = null;
        var files = new List<ContentFile>();

        for (var index = 1; index < args.Length; index += 2)
        {
            var option = args[index];

            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                error = $"{option} has no value.";
                return null;
            }

            var value = args[index + 1];

            if (option == "--competition")
            {
                if (competition is not null || !Guid.TryParse(value, out var id))
                {
                    error = competition is not null ? "--competition was given twice." : $"{value} is not a competition id.";
                    return null;
                }

                competition = id;
            }
            else if (Options.TryGetValue(option, out var part))
            {
                if (files.Any(file => file.Purpose == part.Purpose))
                {
                    error = $"{option} was given twice.";
                    return null;
                }

                files.Add(new ContentFile(part.Purpose, part.Label, value));
            }
            else
            {
                error = $"Unknown option {option}.";
                return null;
            }
        }

        if (competition is null)
        {
            error = "--competition is missing.";
            return null;
        }

        if (files.Count == 0)
        {
            error = "Nothing to import: give at least one of --application, --formal, --merit, --report.";
            return null;
        }

        error = string.Empty;
        return new ImportContentRequest(competition.Value, files);
    }

    /// <summary>The lines to print, and whether everything asked for is in place.</summary>
    public static async Task<(bool Succeeded, IReadOnlyList<string> Lines)> ExecuteAsync(
        AppDbContext context, ImportContentRequest request, CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        var documents = new List<(ContentFile File, JsonElement Definition)>();

        // Everything is read and checked before anything is written.
        foreach (var file in request.Files)
        {
            JsonElement definition;
            try
            {
                using var document = JsonDocument.Parse(await File.ReadAllTextAsync(file.Path, cancellationToken));
                definition = document.RootElement.Clone();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                lines.Add($"The {file.Label} could not be read from {file.Path}: {exception.Message}");
                continue;
            }

            var check = FormSchemaValidator.Validate(definition, file.Purpose);
            if (!check.IsValid)
            {
                lines.Add($"The {file.Label} in {file.Path} does not pass the form contract:");
                lines.AddRange(check.Errors.Select(problem => $"  {problem.Path}: {problem.Message}"));
                continue;
            }

            documents.Add((file, definition));
        }

        if (documents.Count != request.Files.Count)
        {
            lines.Add("Nothing was published.");
            return (false, lines);
        }

        var competition = await context.Competitions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.CompetitionId, cancellationToken);
        if (competition is null)
        {
            return (false, [$"No competition with the id {request.CompetitionId}. Nothing was published."]);
        }

        var service = new FormDefinitionService(context);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        foreach (var (file, definition) in documents)
        {
            var current = await InForceAsync(context, competition, file.Purpose, cancellationToken);
            if (current is not null && JsonElement.DeepEquals(current.Definition, definition))
            {
                lines.Add($"The {file.Label} is already version {current.VersionNumber}. Nothing changed.");
                continue;
            }

            var result = await service.PublishAsync(
                request.CompetitionId, file.Purpose, new FormDefinitionRequest(definition), cancellationToken);
            if (result.Outcome is not FormDefinitionOutcome.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return (false, [$"The {file.Label} was refused ({result.Outcome}). Nothing was published."]);
            }

            lines.Add($"Published the {file.Label} as version {result.Definition!.VersionNumber}.");
        }

        await transaction.CommitAsync(cancellationToken);
        return (true, lines);
    }

    private static Task<FormDefinition?> InForceAsync(
        AppDbContext context, Competition competition, FormPurpose purpose, CancellationToken cancellationToken)
    {
        var id = purpose switch
        {
            FormPurpose.FormalEvaluation => competition.FormalCardDefinitionId,
            FormPurpose.MeritEvaluation => competition.MeritCardDefinitionId,
            FormPurpose.Report => competition.ReportFormDefinitionId,
            _ => competition.FormDefinitionId,
        };

        return id is null
            ? Task.FromResult<FormDefinition?>(null)
            : context.FormDefinitions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }
}
