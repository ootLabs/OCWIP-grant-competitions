"use client";

import { SuffixedNumberField } from "./suffixed-number-field";

/** A percentage input (krok 1.4). */
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
  return (
    <SuffixedNumberField
      label={label}
      value={value}
      onChange={onChange}
      suffix="%"
      min={0}
      max={100}
      widthClassName="w-24"
      fieldErrors={fieldErrors}
    />
  );
}
