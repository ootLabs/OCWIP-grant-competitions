"use client";

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
            className="w-20 rounded-sm border border-border px-2 py-1"
            value={table.minRows ?? 0}
            onChange={(event) =>
              onChange({ ...table, minRows: Number(event.target.value) })
            }
          />
        </label>
      )}

      <ul className="flex flex-col gap-2">
        {table.columns.map((column, index) => (
          <FieldRow
            key={column.key}
            document={document}
            sectionKey={sectionKey}
            fieldKey={fieldKey}
            columnKey={column.key}
            field={column}
            canMoveUp={index > 0}
            canMoveDown={index < table.columns.length - 1}
            onChange={(updated) => {
              const columns = [...table.columns];
              columns[index] = updated;
              onChange({ ...table, columns });
            }}
            onMove={(direction) => {
              const target = index + (direction === "up" ? -1 : 1);
              if (target < 0 || target >= table.columns.length) {
                return;
              }
              const columns = [...table.columns];
              [columns[index], columns[target]] = [columns[target], columns[index]];
              onChange({ ...table, columns });
            }}
            onRemove={() =>
              onChange({
                ...table,
                columns: table.columns.filter((c) => c.key !== column.key),
              })
            }
          />
        ))}
      </ul>

      <AddFieldControl
        types={COLUMN_FIELD_TYPES}
        onAdd={(type, label) => {
          const taken = new Set([
            ...allFieldKeys(document),
            ...table.columns.map((c) => c.key),
          ]);
          onChange({ ...table, columns: [...table.columns, newField(type, label, taken)] });
        }}
      />
    </div>
  );
}
