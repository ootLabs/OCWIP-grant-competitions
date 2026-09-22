"use client";

import { useId } from "react";
import { computeTopLevelValue, isFieldVisible } from "@/lib/forms/evaluate";
import { validateField } from "@/lib/forms/validate";
import { CHOICE_TYPES } from "@/lib/forms/document-types";
import type { AnswerValue } from "@/lib/forms/answer-types";
import type { FormField } from "@/lib/forms/document-types";
import { useRenderer } from "./renderer-context";
import { FieldInput } from "./field-input";

/** Kinds whose input is a group of controls, so the label is a legend, not a `<label htmlFor>`. */
const GROUP_TYPES = new Set([...CHOICE_TYPES, "yesNo"]);

/**
 * One field: its visible label, its input, its help text underneath (always
 * there, always quiet), and its error (only when real and only once touched,
 * proces.md rule 4). A field with no visibleWhen, or whose condition is not
 * met, renders nothing at all rather than a disabled control: hiding a
 * question is not the same as showing it crossed out.
 *
 * Not used for a table field: a table has its own layout in
 * table-field.tsx, not a single labelled control.
 */
export function FieldView({ field }: { field: FormField }) {
  const { document, answers, touched, competitionSettings } = useRenderer();
  const helpId = useId();
  const counterId = useId();
  const errorId = useId();

  if (!isFieldVisible(field, answers)) {
    return null;
  }

  const error = validateField(document, answers, field, competitionSettings);
  // A calculated field is read only, so an applicant can never blur it to
  // "touch" it the way every other field is gated: its only error (a limit,
  // e.g. the grant ceiling) has to show as soon as it is true.
  const showError = error !== null && (field.type === "calculated" || touched.has(field.key));
  // Never a table field here: section-view.tsx routes those to table-field.tsx.
  const value = answers[field.key] as AnswerValue;
  const counter = counterText(field, value);

  const describedBy = [field.help ? helpId : null, counter ? counterId : null, showError ? errorId : null]
    .filter((id): id is string => id !== null)
    .join(" ");

  // A calculated field's own answer is never populated (nothing writes to
  // it, it is read only), so its display comes from the same engine its
  // limit check already runs, not from `answers` like every other field.
  const computedValue =
    field.type === "calculated" ? computeTopLevelValue(document, answers, field) : undefined;

  const input = (
    <FieldInput
      field={field}
      value={value}
      computedValue={computedValue}
      describedBy={describedBy}
      invalid={showError}
    />
  );
  const isGroup = GROUP_TYPES.has(field.type);

  return (
    <div className="flex flex-col gap-1">
      {isGroup ? (
        <fieldset>
          <legend className="text-sm font-medium">
            {field.label}
            {field.required ? <span aria-hidden="true"> *</span> : null}
          </legend>
          {input}
        </fieldset>
      ) : (
        <>
          <label htmlFor={field.key} className="text-sm font-medium">
            {field.label}
            {field.required ? <span aria-hidden="true"> *</span> : null}
          </label>
          {input}
        </>
      )}

      {field.help ? (
        <p id={helpId} className="text-sm">
          {field.help}
        </p>
      ) : null}

      {counter ? (
        <p id={counterId} className="text-sm">
          {counter}
        </p>
      ) : null}

      {showError ? (
        <p id={errorId} role="alert" className="text-sm text-brand-accent-text">
          {error}
        </p>
      ) : null}
    </div>
  );
}

function counterText(field: FormField, value: AnswerValue): string | null {
  if (field.maxLength === undefined || typeof value !== "string") {
    return null;
  }
  return `${value.length} z ${field.maxLength}`;
}
