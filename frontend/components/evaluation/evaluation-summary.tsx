import { formatAmount } from "@/lib/format";
import { amount, type Evaluation } from "@/lib/reviewer-work";

/**
 * The result of one card in a sentence, as the server read it from the
 * answers: pass or fail for a formal card, points and the proposed grant for
 * a merit card. Never computed here, so the screen and the ranking agree.
 */
export function EvaluationSummary({
  evaluation,
}: {
  evaluation: Pick<Evaluation, "stage" | "formalPassed" | "meritScore" | "strategicScore" | "recommendedGrant">;
}) {
  if (evaluation.stage === "Formal") {
    const verdict =
      evaluation.formalPassed === true
        ? "spełnia wymogi formalne"
        : evaluation.formalPassed === false
          ? "nie spełnia wymogów formalnych"
          : "jeszcze bez rozstrzygnięcia, nie każde kryterium ma odpowiedź";
    return <>Wynik oceny formalnej: {verdict}.</>;
  }

  const grant = amount(evaluation.recommendedGrant);
  return (
    <>
      Suma punktów: {amount(evaluation.meritScore) ?? 0}, kryteria strategiczne:{" "}
      {amount(evaluation.strategicScore) ?? 0}
      {grant !== null ? `, proponowana kwota: ${formatAmount(grant)}` : ""}.
    </>
  );
}
