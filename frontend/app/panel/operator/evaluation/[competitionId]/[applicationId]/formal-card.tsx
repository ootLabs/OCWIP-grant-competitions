"use client";

import { useState } from "react";

import { EvaluationWorkspace } from "@/components/evaluation/evaluation-workspace";
import { apiErrorMessage } from "@/lib/api-client";
import { openFormalCard } from "@/lib/operator-evaluation";
import type { Evaluation } from "@/lib/reviewer-work";

/**
 * The formal card of the application for the operator's staff (T-41a). Not
 * opened on entry: opening starts a card, and looking at an application is
 * not yet evaluating it.
 */
export function FormalCard({ applicationId, existing }: { applicationId: string; existing: Evaluation | null }) {
  const [evaluation, setEvaluation] = useState(existing);
  const [opening, setOpening] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function open() {
    setOpening(true);
    setError(null);
    try {
      setEvaluation(await openFormalCard(applicationId));
    } catch (failure) {
      setError(apiErrorMessage(failure, "Nie udało się otworzyć karty oceny formalnej."));
    } finally {
      setOpening(false);
    }
  }

  if (evaluation !== null) {
    return <EvaluationWorkspace evaluation={evaluation} />;
  }

  return (
    <section aria-labelledby="karta-formalna" className="flex flex-col gap-2">
      <h2 id="karta-formalna" className="text-xl">
        Karta oceny formalnej
      </h2>
      <p className="text-sm">Ocena formalna tego wniosku jeszcze się nie zaczęła.</p>
      <div>
        <button
          type="button"
          className="rounded-sm bg-brand-accent px-4 py-2 text-sm text-bg hover:bg-brand-accent-hover disabled:opacity-40"
          disabled={opening}
          onClick={() => void open()}
        >
          Rozpocznij ocenę formalną
        </button>
      </div>
      {error !== null ? (
        <p role="alert" className="text-sm">
          {error}
        </p>
      ) : null}
    </section>
  );
}
