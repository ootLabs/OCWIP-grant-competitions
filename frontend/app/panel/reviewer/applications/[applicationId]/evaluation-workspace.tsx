"use client";

import { useEffect, useRef, useState } from "react";

import { FormRenderer } from "@/components/form-renderer/form-renderer";
import { OfferView } from "@/components/offer-view";
import { apiErrorMessage } from "@/lib/api-client";
import type { FormAnswers } from "@/lib/forms/answer-types";
import { formatAmount } from "@/lib/format";
import { amount, cardOf, finishEvaluation, saveEvaluation, type Evaluation } from "@/lib/reviewer-work";

import { ConfirmFinishDialog } from "./confirm-finish-dialog";

/** A second of quiet before the card saves, the same pace as an application draft (T-29). */
export const AUTOSAVE_DELAY_MS = 1000;

type SaveState = "idle" | "saving" | "saved" | "failed";

/**
 * The expert's merit card (T-40): filled in with the same renderer as an
 * application, saved a second after the last change, and finished only
 * through an explicit confirmation ("zapisz i zakończ etap", step 5.4).
 * A finished card is shown read only; reopening it is P4 on B-02.
 */
export function EvaluationWorkspace({ evaluation: initial }: { evaluation: Evaluation }) {
  const [evaluation, setEvaluation] = useState(initial);
  const [saveState, setSaveState] = useState<SaveState>("idle");
  const [confirming, setConfirming] = useState(false);
  const [finishing, setFinishing] = useState(false);
  const [finishError, setFinishError] = useState<string | null>(null);
  const pending = useRef<FormAnswers | null>(null);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const card = cardOf(evaluation);

  useEffect(
    () => () => {
      if (timer.current) clearTimeout(timer.current);
    },
    [],
  );

  async function flush(): Promise<boolean> {
    if (timer.current) {
      clearTimeout(timer.current);
      timer.current = null;
    }

    const answers = pending.current;
    if (answers === null) return true;
    pending.current = null;

    setSaveState("saving");
    try {
      setEvaluation(await saveEvaluation(evaluation.id, answers));
      setSaveState("saved");
      return true;
    } catch {
      pending.current = answers;
      setSaveState("failed");
      return false;
    }
  }

  function onChange(answers: FormAnswers) {
    pending.current = answers;
    if (timer.current) clearTimeout(timer.current);
    timer.current = setTimeout(() => void flush(), AUTOSAVE_DELAY_MS);
  }

  async function finish() {
    setFinishing(true);
    setFinishError(null);

    try {
      if (!(await flush())) {
        setFinishError("Nie udało się zapisać ostatnich zmian. Spróbuj jeszcze raz.");
        return;
      }
      setEvaluation(await finishEvaluation(evaluation.id));
      setConfirming(false);
    } catch (error) {
      setFinishError(apiErrorMessage(error, "Nie udało się zakończyć oceny."));
    } finally {
      setFinishing(false);
    }
  }

  const finished = evaluation.status === "Finished";

  return (
    <section aria-labelledby="karta" className="flex flex-col gap-4">
      <h2 id="karta" className="text-xl">
        Karta oceny merytorycznej
      </h2>

      <p className="text-sm" aria-live="polite">
        Suma punktów: {amount(evaluation.meritScore) ?? 0}, kryteria strategiczne:{" "}
        {amount(evaluation.strategicScore) ?? 0}
        {amount(evaluation.recommendedGrant) !== null
          ? `, proponowana kwota: ${formatAmount(amount(evaluation.recommendedGrant)!)}`
          : ""}
        .
      </p>

      {finished ? (
        <>
          <p className="text-sm">Ocena zakończona. Karty nie można już zmienić.</p>
          <OfferView document={card.document} answers={card.answers} applicant={card.applicant} />
        </>
      ) : (
        <>
          <FormRenderer
            document={card.document}
            initialAnswers={card.answers}
            competitionSettings={{}}
            applicant={card.applicant}
            onChange={onChange}
          />

          <div className="flex flex-wrap items-center gap-4">
            <p className="text-sm" aria-live="polite">
              {saveState === "saving" ? "Zapisywanie…" : null}
              {saveState === "saved" ? "Zapisano." : null}
              {saveState === "failed" ? "Nie udało się zapisać. Zmiany zostaną wysłane przy następnej próbie." : null}
            </p>
            <button
              type="button"
              className="ml-auto rounded-sm bg-brand-accent px-4 py-2 text-sm text-bg hover:bg-brand-accent-hover"
              onClick={() => setConfirming(true)}
            >
              Zakończ ocenę
            </button>
          </div>
        </>
      )}

      {confirming ? (
        <ConfirmFinishDialog
          finishing={finishing}
          error={finishError}
          onCancel={() => {
            setConfirming(false);
            setFinishError(null);
          }}
          onConfirm={() => void finish()}
        />
      ) : null}
    </section>
  );
}
