"use client";

import type { AnswerValue, FileAnswerValue } from "@/lib/forms/answer-types";
import { formatComputedNumber } from "@/lib/forms/format-computed";
import type { FormField } from "@/lib/forms/document-types";
import { useRenderer } from "./renderer-context";
import { ChoiceInput } from "./choice-input";

const inputClassName =
  "rounded-sm border border-border px-2 py-1 aria-invalid:border-brand-accent";

/**
 * The control itself, one branch per kind (card T-28: "każdy rodzaj pola ze
 * schematu renderuje się i da się wypełnić"). Tables and the choice/yesNo
 * group live elsewhere: this file is only ever a single control that a
 * `<label htmlFor>` can point straight at. repeatableTable and fixedTable
 * never reach here at all: section-view.tsx routes a table field to
 * table-field.tsx before this component is ever asked to render one.
 */
export function FieldInput({
  field,
  value,
  computedValue,
  describedBy,
  invalid,
}: {
  field: FormField;
  value: AnswerValue;
  /** Only for a calculated field: its live value, computed by field-view.tsx. */
  computedValue?: number;
  describedBy: string;
  invalid: boolean;
}) {
  const { onAnswer, onTouch } = useRenderer();
  const common = {
    id: field.key,
    "aria-describedby": describedBy || undefined,
    "aria-invalid": invalid || undefined,
    onBlur: () => onTouch(field.key),
    className: inputClassName,
  } as const;

  if (field.type === "shortText") {
    return (
      <input
        {...common}
        type="text"
        maxLength={field.maxLength}
        value={typeof value === "string" ? value : ""}
        onChange={(event) => onAnswer(field.key, event.target.value)}
      />
    );
  }

  if (field.type === "longText") {
    return (
      <textarea
        {...common}
        maxLength={field.maxLength}
        value={typeof value === "string" ? value : ""}
        onChange={(event) => onAnswer(field.key, event.target.value)}
      />
    );
  }

  if (field.type === "number" || field.type === "amount" || field.type === "percent") {
    return (
      <input
        {...common}
        type="number"
        min={field.minValue}
        max={field.maxValue}
        value={typeof value === "number" ? value : ""}
        onChange={(event) =>
          onAnswer(field.key, event.target.value === "" ? null : Number(event.target.value))
        }
      />
    );
  }

  if (field.type === "date") {
    return (
      <input
        {...common}
        type="date"
        value={typeof value === "string" ? value : ""}
        onChange={(event) => onAnswer(field.key, event.target.value)}
      />
    );
  }

  if (field.type === "dateTime") {
    return (
      <input
        {...common}
        type="datetime-local"
        value={typeof value === "string" ? value : ""}
        onChange={(event) => onAnswer(field.key, event.target.value)}
      />
    );
  }

  if (field.type === "yesNo" || field.type === "singleChoice" || field.type === "multipleChoice") {
    return <ChoiceInput field={field} value={value} describedBy={describedBy} />;
  }

  if (field.type === "file") {
    return (
      <input
        {...common}
        type="file"
        accept={field.file?.allowedFormats.map((f) => `.${f}`).join(",")}
        onChange={(event) => {
          const file = event.target.files?.[0];
          const answer: FileAnswerValue | null =
            file === undefined ? null : { name: file.name, sizeBytes: file.size };
          onAnswer(field.key, answer);
        }}
      />
    );
  }

  if (field.type === "statement") {
    const { className: _unused, ...rest } = common;
    return (
      <label className="flex items-start gap-2 text-sm">
        <input
          {...rest}
          type="checkbox"
          checked={value === true}
          onChange={(event) => onAnswer(field.key, event.target.checked)}
        />
        {field.statementText}
      </label>
    );
  }

  if (field.type === "calculated") {
    return <CalculatedValue field={field} value={computedValue} />;
  }

  return null;
}

function CalculatedValue({ field, value }: { field: FormField; value: number | undefined }) {
  const display = value === undefined ? "" : formatComputedNumber(value, field.calculation?.kind);
  return (
    <input
      id={field.key}
      type="text"
      readOnly
      disabled
      aria-live="polite"
      className={`${inputClassName} bg-surface-muted`}
      value={display}
    />
  );
}
