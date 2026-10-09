"use client";

import { useState } from "react";

import { ReturnForm } from "@/app/panel/operator/applications/[competitionId]/[applicationId]/return-panel";
import { EvaluationWorkspace } from "@/components/evaluation/evaluation-workspace";
import { apiErrorMessage } from "@/lib/api-client";
import type { FormDocument } from "@/lib/forms/document-types";
import { formalShortcomings, openFormalCard } from "@/lib/operator-evaluation";
import type { ApplicationStatus } from "@/lib/operator-applications";
import type { Evaluation } from "@/lib/reviewer-work";

/**
 * The formal card of the application for the operator's staff (T-41a). Not
 * opened on entry: opening starts a card, and looking at an application is
 * not yet evaluating it.
 */
export function FormalCard({
  applicationId,
  existing,
  status,
  document,
}: {
  applicationId: string;
  existing: Evaluation | null;
  /** The application's status: a return is offered only for a submitted one. */
  status: ApplicationStatus;
  /** The application's form, whose sections a return unlocks. */
  document: FormDocument;
}) {
  const [evaluation, setEvaluation] = useState(existing);
  const [returned, setReturned] = useState(false);
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
    // "Wynik negatywny nie zamyka sprawy automatycznie: obok wyniku stoi
    // przycisk zwrotu do poprawy z gotową listą braków z karty" (M5-ocena,
    // P4-17). Offered once the card is finished, so the note filled in from
    // it is the whole list, not the state of the card mid-typing; the
    // operator can still edit it before sending.
    const offerReturn =
      evaluation.status === "Finished" && evaluation.formalPassed === false && status === "Submitted" && !returned;
    return (
      <>
        <EvaluationWorkspace evaluation={evaluation} onEvaluationChange={setEvaluation} />
        {offerReturn ? (
          <section aria-labelledby="zwrot-z-oceny" className="flex flex-col gap-2">
            <h3 id="zwrot-z-oceny" className="text-lg">
              Zwrot do poprawy zamiast odrzucenia
            </h3>
            <p className="text-sm">
              Wynik negatywny nie zamyka sprawy. Jeśli braki da się uzupełnić, zwróć wniosek do poprawy: opis
              poniżej wypełnił się kryteriami ocenionymi na &bdquo;Nie&rdquo;.
            </p>
            <ReturnForm
              applicationId={applicationId}
              document={document}
              initialMessage={formalShortcomings(evaluation)}
              onReturned={() => setReturned(true)}
            />
          </section>
        ) : null}
        {returned ? (
          <p role="status" className="text-sm">
            Wniosek zwrócono do poprawy. Wnioskodawca dostał wiadomość z listą braków.
          </p>
        ) : null}
      </>
    );
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
