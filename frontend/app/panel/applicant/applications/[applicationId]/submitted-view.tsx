import { OfferView } from "@/components/offer-view";
import { statusActionClassName } from "@/components/status-page";
import type { Application, ApplicationForm, Attachment } from "@/lib/applicant-applications";
import { confirmationPdfUrl } from "@/lib/applicant-applications";
import { formatAmount, formatFileSize, formatMoment } from "@/lib/format";
import type { FormAnswers } from "@/lib/forms/answer-types";
import { applicationStatusLabels, attachmentUrl } from "@/lib/operator-applications";

import { EvaluationCards } from "./evaluation-cards";
import { TechnicalBlock } from "./technical-block";

/**
 * A submitted application, read only (T-33, T-34): the confirmation an
 * applicant sees right after submitting and, unchanged, every time they open
 * this same address again later. "Wersja robocza nie może wyglądać jak
 * złożona" (proces.md) holds in the other direction too: a submitted
 * application has nothing left to edit, so this view offers none.
 */
export function SubmittedView({
  application,
  form,
  competitionTitle,
  attachments,
}: {
  application: Application;
  form: ApplicationForm;
  competitionTitle: string;
  attachments: readonly Attachment[];
}) {
  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-2xl">Wniosek {application.number}</h1>
        <p className="text-sm">{competitionTitle}</p>
      </div>

      <p className="text-sm">
        Wniosek został złożony {formatMoment(application.submittedAt!)}. Treść
        jest zamrożona: nie można jej już zmieniać.
      </p>

      <p>
        <a href={confirmationPdfUrl(application.id)} className={statusActionClassName}>
          Pobierz potwierdzenie (PDF)
        </a>
      </p>

      {application.status !== "Submitted" ? (
        <section className="flex flex-col gap-1">
          <h2 className="text-xl">Wynik konkursu</h2>
          <p>{applicationStatusLabels[application.status]}</p>
          {application.awardedGrant !== null && application.awardedGrant !== undefined ? (
            <p>Przyznana kwota: {formatAmount(application.awardedGrant)}</p>
          ) : null}
        </section>
      ) : null}

      <TechnicalBlock application={application} versionNumber={form.versionNumber} />

      <EvaluationCards applicationId={application.id} />

      <section className="flex flex-col gap-2">
        <h2 className="text-xl">Załączniki</h2>
        {attachments.length === 0 ? (
          <p className="text-sm">Do wniosku nie dołączono plików.</p>
        ) : (
          <ul className="flex flex-col gap-1 text-sm">
            {attachments.map((attachment) => (
              <li key={attachment.id}>
                <a href={attachmentUrl(attachment.id)} className="underline">
                  {attachment.fileName}
                </a>{" "}
                ({formatFileSize(attachment.sizeInBytes)})
              </li>
            ))}
          </ul>
        )}
      </section>

      <OfferView document={form.document} answers={application.answers as FormAnswers} />
    </div>
  );
}
