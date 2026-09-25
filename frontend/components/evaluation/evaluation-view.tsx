import { OfferView } from "@/components/offer-view";
import { formatMoment } from "@/lib/format";
import { cardOf, type Evaluation } from "@/lib/reviewer-work";

import { EvaluationSummary } from "./evaluation-summary";

/**
 * Somebody else's card, read only (T-41a): who filled it in, whether it is
 * finished, the result and every answer. An unfinished card is shown as it
 * stands, marked as such, because the operator watches progress, not only
 * results.
 */
export function EvaluationView({ evaluation, author }: { evaluation: Evaluation; author: string }) {
  const card = cardOf(evaluation);
  const finished = evaluation.status === "Finished";

  return (
    <article className="flex flex-col gap-2 rounded-sm border border-border p-4">
      <h3 className="text-lg">{author || "Oceniający bez nazwiska"}</h3>
      <p className="text-sm">
        {finished && evaluation.finishedAt
          ? `Ocena zakończona ${formatMoment(evaluation.finishedAt)}.`
          : "Ocena w toku, karta może się jeszcze zmienić."}{" "}
        <EvaluationSummary evaluation={evaluation} />
      </p>
      <details>
        <summary className="cursor-pointer text-sm underline">Pokaż odpowiedzi na karcie</summary>
        <OfferView document={card.document} answers={card.answers} applicant={card.applicant} />
      </details>
    </article>
  );
}
