"use client";

import { useEffect, useRef } from "react";

/**
 * The one confirmation before "zapisz i zakończ etap" (T-40): after it the
 * card cannot be edited. A native dialog for the same reason as the
 * applicant's submission (T-34): focus trap and Escape come with it.
 */
export function ConfirmFinishDialog({
  finishing,
  error,
  onCancel,
  onConfirm,
}: {
  finishing: boolean;
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
      aria-labelledby="confirm-finish-title"
      onCancel={(event) => {
        event.preventDefault();
        onCancel();
      }}
      className="rounded-sm border border-border p-6 backdrop:bg-black/40"
    >
      <p id="confirm-finish-title" className="text-lg">
        Po zakończeniu oceny nie będzie można już zmienić karty.
      </p>

      {error !== null ? (
        <p role="alert" className="mt-2 text-sm text-brand-accent-text">
          {error}
        </p>
      ) : null}

      <div className="mt-4 flex justify-end gap-3">
        <button type="button" className="text-sm underline disabled:opacity-40" onClick={onCancel} disabled={finishing}>
          Wróć
        </button>
        <button
          type="button"
          className="rounded-sm bg-brand-accent px-4 py-2 text-sm text-bg hover:bg-brand-accent-hover disabled:opacity-40"
          onClick={onConfirm}
          disabled={finishing}
        >
          {finishing ? "Kończenie…" : "Zakończ ocenę"}
        </button>
      </div>
    </dialog>
  );
}
