/**
 * One error per field, in Polish, or none (T-28: "błąd pojawia się tylko
 * wtedy, gdy naprawdę jest", proces.md rule 4). Checked in a fixed order so
 * the message an applicant sees is always the most useful one first:
 * required beats shape, shape beats a limit, because there is no point
 * telling somebody their amount is over budget before telling them the
 * field is empty.
 */
import { resolveTableRows, type AnswerValue, type FormAnswers } from "./answer-types";
import { computeTopLevelValue, isFieldVisible } from "./evaluate";
import type { CompetitionLimitSettings } from "./limits";
import { evaluateLimit } from "./limits";
import { formatAmount, formatPercent } from "../format";
import type { ApplicantKind, FormDocument, FormField } from "./document-types";

function isEmpty(value: AnswerValue): boolean {
  if (value === null || value === undefined) {
    return true;
  }
  if (typeof value === "string") {
    return value.trim().length === 0;
  }
  if (Array.isArray(value)) {
    return value.length === 0;
  }
  return false;
}

/** The one error message for a top-level field's current answer, or null. */
export function validateField(
  document: FormDocument,
  answers: FormAnswers,
  field: FormField,
  competitionSettings: CompetitionLimitSettings,
): string | null {
  if (field.type === "calculated") {
    // Read only: never wrong by itself, only ever an input to a limit on it.
    return checkLimits(document, answers, field, competitionSettings);
  }

  if (field.type === "statement") {
    // `false` (explicitly unchecked) is not the same as never answered, but
    // for a required declaration both mean "not affirmed": isEmpty below
    // would let an unchecked box straight through, because false counts as
    // a complete answer to a yesNo question but not to "I declare that...".
    return field.required && answers[field.key] !== true ? "To pole jest wymagane." : null;
  }

  const value = answers[field.key] as AnswerValue;

  if (field.required && isEmpty(value)) {
    return "To pole jest wymagane.";
  }

  if (isEmpty(value)) {
    return null;
  }

  const shapeError = checkShape(field, value);
  if (shapeError !== null) {
    return shapeError;
  }

  return checkLimits(document, answers, field, competitionSettings);
}

function checkShape(field: FormField, value: AnswerValue): string | null {
  if ((field.type === "shortText" || field.type === "longText") && typeof value === "string") {
    if (field.maxLength !== undefined && value.length > field.maxLength) {
      return `Przekroczono limit ${field.maxLength} znaków (jest ${value.length}).`;
    }
    if (field.minLength !== undefined && value.length < field.minLength) {
      return `Wymagane co najmniej ${field.minLength} znaków (jest ${value.length}).`;
    }
  }

  if (
    (field.type === "number" || field.type === "amount" || field.type === "percent") &&
    typeof value === "number"
  ) {
    if (field.minValue !== undefined && value < field.minValue) {
      return `Wartość nie może być mniejsza niż ${field.minValue}.`;
    }
    if (field.maxValue !== undefined && value > field.maxValue) {
      return `Wartość nie może być większa niż ${field.maxValue}.`;
    }
  }

  return null;
}

function checkLimits(
  document: FormDocument,
  answers: FormAnswers,
  field: FormField,
  competitionSettings: CompetitionLimitSettings,
): string | null {
  if (!field.limits || field.limits.length === 0) {
    return null;
  }

  const currentValue = computeTopLevelValue(document, answers, field);
  // A ratio calculation is always a percentage (evaluate.ts's combine()
  // multiplies it by 100 for exactly that reason), so its limit message
  // needs the same unit a plain percent field gets.
  const isPercent = field.type === "percent" || field.calculation?.kind === "ratio";
  const format = isPercent ? formatPercent : formatAmount;

  for (const limit of field.limits) {
    const evaluation = evaluateLimit(document, answers, limit, currentValue, competitionSettings);
    if (evaluation !== null && evaluation.exceeded) {
      return `Przekroczono dopuszczalną wartość o ${format(-evaluation.remaining)}. Maksymalnie ${format(evaluation.allowedAmount)}.`;
    }
  }

  return null;
}

/** The one error for a single table cell: shape only, no limits (those sit on the summary field). */
export function validateCell(column: FormField, value: AnswerValue): string | null {
  if (column.required && isEmpty(value)) {
    return "Wymagane.";
  }
  if (isEmpty(value)) {
    return null;
  }
  return checkShape(column, value);
}

/** Whether a repeatableTable's row count satisfies its own widths. */
export function validateRowCount(field: FormField, rowCount: number): string | null {
  const table = field.table;
  if (table === undefined || field.type !== "repeatableTable") {
    return null;
  }
  if (table.minRows !== undefined && rowCount < table.minRows) {
    return `Wymagany co najmniej ${table.minRows} wiersz (jest ${rowCount}).`;
  }
  if (table.maxRows !== undefined && rowCount > table.maxRows) {
    return `Dopuszczalne najwyżej ${table.maxRows} wierszy (jest ${rowCount}).`;
  }
  return null;
}

export type SectionStatus = "ready" | "inProgress" | "hasErrors";

/**
 * The section nav's own read of a section's state, independent of whether
 * any field has been touched: touched only gates the inline message under a
 * field (form-renderer.tsx), not this summary.
 */
export function sectionStatus(
  document: FormDocument,
  answers: FormAnswers,
  section: FormDocument["sections"][number],
  competitionSettings: CompetitionLimitSettings,
  applicant?: ApplicantKind,
): SectionStatus {
  let hasError = false;
  let hasIncomplete = false;

  for (const field of section.fields) {
    // A hidden field cannot be filled in, so it cannot be missing either:
    // otherwise a section never reaches "ready" while a conditional field
    // it does not currently ask for sits required and empty.
    if (!isFieldVisible(field, answers, applicant)) {
      continue;
    }

    if (field.table) {
      // Goes through resolveTableRows, not `answers[field.key]` directly: a
      // fixedTable's rows are structural (three named group members) and
      // read as zero rows from answers alone until a cell is edited, which
      // would otherwise report an entirely empty required table as "ready".
      const rows = resolveTableRows(field, answers);
      if (validateRowCount(field, rows.length) !== null) {
        hasIncomplete = true;
      }
      for (const row of rows) {
        for (const column of field.table.columns) {
          const cellError = validateCell(column, row[column.key]);
          if (cellError === null) {
            continue;
          }
          // Same split as the plain-field branch below: a required cell
          // nobody has typed into yet is incomplete, not wrong.
          if (column.required && isEmpty(row[column.key])) {
            hasIncomplete = true;
          } else {
            hasError = true;
          }
        }
      }
      continue;
    }

    const error = validateField(document, answers, field, competitionSettings);
    if (error === null) {
      continue;
    }
    // A calculated field is never "incomplete": it has no empty state of its
    // own; an error on it can only be a limit that is genuinely exceeded.
    if (field.type !== "calculated" && field.required && isEmpty(answers[field.key] as AnswerValue)) {
      hasIncomplete = true;
    } else {
      hasError = true;
    }
  }

  if (hasError) {
    return "hasErrors";
  }
  return hasIncomplete ? "inProgress" : "ready";
}
