"use client";

import { useState } from "react";

import { ConfirmDialog } from "@/components/confirm-dialog";
import { ApiError, apiErrorMessage } from "@/lib/api-client";
import {
  changeCompetitionStatus,
  deactivateCompetition,
  restoreCompetition,
  type CompetitionStatus,
  type OperatorCompetition,
} from "@/lib/operator-competitions";

type Action =
  | { readonly kind: "status"; readonly target: CompetitionStatus }
  | { readonly kind: "deactivate" }
  | { readonly kind: "restore" };

/** What each move is called on the button and what the operator is asked to confirm. */
const moves: Partial<Record<CompetitionStatus, { readonly label: string; readonly confirm: string }>> = {
  Published: {
    label: "Opublikuj konkurs",
    confirm:
      "Opublikowany konkurs jest widoczny publicznie i od rozpoczęcia naboru przyjmuje wnioski. Opublikować?",
  },
  Closed: {
    label: "Zamknij nabór",
    confirm: "Po zamknięciu naboru nikt nie złoży już wniosku, także przy naborze ciągłym. Zamknąć?",
  },
  UnderReview: {
    label: "Rozpocznij ocenę",
    confirm: "Konkurs przejdzie do oceny. Rozpocząć?",
  },
  Archived: {
    label: "Przenieś do archiwum",
    confirm: "Konkurs zniknie z listy aktualnych konkursów, a jego strona zostanie pod tym samym adresem. Przenieść?",
  },
};

/**
 * The lifecycle buttons of the competition page (T-97), drawn from the
 * competition's own `allowedTransitions`: the transition table stays the one
 * answer to "what can happen next", and this file only names the moves.
 * Resolving is not among them: it comes with approving the results on the
 * evaluation screen, in the same transaction.
 */
export function StatusActions({
  competition,
  onChanged,
}: {
  competition: OperatorCompetition;
  onChanged: (competition: OperatorCompetition) => void;
}) {
  const [pending, setPending] = useState<Action | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [gaps, setGaps] = useState<string[]>([]);

  async function run(action: Action) {
    setBusy(true);
    setError(null);
    setGaps([]);

    try {
      const updated =
        action.kind === "status"
          ? await changeCompetitionStatus(competition.id, action.target)
          : action.kind === "deactivate"
            ? await deactivateCompetition(competition.id)
            : await restoreCompetition(competition.id);
      setPending(null);
      onChanged(updated);
    } catch (failure) {
      if (failure instanceof ApiError && failure.fieldErrors["publication"]) {
        setGaps(failure.fieldErrors["publication"]);
      }
      setError(apiErrorMessage(failure, "Nie udało się zmienić stanu konkursu. Spróbuj ponownie."));
      // Closed, so the reason and the list of gaps show under the buttons.
      setPending(null);
    } finally {
      setBusy(false);
    }
  }

  const title =
    pending === null
      ? ""
      : pending.kind === "status"
        ? (moves[pending.target]?.confirm ?? "")
        : pending.kind === "deactivate"
          ? "Dezaktywowany konkurs znika z listy publicznej. Dane zostają i konkurs da się przywrócić. Dezaktywować?"
          : "Przywrócić konkurs?";

  const targets = competition.allowedTransitions.filter((target) => moves[target] !== undefined);

  return (
    <section className="flex flex-col gap-2">
      <h2 className="text-xl">Co dalej</h2>

      {competition.isActive ? (
        <div className="flex flex-wrap gap-3">
          {targets.map((target) => (
            <button
              key={target}
              type="button"
              className="rounded-sm border border-brand-accent px-4 py-2 text-sm text-brand-accent-text hover:bg-brand-accent hover:text-bg disabled:opacity-40"
              disabled={busy || (target === "Published" && competition.publicationGaps.length > 0)}
              onClick={() => setPending({ kind: "status", target })}
            >
              {moves[target]!.label}
            </button>
          ))}
          <button
            type="button"
            className="text-sm underline"
            disabled={busy}
            onClick={() => setPending({ kind: "deactivate" })}
          >
            Dezaktywuj konkurs
          </button>
        </div>
      ) : (
        <div className="flex flex-col gap-2 text-sm">
          <p>Konkurs jest dezaktywowany: nie widać go publicznie i nic w nim nie można zmienić.</p>
          <button
            type="button"
            className="self-start rounded-sm border border-brand-accent px-4 py-2 text-brand-accent-text hover:bg-brand-accent hover:text-bg"
            disabled={busy}
            onClick={() => setPending({ kind: "restore" })}
          >
            Przywróć konkurs
          </button>
        </div>
      )}

      {competition.status === "UnderReview" ? (
        <p className="text-sm">Konkurs rozstrzyga zatwierdzenie wyników na ekranie oceny.</p>
      ) : null}

      {error !== null && pending === null ? (
        <div role="alert" className="text-sm text-brand-accent-text">
          <p>{error}</p>
          {gaps.length > 0 ? (
            <ul className="list-disc pl-5">
              {gaps.map((gap) => (
                <li key={gap}>{gap}</li>
              ))}
            </ul>
          ) : null}
        </div>
      ) : null}

      {pending !== null ? (
        <ConfirmDialog
          title={title}
          confirmLabel="Tak"
          busyLabel="Zapisywanie…"
          busy={busy}
          error={null}
          onCancel={() => {
            setPending(null);
            setError(null);
          }}
          onConfirm={() => void run(pending)}
        />
      ) : null}
    </section>
  );
}
