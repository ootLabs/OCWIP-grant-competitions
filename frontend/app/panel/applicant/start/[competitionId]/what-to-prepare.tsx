import { attachmentRequirementLabels, fileFormatLabels } from "@/app/competitions/labels";
import type { PublicCompetition } from "@/lib/competitions";
import { formatAmount, formatFileSize, formatMoment } from "@/lib/format";

/**
 * "Co przygotować" (T-99, R-10): before the first field of the application,
 * what the applicant will need, drawn from the competition itself: its
 * deadline, the attachments it requires and their formats, paper submission
 * and the grant ceiling. Nothing here is typed in twice; a competition with
 * no required attachment simply shows none.
 */
export function WhatToPrepare({ competition }: { competition: PublicCompetition }) {
  const attachments = competition.attachments;

  return (
    <section aria-labelledby="co-przygotowac" className="flex flex-col gap-2 rounded-sm border border-border-muted px-4 py-3">
      <h2 id="co-przygotowac" className="text-lg">
        Co przygotować
      </h2>
      <ul className="list-disc pl-5 text-sm">
        <li>{competition.intake.message}</li>
        <li>Dotacja do {formatAmount(competition.maxGrantAmount)} na jeden wniosek.</li>
        <li>
          Dane organizacji albo grupy: NIP, numer w rejestrze i rachunek bankowy organizacji, a przy
          grupie dane trzech jej członków.
        </li>
        {attachments.length === 0 ? (
          <li>Konkurs nie wymaga załączników.</li>
        ) : (
          <li>
            Załączniki, każdy do {formatFileSize(competition.maxAttachmentSizeInBytes)}, razem do{" "}
            {formatFileSize(competition.maxApplicationSizeInBytes)}:
            <ul className="list-disc pl-5">
              {attachments.map((attachment) => (
                <li key={attachment.id}>
                  {attachment.title} ({attachmentRequirementLabels[attachment.requirement].toLowerCase()},{" "}
                  {attachment.allowedFormats.map((format) => fileFormatLabels[format]).join(", ")})
                </li>
              ))}
            </ul>
          </li>
        )}
        {competition.requiresPaperSubmission ? (
          <li>
            Wersję papierową trzeba dostarczyć
            {competition.paperSubmissionDeadline ? ` do ${formatMoment(competition.paperSubmissionDeadline)}` : ""}
            {competition.paperSubmissionAddress ? ` na adres: ${competition.paperSubmissionAddress}` : ""}.
          </li>
        ) : null}
        {competition.rulesUrl ? (
          <li>
            <a className="text-text-link underline" href={competition.rulesUrl} target="_blank" rel="noopener noreferrer">
              Regulamin konkursu
            </a>
          </li>
        ) : null}
      </ul>
    </section>
  );
}
