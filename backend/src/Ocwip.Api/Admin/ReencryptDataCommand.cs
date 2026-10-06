using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Data;
using Ocwip.Api.Data.Encryption;
using Ocwip.Api.Models.Forms;

namespace Ocwip.Api.Admin;

/// <summary>
/// Rewrites every sensitive value with the current key (T-47a). Two uses,
/// one command:
/// - once after the EncryptSensitiveFields migration, so the rows written
///   before it stop being plaintext;
/// - after a rotation (a new FieldEncryption:Keys:&lt;n+1&gt; beside the old
///   one), so the old key can be retired once this has run.
///
/// Reads through the model, which decrypts with any configured key, and
/// writes the same values back marked as changed, which encrypts with the
/// current one. Batch by batch, each in its own save, so a large table does
/// not sit in memory and a failure halfway leaves every row readable: a row
/// is always under one of the configured keys. Safe to run again.
/// </summary>
internal static class ReencryptDataCommand
{
    public const string Verb = "reencrypt-data";

    private const int Batch = 200;

    public static async Task<IReadOnlyList<string>> ExecuteAsync(
        AppDbContext context,
        Services.IAttachmentStorage storage,
        CancellationToken cancellationToken)
    {
        // Before the first write, so a missing key fails with its own message.
        var version = FieldEncryption.Cipher.CurrentVersion;
        context.KeepUpdatedAt = true;

        var entities = await RewriteAsync(context, context.Entities.OrderBy(x => x.Id), (entry, _) =>
        {
            foreach (var name in new[] { "Address", "CorrespondenceAddress", "Phone", "Email", "BankAccount", "Representatives" })
            {
                entry.Property(name).IsModified = true;
            }
        }, cancellationToken);

        var users = await RewriteAsync(context, context.Users.Where(x => x.Pesel != null).OrderBy(x => x.Id),
            (entry, _) => entry.Property("Pesel").IsModified = true, cancellationToken);

        var contracts = await RewriteAsync(context, context.Contracts.OrderBy(x => x.Id),
            (entry, _) => entry.Property("Values").IsModified = true, cancellationToken);

        var applications = await RewriteAsync(context, context.Applications.Include(x => x.FormDefinition).OrderBy(x => x.Id), (entry, application) =>
        {
            application.Answers = SensitiveAnswers.Protect(application.Answers, SensitiveKeysOf(application.FormDefinition));
            entry.Property("Answers").IsModified = true;
            if (application.EntitySnapshot is not null)
            {
                entry.Property("EntitySnapshot").IsModified = true;
            }
        }, cancellationToken);

        // Earlier versions of returned applications (T-103), under their own purpose.
        var forms = await context.FormDefinitions.AsNoTracking()
            .Where(x => context.ApplicationVersions.Any(v => v.FormDefinitionId == x.Id))
            .ToDictionaryAsync(x => x.Id, x => SensitiveKeysOf(x), cancellationToken);
        var versions = await RewriteAsync(context, context.ApplicationVersions.OrderBy(x => x.Id), (entry, version) =>
        {
            version.Answers = SensitiveAnswers.Protect(
                version.Answers, forms[version.FormDefinitionId], Data.Configurations.ApplicationVersionConfiguration.AnswersPurpose);
            entry.Property("Answers").IsModified = true;
            if (version.EntitySnapshot is not null)
            {
                entry.Property("EntitySnapshot").IsModified = true;
            }
        }, cancellationToken);

        var reports = await RewriteAsync(context, context.Reports
            .Include(x => x.FormDefinition)
            .Include(x => x.Application).ThenInclude(x => x.FormDefinition)
            .OrderBy(x => x.Id), (entry, report) =>
        {
            var keys = ReportSensitiveKeysOf(report.FormDefinition, report.Application.FormDefinition);
            report.Answers = SensitiveAnswers.Protect(report.Answers, keys, SensitiveAnswers.ReportPurpose);
            report.Prefill = SensitiveAnswers.Protect(report.Prefill, keys, SensitiveAnswers.ReportPurpose);
            entry.Property("Answers").IsModified = true;
            entry.Property("Prefill").IsModified = true;
        }, cancellationToken);

        // Earlier versions of returned reports (S-35), each column under its
        // own purpose. The keys come from the form the version was filled on
        // and from the application it prefills from, read the tolerant way
        // for the same reason the reports above are: a version left under the
        // old key is one the rotation cannot retire that key without losing.
        var keptForms = await context.FormDefinitions.AsNoTracking()
            .Where(x => context.ReportVersions.Any(v => v.FormDefinitionId == x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var keptApplicationForms = await context.Reports.AsNoTracking()
            .Where(x => context.ReportVersions.Any(v => v.ReportId == x.Id))
            .Select(x => new { x.Id, Form = x.Application.FormDefinition })
            .ToDictionaryAsync(x => x.Id, x => x.Form, cancellationToken);
        var keptKeys = new Dictionary<(Guid Form, Guid Application), IReadOnlySet<string>>();

        var keptVersions = await RewriteAsync(context, context.ReportVersions.OrderBy(x => x.Id), (entry, kept) =>
        {
            var application = keptApplicationForms[kept.ReportId];
            if (!keptKeys.TryGetValue((kept.FormDefinitionId, application.Id), out var keys))
            {
                keys = ReportSensitiveKeysOf(keptForms[kept.FormDefinitionId], application);
                keptKeys[(kept.FormDefinitionId, application.Id)] = keys;
            }

            kept.Answers = SensitiveAnswers.Protect(
                kept.Answers, keys, Data.Configurations.ReportVersionConfiguration.AnswersPurpose);
            kept.Prefill = SensitiveAnswers.Protect(
                kept.Prefill, keys, Data.Configurations.ReportVersionConfiguration.PrefillPurpose);
            entry.Property("Answers").IsModified = true;
            entry.Property("Prefill").IsModified = true;
        }, cancellationToken);

        // Evaluation cards (T-38): their own purpose, their own card version.
        var cards = await context.FormDefinitions.AsNoTracking()
            .Where(x => context.Evaluations.Any(e => e.FormDefinitionId == x.Id))
            .ToDictionaryAsync(x => x.Id, x => SensitiveKeysOf(x), cancellationToken);
        var evaluations = await RewriteAsync(context, context.Evaluations.OrderBy(x => x.Id), (entry, evaluation) =>
        {
            evaluation.Answers = SensitiveAnswers.Protect(
                evaluation.Answers, cards[evaluation.FormDefinitionId], SensitiveAnswers.EvaluationPurpose);
            entry.Property("Answers").IsModified = true;
        }, cancellationToken);

        // The attachments themselves (S-38), not a column: a file written
        // before that card is still plaintext on the volume, and one under an
        // older key has to move too, or retiring that key would leave it
        // unreadable.
        var files = 0;
        foreach (var path in await context.Attachments.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => x.StoragePath)
            .ToListAsync(cancellationToken))
        {
            if (await storage.RewriteAsync(path, cancellationToken))
            {
                files++;
            }
        }

        return
        [
            $"Rewrote with key {version}: {entities} entities, {users} accounts with a PESEL, " +
            $"{applications} applications, {versions} earlier versions, {reports} reports, " +
            $"{keptVersions} earlier report versions, {evaluations} evaluation cards, {contracts} contracts, {files} attachment files.",
        ];
    }

    /// <summary>
    /// The sensitive answers of a form version: from the parsed form, or,
    /// when it no longer passes the contract, from its marks as stored, so
    /// a form that went out of date is never read as "nothing sensitive".
    /// </summary>
    internal static IReadOnlySet<string> SensitiveKeysOf(Models.FormDefinition definition) =>
        FormSchemaValidator.Validate(definition.Definition, definition.Purpose).Document is { } form
            ? SensitiveAnswers.Keys(form)
            : SensitiveAnswers.MarkedKeys(definition.Definition);

    /// <summary>
    /// A report's sensitive answers, tolerant on both sides: the report's own
    /// form and the application it prefills from may each have gone out of
    /// date. Reading them strictly would stop the rotation on that row, and
    /// reports are rewritten last, so the run would end with the rest of the
    /// database under the new key and the reports under the old one.
    /// </summary>
    private static IReadOnlySet<string> ReportSensitiveKeysOf(
        Models.FormDefinition report,
        Models.FormDefinition application)
    {
        var fromApplication = SensitiveKeysOf(application);

        return FormSchemaValidator.Validate(report.Definition, report.Purpose).Document is { } form
            ? SensitiveAnswers.ReportKeys(form, fromApplication)
            : SensitiveAnswers.MarkedReportKeys(report.Definition, fromApplication);
    }

    private static async Task<int> RewriteAsync<T>(
        AppDbContext context,
        IQueryable<T> rows,
        Action<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<T>, T> touch,
        CancellationToken cancellationToken)
        where T : class
    {
        var count = 0;
        for (var skip = 0; ; skip += Batch)
        {
            var batch = await rows.Skip(skip).Take(Batch).ToListAsync(cancellationToken);
            if (batch.Count == 0)
            {
                return count;
            }

            foreach (var row in batch)
            {
                touch(context.Entry(row), row);
            }

            await context.SaveChangesAsync(cancellationToken);
            context.ChangeTracker.Clear();
            count += batch.Count;
        }
    }
}
