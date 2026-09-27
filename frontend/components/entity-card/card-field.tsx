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
      <FieldErrorList id={errorsId} errors={errors} />
    </div>
  );
}

/** A choice of the card (legal form, register), with the backend's messages under it. */
export function Select<T extends string>({
  id,
  label,
  value,
  options,
  onChange,
  errors,
}: {
  id: string;
  label: string;
  value: T | "";
  options: Record<T, string>;
  onChange: (value: T | "") => void;
  errors?: string[];
}) {
  const invalid = (errors ?? []).length > 0;

  return (
    <div className="flex flex-col gap-1 text-sm">
      <label htmlFor={id}>{label}</label>
      <select
        id={id}
        className={cardInputClassName}
        aria-invalid={invalid || undefined}
        aria-describedby={invalid ? `${id}-errors` : undefined}
        value={value}
        onChange={(event) => onChange(event.target.value as T | "")}
      >
        <option value="">Wybierz…</option>
        {(Object.keys(options) as T[]).map((key) => (
          <option key={key} value={key}>
            {options[key]}
          </option>
        ))}
      </select>
      <FieldErrorList id={`${id}-errors`} errors={errors} />
    </div>
  );
}

/** The backend's sentences for one field or one group of fields, as they came. */
export function FieldErrorList({ id, errors }: { id?: string; errors?: string[] }) {
  if (!errors || errors.length === 0) {
    return null;
  }

  return (
    <ul id={id} className="text-sm text-brand-accent-text">
      {errors.map((error) => (
        <li key={error}>{error}</li>
      ))}
    </ul>
  );
}
