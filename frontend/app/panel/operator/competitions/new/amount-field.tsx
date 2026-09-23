"use client";

import { amountInWords } from "@/lib/amount-in-words";

import { SuffixedNumberField } from "./suffixed-number-field";

/**
 * A money input with the amount spelled out underneath it (T-22, step 1.4:
 * "Pod każdym polem kwotowym dopisujemy kwotę słownie, bo wchodzi potem do
 * umowy"). Used for every one of the four amount fields in the limits step.
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
  const numeric = value.trim() === "" ? null : Number(value);
  // amountInWords only spells out amounts below a billion (see its own
  // comment): CompetitionRequestValidator allows amounts far larger than
  // that, so an operator typing one must get no hint, not a crashed wizard.
  const words =
    numeric !== null &&
    Number.isFinite(numeric) &&
    numeric >= 0 &&
    numeric < 1_000_000_000
      ? amountInWords(numeric)
      : null;

  return (
    <SuffixedNumberField
      label={label}
      value={value}
      onChange={onChange}
      suffix="zł"
      widthClassName="w-40"
      hint={words !== null ? <span className="italic">{words}</span> : null}
      fieldErrors={fieldErrors}
    />
  );
}
