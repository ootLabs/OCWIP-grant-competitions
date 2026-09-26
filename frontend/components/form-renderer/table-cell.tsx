"use client";

import { useId } from "react";
import { computeRowValue } from "@/lib/forms/evaluate";
import { validateCell } from "@/lib/forms/validate";
import { formatComputedNumber } from "@/lib/forms/format-computed";
import { answerText } from "@/lib/forms/answer-text";
import { yesNoFromWireValue, yesNoWireValue } from "@/lib/forms/document-types";
import type { AnswerValue, TableRowAnswers } from "@/lib/forms/answer-types";
import type { FormField } from "@/lib/forms/document-types";
import { cellKey, useRenderer } from "./renderer-context";

/** One cell of one row of a table field: its input, or its computed value, and its own error. */
export function TableCell({
  field,
  column,
  rowIndex,
  row,
}: {
  field: FormField;
  column: FormField;
  rowIndex: number;
  row: TableRowAnswers;
}) {
  const { touched, onAnswerCell, onTouch } = useRenderer();
  const errorId = useId();
  const key = cellKey(field.key, rowIndex, column.key);
  const value = row[column.key];
  const error = validateCell(column, value);
  const showError = error !== null && touched.has(key);
  // The cell is named by aria-label, not by its column header, so the
  // header's "(wymagane)" has to be repeated here to reach a screen reader.
  const label = `${column.label}${column.required ? " (wymagane)" : ""}, wiersz ${rowIndex + 1}`;

  return (
    <td className="border-b border-border-muted px-2 py-1">
      {column.readOnly ? (
        // "Było" (T-50a): the application's value, text rather than an input.
        <span>{answerText(column, value) ?? ""}</span>
      ) : column.type === "calculated" ? (
        <span aria-live="polite">
          {formatComputedNumber(computeRowValue(field, row, column.key), column.calculation?.kind)}
        </span>
      ) : (
        <CellInput
          column={column}
          value={value}
          label={label}
          invalid={showError}
          describedBy={showError ? errorId : undefined}
          onChange={(next) => onAnswerCell(field.key, rowIndex, column.key, next)}
          onBlur={() => onTouch(key)}
        />
      )}
      {showError ? (
        <span id={errorId} role="alert" className="block text-xs text-brand-accent-text">
          {error}
        </span>
      ) : null}
    </td>
  );
}

function CellInput({
  column,
  value,
  label,
  invalid,
  describedBy,
  onChange,
  onBlur,
}: {
  column: FormField;
  value: AnswerValue;
  label: string;
  invalid: boolean;
  describedBy: string | undefined;
  onChange: (value: AnswerValue) => void;
  onBlur: () => void;
}) {
  const className = "w-full rounded-sm border border-border-control px-1 py-0.5 aria-invalid:border-brand-accent";
  const common = {
    "aria-label": label,
    "aria-invalid": invalid || undefined,
    "aria-describedby": describedBy,
    onBlur,
    className,
  } as const;

  if (column.type === "number" || column.type === "amount" || column.type === "percent") {
    return (
      <input
        {...common}
        type="number"
        value={typeof value === "number" ? value : ""}
        onChange={(event) => onChange(event.target.value === "" ? null : Number(event.target.value))}
      />
    );
  }

  if (column.type === "date") {
    return (
      <input
        {...common}
        type="date"
        value={typeof value === "string" ? value : ""}
        onChange={(event) => onChange(event.target.value)}
      />
    );
  }

  if (column.type === "dateTime") {
    return (
      <input
        {...common}
        type="datetime-local"
        value={typeof value === "string" ? value : ""}
        onChange={(event) => onChange(event.target.value)}
      />
    );
  }

  if (column.type === "yesNo") {
    return (
      <select
        {...common}
        value={typeof value === "boolean" ? yesNoWireValue(value) : ""}
        onChange={(event) =>
          onChange(event.target.value === "" ? null : yesNoFromWireValue(event.target.value))
        }
      >
        <option value="">(wybierz)</option>
        <option value="true">Tak</option>
        <option value="false">Nie</option>
      </select>
    );
  }

  if (column.type === "singleChoice") {
    return (
      <select
        {...common}
        value={typeof value === "string" ? value : ""}
        onChange={(event) => onChange(event.target.value === "" ? null : event.target.value)}
      >
        <option value="">(wybierz)</option>
        {(column.options ?? []).map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
    );
  }

  if (column.type === "multipleChoice") {
    const selected = Array.isArray(value) ? value : [];
    return (
      <select
        {...common}
        multiple
        value={selected as string[]}
        onChange={(event) =>
          onChange(Array.from(event.target.selectedOptions, (option) => option.value))
        }
      >
        {(column.options ?? []).map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
    );
  }

  return (
    <input
      {...common}
      type="text"
      maxLength={column.maxLength}
      value={typeof value === "string" ? value : ""}
      onChange={(event) => onChange(event.target.value)}
    />
  );
}
