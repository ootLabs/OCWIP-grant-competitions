"use client";

import { useId } from "react";

import { FieldError } from "./field-error";

/**
 * The input chrome `AmountField` and `PercentField` both need: a label
 * pointed at the input by `id` rather than wrapping it, a unit suffix next
 * to the input, an optional hint line underneath, and a field error.
 *
 * The label is separate from the wrapping `<label>` pattern on purpose: a
 * suffix (or a hint that changes on every keystroke, see AmountField) folded
 * into a wrapping label becomes part of the input's accessible name, and a
 * screen reader would read the whole thing, changing, as the field's label.
 */
export function SuffixedNumberField({
  label,
  value,
  onChange,
  suffix,
  min = 0,
  max,
  widthClassName = "w-40",
  hint,
  fieldErrors,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  suffix: string;
  min?: number;
  max?: number;
  widthClassName?: string;
  hint?: React.ReactNode;
  fieldErrors?: string[];
}) {
  const id = useId();

  return (
    <div className="flex flex-col gap-1 text-sm">
      <label htmlFor={id}>{label}</label>
      <span className="flex items-center gap-2">
        <input
          id={id}
          type="number"
          min={min}
          max={max}
          step="0.01"
          inputMode="decimal"
          className={`${widthClassName} rounded-sm border border-border px-2 py-1`}
          value={value}
          onChange={(event) => onChange(event.target.value)}
        />
        {suffix}
      </span>
      {hint}
      <FieldError messages={fieldErrors} />
    </div>
  );
}
