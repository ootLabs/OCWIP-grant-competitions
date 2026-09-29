/**
 * Whether a section may be moved or removed, and what stands in the way
 * (T-26a).
 *
 * Both answers come from the same rule the backend enforces: a visibility
 * condition may only read an answer given strictly ABOVE it
 * (FormSchemaReferences). Reordering sections is the one edit in the creator
 * that can break that rule without touching a single condition, because it
 * moves the answer, not the question.
 *
 * Calculations and limits are deliberately absent here, and the reason is
 * not that they stay inside one section: the contract allows an operand in
 * another section, and T-30 will make that routine. It is that
 * FormSchemaReferences imposes no ordering on them at all. CheckCondition
 * receives the position table, CheckCalculation and CheckLimits do not, so a
 * value may be computed from an answer given further down and reordering
 * cannot break either one.
 */
import { moveSection } from "./document-edit";
import { findReferencesTo } from "./document-references";
import type { FormDocument, FormSection } from "./document-types";

export interface ConditionViolation {
  /** Stable across documents, so a violation can be told from a new one. */
  readonly id: string;
  /** What would stop working, named the way an operator named it. */
  readonly dependentLabel: string;
  /** The answer it reads, named the same way. */
  readonly sourceLabel: string;
}

interface Position {
  readonly index: number;
  readonly label: string;
}

/**
 * Where every field sits, in the order the backend walks them
 * (FormDocument.AllFields): a column is keyed "table.column", because the
 * value column of table A and of table C are two different things.
 */
function fieldPositions(document: FormDocument): Map<string, Position> {
  const positions = new Map<string, Position>();
  let index = 0;

  for (const section of document.sections) {
    for (const field of section.fields) {
      positions.set(field.key, { index, label: field.label });
      index += 1;

      for (const column of field.table?.columns ?? []) {
        positions.set(`${field.key}.${column.key}`, {
          index,
          label: `${field.label} / ${column.label}`,
        });
        index += 1;
      }
    }
  }

  return positions;
}

/**
 * Where a section stands, as the backend measures it: at its first field. An
 * empty section has no first field, so it stands where that field would go,
 * which keeps this from throwing on a draft mid-edit. The backend never sees
 * such a document, because a section without fields is refused before the
 * reference checks run.
 */
function sectionPositions(document: FormDocument): Map<string, number> {
  const positions = new Map<string, number>();
  let index = 0;

  for (const section of document.sections) {
    positions.set(section.key, index);

    for (const field of section.fields) {
      index += 1 + (field.table?.columns.length ?? 0);
    }
  }

  return positions;
}

/** The key a reference means: a sibling column first, then a field. */
function resolve(
  reference: string,
  owningTableKey: string | null,
  positions: ReadonlyMap<string, Position>,
): string {
  if (owningTableKey === null) {
    return reference;
  }

  const sibling = `${owningTableKey}.${reference}`;
  return positions.has(sibling) ? sibling : reference;
}

/**
 * Every visibility condition that reads an answer given at or below itself.
 * A condition pointing at a field that does not exist at all is left out: it
 * is already broken, and reordering neither caused it nor fixes it.
 */
export function conditionViolations(document: FormDocument): ConditionViolation[] {
  const positions = fieldPositions(document);
  const sections = sectionPositions(document);
  const violations: ConditionViolation[] = [];

  const check = (
    id: string,
    dependentLabel: string,
    at: number,
    reference: string,
    owningTableKey: string | null,
  ): void => {
    const source = positions.get(resolve(reference, owningTableKey, positions));

    if (source !== undefined && source.index >= at) {
      violations.push({ id, dependentLabel, sourceLabel: source.label });
    }
  };

  for (const section of document.sections) {
    if (section.visibleWhen != null) {
      check(
        section.key,
        `sekcja "${section.title}"`,
        sections.get(section.key)!,
        section.visibleWhen.field,
        null,
      );
    }

    for (const field of section.fields) {
      const at = positions.get(field.key)!.index;

      if (field.visibleWhen != null) {
        check(`${section.key}:${field.key}`, field.label, at, field.visibleWhen.field, null);
      }

      for (const column of field.table?.columns ?? []) {
        if (column.visibleWhen == null) {
          continue;
        }

        check(
          `${section.key}:${field.key}.${column.key}`,
          `${field.label} / ${column.label}`,
          positions.get(`${field.key}.${column.key}`)!.index,
          column.visibleWhen.field,
          field.key,
        );
      }
    }
  }

  return violations;
}

/**
 * What a move would break that is not broken already. Subtracting the
 * document's current violations matters: a form that arrived broken (an
 * import, an older contract) must not have every one of its sections frozen
 * in place by a fault the operator did not cause and this move does not
 * worsen.
 */
export function sectionMoveBlockers(
  document: FormDocument,
  sectionKey: string,
  direction: "up" | "down",
): ConditionViolation[] {
  const before = new Set(conditionViolations(document).map((violation) => violation.id));

  return conditionViolations(moveSection(document, sectionKey, direction)).filter(
    (violation) => !before.has(violation.id),
  );
}

/**
 * What still reads this section's answers from outside it. A reference from
 * within the section is no obstacle: it leaves together with the section.
 */
export function sectionRemovalBlockers(
  document: FormDocument,
  section: FormSection,
): string[] {
  const labels = new Set<string>();

  for (const field of section.fields) {
    const keys = [field.key, ...(field.table?.columns ?? []).map((column) => column.key)];

    for (const key of keys) {
      for (const reference of findReferencesTo(document, key)) {
        if (reference.sectionKey !== section.key) {
          labels.add(reference.fieldLabel);
        }
      }
    }
  }

  return [...labels];
}
