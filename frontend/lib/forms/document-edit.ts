/**
 * Pure edits over a FormDocument (T-26).
 *
 * Every screen in the creator calls one of these instead of touching
 * `sections`/`fields`/`table.columns` arrays directly, so "how do I reach a
 * table column two levels down" is answered once. Nothing here mutates its
 * argument: each function returns a new document, which is what lets the
 * builder keep a simple undo history of past documents.
 */
import type {
  FormDocument,
  FormField,
  FormSection,
  FormTableRow,
} from "./document-types";

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

export function addSection(
  document: FormDocument,
  section: FormSection,
): FormDocument {
  return { ...document, sections: [...document.sections, section] };
}

export function removeSection(
  document: FormDocument,
  sectionKey: string,
): FormDocument {
  return {
    ...document,
    sections: document.sections.filter((section) => section.key !== sectionKey),
  };
}

export function moveSection(
  document: FormDocument,
  sectionKey: string,
  direction: "up" | "down",
): FormDocument {
  return {
    ...document,
    sections: reorder(
      document.sections,
      (section) => section.key === sectionKey,
      direction === "up" ? -1 : 1,
    ),
  };
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

/**
 * The rows of a table of fixed size (T-26a), which are labels written by the
 * operator rather than fields: the three members of an informal group, the
 * named lines of a cost table. A table of variable size has none, because its
 * rows are the applicant's to add.
 */
export function updateTableRows(
  document: FormDocument,
  path: Omit<FieldPath, "columnKey">,
  update: (rows: readonly FormTableRow[]) => FormTableRow[],
): FormDocument {
  return updateField(document, path, (field) => ({
    ...field,
    table: { ...field.table!, rows: update(field.table!.rows ?? []) },
  }));
}

export function addTableRow(
  document: FormDocument,
  path: Omit<FieldPath, "columnKey">,
  row: FormTableRow,
): FormDocument {
  return updateTableRows(document, path, (rows) => [...rows, row]);
}

export function removeTableRow(
  document: FormDocument,
  path: Omit<FieldPath, "columnKey">,
  rowKey: string,
): FormDocument {
  return updateTableRows(document, path, (rows) =>
    rows.filter((row) => row.key !== rowKey),
  );
}

export function renameTableRow(
  document: FormDocument,
  path: Omit<FieldPath, "columnKey">,
  rowKey: string,
  label: string,
): FormDocument {
  return updateTableRows(document, path, (rows) =>
    rows.map((row) => (row.key === rowKey ? { ...row, label } : row)),
  );
}

export function moveTableRow(
  document: FormDocument,
  path: Omit<FieldPath, "columnKey">,
  rowKey: string,
  direction: "up" | "down",
): FormDocument {
  return updateTableRows(document, path, (rows) =>
    reorder(rows, (row) => row.key === rowKey, direction === "up" ? -1 : 1),
  );
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

/**
 * Swaps the item `isTarget` matches with its neighbour `offset` places away.
 * A move past either end is a no-op rather than wrapping around, which would
 * read as the button doing nothing rather than as a boundary. Exported for
 * anything that reorders a plain list the same way a field or column does,
 * for example a table row (components/form-renderer/form-renderer.tsx).
 */
export function reorder<T>(
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
