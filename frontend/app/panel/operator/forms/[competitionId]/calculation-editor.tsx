"use client";

import { calculationOperandCandidates } from "@/lib/forms/document-candidates";
import type { CalculationKind, FormCalculation, FormDocument } from "@/lib/forms/document-types";
import { CALCULATION_KIND_LABELS } from "@/lib/forms/labels";

const REQUIRED_OPERANDS: Record<CalculationKind, { min: number; exact: boolean }> = {
  sum: { min: 1, exact: false },
  ratio: { min: 2, exact: true },
  product: { min: 2, exact: false },
  difference: { min: 2, exact: false },
};

/**
 * "Ile to jest": kind of arithmetic plus which fields feed it, picked from
 * dropdowns instead of typed as a formula (card T-26, decision D11's engine
 * needs this, T-30's cross-section version does not exist yet).
 *
 * A column's own calculation cannot sum, because summing needs many rows to
 * add up and a single row inside a table is only ever one of them; kind is
 * fixed to what the context allows instead of listing a choice nothing can
 * use.
 */
export function CalculationEditor({
  document,
  sectionKey,
  fieldKey,
  columnKey,
  value,
  onChange,
}: {
  document: FormDocument;
  sectionKey: string;
  fieldKey: string;
  columnKey?: string;
  value: FormCalculation;
  onChange: (value: FormCalculation) => void;
}) {
  const availableKinds = (Object.keys(CALCULATION_KIND_LABELS) as CalculationKind[]).filter(
    (kind) => columnKey === undefined || kind !== "sum",
  );

  const candidates = calculationOperandCandidates(
    document,
    sectionKey,
    fieldKey,
    columnKey,
    value.kind,
  );

  const requirement = REQUIRED_OPERANDS[value.kind];
  const hint = requirement.exact
    ? `wybierz dokładnie ${requirement.min}`
    : `wybierz co najmniej ${requirement.min}`;

  return (
    <div className="flex flex-col gap-2">
      <label className="flex items-center gap-2 text-sm">
        Sposób liczenia
        <select
          className="rounded-sm border border-border-control px-2 py-1"
          value={value.kind}
          onChange={(event) => {
            const kind = event.target.value as CalculationKind;
            onChange({ kind, operands: [] });
          }}
        >
          {availableKinds.map((kind) => (
            <option key={kind} value={kind}>
              {CALCULATION_KIND_LABELS[kind]}
            </option>
          ))}
        </select>
      </label>

      <fieldset className="flex flex-col gap-1">
        <legend className="text-sm">Składniki ({hint})</legend>
        {candidates.length === 0 ? (
          <p className="text-sm">
            Brak pól, z których można to policzyć. Dodaj najpierw potrzebne pola
            liczbowe.
          </p>
        ) : null}
        {candidates.map((candidate) => {
          const checked = value.operands.includes(candidate.key);
          const disabled =
            requirement.exact &&
            !checked &&
            value.operands.length >= requirement.min;

          return (
            <label key={candidate.key} className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={checked}
                disabled={disabled}
                onChange={(event) => {
                  const operands = event.target.checked
                    ? [...value.operands, candidate.key]
                    : value.operands.filter((k) => k !== candidate.key);
                  onChange({ ...value, operands });
                }}
              />
              {candidate.label}
            </label>
          );
        })}
      </fieldset>
    </div>
  );
}
