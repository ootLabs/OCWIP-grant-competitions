using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Data;
using Ocwip.Api.Data.Encryption;
using Ocwip.Api.Models.Forms;
using Ocwip.Api.Services.Reports;

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

    public static async Task<IReadOnlyList<string>> ExecuteAsync(AppDbContext context, CancellationToken cancellationToken)
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
            var keys = SensitiveAnswers.ReportKeys(
                ReportReader.Document(report.FormDefinition), ReportReader.Document(report.Application.FormDefinition));
            report.Answers = SensitiveAnswers.Protect(report.Answers, keys, SensitiveAnswers.ReportPurpose);
            report.Prefill = SensitiveAnswers.Protect(report.Prefill, keys, SensitiveAnswers.ReportPurpose);
            entry.Property("Answers").IsModified = true;
            entry.Property("Prefill").IsModified = true;
        }, cancellationToken);

        return
        [
            $"Rewrote with key {version}: {entities} entities, {users} accounts with a PESEL, " +
            $"{applications} applications, {versions} earlier versions, {reports} reports, {contracts} contracts.",
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
