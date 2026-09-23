"use client";

import { useId } from "react";

import { amountInWords } from "@/lib/amount-in-words";

import { FieldError } from "./field-error";

/**
 * A money input with the amount spelled out underneath it (T-22, step 1.4:
 * "Pod każdym polem kwotowym dopisujemy kwotę słownie, bo wchodzi potem do
 * umowy"). Used for every one of the four amount fields in the limits step.
 *
 * The label is a separate element pointed at the input by `id`, not a
 * wrapping `<label>`: nesting the "zł" suffix and the words hint inside a
 * wrapping label would fold them into the input's accessible name, so a
 * screen reader would read the whole spelled out amount as part of the
 * field's label and that name would change on every keystroke.
 */
export function AmountField({
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
  const numeric = value.trim() === "" ? null : Number(value);
  const words =
    numeric !== null && Number.isFinite(numeric) && numeric >= 0
      ? amountInWords(numeric)
      : null;

  return (
    <div className="flex flex-col gap-1 text-sm">
      <label htmlFor={id}>{label}</label>
      <span className="flex items-center gap-2">
        <input
          id={id}
          type="number"
          min={0}
          step="0.01"
          inputMode="decimal"
          className="w-40 rounded-sm border border-border px-2 py-1"
          value={value}
          onChange={(event) => onChange(event.target.value)}
        />
        zł
      </span>
      {words !== null ? <span className="italic">{words}</span> : null}
      <FieldError messages={fieldErrors} />
    </div>
  );
}
