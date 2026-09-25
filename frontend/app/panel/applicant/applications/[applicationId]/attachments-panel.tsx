"use client";

import { useId, useState } from "react";

import { attachmentRequirementLabels, fileFormatLabels } from "@/app/competitions/labels";
import { apiErrorMessage } from "@/lib/api-client";
import {
  replaceAttachment,
  uploadAttachment,
  type Attachment,
} from "@/lib/applicant-applications";
import type { CompetitionAttachment } from "@/lib/competitions";
import { formatFileSize } from "@/lib/format";

/** Where the submit gate's coarse attachment check jumps to (draft-workspace.tsx). */
export const attachmentsAnchorId = "zalaczniki";

/**
 * Co przygotować, i co już dołączono (T-34, proces.md rule 7: "załącznik to
 * jeden ruch").
 *
 * The two lists are deliberately not paired one to one: `attachments` does
 * not name which requirement it answers, R-30/rozbieznosci.md, so this shows
 * what is wanted and what has been added as two honest lists rather than
 * pretending a link the backend cannot yet make. "Dodane pliki" is what T-33
 * already checks is present at all before submission; matching a specific
 * file to a specific requirement stays a human judgement until that link
 * exists.
 */
export function AttachmentsPanel({
  applicationId,
  requirements,
  attachments,
  onUploaded,
  onReplaced,
}: {
  applicationId: string;
  requirements: readonly CompetitionAttachment[];
  attachments: readonly Attachment[];
  onUploaded: (attachment: Attachment) => void;
  /** The id of the row this replaces, then the new row itself: a caller
   * keeping a flat list needs both to drop the old one and add the new. */
  onReplaced: (replacedId: string, attachment: Attachment) => void;
}) {
  const required = requirements.filter((item) => item.requirement !== "Optional");
  const optional = requirements.filter((item) => item.requirement === "Optional");

  return (
    <section id={attachmentsAnchorId} className="flex flex-col gap-4">
      <h2 className="text-xl">Załączniki</h2>

      {requirements.length > 0 ? (
        <div className="flex flex-col gap-4">
          <RequirementList title="Wymagane" items={required} />
          <RequirementList title="Nieobowiązkowe" items={optional} />
        </div>
      ) : (
        <p className="text-sm">Ten konkurs nie wymaga żadnych załączników poza samym wnioskiem.</p>
      )}

      <UploadArea applicationId={applicationId} onUploaded={onUploaded} />

      {attachments.length > 0 ? (
        <ul className="flex flex-col gap-2">
          {attachments.map((attachment) => (
            <AttachmentRow key={attachment.id} attachment={attachment} onReplaced={onReplaced} />
          ))}
        </ul>
      ) : null}
    </section>
  );
}

function RequirementList({
  title,
  items,
}: {
  title: string;
  items: readonly CompetitionAttachment[];
}) {
  if (items.length === 0) {
    return null;
  }

  return (
    <div>
      <h3 className="text-sm font-semibold">{title}</h3>
      <ul className="mt-2 flex list-none flex-col gap-2">
        {items.map((item) => (
          <li key={item.id} className="rounded-sm border border-border-muted px-3 py-2 text-sm">
            <p className="font-medium">{item.title}</p>
            <p>
              {attachmentRequirementLabels[item.requirement]}
              {item.allowedFormats.length === 0
                ? null
                : ` · formaty: ${item.allowedFormats.map((format) => fileFormatLabels[format]).join(", ")}`}
            </p>
            {item.description === null ? null : <p>{item.description}</p>}
          </li>
        ))}
      </ul>
    </div>
  );
}

function UploadArea({
  applicationId,
  onUploaded,
}: {
  applicationId: string;
  onUploaded: (attachment: Attachment) => void;
}) {
  const inputId = useId();
  const [dragOver, setDragOver] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function upload(files: FileList | null) {
    if (files === null || files.length === 0) {
      return;
    }

    setUploading(true);
    setError(null);

    // One file failing (wrong format, too large) must not stop the rest of
    // the batch from going up: a drop of several files is one gesture, and
    // an applicant should not have to notice which ones silently never made
    // it and redo the drop just for those.
    const picked = Array.from(files);
    const results = await Promise.allSettled(
      picked.map((file) => uploadAttachment(applicationId, file)),
    );

    const failures: { name: string; message: string }[] = [];
    results.forEach((result, index) => {
      if (result.status === "fulfilled") {
        onUploaded(result.value);
      } else {
        failures.push({
          name: picked[index]!.name,
          message: apiErrorMessage(result.reason, "Nie udało się przesłać pliku."),
        });
      }
    });

    if (failures.length === 1) {
      // The single file case (by far the common one) keeps showing exactly
      // what the backend said was wrong with it, with nothing else to name.
      setError(failures[0]!.message);
    } else if (failures.length > 1) {
      setError(failures.map((failure) => `${failure.name}: ${failure.message}`).join(" "));
    }

    setUploading(false);
  }

  return (
    <div>
      {/* The label wraps the input, so a click or a keyboard activation on
          the whole area opens the file picker without any script: dragging
          is one way in, never the only one (T-34's own checklist). */}
      <label
        htmlFor={inputId}
        onDragOver={(event) => {
          event.preventDefault();
          setDragOver(true);
        }}
        onDragLeave={() => setDragOver(false)}
        onDrop={(event) => {
          event.preventDefault();
          setDragOver(false);
          void upload(event.dataTransfer.files);
        }}
        className={`flex cursor-pointer flex-col items-center gap-1 rounded-sm border border-dashed px-4 py-6 text-center text-sm ${
          dragOver ? "border-brand-accent bg-surface-muted" : "border-border"
        }`}
      >
        <span>Przeciągnij plik tutaj albo kliknij, żeby go wybrać.</span>
        <input
          id={inputId}
          type="file"
          className="sr-only"
          disabled={uploading}
          onChange={(event) => {
            void upload(event.target.files);
            event.target.value = "";
          }}
        />
      </label>

      {uploading ? <p className="mt-1 text-sm">Przesyłanie…</p> : null}
      {error !== null ? (
        <p role="alert" className="mt-1 text-sm text-brand-accent-text">
          {error}
        </p>
      ) : null}
    </div>
  );
}

function AttachmentRow({
  attachment,
  onReplaced,
}: {
  attachment: Attachment;
  onReplaced: (replacedId: string, attachment: Attachment) => void;
}) {
  const inputId = useId();
  const [error, setError] = useState<string | null>(null);

  async function replace(files: FileList | null) {
    const file = files?.[0];
    if (file === undefined) {
      return;
    }

    setError(null);

    try {
      onReplaced(attachment.id, await replaceAttachment(attachment.id, file));
    } catch (thrown) {
      setError(apiErrorMessage(thrown, "Nie udało się zastąpić pliku."));
    }
  }

  return (
    <li className="flex items-center justify-between gap-3 rounded-sm border border-border-muted px-3 py-2 text-sm">
      <span>
        {attachment.fileName} ({formatFileSize(attachment.sizeInBytes)})
      </span>
      <span className="flex items-center gap-2">
        {error !== null ? (
          <span role="alert" className="text-brand-accent-text">
            {error}
          </span>
        ) : null}
        <label htmlFor={inputId} className="cursor-pointer underline">
          Zastąp
        </label>
        <input
          id={inputId}
          type="file"
          className="sr-only"
          onChange={(event) => {
            void replace(event.target.files);
            event.target.value = "";
          }}
        />
      </span>
    </li>
  );
}
