"use client";

import { useId } from "react";

export const cardInputClassName =
  "rounded-sm border border-border-control px-2 py-2 aria-invalid:border-brand-accent";

type CardFieldProps = {
  label: string;
  name: string;
  value: string;
  onChange: (value: string) => void;
  required?: boolean;
  type?: "text" | "email" | "tel";
  autoComplete?: string;
  inputMode?: "text" | "numeric" | "tel" | "email";
  hint?: string;
  /** The backend's own sentences for this field, shown as they came. */
  errors?: string[];
};

/**
 * One input of the Podmiot card (T-93). The same pattern as AccountField:
 * the backend's messages tied to the input by aria-describedby, so a screen
 * reader reads the reason when the field gets focus, and the form says once
 * that something needs fixing.
 */
export function CardField({
  label,
  name,
  value,
  onChange,
  required = false,
  type = "text",
  autoComplete = "off",
  inputMode,
  hint,
  errors = [],
}: CardFieldProps) {
  const hintId = useId();
  const errorsId = useId();
  const invalid = errors.length > 0;
  const describedBy = [hint ? hintId : null, invalid ? errorsId : null]
    .filter((id): id is string => id !== null)
    .join(" ");

  return (
    <div className="flex flex-col gap-1 text-sm">
      <label className="flex flex-col gap-1">
        <span>
          {label}
          {required ? null : <span> (nieobowiązkowe)</span>}
        </span>
        <input
          aria-describedby={describedBy || undefined}
          aria-invalid={invalid || undefined}
          autoComplete={autoComplete}
          className={cardInputClassName}
          inputMode={inputMode}
          name={name}
          onChange={(event) => onChange(event.target.value)}
          required={required}
          type={type}
          value={value}
        />
      </label>
      {hint ? <p id={hintId}>{hint}</p> : null}
      {invalid ? (
        <ul className="text-brand-accent-text" id={errorsId}>
          {errors.map((error) => (
            <li key={error}>{error}</li>
          ))}
        </ul>
      ) : null}
    </div>
  );
}
