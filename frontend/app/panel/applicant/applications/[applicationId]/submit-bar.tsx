"use client";

import { useState } from "react";

import type { SubmissionGap } from "@/lib/forms/submission-gaps";
import { cardClassName, primaryActionClassName } from "@/components/ui/styles";

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
  closed = false,
}: {
  gaps: readonly SubmissionGap[];
  onJump: (gap: SubmissionGap) => void;
  onContinue: () => void;
  /** The intake or the correction window has closed: nothing can be submitted (O-19). */
  closed?: boolean;
}) {
  const [showAll, setShowAll] = useState(false);
  const ready = gaps.length === 0;
  // A new draft lacks dozens of answers. The first few are the next thing to
  // do; the whole list waits one click away, so the side column this card
  // sits in stays about one screen tall instead of three.
  const shown = showAll ? gaps : gaps.slice(0, SHORT_LIST);

  return (
    <div className={`${cardClassName} flex flex-col gap-4 p-5 text-sm`}>
      <button type="button" className={`${primaryActionClassName} w-full`} disabled={!ready || closed} onClick={onContinue}>
        Złóż wniosek
      </button>

      {!ready ? (
        <div className="flex flex-col gap-2">
          <p className="font-semibold">Zanim złożysz wniosek, uzupełnij ({gaps.length}):</p>
          <ul className="flex list-none flex-col gap-2">
            {shown.map((gap, index) => (
              <li className="flex gap-2" key={`${gap.fieldKey}-${index}`}>
                <span aria-hidden="true" className="mt-1.5 size-1.5 shrink-0 rounded-full bg-status-negative-text" />
                <button type="button" className="text-left text-brand-accent-text underline" onClick={() => onJump(gap)}>
                  {gap.sectionTitle ? `${gap.sectionTitle}: ` : ""}
                  {gap.fieldLabel} - {gap.message}
                </button>
              </li>
            ))}
          </ul>
          {gaps.length > SHORT_LIST ? (
            <button
              type="button"
              aria-expanded={showAll}
              className="self-start font-semibold underline"
              onClick={() => setShowAll((value) => !value)}
            >
              {showAll ? "Pokaż mniej" : `Pokaż wszystkie (${gaps.length})`}
            </button>
          ) : null}
        </div>
      ) : closed ? null : (
        <p>Wniosek jest kompletny. Sprawdź podsumowanie i złóż go.</p>
      )}
    </div>
  );
}

/** How many gaps are listed before the rest is folded away. */
const SHORT_LIST = 5;
