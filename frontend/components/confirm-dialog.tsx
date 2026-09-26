"use client";

import { useEffect, useRef } from "react";

/**
 * A confirmation before a step that cannot be simply undone, as a native
 * dialog (focus trap and Escape come with it, like the other confirmations
 * of this product). Introduced with the reports (T-50a); the older dialogs
 * that say the same thing in their own words can move here later.
 */
export function ConfirmDialog({
  title,
  confirmLabel,
  busyLabel,
  busy,
  error,
  onCancel,
  onConfirm,
}: {
  title: string;
  confirmLabel: string;
  busyLabel: string;
  busy: boolean;
  error: string | null;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  const ref = useRef<HTMLDialogElement>(null);

  useEffect(() => {
    ref.current?.showModal?.();
  }, []);

  return (
    <dialog
      ref={ref}
      aria-labelledby="confirm-dialog-title"
      onCancel={(event) => {
        event.preventDefault();
        onCancel();
      }}
      className="rounded-sm border border-border p-6 backdrop:bg-black/40"
    >
      <p id="confirm-dialog-title" className="text-lg">
        {title}
      </p>
      {error !== null ? (
        <p role="alert" className="mt-2 text-sm text-brand-accent-text">
          {error}
        </p>
      ) : null}
      <div className="mt-4 flex justify-end gap-3">
        <button type="button" className="text-sm underline disabled:opacity-40" onClick={onCancel} disabled={busy}>
          Wróć
        </button>
        <button
          type="button"
          className="rounded-sm bg-brand-accent px-4 py-2 text-sm text-bg hover:bg-brand-accent-hover disabled:opacity-40"
          onClick={onConfirm}
          disabled={busy}
        >
          {busy ? busyLabel : confirmLabel}
        </button>
      </div>
    </dialog>
  );
}
