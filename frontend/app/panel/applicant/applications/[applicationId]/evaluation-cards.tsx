"use client";

import { useEffect, useState } from "react";

import { EvaluationSummary } from "@/components/evaluation/evaluation-summary";
import { OfferView } from "@/components/offer-view";
import { fetchEvaluationCards, type ApplicantEvaluationCard } from "@/lib/applicant-applications";
import { cardOf } from "@/lib/reviewer-work";

type Load =
  | { readonly status: "loading" }
  | { readonly status: "error" }
  | { readonly status: "ready"; readonly shared: boolean; readonly cards: ApplicantEvaluationCard[] };

/**
 * The evaluation cards of this application, once the operator shared them
 * with every applicant of the competition (T-41b, report step 5.5). Nothing
 * says who evaluated: the API does not send it. Before sharing the section
 * is absent, because there is nothing an applicant could do with "not yet".
 */
export function EvaluationCards({ applicationId }: { applicationId: string }) {
  const [load, setLoad] = useState<Load>({ status: "loading" });

  useEffect(() => {
    let current = true;
    fetchEvaluationCards(applicationId)
      .then((result) => {
        if (current) setLoad({ status: "ready", shared: result.shared, cards: result.cards });
      })
      .catch(() => {
        if (current) setLoad({ status: "error" });
      });
    return () => {
      current = false;
    };
  }, [applicationId]);

  if (load.status === "loading" || (load.status === "ready" && !load.shared)) {
    return null;
  }

  return (
    <section aria-labelledby="karty-oceny" className="flex flex-col gap-4">
      <h2 id="karty-oceny" className="text-xl">
        Karty oceny wniosku
      </h2>
      {load.status === "error" ? <p className="text-sm">Nie udało się pobrać kart oceny.</p> : null}
      {load.status === "ready" && load.cards.length === 0 ? (
        <p className="text-sm">Ten wniosek nie ma zakończonych kart oceny.</p>
      ) : null}
      {load.status === "ready"
        ? load.cards.map((card, index) => {
            const meritNumber = load.cards.slice(0, index + 1).filter((c) => c.stage === "Merit").length;
            const title =
              card.stage === "Formal" ? "Karta oceny formalnej" : `Karta oceny merytorycznej ${meritNumber}`;
            const view = cardOf(card);
            return (
              <article key={index} className="flex flex-col gap-2 rounded-sm border border-border p-4">
                <h3 className="text-lg">{title}</h3>
                <p className="text-sm">
                  <EvaluationSummary evaluation={card} />
                </p>
                <OfferView document={view.document} answers={view.answers} applicant={view.applicant} />
              </article>
            );
          })
        : null}
    </section>
  );
}
