"use client";

import { STEP_LABELS, type WizardStepId } from "@/lib/competition-wizard/types";

/**
 * Status of the wizard's own draft, separate from the confirmed publish
 * action (publish-step.tsx): saving here only ever writes a Draft.
 */
export function SaveBar({
  savedAt,
  saving,
  structuralGaps,
  errorMessage,
  stepsWithErrors,
  onSave,
}: {
  savedAt: string | null;
  saving: boolean;
  structuralGaps: readonly string[];
  errorMessage: string | null;
  stepsWithErrors: ReadonlySet<WizardStepId>;
  onSave: () => void;
}) {
  return (
    <div className="flex flex-col gap-2 rounded-sm border border-border-muted bg-surface-muted px-3 py-2 text-sm">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p>
          {savedAt === null
            ? "Konkurs jeszcze nie zapisany."
            : `Zapisano jako roboczy ${new Date(savedAt).toLocaleString("pl-PL")}.`}
        </p>
        <button
          type="button"
          className="rounded-sm border border-border px-3 py-1.5 disabled:opacity-40"
          disabled={saving}
          onClick={onSave}
        >
          {saving ? "Zapisywanie…" : "Zapisz"}
        </button>
      </div>

      {structuralGaps.length > 0 ? (
        <p>
          Żeby zapisać, uzupełnij najpierw: {structuralGaps.join(", ")}.
        </p>
      ) : null}

      {errorMessage !== null ? (
        <p role="alert" className="text-brand-accent-text">
          {errorMessage}
        </p>
      ) : null}

      {stepsWithErrors.size > 0 ? (
        <p role="alert" className="text-brand-accent-text">
          Sprawdź kroki:{" "}
          {Array.from(stepsWithErrors)
            .map((step) => STEP_LABELS[step])
            .join(", ")}
          .
        </p>
      ) : null}
    </div>
  );
}
