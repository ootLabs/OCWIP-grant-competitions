/**
 * What a form document does at answer time (T-28): whether a section or
 * field is visible given the answers so far, and what a calculated field's
 * value is right now. Both read the document's own rules
 * (docs/kontrakt-formularza.md); nothing here is renderer-specific, which is
 * what makes it independently testable from the components that call it.
 */
import type { AnswerValue, FormAnswers, TableRowAnswers } from "./answer-types";
import { resolveTableRows } from "./answer-types";
import { yesNoWireValue, type ApplicantKind } from "./document-types";
import type {
  FormDocument,
  FormField,
  FormSection,
  VisibleWhen,
} from "./document-types";

// ---------------------------------------------------------------------------
// Visibility
// ---------------------------------------------------------------------------

/**
 * The wire form of an answer to the field a condition reads: yesNo is
 * "true"/"false" (document-candidates.ts), singleChoice is its one selected
 * value, multipleChoice is compared as "any selected value matches".
 */
function conditionAnswerMatches(value: AnswerValue, equalsAnyOf: readonly string[]): boolean {
  if (typeof value === "boolean") {
    return equalsAnyOf.includes(yesNoWireValue(value));
  }

  if (Array.isArray(value)) {
    return value.some((v) => equalsAnyOf.includes(v));
  }

  if (typeof value === "string") {
    return equalsAnyOf.includes(value);
  }

  return false;
}

function isConditionMet(condition: VisibleWhen | null | undefined, answers: FormAnswers): boolean {
  if (condition == null) {
    return true;
  }

  const value = answers[condition.field] as AnswerValue;
  return conditionAnswerMatches(value, condition.equalsAnyOf);
}

export function isSectionVisible(section: FormSection, answers: FormAnswers): boolean {
  return isConditionMet(section.visibleWhen, answers);
}

/**
 * `applicant` is who the answers are about, for a field asked of some kinds
 * only (appliesTo, T-38, evaluation cards). Left out, every field reads as
 * asked, which is all an application form ever needs: it may not carry
 * appliesTo (FormPurposeRules.cs).
 */
export function isFieldVisible(field: FormField, answers: FormAnswers, applicant?: ApplicantKind): boolean {
  if (field.appliesTo && applicant && !field.appliesTo.includes(applicant)) {
    return false;
  }
  return isConditionMet(field.visibleWhen, answers);
}

// ---------------------------------------------------------------------------
// Calculated values
// ---------------------------------------------------------------------------

function toNumber(value: AnswerValue): number {
  if (typeof value === "number") {
    return value;
  }
  if (typeof value === "string" && value.trim() !== "") {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : 0;
  }
  return 0;
}

/** All top-level fields of the document, sections flattened in order. */
export function allTopLevelFields(document: FormDocument): FormField[] {
  return document.sections.flatMap((section) => section.fields);
}

/**
 * A calculated top-level field's value, resolving operands that are
 * themselves calculated fields as needed. `resolving` guards against a cycle
 * the backend's own validator would have refused (FormSchemaReferences.cs),
 * so this is a safety net, not the primary defence: a cycle here reads as 0
 * instead of hanging the tab.
 */
export function computeTopLevelValue(
  document: FormDocument,
  answers: FormAnswers,
  field: FormField,
  cache: Map<string, number> = new Map(),
  resolving: Set<string> = new Set(),
): number {
  if (cache.has(field.key)) {
    return cache.get(field.key)!;
  }

  // A scored yes or no counts its points when answered "yes" (T-38), the
  // same rule as AnswerCalculator.Value on the server.
  if (field.type === "yesNo" && field.points !== undefined) {
    return answers[field.key] === true ? field.points : 0;
  }

  if (field.type !== "calculated" || !field.calculation) {
    return toNumber(answers[field.key] as AnswerValue);
  }

  if (resolving.has(field.key)) {
    return 0;
  }
  resolving.add(field.key);

  const { kind, operands } = field.calculation;
  const values = operands.map((operand) =>
    resolveOperand(document, answers, operand, cache, resolving),
  );

  const result = combine(kind, values);
  resolving.delete(field.key);
  cache.set(field.key, result);
  return result;
}

function resolveOperand(
  document: FormDocument,
  answers: FormAnswers,
  operand: string,
  cache: Map<string, number>,
  resolving: Set<string>,
): number {
  if (operand.includes(".")) {
    // A qualified table.column key: the sum of that column down every row.
    // Unambiguous because a bare field key can never contain "." itself
    // (document-keys.ts's isValidKey), so this split never misreads a plain
    // top-level field for a table reference.
    const [tableKey, columnKey] = operand.split(".", 2);
    const tableField = allTopLevelFields(document).find((f) => f.key === tableKey);

    if (tableField === undefined) {
      return 0;
    }

    const rows = resolveTableRows(tableField, answers);
    return rows.reduce((sum, row) => sum + computeRowValue(tableField, row, columnKey), 0);
  }

  const target = allTopLevelFields(document).find((f) => f.key === operand);
  if (target === undefined) {
    return 0;
  }
  return computeTopLevelValue(document, answers, target, cache, resolving);
}

function combine(kind: "sum" | "product" | "ratio" | "difference", values: number[]): number {
  switch (kind) {
    case "sum":
      return values.reduce((a, b) => a + b, 0);
    case "product":
      return values.reduce((a, b) => a * b, 1);
    case "difference":
      return values.slice(1).reduce((a, b) => a - b, values[0] ?? 0);
    case "ratio": {
      const [numerator, denominator] = values;
      return denominator ? (numerator / denominator) * 100 : 0;
    }
    default:
      return 0;
  }
}

/**
 * A calculated column's value for one row. Operands are sibling columns of
 * the same row, read bare (docs/kontrakt-formularza.md: "wewnątrz tabeli
 * odwołania są krótkie"), so this never leaves the row it was asked about.
 * `resolving` is the same safety net as computeTopLevelValue's: a cycle the
 * backend would have refused reads as 0 instead of hanging the tab.
 */
export function computeRowValue(
  tableField: FormField,
  row: TableRowAnswers,
  columnKey: string,
  cache: Map<string, number> = new Map(),
  resolving: Set<string> = new Set(),
): number {
  if (cache.has(columnKey)) {
    return cache.get(columnKey)!;
  }

  const column = tableField.table?.columns.find((c) => c.key === columnKey);
  if (column === undefined || column.type !== "calculated" || !column.calculation) {
    return toNumber(row[columnKey]);
  }

  if (resolving.has(columnKey)) {
    return 0;
  }
  resolving.add(columnKey);

  const values = column.calculation.operands.map((operand) =>
    computeRowValue(tableField, row, operand, cache, resolving),
  );
  const result = combine(column.calculation.kind, values);
  resolving.delete(columnKey);
  cache.set(columnKey, result);
  return result;
}
