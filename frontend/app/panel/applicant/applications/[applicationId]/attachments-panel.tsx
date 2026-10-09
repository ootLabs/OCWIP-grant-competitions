"use client";

import { attachmentRequirementLabels, fileFormatLabels } from "@/app/competitions/labels";
import type { Attachment } from "@/lib/applicant-applications";
import type { CompetitionAttachment } from "@/lib/competitions";

import { AttachmentRow, UploadArea } from "./attachment-upload";

/** Where the submit gate's attachment gaps jump to (draft-workspace.tsx). */
export const attachmentsAnchorId = "zalaczniki";

/** The DOM id of one requirement's tile, so a gap can lead straight to it. */
export function requirementAnchorId(requirementId: string): string {
  return `${attachmentsAnchorId}-${requirementId}`;
}

/**
 * Załączniki (T-34, T-101): one tile per requirement of the competition,
 * with the files that answer it and a place to add one, the way the
 * submission counts them. A file sent without a requirement (before T-101)
 * stays listed below as "Inne pliki": it counts for nothing, and saying so
 * is better than hiding it.
 */
export function AttachmentsPanel({
  applicationId,
  requirements,
  attachments,
  onUploaded,
  onReplaced,
  onWithdrawn,
}: {
  applicationId: string;
  requirements: readonly CompetitionAttachment[];
  attachments: readonly Attachment[];
  onUploaded: (attachment: Attachment) => void;
  /** The id of the row this replaces, then the new row itself: a caller
   * keeping a flat list needs both to drop the old one and add the new. */
  onReplaced: (replacedId: string, attachment: Attachment) => void;
  /** A file taken back (P4-14): the caller drops it from its list. */
  onWithdrawn?: (withdrawnId: string) => void;
}) {
  const known = new Set(requirements.map((item) => item.id));
  const others = attachments.filter((attachment) => !attachment.requirementId || !known.has(attachment.requirementId));

  return (
    <section id={attachmentsAnchorId} className="flex flex-col gap-4">
      <h2 className="text-xl">Załączniki</h2>

      {requirements.length === 0 ? (
        <>
          <p className="text-sm">Ten konkurs nie wymaga żadnych załączników poza samym wnioskiem.</p>
          <UploadArea applicationId={applicationId} onUploaded={onUploaded} />
        </>
      ) : (
        <ul className="flex list-none flex-col gap-3">
          {requirements.map((item) => (
            <RequirementTile
              key={item.id}
              applicationId={applicationId}
              requirement={item}
              files={attachments.filter((attachment) => attachment.requirementId === item.id)}
              onUploaded={onUploaded}
              onReplaced={onReplaced}
              onWithdrawn={onWithdrawn}
            />
          ))}
        </ul>
      )}

      {others.length > 0 && requirements.length > 0 ? (
        <div className="flex flex-col gap-2">
          <h3 className="text-sm font-semibold">Inne pliki</h3>
          <p className="text-sm">
            Te pliki nie są przypisane do żadnego wymaganego załącznika, więc nie liczą się przy złożeniu. Dodaj je
            ponownie w odpowiednim miejscu wyżej.
          </p>
          <ul className="flex flex-col gap-2">
            {others.map((attachment) => (
              <AttachmentRow
                key={attachment.id}
                attachment={attachment}
                onReplaced={onReplaced}
                onWithdrawn={onWithdrawn}
              />
            ))}
          </ul>
        </div>
      ) : null}
    </section>
  );
}

function RequirementTile({
  applicationId,
  requirement,
  files,
  onUploaded,
  onReplaced,
  onWithdrawn,
}: {
  applicationId: string;
  requirement: CompetitionAttachment;
  files: readonly Attachment[];
  onUploaded: (attachment: Attachment) => void;
  onReplaced: (replacedId: string, attachment: Attachment) => void;
  onWithdrawn?: (withdrawnId: string) => void;
}) {
  const headingId = `${requirementAnchorId(requirement.id)}-tytul`;

  return (
    <li
      id={requirementAnchorId(requirement.id)}
      aria-labelledby={headingId}
      className="flex flex-col gap-2 rounded-sm border border-border-muted px-3 py-3 text-sm"
    >
      <h3 id={headingId} className="font-medium">
        {requirement.title}
      </h3>
      <p>
        {attachmentRequirementLabels[requirement.requirement]}
        {requirement.allowedFormats.length === 0
          ? null
          : ` · formaty: ${requirement.allowedFormats.map((format) => fileFormatLabels[format]).join(", ")}`}
        {files.length > 0 ? " · dodano" : " · jeszcze nie dodano"}
      </p>
      {requirement.description === null ? null : <p>{requirement.description}</p>}
      {files.length > 0 ? (
        <ul className="flex flex-col gap-2">
          {files.map((attachment) => (
            <AttachmentRow
              key={attachment.id}
              attachment={attachment}
              onReplaced={onReplaced}
              onWithdrawn={onWithdrawn}
            />
          ))}
        </ul>
      ) : null}
      <UploadArea
        applicationId={applicationId}
        requirementId={requirement.id}
        prompt={`Dodaj plik: ${requirement.title}. Przeciągnij go tutaj albo kliknij, żeby wybrać.`}
        onUploaded={onUploaded}
      />
    </li>
  );
}
