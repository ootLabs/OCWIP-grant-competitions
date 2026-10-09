/**
 * Everything still missing or wrong before an application can be submitted
 * (T-34, proces.md krok 3.7): "Zostały trzy rzeczy do uzupełnienia" with a
 * link straight to each one.
 *
 * A thin walk over the same rules validate.ts already runs field by field
 * for the renderer's own inline errors, flattened into one list instead of
 * one verdict per section: sectionStatus answers "is section 2 ready", this
 * answers "what, exactly, is still wrong, everywhere".
 */
import { resolveTableRows, type FormAnswers } from "./answer-types";
import { isFieldVisible, isSectionVisible } from "./evaluate";
import { fieldAnchorId } from "./field-anchor";
import { fieldDisplayName } from "./labels";
import type { CompetitionLimitSettings } from "./limits";
import {
  TABLE_TYPES,
  type ApplicantKind,
  type FormDocument,
  type FormField,
  type FormSection,
} from "./document-types";
import { validateCell, validateField, validateRowCount } from "./validate";

export interface SubmissionGap {
  readonly sectionKey: string;
  readonly sectionTitle: string;
  /** The field the gap is on. For a table cell, the table field itself: the
   * anchor and "popraw" both land on the table, not on one cell. */
  readonly fieldKey: string;
  readonly fieldLabel: string;
  readonly message: string;
  readonly anchorId: string;
}

/**
 * Every visible field (and table row/cell) with a real problem, in document
 * order. Only what validateField/validateCell would show inline once
 * touched: this list is what makes them visible before that, since nobody
 * has touched anything yet on a draft reopened days later.
 *
 * `applicant` is who the answers are about, and it has to be the same one the
 * renderer was given: a field asked of some kinds only (appliesTo) is not on
 * screen for the others, so counting it as missing would list a gap nobody
 * can close. The report form asks for a leader of one kind and a board of
 * another (seed/forms/report-2026.json), so without it every report held
 * about a dozen gaps that could never go away. Left out, every field reads as
 * asked, which is all an application form needs: it may not carry appliesTo
 * (FormPurposeRules.cs).
 */
export function submissionGaps(
  document: FormDocument,
  answers: FormAnswers,
  competitionSettings: CompetitionLimitSettings,
  applicant?: ApplicantKind,
): SubmissionGap[] {
  const gaps: SubmissionGap[] = [];

  for (const section of document.sections) {
    if (!isSectionVisible(section, answers)) {
      continue;
    }

    for (const field of section.fields) {
      if (!isFieldVisible(field, answers, applicant)) {
        continue;
      }

      if (TABLE_TYPES.has(field.type) && field.table) {
        gaps.push(...tableGaps(section, field, answers));
        continue;
      }

      const message = validateField(document, answers, field, competitionSettings);
      if (message !== null) {
        gaps.push(gap(section, field, message));
      }
    }
  }

  return gaps;
}

function tableGaps(
  section: FormSection,
  field: FormField,
  answers: FormAnswers,
): SubmissionGap[] {
  const table = field.table!;
  const rows = resolveTableRows(field, answers);
  const gaps: SubmissionGap[] = [];

  const rowCountMessage = validateRowCount(field, rows.length);
  if (rowCountMessage !== null) {
    gaps.push(gap(section, field, rowCountMessage));
  }

  rows.forEach((row, rowIndex) => {
    table.columns.forEach((column) => {
      const message = validateCell(column, row[column.key]);
      if (message !== null) {
        gaps.push(
          gap(section, field, `Wiersz ${rowIndex + 1}, kolumna "${column.label}": ${message}`),
        );
      }
    });
  });

  return gaps;
}

function gap(section: FormSection, field: FormField, message: string): SubmissionGap {
  return {
    sectionKey: section.key,
    sectionTitle: section.title,
    fieldKey: field.key,
    fieldLabel: fieldDisplayName(field),
    message,
    anchorId: fieldAnchorId(field.key),
  };
}
