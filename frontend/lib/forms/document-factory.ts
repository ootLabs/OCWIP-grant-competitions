/**
 * Default shapes for a new section or field (T-26).
 *
 * Every factory here produces something the backend's FormSchemaValidator
 * would accept as is: a textual field already carries a maxLength, a
 * calculated field already carries an (empty) calculation, and so on. The
 * creator's job is to keep the document valid at every step, not just at the
 * end.
 */
import { uniqueKey } from "./document-keys";
import {
  CHOICE_TYPES,
  TABLE_TYPES,
  TEXTUAL_TYPES,
  type FormField,
  type FormFieldType,
  type FormSection,
} from "./document-types";

export function newSection(
  title: string,
  taken: ReadonlySet<string>,
): FormSection {
  return {
    key: uniqueKey(title, taken),
    title,
    description: "",
    visibleWhen: null,
    fields: [],
  };
}

/**
 * A new field of the given kind, with the defaults its kind needs to be
 * valid. `isColumn` matters only for a calculated field: a column can never
 * sum, because summing turns many rows into one and a single row is never
 * "many" (see calculation-editor.tsx), so a column defaults to a product
 * instead of the top level default of a sum.
 */
export function newField(
  type: FormFieldType,
  label: string,
  taken: ReadonlySet<string>,
  isColumn = false,
): FormField {
  const base: FormField = {
    key: uniqueKey(label, taken),
    type,
    label,
    help: "",
    required: true,
    printed: true,
    visibleWhen: null,
  };

  if (TEXTUAL_TYPES.has(type)) {
    return { ...base, maxLength: 500 };
  }

  if (CHOICE_TYPES.has(type)) {
    return { ...base, options: [] };
  }

  if (TABLE_TYPES.has(type)) {
    return {
      ...base,
      table:
        type === "repeatableTable"
          ? { columns: [], minRows: 1 }
          : { columns: [], rows: [] },
    };
  }

  if (type === "file") {
    return { ...base, file: { allowedFormats: [], maxSizeMegabytes: 10 } };
  }

  if (type === "statement") {
    return { ...base, statementText: "", printed: false };
  }

  if (type === "calculated") {
    return {
      ...base,
      required: false,
      calculation: { kind: isColumn ? "product" : "sum", operands: [] },
    };
  }

  return base;
}
