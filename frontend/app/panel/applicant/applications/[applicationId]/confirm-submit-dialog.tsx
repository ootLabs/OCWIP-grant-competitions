"use client";

import { useEffect, useRef } from "react";

/**
 * "Jedno okno potwierdzenia przy złożeniu, jedyne w całej ścieżce"
 * (T-34, proces.md rule 1). A native `<dialog>` rather than a hand rolled
 * overlay: `showModal()` traps focus and answers Escape on its own, which is
 * exactly what "cała ścieżka przechodzi się klawiaturą" asks for here
 * without writing a focus trap by hand.
 */
export function ConfirmSubmitDialog({
  submitting,
  error,
  onCancel,
  onConfirm,
}: {
  submitting: boolean;
  error: string | null;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  const ref = useRef<HTMLDialogElement>(null);

  useEffect(() => {
    // Optional even on the method itself: jsdom (this component's own
    // tests) has no <dialog> support at all, and a crash here would be
    // this component's whole render, not only the modal behaviour.
    ref.current?.showModal?.();
  }, []);

  return (
    <dialog
      ref={ref}
      aria-labelledby="confirm-submit-title"
      onCancel={(event) => {
        // The native Escape close: routed through the same handler as the
        // button so this component's `open` prop (via unmounting) and the
        // dialog's own state can never disagree.
        event.preventDefault();
        onCancel();
      }}
      className="rounded-sm border border-border p-6 backdrop:bg-black/40"
    >
      <p id="confirm-submit-title" className="text-lg">
        Po złożeniu wniosku nie będzie można go już edytować.
      </p>

      {error !== null ? (
        <p role="alert" className="mt-2 text-sm text-brand-accent-text">
          {error}
        </p>
      ) : null}

      <div className="mt-4 flex justify-end gap-3">
        <button
          type="button"
          className="text-sm underline disabled:opacity-40"
          onClick={onCancel}
          disabled={submitting}
        >
          Wróć
        </button>
        <button
          type="button"
          className="rounded-sm bg-brand-accent px-4 py-2 text-sm text-bg hover:bg-brand-accent-hover disabled:opacity-40"
          onClick={onConfirm}
          disabled={submitting}
        >
          {submitting ? "Składanie…" : "Złóż wniosek"}
        </button>
      </div>
    </dialog>
  );
}
