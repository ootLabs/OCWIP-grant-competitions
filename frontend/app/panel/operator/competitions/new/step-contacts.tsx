"use client";

import type { OperatorAccount } from "@/lib/operator-competitions";
import type { CompetitionDraft } from "@/lib/competition-wizard/types";

import { FieldError } from "./field-error";

/** Krok 1.6: Osoby kontaktowe (docs/runbook/pola.md). */
export function StepContacts({
  draft,
  onChange,
  fieldErrors,
  operators,
}: {
  draft: CompetitionDraft;
  onChange: (patch: Partial<CompetitionDraft>) => void;
  fieldErrors: Record<string, string[]>;
  /** null while the staff directory (GET /accounts/operators) is loading. */
  operators: OperatorAccount[] | null;
}) {
  const toggle = (userId: string, enabled: boolean) => {
    onChange({
      contactUserIds: enabled
        ? [...draft.contactUserIds, userId]
        : draft.contactUserIds.filter((id) => id !== userId),
    });
  };

  return (
    <div className="flex flex-col gap-4">
      <fieldset className="flex flex-col gap-1 text-sm">
        <legend>
          Osoby do kontaktu w konkursie, widoczne dla wnioskodawców na
          stronie konkursu
        </legend>

        {operators === null ? (
          <p>Wczytywanie listy pracowników…</p>
        ) : operators.length === 0 ? (
          <p>Brak aktywnych kont pracowników do wyboru.</p>
        ) : (
          operators.map((operator) => (
            <label key={operator.id} className="flex items-center gap-2">
              <input
                type="checkbox"
                checked={draft.contactUserIds.includes(operator.id)}
                onChange={(event) => toggle(operator.id, event.target.checked)}
              />
              {operator.firstName} {operator.lastName} ({operator.email})
            </label>
          ))
        )}
        <FieldError messages={fieldErrors.contactUserIds} />
      </fieldset>

      <label className="flex flex-col gap-1 text-sm">
        Treść wiadomości e-mail wysyłanej po złożeniu wniosku
        <textarea
          className="rounded-sm border border-border px-2 py-1"
          value={draft.submissionEmailBody}
          onChange={(event) =>
            onChange({ submissionEmailBody: event.target.value })
          }
        />
        <FieldError messages={fieldErrors.submissionEmailBody} />
      </label>
    </div>
  );
}
