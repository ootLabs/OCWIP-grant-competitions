"use client";

import {
  conditionValueOptions,
  visibilityCandidates,
  type FieldCandidate,
} from "@/lib/forms/document-candidates";
import type { FormDocument, VisibleWhen } from "@/lib/forms/document-types";

/**
 * "Widoczne, gdy pole X ma wartość Y" as two dropdowns, never an expression
 * (card T-26). Shared by a section's own condition and a field's condition:
 * `fieldKey` omitted means the section itself is the target.
 */
export function VisibleWhenEditor({
  document,
  sectionKey,
  fieldKey,
  value,
  onChange,
}: {
  document: FormDocument;
  sectionKey: string;
  fieldKey?: string;
  value: VisibleWhen | null | undefined;
  onChange: (value: VisibleWhen | null) => void;
}) {
  const candidates = visibilityCandidates(document, sectionKey, fieldKey);
  const sourceField = findSourceField(document, value?.field);
  const valueOptions = sourceField ? conditionValueOptions(sourceField) : [];

  if (candidates.length === 0 && value == null) {
    return (
      <p className="text-sm">
        Warunek widoczności będzie dostępny, gdy w formularzu pojawi się
        wcześniejsze pole z odpowiedzią tak/nie albo wyborem.
      </p>
    );
  }

  return (
    <div className="flex flex-col gap-2 sm:flex-row sm:items-center">
      <label className="flex flex-col gap-1 text-sm sm:flex-row sm:items-center sm:gap-2">
        Widoczne zawsze, chyba że:
        <select
          className="rounded-sm border border-border-control px-2 py-1"
          value={value?.field ?? ""}
          onChange={(event) => {
            const field = event.target.value;
            if (field === "") {
              onChange(null);
              return;
            }
            onChange({ field, equalsAnyOf: [] });
          }}
        >
          <option value="">(zawsze widoczne)</option>
          {candidates.map((candidate) => (
            <option key={candidate.key} value={candidate.key}>
              {candidate.label}
            </option>
          ))}
        </select>
      </label>

      {value != null ? (
        <ValueMultiSelect
          options={valueOptions}
          selected={value.equalsAnyOf}
          onChange={(equalsAnyOf) => onChange({ field: value.field, equalsAnyOf })}
        />
      ) : null}
    </div>
  );
}

function ValueMultiSelect({
  options,
  selected,
  onChange,
}: {
  options: FieldCandidate[];
  selected: readonly string[];
  onChange: (values: string[]) => void;
}) {
  return (
    <fieldset className="flex flex-wrap gap-3">
      <legend className="sr-only">ma jedną z wartości</legend>
      {options.map((option) => (
        <label key={option.key} className="flex items-center gap-1 text-sm">
          <input
            type="checkbox"
            checked={selected.includes(option.key)}
            onChange={(event) => {
              onChange(
                event.target.checked
                  ? [...selected, option.key]
                  : selected.filter((v) => v !== option.key),
              );
            }}
          />
          {option.label}
        </label>
      ))}
    </fieldset>
  );
}

function findSourceField(document: FormDocument, key: string | undefined) {
  if (key === undefined) {
    return null;
  }
  for (const section of document.sections) {
    const field = section.fields.find((f) => f.key === key);
    if (field) {
      return field;
    }
  }
  return null;
}
