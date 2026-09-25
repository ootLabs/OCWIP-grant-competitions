"use client";

import { resolveTableRows } from "@/lib/forms/answer-types";
import { validateRowCount } from "@/lib/forms/validate";
import type { TableRowAnswers } from "@/lib/forms/answer-types";
import type { FormField } from "@/lib/forms/document-types";
import { fieldAnchorId } from "@/lib/forms/field-anchor";
import { useRenderer } from "./renderer-context";
import { TableCell } from "./table-cell";
import { RequiredMark } from "./field-view";

/**
 * repeatableTable and fixedTable, "the hardest component of the whole front"
 * (card T-28). A repeatable table's rows are the applicant's own data; a
 * fixed one's rows are the definition's own `table.rows` (three named
 * members of an informal group, for example), so this reads row identity
 * from whichever of the two the field actually has.
 */
export function TableField({ field }: { field: FormField }) {
  const { answers, touched, onAddRow, onRemoveRow, onMoveRow } = useRenderer();
  const table = field.table!;
  const isFixed = field.type === "fixedTable";
  const rows = resolveTableRows(field, answers);
  const rowCountError = validateRowCount(field, rows.length);
  const showRowCountError = rowCountError !== null && touched.has(field.key);

  return (
    <fieldset id={fieldAnchorId(field.key)} className="flex flex-col gap-2">
      <legend className="text-sm font-medium">
        {field.label}
        {field.required ? <RequiredMark /> : null}
      </legend>
      {field.help ? <p className="text-sm">{field.help}</p> : null}

      <div className="overflow-x-auto">
        <table className="w-full border-collapse text-sm">
          <thead>
            <tr>
              {!isFixed ? <th scope="col" /> : null}
              {table.columns.map((column) => (
                <th key={column.key} scope="col" className="border-b border-border px-2 py-1 text-left">
                  {column.label}
                  {column.required ? <RequiredMark /> : null}
                </th>
              ))}
              {!isFixed ? <th scope="col" /> : null}
            </tr>
          </thead>
          <tbody>
            {rows.map((row, rowIndex) => (
              <TableRow
                key={rowIndex}
                field={field}
                rowIndex={rowIndex}
                rowLabel={isFixed ? table.rows![rowIndex].label : undefined}
                row={row}
                isFixed={isFixed}
                isLast={rowIndex === rows.length - 1}
                onRemove={() => onRemoveRow(field.key, rowIndex)}
                onMove={(direction) => onMoveRow(field.key, rowIndex, direction)}
              />
            ))}
          </tbody>
        </table>
      </div>

      {!isFixed ? (
        <button
          type="button"
          className="self-start text-sm underline"
          onClick={() => onAddRow(field.key)}
        >
          Dodaj wiersz
        </button>
      ) : null}

      {showRowCountError ? (
        <p role="alert" className="text-sm text-brand-accent-text">
          {rowCountError}
        </p>
      ) : null}
    </fieldset>
  );
}

function TableRow({
  field,
  rowIndex,
  rowLabel,
  row,
  isFixed,
  isLast,
  onRemove,
  onMove,
}: {
  field: FormField;
  rowIndex: number;
  rowLabel: string | undefined;
  row: TableRowAnswers;
  isFixed: boolean;
  isLast: boolean;
  onRemove: () => void;
  onMove: (direction: "up" | "down") => void;
}) {
  const table = field.table!;

  return (
    <tr>
      {!isFixed ? (
        <th scope="row" className="px-1 py-1 font-normal">
          <span className="flex gap-1">
            <button
              type="button"
              className="text-xs underline disabled:no-underline disabled:opacity-40"
              disabled={rowIndex === 0}
              onClick={() => onMove("up")}
              aria-label={`Przesuń wiersz ${rowIndex + 1} w górę`}
            >
              Góra
            </button>
            <button
              type="button"
              className="text-xs underline disabled:no-underline disabled:opacity-40"
              disabled={isLast}
              onClick={() => onMove("down")}
              aria-label={`Przesuń wiersz ${rowIndex + 1} w dół`}
            >
              Dół
            </button>
          </span>
        </th>
      ) : null}

      {isFixed ? (
        <th scope="row" className="border-b border-border-muted px-2 py-1 text-left font-normal">
          {rowLabel}
        </th>
      ) : null}

      {table.columns.map((column) => (
        <TableCell key={column.key} field={field} column={column} rowIndex={rowIndex} row={row} />
      ))}

      {!isFixed ? (
        <td className="border-b border-border-muted px-1 py-1">
          <button type="button" className="text-xs underline" onClick={onRemove}>
            Usuń wiersz
          </button>
        </td>
      ) : null}
    </tr>
  );
}
