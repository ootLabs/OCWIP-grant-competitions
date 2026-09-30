/**
 * How a calculated field's live value is shown (T-28), whether it is a whole
 * top-level field or one cell of a table row. A ratio calculation always
 * means a percentage (evaluate.ts's `combine` multiplies it by 100 for
 * exactly that reason); money reuses lib/format.ts rather than a bare
 * `Intl.NumberFormat` call, so a calculated total reads exactly like the same
 * number typed into an `amount` field.
 *
 * The unit comes from computed-unit.ts, not from the calculation kind: an
 * application sums złotówki, an evaluation card sums points, and the card's
 * "Suma (0-50 punktów)" used to read "44,00 zł".
 */
import { formatAmount, formatPercent } from "../format";
import type { ComputedUnit } from "./computed-unit";

export function formatComputedNumber(value: number, unit: ComputedUnit): string {
  if (unit === "percent") {
    return formatPercent(value);
  }

  return unit === "amount" ? formatAmount(value) : formatNumber(value);
}

/**
 * Points, with no currency and no forced decimals: a card scored 44 reads
 * "44", not "44,00". Grouping stays off, because the numbers are small and a
 * separator here would only invite reading it as money again.
 */
function formatNumber(value: number): string {
  return Number.isInteger(value)
    ? String(value)
    : value.toFixed(2).replace(".", ",");
}
