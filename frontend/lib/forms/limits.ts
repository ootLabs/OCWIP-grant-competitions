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
  /** The key of the field the limit sits on, so a basis that counts it can be solved for it. */
  limitedFieldKey?: string,
): LimitEvaluation | null {
  const basisValue = resolveBasis(document, answers, limit.basis, competitionSettings);
  if (basisValue === null) {
    return null;
  }

  // A percentage from a competition setting the operator left empty means
  // the cost category has no threshold in this competition (T-31), so there
  // is nothing to exceed; the backend skips it the same way.
  const percent =
    limit.percentFrom !== undefined
      ? resolveBasis(document, answers, limit.percentFrom, competitionSettings)
      : (limit.percent ?? null);
  if (limit.kind === "maxPercentOf" && percent === null) {
    return null;
  }

  let allowedAmount =
    limit.kind === "maxAmount" ? basisValue : (basisValue * (percent ?? 0)) / 100;

  if (limit.kind === "maxPercentOf" && percent !== null && limitedFieldKey !== undefined) {
    const selfInclusive = selfInclusiveCeiling(document, answers, limit.basis, limitedFieldKey, percent);
    if (selfInclusive !== null) {
      allowedAmount = selfInclusive;
    }
  }

  const remaining = allowedAmount - currentValue;

  return { allowedAmount, remaining, exceeded: remaining < 0 };
}

/**
 * The ceiling of a percentage limit whose basis contains the limited field
 * itself, or null when it does not (P4-13). Indirect costs capped at 10
 * percent of a grant that sums them: at C = 800 the cap read 600, at 600 it
 * read 580, then 578, and the applicant chased it down. The value that
 * actually clears the limit solves C = p x (rest + C).
 *
 * The basis is measured with the limited field forced to 0, 1 and 2; only a
 * basis that grows in a straight line with it is solved, anything else keeps
 * the plain percentage of the basis as it stands. Rounded down to the grosz,
 * because the rounded up amount would itself be over.
 */
function selfInclusiveCeiling(
  document: FormDocument,
  answers: FormAnswers,
  basis: string,
  limitedFieldKey: string,
  percent: number,
): number | null {
  if (competitionBasisSetting(basis) !== null) {
    return null;
  }

  const basisField = allTopLevelFields(document).find((f) => f.key === basis);
  if (basisField === undefined) {
    return null;
  }

  const basisAt = (value: number) =>
    computeTopLevelValue(document, answers, basisField, new Map([[limitedFieldKey, value]]));
  const at0 = basisAt(0);
  const slope = basisAt(1) - at0;
  const linear = Math.abs(basisAt(2) - at0 - 2 * slope) < 1e-9;
  const denominator = 100 - percent * slope;
  if (slope === 0 || !linear || denominator <= 0) {
    return null;
  }

  return Math.floor(((percent * at0) / denominator) * 100 + 1e-9) / 100;
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
