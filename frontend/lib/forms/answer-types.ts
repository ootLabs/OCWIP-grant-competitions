/**
 * The shape of what an applicant has typed into a rendered form (T-28).
 *
 * Since T-30 this is the wire contract, not only the renderer's choice: the
 * backend checks `applications.answers` against exactly this shape
 * (Models/Forms/AnswerValidator.cs, docs/kontrakt-formularza.md, "Odpowiedzi
 * wnioskodawcy") and refuses anything else, already in a draft. Changing a
 * kind's value here without changing it there turns every autosave of that
 * kind into a 400.
 */
import type { FormField } from "./document-types";

/** A single file picked in the browser. No upload happens before T-32. */
export interface FileAnswerValue {
  readonly name: string;
  readonly sizeBytes: number;
}

export type AnswerValue =
  | string
  | number
  | boolean
  | readonly string[]
  | FileAnswerValue
  | null
  | undefined;

/** One row of a table field, keyed by column key. */
export type TableRowAnswers = Record<string, AnswerValue>;

/**
 * The whole form's answers, keyed by field key at the top level and, for a
 * table field, an array of row answers instead of a single value.
 */
export type FormAnswers = Record<string, AnswerValue | readonly TableRowAnswers[]>;

/**
 * A table field's rows, always exactly as many as the table actually shows
 * right now, gaps filled with an empty row.
 *
 * A repeatableTable's rows are whatever the applicant has added, so this is
 * just what is stored. A fixedTable's rows are structural (`table.rows`, the
 * three named members of an informal group), and nothing is stored under the
 * field's key until a specific cell is edited: reading `answers` directly
 * for a fixedTable undercounts it down to zero rows until then, which is
 * wrong for both rendering and validation. Going through this function
 * instead of `answers[field.key]` directly is what keeps every caller
 * (table-field.tsx, validate.ts, form-renderer.tsx) agreeing on row count.
 */
export function resolveTableRows(
  field: FormField,
  answers: FormAnswers,
): readonly TableRowAnswers[] {
  const table = field.table;
  if (table === undefined) {
    return [];
  }

  const stored = (answers[field.key] as readonly TableRowAnswers[] | undefined) ?? [];
  const count = table.rows !== undefined ? table.rows.length : stored.length;

  // Array.from over a length, not the stored array itself: a fixedTable
  // edited out of order (only the last row touched so far) would otherwise
  // leave holes that Array.prototype.forEach silently skips.
  return Array.from({ length: count }, (_, index) => stored[index] ?? {});
}
