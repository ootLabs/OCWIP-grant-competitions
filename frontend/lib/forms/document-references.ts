/**
 * Who points at a given field, by key (T-26).
 *
 * Used before a field is deleted or moved, so an operator never silently
 * breaks a visibility condition, a calculation or a limit sitting somewhere
 * else in the document.
 *
 * A table column is written two different ways depending on where the
 * reference sits (docs/kontrakt-formularza.md, section "Tabela"): a sibling
 * column in the same table names it bare ("cena"), anything outside the
 * table names it qualified ("budzet_a.cena"). `findReferencesTo` resolves
 * which form applies at each site instead of asking the caller to know.
 */
import type { FormDocument, FormField, FormSection } from "./document-types";

export interface FieldReference {
  readonly sectionKey: string;
  readonly fieldKey: string;
  readonly fieldLabel: string;
  readonly via: "visibleWhen" | "calculation" | "limit";
}

/** Where a key lives: at document level, or inside a table under `ownerTableKey`. */
function locateOwnerTable(document: FormDocument, key: string): string | null {
  for (const section of document.sections) {
    for (const field of section.fields) {
      if (field.key === key) {
        return null;
      }
      if (field.table?.columns.some((column) => column.key === key)) {
        return field.key;
      }
    }
  }

  return null;
}

/** The bare key ever means the target field, so this is enough to detect a match. */
function isKnownColumn(document: FormDocument, key: string): boolean {
  return document.sections.some((section) =>
    section.fields.some((field) => field.table?.columns.some((c) => c.key === key)),
  );
}

export function findReferencesTo(
  document: FormDocument,
  key: string,
): FieldReference[] {
  const ownerTableKey = isKnownColumn(document, key) ? locateOwnerTable(document, key) : null;
  const references: FieldReference[] = [];

  const matchKeyFor = (siteOwnerTableKey: string | null): string =>
    ownerTableKey === null || siteOwnerTableKey === ownerTableKey
      ? key
      : `${ownerTableKey}.${key}`;

  for (const section of document.sections) {
    if (section.visibleWhen?.field === key) {
      references.push({
        sectionKey: section.key,
        fieldKey: section.key,
        fieldLabel: `sekcja "${section.title}"`,
        via: "visibleWhen",
      });
    }

    for (const field of section.fields) {
      collectFieldReferences(field, matchKeyFor(null), section, references);

      for (const column of field.table?.columns ?? []) {
        collectFieldReferences(
          column,
          matchKeyFor(field.key),
          section,
          references,
          field,
        );
      }
    }
  }

  return references;
}

function collectFieldReferences(
  field: FormField,
  matchKey: string,
  section: FormSection,
  references: FieldReference[],
  owningTable?: FormField,
): void {
  const label = owningTable ? `${owningTable.label} / ${field.label}` : field.label;

  if (field.visibleWhen?.field === matchKey) {
    references.push({
      sectionKey: section.key,
      fieldKey: field.key,
      fieldLabel: label,
      via: "visibleWhen",
    });
  }

  if ((field.calculation?.operands ?? []).includes(matchKey)) {
    references.push({
      sectionKey: section.key,
      fieldKey: field.key,
      fieldLabel: label,
      via: "calculation",
    });
  }

  if ((field.limits ?? []).some((limit) => limit.basis === matchKey)) {
    references.push({
      sectionKey: section.key,
      fieldKey: field.key,
      fieldLabel: label,
      via: "limit",
    });
  }
}
