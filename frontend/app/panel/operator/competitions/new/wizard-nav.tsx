"use client";

import { STEP_LABELS, WIZARD_STEPS, type WizardStepId } from "@/lib/competition-wizard/types";

/**
 * The seven steps plus the summary, always clickable in any order (T-22:
 * "walidacja nie blokuje przechodzenia między krokami"). A step with a
 * backend field error lit up gets a mark, but nothing here is disabled.
 */
export function WizardNav({
  current,
  stepsWithErrors,
  onSelect,
}: {
  current: WizardStepId;
  stepsWithErrors: ReadonlySet<WizardStepId>;
  onSelect: (step: WizardStepId) => void;
}) {
  return (
    <nav aria-label="Kroki kreatora ogłoszenia konkursu">
      <ol className="flex flex-col gap-1">
        {WIZARD_STEPS.map((step) => {
          const isCurrent = step === current;
          const hasError = stepsWithErrors.has(step);

          return (
            <li key={step}>
              <button
                type="button"
                aria-current={isCurrent ? "step" : undefined}
                className={`flex w-full items-center justify-between gap-2 rounded-sm border px-3 py-2 text-left text-sm ${
                  isCurrent
                    ? "border-brand-accent bg-surface-muted"
                    : "border-border-muted"
                }`}
                onClick={() => onSelect(step)}
              >
                <span>{STEP_LABELS[step]}</span>
                {hasError ? (
                  <span
                    aria-label="Ten krok ma błąd do poprawy"
                    className="text-brand-accent-text"
                  >
                    ●
                  </span>
                ) : null}
              </button>
            </li>
          );
        })}
      </ol>
    </nav>
  );
}
