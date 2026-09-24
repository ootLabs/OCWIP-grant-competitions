"use client";

import { useId } from "react";

const inputClassName =
  "rounded-sm border border-border px-2 py-2 aria-invalid:border-brand-accent";

/** The one submit button every account screen has, same as on /login. */
export const accountSubmitClassName =
  "inline-flex w-full items-center justify-center rounded-sm bg-brand-accent px-5 py-3 text-bg hover:bg-brand-accent-hover disabled:opacity-70";

type AccountFieldProps = {
  label: string;
  name: string;
  type: "email" | "password" | "text";
  autoComplete: string;
  value: string;
  onChange: (value: string) => void;
  /** The backend's own sentences for this field, shown as they came. */
  errors?: string[];
  /** A standing hint under the input, for example the password policy. */
  hint?: string;
};

/**
 * One input of an account screen (T-12.8), with its label and the backend's
 * messages for it.
 *
 * The messages are tied to the input by aria-describedby, not announced one by
 * one: the form says once, in its own alert, that something needs fixing, and a
 * screen reader reads each field's reason when the field gets focus.
 */
export function AccountField({
  label,
  name,
  type,
  autoComplete,
  value,
  onChange,
  errors = [],
  hint,
}: AccountFieldProps) {
  const hintId = useId();
  const errorsId = useId();
  const invalid = errors.length > 0;

  const describedBy = [hint ? hintId : null, invalid ? errorsId : null]
    .filter((id): id is string => id !== null)
    .join(" ");

  return (
    <div className="flex flex-col gap-1 text-sm">
      <label className="flex flex-col gap-1">
        {label}
        <input
          aria-describedby={describedBy || undefined}
          aria-invalid={invalid || undefined}
          autoComplete={autoComplete}
          className={inputClassName}
          name={name}
          onChange={(event) => onChange(event.target.value)}
          required
          type={type}
          value={value}
        />
      </label>

      {hint && (
        <p id={hintId}>
          {hint}
        </p>
      )}

      {invalid && (
        <ul className="text-brand-accent-text" id={errorsId}>
          {errors.map((error) => (
            <li key={error}>{error}</li>
          ))}
        </ul>
      )}
    </div>
  );
}
