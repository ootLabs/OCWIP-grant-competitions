/**
 * D12: a limit is measured, and the measurement is invertible. The rule
 * itself only ever compares two numbers, but the whole point of storing a
 * limit as "kind plus basis" instead of a plain boolean check is that the
 * message can say how much room is left, not just that there is none
 * (docs/kontrakt-formularza.md, "Limity").
 */
import type { FormAnswers } from "./answer-types";
import { allTopLevelFields, computeTopLevelValue } from "./evaluate";
import { competitionBasisSetting } from "./document-types";
import type { FormDocument, FormLimit } from "./document-types";

/** The six competition settings a limit's basis may name (docs/kontrakt-formularza.md). */
export type CompetitionLimitSettings = Partial<
  Record<
    | "maxGrantAmount"
    | "minGrantAmount"
    | "totalPoolAmount"
    | "maxIndirectCostPercent"
    | "maxInstitutionalDevelopmentPercent"
    | "maxAverageAnnualRevenue",
    number
  >
>;

export interface LimitEvaluation {
  /** The concrete ceiling, in the limited field's own unit, right now. */
  readonly allowedAmount: number;
  /** allowedAmount minus the current value. Negative once the limit is passed. */
  readonly remaining: number;
  readonly exceeded: boolean;
}

/** Null when the basis cannot be resolved (an unset competition setting). */
export function evaluateLimit(
  document: FormDocument,
  answers: FormAnswers,
  limit: FormLimit,
  currentValue: number,
  competitionSettings: CompetitionLimitSettings,
): LimitEvaluation | null {
  const basisValue = resolveBasis(document, answers, limit.basis, competitionSettings);
  if (basisValue === null) {
    return null;
  }

  const allowedAmount =
    limit.kind === "maxAmount" ? basisValue : (basisValue * (limit.percent ?? 0)) / 100;
  const remaining = allowedAmount - currentValue;

  return { allowedAmount, remaining, exceeded: remaining < 0 };
}

function resolveBasis(
  document: FormDocument,
  answers: FormAnswers,
  basis: string,
  competitionSettings: CompetitionLimitSettings,
): number | null {
  const setting = competitionBasisSetting(basis);
  if (setting !== null) {
    return competitionSettings[setting as keyof CompetitionLimitSettings] ?? null;
  }

  const field = allTopLevelFields(document).find((f) => f.key === basis);
  return field === undefined ? null : computeTopLevelValue(document, answers, field);
}
