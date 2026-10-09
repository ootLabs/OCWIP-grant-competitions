"use client";

import { useEffect, useId, useRef, useState } from "react";

import { apiErrorMessage } from "@/lib/api-client";
import { ConfirmDialog } from "@/components/confirm-dialog";
import {
  replaceAttachment,
  uploadAttachment,
  withdrawAttachment,
  type Attachment,
} from "@/lib/applicant-applications";
import { formatFileSize } from "@/lib/format";

/**
 * Sending a file and replacing one (T-32, T-34): the drop area and the row
 * of an uploaded file, shared by every requirement's tile (T-101).
 */
export function UploadArea({
  applicationId,
  requirementId,
  prompt = "Przeciągnij plik tutaj albo kliknij, żeby go wybrać.",
  onUploaded,
  fileIds = "",
}: {
  applicationId: string;
  /** The requirement the files answer (T-101); none for a file of its own. */
  requirementId?: string;
  prompt?: string;
  onUploaded: (attachment: Attachment) => void;
  /** The ids of the files already answering here, so a change made elsewhere on the tile is noticed. */
  fileIds?: string;
}) {
  const inputId = useId();
  const [dragOver, setDragOver] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // O-09: a refusal stayed on the tile after its files were put right in
  // another way ("Zastąp", "Wycofaj" on a row). It goes once the files
  // change, except when this area's own upload changed them: a partly
  // refused drop has to keep saying which files did not make it.
  const ownChange = useRef(false);
  useEffect(() => {
    if (ownChange.current) {
      ownChange.current = false;
      return;
    }
    setError(null);
  }, [fileIds]);

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
      picked.map((file) => uploadAttachment(applicationId, file, requirementId)),
    );

    const failures: { name: string; message: string }[] = [];
    results.forEach((result, index) => {
      if (result.status === "fulfilled") {
        ownChange.current = true;
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
        <span>{prompt}</span>
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

export function AttachmentRow({
  attachment,
  onReplaced,
  onWithdrawn,
}: {
  attachment: Attachment;
  onReplaced: (replacedId: string, attachment: Attachment) => void;
  /** Absent where files cannot be taken back; the row then offers only "Zastąp". */
  onWithdrawn?: (withdrawnId: string) => void;
}) {
  const inputId = useId();
  const [error, setError] = useState<string | null>(null);
  const [confirming, setConfirming] = useState(false);
  const [withdrawing, setWithdrawing] = useState(false);
  const [withdrawError, setWithdrawError] = useState<string | null>(null);

  async function withdraw() {
    setWithdrawing(true);
    setWithdrawError(null);
    try {
      await withdrawAttachment(attachment.id);
      setConfirming(false);
      onWithdrawn?.(attachment.id);
    } catch (thrown) {
      setWithdrawError(apiErrorMessage(thrown, "Nie udało się wycofać pliku."));
    } finally {
      setWithdrawing(false);
    }
  }

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
        {/* The input inside its label, not beside it: the input is sr-only,
            and the focus ring app/globals.css draws for one goes on the
            label that wraps it, the thing the eye reads as the control. */}
        <label className="cursor-pointer underline">
          Zastąp
          {/* Every row has one, so the name says which file it replaces. */}
          <span className="sr-only"> {attachment.fileName}</span>
          <input
            id={inputId}
            type="file"
            className="sr-only"
            onChange={(event) => {
              void replace(event.target.files);
              event.target.value = "";
            }}
          />
        </label>
        {/* P4-14: a file dropped twice on the same tile, or the wrong file,
            used to stay in the application and go to the experts. */}
        {onWithdrawn ? (
          <button type="button" className="underline" onClick={() => setConfirming(true)}>
            Wycofaj
            <span className="sr-only"> {attachment.fileName}</span>
          </button>
        ) : null}
      </span>
      {confirming ? (
        <ConfirmDialog
          title={`Wycofać plik ${attachment.fileName}? Nie będzie częścią wniosku.`}
          confirmLabel="Wycofaj plik"
          busyLabel="Wycofywanie…"
          busy={withdrawing}
          error={withdrawError}
          onCancel={() => {
            setConfirming(false);
            setWithdrawError(null);
          }}
          onConfirm={() => void withdraw()}
        />
      ) : null}
    </li>
  );
}
