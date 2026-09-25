import { formatMoment } from "@/lib/format";
import type { Application } from "@/lib/applicant-applications";

/**
 * "Informacje techniczne" (D15, R-13): the number, the form version, when
 * this version was written, and the checksum, together, so a phone call to
 * OCWIP can name exactly which version of the answers it is about.
 */
export function TechnicalBlock({
  application,
  versionNumber,
}: {
  application: Application;
  versionNumber: number;
}) {
  return (
    <div className="rounded-sm border border-border-muted px-4 py-3 text-sm">
      <p className="font-semibold">Informacje techniczne</p>
      <dl className="mt-2 grid grid-cols-[max-content_1fr] gap-x-4 gap-y-1">
        {application.number !== null ? (
          <>
            <dt>Numer wniosku</dt>
            <dd>{application.number}</dd>
          </>
        ) : null}
        <dt>Wersja formularza</dt>
        <dd>{versionNumber}</dd>
        <dt>{application.status !== "Draft" ? "Złożono" : "Zapisano"}</dt>
        <dd>
          {formatMoment(
            application.status !== "Draft"
              ? application.submittedAt!
              : application.lastSavedAt,
          )}
        </dd>
        <dt>Suma kontrolna</dt>
        <dd className="font-mono">{application.checksum}</dd>
      </dl>
    </div>
  );
}
