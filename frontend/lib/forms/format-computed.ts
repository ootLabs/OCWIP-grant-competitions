/**
 * How a calculated field's live value is shown (T-28), whether it is a whole
 * top-level field or one cell of a table row. A ratio calculation always
 * means a percentage (evaluate.ts's `combine` multiplies it by 100 for
 * exactly that reason); every other kind is a plain amount. Both reuse
 * lib/format.ts rather than a bare `Intl.NumberFormat` call, so a calculated
 * total reads exactly like the same number typed into an `amount` field.
 */
import { formatAmount, formatPercent } from "../format";
import type { CalculationKind } from "./document-types";

export function formatComputedNumber(value: number, kind: CalculationKind | undefined): string {
  return kind === "ratio" ? formatPercent(value) : formatAmount(value);
}
