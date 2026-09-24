/**
 * What an operator is allowed to pick, for the three places the creator asks
 * "which field" instead of asking for an expression: a visibility condition,
 * a calculation operand, a limit basis (T-26).
 *
 * Every list here is built so that whatever the operator picks from it is
 * already valid on the wire (docs/kontrakt-formularza.md): a visibility
 * candidate always stands above its target, a calculation operand is always
 * numeric and reachable, a limit basis is always something a number can be
 * compared against. The creator has nothing left to validate after the pick
 * because the picker never offers the invalid choice in the first place.
 */
import {
  COMPETITION_LIMIT_BASES,
  CONDITION_SOURCE_TYPES,
  NUMERIC_TYPES,
  competitionBasis,
  type FormDocument,
  type FormField,
} from "./document-types";
import { COMPETITION_BASIS_LABELS } from "./labels";

export interface FieldCandidate {
  /** The exact string to store on the wire: bare or table-qualified. */
  readonly key: string;
  readonly label: string;
}

/**
 * Fields a visibility condition may read: singleChoice, multipleChoice or
 * yesNo, standing strictly above the target. `fieldKey` omitted means the
 * target is the section itself, so every field of an earlier section
 * qualifies and nothing in this section does yet, because nothing in it has
 * been answered before the section is shown.
 */
export function visibilityCandidates(
  document: FormDocument,
  sectionKey: string,
  fieldKey?: string,
): FieldCandidate[] {
  const candidates: FieldCandidate[] = [];

  for (const section of document.sections) {
    const isTargetSection = section.key === sectionKey;

    // A section's own condition can only read an earlier section: nothing in
    // this section has been answered yet at the point it would be shown.
    if (isTargetSection && fieldKey === undefined) {
      break;
    }

    for (const field of section.fields) {
      if (isTargetSection && field.key === fieldKey) {
        break;
      }

      if (CONDITION_SOURCE_TYPES.has(field.type)) {
        candidates.push({ key: field.key, label: field.label });
      }
    }

    if (isTargetSection) {
      break;
    }
  }

  return candidates;
}

/** The options a yesNo/singleChoice/multipleChoice field can be compared to. */
export function conditionValueOptions(field: FormField): FieldCandidate[] {
  if (field.type === "yesNo") {
    // The wire value has to be exactly "true"/"false": the backend's
    // FormSchemaReferences rejects anything else for a yesNo condition. The
    // Polish label is what the operator sees; the key is what gets stored.
    return [
      { key: "true", label: "Tak" },
      { key: "false", label: "Nie" },
    ];
  }

  return (field.options ?? []).map((option) => ({
    key: option.value,
    label: option.label,
  }));
}

/**
 * Operands a calculation may read, restricted to the same section a
 * cross-section value link is T-30, not this card (docs/kontrakt-formularza.md,
 * "Czego kontrakt świadomie nie ma"):
 *
 * - a column's own calculation reads its numeric sibling columns, bare;
 * - a section level "sum" reads a numeric column of a table in the same
 *   section, qualified (`table.column`), because that is the only way a
 *   table's many rows become one value a further calculation can use, and
 *   also other numeric section level fields, bare, because the grant is the
 *   total of three cost tables (T-31);
 * - any other section level calculation reads other numeric section level
 *   fields, bare.
 */
export function calculationOperandCandidates(
  document: FormDocument,
  sectionKey: string,
  fieldKey: string,
  columnKey: string | undefined,
  kind: "sum" | "product" | "ratio" | "difference",
): FieldCandidate[] {
  const section = document.sections.find((s) => s.key === sectionKey);
  if (section === undefined) {
    return [];
  }

  if (columnKey !== undefined) {
    const table = section.fields.find((f) => f.key === fieldKey)?.table;
    return (table?.columns ?? [])
      .filter((column) => column.key !== columnKey && NUMERIC_TYPES.has(column.type))
      .map((column) => ({ key: column.key, label: column.label }));
  }

  if (kind === "sum") {
    const candidates: FieldCandidate[] = [];
    for (const field of section.fields) {
      for (const column of field.table?.columns ?? []) {
        if (NUMERIC_TYPES.has(column.type)) {
          candidates.push({
            key: `${field.key}.${column.key}`,
            label: `${field.label} / ${column.label}`,
          });
        }
      }
      if (field.key !== fieldKey && NUMERIC_TYPES.has(field.type)) {
        candidates.push({ key: field.key, label: field.label });
      }
    }
    return candidates;
  }

  return section.fields
    .filter((field) => field.key !== fieldKey && NUMERIC_TYPES.has(field.type))
    .map((field) => ({ key: field.key, label: field.label }));
}

/**
 * What a limit may be measured against: a competition setting, or another
 * numeric field of the same section (docs/kontrakt-formularza.md, "Limity").
 */
export function limitBasisCandidates(
  document: FormDocument,
  sectionKey: string,
  fieldKey: string,
): FieldCandidate[] {
  const competitionCandidates = COMPETITION_LIMIT_BASES.map((setting) => ({
    key: competitionBasis(setting),
    label: COMPETITION_BASIS_LABELS[setting],
  }));

  const section = document.sections.find((s) => s.key === sectionKey);
  const fieldCandidates =
    section?.fields
      .filter((field) => field.key !== fieldKey && NUMERIC_TYPES.has(field.type))
      .map((field) => ({ key: field.key, label: field.label })) ?? [];

  return [...competitionCandidates, ...fieldCandidates];
}
