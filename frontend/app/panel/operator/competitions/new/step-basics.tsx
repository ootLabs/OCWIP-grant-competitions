"use client";

import Link from "next/link";

import { describeDeadline } from "@/lib/competition-wizard/intake-sentence";
import type { CompetitionDraft } from "@/lib/competition-wizard/types";

import { FieldError } from "./field-error";

/**
 * Krok 1.1: Dane konkursu (docs/runbook/pola.md). "Data publikacji konkursu"
 * from the report's own table of this step is deliberately not here:
 * publishing stays the confirmed click this card itself asks for (R-27 in
 * docs/runbook/rozbieznosci.md is still open).
 */
export function StepBasics({
  draft,
  onChange,
  fieldErrors,
  competitionId,
}: {
  draft: CompetitionDraft;
  onChange: (patch: Partial<CompetitionDraft>) => void;
  fieldErrors: Record<string, string[]>;
  competitionId: string | null;
}) {
  const deadlineSentence = describeDeadline(
    draft.endDateLocal,
    draft.isContinuousIntake,
  );

  return (
    <div className="flex flex-col gap-4">
      <label className="flex flex-col gap-1 text-sm">
        Numer konkursu
        <input
          className="w-64 rounded-sm border border-border px-2 py-1"
          placeholder="np. 1/2026"
          value={draft.number}
          onChange={(event) => onChange({ number: event.target.value })}
        />
        <FieldError messages={fieldErrors.number} />
      </label>

      <label className="flex flex-col gap-1 text-sm">
        Tytuł konkursu
        <input
          className="rounded-sm border border-border px-2 py-1"
          value={draft.title}
          onChange={(event) => onChange({ title: event.target.value })}
        />
        <FieldError messages={fieldErrors.title} />
      </label>

      <label className="flex items-center gap-2 text-sm">
        <input
          type="checkbox"
          checked={draft.isContinuousIntake}
          onChange={(event) =>
            onChange({ isContinuousIntake: event.target.checked })
          }
        />
        Nabór ciągły, bez terminu zakończenia
      </label>

      <label className="flex flex-col gap-1 text-sm">
        Rozpoczęcie naboru wniosków
        <input
          type="datetime-local"
          className="w-64 rounded-sm border border-border px-2 py-1"
          value={draft.startDateLocal}
          onChange={(event) => onChange({ startDateLocal: event.target.value })}
        />
        <FieldError messages={fieldErrors.startDate} />
      </label>

      {draft.isContinuousIntake ? null : (
        <label className="flex flex-col gap-1 text-sm">
          Zakończenie naboru wniosków
          <input
            type="datetime-local"
            className="w-64 rounded-sm border border-border px-2 py-1"
            value={draft.endDateLocal}
            onChange={(event) =>
              onChange({ endDateLocal: event.target.value })
            }
          />
          <FieldError messages={fieldErrors.endDate} />
        </label>
      )}

      {deadlineSentence !== null ? (
        <p className="rounded-sm border border-border-muted bg-surface-muted px-3 py-2 text-sm">
          {deadlineSentence}
        </p>
      ) : null}

      <label className="flex flex-col gap-1 text-sm">
        Informacja pokazywana po złożeniu wniosku
        <textarea
          className="rounded-sm border border-border px-2 py-1"
          value={draft.submissionNotice}
          onChange={(event) =>
            onChange({ submissionNotice: event.target.value })
          }
        />
        <FieldError messages={fieldErrors.submissionNotice} />
      </label>

      <div className="flex flex-col gap-1 rounded-sm border border-border-muted px-3 py-2 text-sm">
        <p className="font-semibold">Formularz wniosku</p>
        {competitionId === null ? (
          <p>Zapisz konkurs, żeby wybrać albo zbudować formularz wniosku.</p>
        ) : (
          <Link
            className="text-text-link underline"
            href={`/panel/operator/forms/${competitionId}`}
          >
            Przejdź do kreatora formularza wniosku
          </Link>
        )}
      </div>
    </div>
  );
}
