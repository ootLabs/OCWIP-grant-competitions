"use client";

import type { CompetitionDraft } from "@/lib/competition-wizard/types";

import { FieldError } from "./field-error";

/**
 * Krok 1.3: Forma dostarczenia (docs/runbook/pola.md). "Nie budujemy pod
 * papier osobnego obiegu": ten krok tylko zapisuje wymóg, termin i adres.
 */
export function StepPaper({
  draft,
  onChange,
  fieldErrors,
}: {
  draft: CompetitionDraft;
  onChange: (patch: Partial<CompetitionDraft>) => void;
  fieldErrors: Record<string, string[]>;
}) {
  return (
    <div className="flex flex-col gap-4">
      <label className="flex items-center gap-2 text-sm">
        <input
          type="checkbox"
          checked={draft.requiresPaperSubmission}
          onChange={(event) =>
            onChange({ requiresPaperSubmission: event.target.checked })
          }
        />
        Wymagane jest złożenie dokumentów także w wersji papierowej
      </label>

      {draft.requiresPaperSubmission ? (
        <>
          <label className="flex flex-col gap-1 text-sm">
            Termin składania wersji papierowej
            <input
              type="datetime-local"
              className="w-64 rounded-sm border border-border-control px-2 py-1"
              value={draft.paperSubmissionDeadlineLocal}
              onChange={(event) =>
                onChange({ paperSubmissionDeadlineLocal: event.target.value })
              }
            />
            <FieldError messages={fieldErrors.paperSubmissionDeadline} />
          </label>

          <label className="flex flex-col gap-1 text-sm">
            Adres do złożenia wersji papierowej
            <textarea
              className="rounded-sm border border-border-control px-2 py-1"
              value={draft.paperSubmissionAddress}
              onChange={(event) =>
                onChange({ paperSubmissionAddress: event.target.value })
              }
            />
            <FieldError messages={fieldErrors.paperSubmissionAddress} />
          </label>
        </>
      ) : null}
    </div>
  );
}
