"use client";

import type { CompetitionDraft } from "@/lib/competition-wizard/types";

import { FieldError } from "./field-error";

/** Krok 1.2: Opis konkursu (docs/runbook/pola.md). */
export function StepDescription({
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
      <label className="flex flex-col gap-1 text-sm">
        Opis konkursu: cel, kto może startować, na co
        <textarea
          rows={6}
          className="rounded-sm border border-border-control px-2 py-1"
          value={draft.description}
          onChange={(event) => onChange({ description: event.target.value })}
        />
        <FieldError messages={fieldErrors.description} />
      </label>

      <label className="flex flex-col gap-1 text-sm">
        Zakładane rezultaty konkursu
        <textarea
          className="rounded-sm border border-border-control px-2 py-1"
          value={draft.expectedResults}
          onChange={(event) =>
            onChange({ expectedResults: event.target.value })
          }
        />
        <FieldError messages={fieldErrors.expectedResults} />
      </label>

      <label className="flex flex-col gap-1 text-sm">
        Adres strony z regulaminem
        <input
          type="url"
          placeholder="https://"
          className="rounded-sm border border-border-control px-2 py-1"
          value={draft.rulesUrl}
          onChange={(event) => onChange({ rulesUrl: event.target.value })}
        />
        <FieldError messages={fieldErrors.rulesUrl} />
      </label>
    </div>
  );
}
