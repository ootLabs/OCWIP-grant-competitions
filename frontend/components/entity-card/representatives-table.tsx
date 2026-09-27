"use client";

import type { EntityRepresentative } from "@/lib/entity-card";

import { CardField } from "./card-field";

type FieldErrors = Record<string, string[]>;

/**
 * "Osoby uprawnione do reprezentowania" (pola.md step 2.2): at least one row,
 * each with a first name, last name and function. Rows, not a spreadsheet
 * grid, so each input keeps a real label on a phone.
 */
export function RepresentativesTable({
  rows,
  onChange,
  errors,
}: {
  rows: EntityRepresentative[];
  onChange: (rows: EntityRepresentative[]) => void;
  errors: FieldErrors;
}) {
  const update = (index: number, patch: Partial<EntityRepresentative>) =>
    onChange(rows.map((row, i) => (i === index ? { ...row, ...patch } : row)));

  const listErrors = errors["representatives"] ?? [];

  return (
    <fieldset className="flex flex-col gap-3">
      <legend className="text-base">Osoby uprawnione do reprezentowania</legend>
      {listErrors.length > 0 ? (
        <ul className="text-sm text-brand-accent-text">
          {listErrors.map((error) => (
            <li key={error}>{error}</li>
          ))}
        </ul>
      ) : null}

      {rows.map((row, index) => (
        <div
          key={index}
          className="grid gap-3 rounded-sm border border-border-muted p-3 sm:grid-cols-3"
          role="group"
          aria-label={`Osoba ${index + 1}`}
        >
          <CardField
            label="Imię"
            name={`representatives[${index}].firstName`}
            required
            value={row.firstName}
            onChange={(firstName) => update(index, { firstName })}
            errors={errors[`representatives[${index}].firstName`]}
          />
          <CardField
            label="Nazwisko"
            name={`representatives[${index}].lastName`}
            required
            value={row.lastName}
            onChange={(lastName) => update(index, { lastName })}
            errors={errors[`representatives[${index}].lastName`]}
          />
          <CardField
            label="Funkcja"
            name={`representatives[${index}].function`}
            required
            value={row.function}
            onChange={(value) => update(index, { function: value })}
            errors={errors[`representatives[${index}].function`]}
          />
          {rows.length > 1 ? (
            <button
              type="button"
              className="justify-self-start text-sm underline sm:col-span-3"
              onClick={() => onChange(rows.filter((_, i) => i !== index))}
            >
              Usuń osobę {index + 1}
            </button>
          ) : null}
        </div>
      ))}

      <button
        type="button"
        className="self-start text-sm underline"
        onClick={() => onChange([...rows, { firstName: "", lastName: "", function: "" }])}
      >
        Dodaj osobę
      </button>
    </fieldset>
  );
}
