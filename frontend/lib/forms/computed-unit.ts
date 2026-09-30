/**
 * What a calculated field counts, so its live value can be printed in the
 * right unit.
 *
 * The renderer used to decide from the calculation kind alone: a ratio was a
 * percentage and everything else was money. That is true of an application,
 * where every sum is złotówki, and false of an evaluation card, where the
 * sums are points. "Suma (0-50 punktów)" showed as "44,00 zł" to the expert
 * filling the card in.
 *
 * The operands say which it is. Points are `number` fields, money is `amount`
 * fields, and the seeded cards and forms already separate the two, so nothing
 * has to be tagged by hand.
 */
import { allTopLevelFields } from "./evaluate";
import type { FormDocument, FormField } from "./document-types";

/** A table column is a FormField in this schema; named for what it is here. */
type FormColumn = FormField;

export type ComputedUnit = "amount" | "number" | "percent";

/** A calculated field standing on its own in a section. */
export function topLevelComputedUnit(document: FormDocument, field: FormField): ComputedUnit {
  if (field.calculation?.kind === "ratio") {
    return "percent";
  }

  const operands = field.calculation?.operands ?? [];
  const countsMoney = operands.some((operand) =>
    operandIsAmount(document, operand, new Set()),
  );

  return countsMoney ? "amount" : "number";
}

/** A calculated cell, whose operands are the columns beside it. */
export function columnComputedUnit(tableField: FormField, column: FormColumn): ComputedUnit {
  if (column.calculation?.kind === "ratio") {
    return "percent";
  }

  const columns = tableField.table?.columns ?? [];
  const countsMoney = (column.calculation?.operands ?? []).some((operand) =>
    columnIsAmount(columns, operand, new Set()),
  );

  return countsMoney ? "amount" : "number";
}

/**
 * A "table.column" operand counts money when that column does; a bare key
 * when that field does. A calculated operand is followed, with the same cycle
 * guard evaluate.ts keeps: the backend refuses a cycle, so this is a safety
 * net rather than the defence.
 */
function operandIsAmount(document: FormDocument, operand: string, seen: Set<string>): boolean {
  if (seen.has(operand)) {
    return false;
  }
  seen.add(operand);

  if (operand.includes(".")) {
    const [tableKey, columnKey] = operand.split(".", 2);
    const tableField = allTopLevelFields(document).find((field) => field.key === tableKey);
    return columnIsAmount(tableField?.table?.columns ?? [], columnKey, seen);
  }

  const field = allTopLevelFields(document).find((candidate) => candidate.key === operand);

  if (field === undefined) {
    return false;
  }

  if (field.type === "calculated") {
    return (field.calculation?.operands ?? []).some((next) =>
      operandIsAmount(document, next, seen),
    );
  }

  return field.type === "amount";
}

function columnIsAmount(columns: readonly FormColumn[], key: string, seen: Set<string>): boolean {
  const column = columns.find((candidate) => candidate.key === key);

  if (column === undefined) {
    return false;
  }

  if (column.type === "calculated") {
    return (column.calculation?.operands ?? []).some((next) =>
      !seen.has(`${key}.${next}`) && columnIsAmount(columns, next, seen.add(`${key}.${next}`)),
    );
  }

  return column.type === "amount";
}
