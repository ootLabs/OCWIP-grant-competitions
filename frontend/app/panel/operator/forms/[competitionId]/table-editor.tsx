"use client";

import {
  addColumnToTable,
  findField,
  moveField,
  removeField,
  updateField,
} from "@/lib/forms/document-edit";
import { newField } from "@/lib/forms/document-factory";
import { allFieldKeys } from "@/lib/forms/document-keys";
import {
  COLUMN_FIELD_TYPES,
  type FormDocument,
  type FormTable,
} from "@/lib/forms/document-types";
import { AddFieldControl } from "./add-field-control";
import { FieldRow } from "./field-row";

/**
 * The columns of a repeatableTable or fixedTable, each an ordinary field with
 * a narrower set of kinds (no table inside a table, no attachment inside a
 * row: FormFieldTypes.IsAllowedInTable on the backend). This is also where
 * the budget table's row value gets built: add a "Pole wyliczane" column,
 * pick "Iloczyn składników" and its two numeric siblings, done (card T-26,
 * "tabela budżetu").
 */
export function TableEditor({
  document,
  sectionKey,
  fieldKey,
  table,
  onChange,
}: {
  document: FormDocument;
  sectionKey: string;
  fieldKey: string;
  table: FormTable;
  onChange: (table: FormTable) => void;
}) {
  const isFixed = table.rows !== undefined;

  return (
    <div className="flex flex-col gap-3">
      {isFixed ? (
        <p className="text-sm">
          Tabela ma stałą liczbę wierszy ({table.rows?.length ?? 0}). Zmiana liczby
          wierszy nie jest jeszcze dostępna w kreatorze.
        </p>
      ) : (
        <label className="flex items-center gap-2 text-sm">
          Minimalna liczba wierszy
          <input
            type="number"
            min={0}
            className="w-20 rounded-sm border border-border-control px-2 py-1"
            value={table.minRows ?? 0}
            onChange={(event) =>
              onChange({ ...table, minRows: Number(event.target.value) })
            }
          />
        </label>
      )}

      <ul className="flex flex-col gap-2">
        {table.columns.map((column, index) => {
          const path = { sectionKey, fieldKey, columnKey: column.key };

          return (
            <FieldRow
              key={column.key}
              document={document}
              sectionKey={sectionKey}
              fieldKey={fieldKey}
              columnKey={column.key}
              field={column}
              canMoveUp={index > 0}
              canMoveDown={index < table.columns.length - 1}
              onChange={(updated) =>
                onChange(tableOf(updateField(document, path, () => updated), sectionKey, fieldKey))
              }
              onMove={(direction) =>
                onChange(tableOf(moveField(document, path, direction), sectionKey, fieldKey))
              }
              onRemove={() =>
                onChange(tableOf(removeField(document, path), sectionKey, fieldKey))
              }
            />
          );
        })}
      </ul>

      <AddFieldControl
        types={COLUMN_FIELD_TYPES}
        onAdd={(type, label) => {
          const taken = new Set([
            ...allFieldKeys(document),
            ...table.columns.map((c) => c.key),
          ]);
          const column = newField(type, label, taken, true);
          onChange(
            tableOf(
              addColumnToTable(document, { sectionKey, fieldKey }, column),
              sectionKey,
              fieldKey,
            ),
          );
        }}
      />
    </div>
  );
}

/** Pulls the table field's own `table` back out of a whole-document edit. */
function tableOf(document: FormDocument, sectionKey: string, fieldKey: string): FormTable {
  return findField(document, { sectionKey, fieldKey })!.table!;
}
