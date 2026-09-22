"use client";

import type { AnswerValue } from "@/lib/forms/answer-types";
import type { FormField } from "@/lib/forms/document-types";
import { yesNoFromWireValue, yesNoWireValue } from "@/lib/forms/document-types";
import { useRenderer } from "./renderer-context";

/**
 * yesNo, singleChoice and multipleChoice: a group of radios or checkboxes
 * under the fieldset/legend field-view.tsx already drew, so this only ever
 * renders the options themselves, each with its own visible label.
 */
export function ChoiceInput({
  field,
  value,
  describedBy,
}: {
  field: FormField;
  value: AnswerValue;
  describedBy: string;
}) {
  const { onAnswer, onTouch } = useRenderer();

  const options =
    field.type === "yesNo"
      ? [
          { value: "true", label: "Tak" },
          { value: "false", label: "Nie" },
        ]
      : (field.options ?? []);

  const isMultiple = field.type === "multipleChoice";
  const selected = new Set(
    isMultiple ? (Array.isArray(value) ? value : []) : [toWireValue(field, value)],
  );

  return (
    <div className="flex flex-col gap-1" aria-describedby={describedBy || undefined}>
      {options.map((option) => (
        <label key={option.value} className="flex items-center gap-2 text-sm">
          <input
            type={isMultiple ? "checkbox" : "radio"}
            name={field.key}
            checked={selected.has(option.value)}
            onBlur={() => onTouch(field.key)}
            onChange={(event) => {
              if (!isMultiple) {
                onAnswer(field.key, fromWireValue(field, option.value));
                return;
              }
              const current = Array.isArray(value) ? [...value] : [];
              onAnswer(
                field.key,
                event.target.checked
                  ? [...current, option.value]
                  : current.filter((v) => v !== option.value),
              );
            }}
          />
          {option.label}
        </label>
      ))}
    </div>
  );
}

function toWireValue(field: FormField, value: AnswerValue): string | undefined {
  if (field.type === "yesNo") {
    return typeof value === "boolean" ? yesNoWireValue(value) : undefined;
  }
  return typeof value === "string" ? value : undefined;
}

function fromWireValue(field: FormField, wireValue: string): AnswerValue {
  return field.type === "yesNo" ? yesNoFromWireValue(wireValue) : wireValue;
}
