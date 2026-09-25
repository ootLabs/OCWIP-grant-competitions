"use client";

import type { SubmissionGap } from "@/lib/forms/submission-gaps";

export type Stage = "filling" | "reviewing";

/**
 * Always visible bar (T-34, proces.md rule 6): the one "Złóż wniosek" button,
 * disabled with the list of what is still missing while any gap remains, and
 * the same button as the step forward once the form is complete.
 */
export function SubmitBar({
  gaps,
  onJump,
  onContinue,
}: {
  gaps: readonly SubmissionGap[];
  onJump: (gap: SubmissionGap) => void;
  onContinue: () => void;
}) {
  const ready = gaps.length === 0;

  return (
    <div className="flex flex-col gap-2 rounded-sm border border-border-muted bg-surface-muted px-4 py-3 text-sm">
      <div className="flex items-center justify-between gap-3">
        <button
          type="button"
          className="rounded-sm bg-brand-accent px-4 py-2 text-bg hover:bg-brand-accent-hover disabled:cursor-not-allowed disabled:opacity-40"
          disabled={!ready}
          onClick={onContinue}
        >
          Złóż wniosek
        </button>
      </div>

      {!ready ? (
        <div>
          <p>Zanim złożysz wniosek, uzupełnij:</p>
          <ul className="mt-1 flex list-none flex-col gap-1">
            {gaps.map((gap, index) => (
              <li key={`${gap.fieldKey}-${index}`}>
                <button type="button" className="text-left underline" onClick={() => onJump(gap)}>
                  {gap.sectionTitle ? `${gap.sectionTitle}: ` : ""}
                  {gap.fieldLabel} - {gap.message}
                </button>
              </li>
            ))}
          </ul>
        </div>
      ) : null}
    </div>
  );
}
