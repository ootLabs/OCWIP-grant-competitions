/**
 * Pure edits over a FormDocument (T-26).
 *
 * Every screen in the creator calls one of these instead of touching
 * `sections`/`fields`/`table.columns` arrays directly, so "how do I reach a
 * table column two levels down" is answered once. Nothing here mutates its
 * argument: each function returns a new document, which is what lets the
 * builder keep a simple undo history of past documents.
 */
import type { FormDocument, FormField, FormSection } from "./document-types";

/** Addresses either a section-level field or, with `columnKey`, its column. */
export interface FieldPath {
  readonly sectionKey: string;
  readonly fieldKey: string;
  readonly columnKey?: string;
}

export function updateSection(
  document: FormDocument,
  sectionKey: string,
  update: (section: FormSection) => FormSection,
): FormDocument {
  return {
    ...document,
    sections: document.sections.map((section) =>
      section.key === sectionKey ? update(section) : section,
    ),
  };
}

export function updateField(
  document: FormDocument,
  path: FieldPath,
  update: (field: FormField) => FormField,
): FormDocument {
  return updateSection(document, path.sectionKey, (section) => ({
    ...section,
    fields: section.fields.map((field) => {
      if (field.key !== path.fieldKey) {
        return field;
      }

      if (path.columnKey === undefined) {
        return update(field);
      }

      return {
        ...field,
        table: {
          ...field.table!,
          columns: field.table!.columns.map((column) =>
            column.key === path.columnKey ? update(column) : column,
          ),
        },
      };
    }),
  }));
}

export function addFieldToSection(
  document: FormDocument,
  sectionKey: string,
  field: FormField,
): FormDocument {
  return updateSection(document, sectionKey, (section) => ({
    ...section,
    fields: [...section.fields, field],
  }));
}

export function addColumnToTable(
  document: FormDocument,
  path: Omit<FieldPath, "columnKey">,
  column: FormField,
): FormDocument {
  return updateField(document, path, (field) => ({
    ...field,
    table: { ...field.table!, columns: [...field.table!.columns, column] },
  }));
}

/** Removes a section-level field, or a table column when `columnKey` is set. */
export function removeField(document: FormDocument, path: FieldPath): FormDocument {
  if (path.columnKey === undefined) {
    return updateSection(document, path.sectionKey, (section) => ({
      ...section,
      fields: section.fields.filter((field) => field.key !== path.fieldKey),
    }));
  }

  return updateField(
    document,
    { sectionKey: path.sectionKey, fieldKey: path.fieldKey },
    (field) => ({
      ...field,
      table: {
        ...field.table!,
        columns: field.table!.columns.filter((column) => column.key !== path.columnKey),
      },
    }),
  );
}

/**
 * Moves a field or column one place up or down among its siblings. A move
 * past either end is a no-op rather than wrapping around, which would read
 * as the button doing nothing rather than as a boundary.
 */
export function moveField(
  document: FormDocument,
  path: FieldPath,
  direction: "up" | "down",
): FormDocument {
  const offset = direction === "up" ? -1 : 1;

  if (path.columnKey === undefined) {
    return updateSection(document, path.sectionKey, (section) => ({
      ...section,
      fields: reorder(section.fields, (field) => field.key === path.fieldKey, offset),
    }));
  }

  return updateField(
    document,
    { sectionKey: path.sectionKey, fieldKey: path.fieldKey },
    (field) => ({
      ...field,
      table: {
        ...field.table!,
        columns: reorder(
          field.table!.columns,
          (column) => column.key === path.columnKey,
          offset,
        ),
      },
    }),
  );
}

function reorder<T>(
  items: readonly T[],
  isTarget: (item: T) => boolean,
  offset: -1 | 1,
): T[] {
  const index = items.findIndex(isTarget);
  const target = index + offset;

  if (index === -1 || target < 0 || target >= items.length) {
    return [...items];
  }

  const copy = [...items];
  [copy[index], copy[target]] = [copy[target], copy[index]];
  return copy;
}

export function findField(document: FormDocument, path: FieldPath): FormField | null {
  const section = document.sections.find((s) => s.key === path.sectionKey);
  const field = section?.fields.find((f) => f.key === path.fieldKey);

  if (path.columnKey === undefined) {
    return field ?? null;
  }

  return field?.table?.columns.find((c) => c.key === path.columnKey) ?? null;
}
