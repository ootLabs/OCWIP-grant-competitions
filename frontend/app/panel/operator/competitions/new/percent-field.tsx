"use client";

import { useId } from "react";

import { FieldError } from "./field-error";

/**
 * A percentage input (krok 1.4). The "%" sits next to the input, not inside
 * a wrapping label, for the same reason AmountField's "zł" does not: a
 * suffix folded into the label would become part of the field's accessible
 * name.
 */
export function PercentField({
  label,
  value,
  onChange,
  fieldErrors,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
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
          min={0}
          max={100}
          step="0.01"
          className="w-24 rounded-sm border border-border px-2 py-1"
          value={value}
          onChange={(event) => onChange(event.target.value)}
        />
        %
      </span>
      <FieldError messages={fieldErrors} />
    </div>
  );
}
