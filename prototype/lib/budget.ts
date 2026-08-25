/**
 * Budget arithmetic for the visualisation, kept out of the component so the
 * rule it encodes stays readable: exceeding the grant limit must point at a
 * concrete budget position, never raise a generic form error.
 */
export type BudgetRow = {
  name: string;
  /** Kept as typed text, not a number, so an emptied field does not become 0. */
  amount: string;
};

export type BudgetSummary = {
  total: number;
  over: boolean;
  /** How much the budget exceeds the limit by. Zero when it fits. */
  excess: number;
  remaining: number;
  /** Share of the limit used, uncapped, so 104 percent is visible as such. */
  usedPercent: number;
  /** Index of the largest position: the one worth cutting first. */
  largestIndex: number;
};

export function parseAmount(amount: string): number {
  const digits = amount.replace(/[^0-9]/g, "");
  if (digits === "") {
    return 0;
  }
  return Number.parseInt(digits, 10);
}

/** Thin space between thousands, the way Polish typesets amounts. */
export function formatAmount(value: number): string {
  return String(value).replace(/\B(?=(\d{3})+(?!\d))/g, " ");
}

export function summarise(rows: BudgetRow[], limit: number): BudgetSummary {
  const total = rows.reduce((sum, row) => sum + parseAmount(row.amount), 0);

  let largestIndex = 0;
  rows.forEach((row, index) => {
    if (parseAmount(row.amount) > parseAmount(rows[largestIndex].amount)) {
      largestIndex = index;
    }
  });

  const over = total > limit;

  return {
    total,
    over,
    excess: over ? total - limit : 0,
    remaining: over ? 0 : limit - total,
    usedPercent: limit === 0 ? 0 : Math.round((total / limit) * 100),
    largestIndex,
  };
}
