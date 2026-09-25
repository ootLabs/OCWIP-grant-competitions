"use client";

import type { FieldRole, FormDocument, FormField } from "@/lib/forms/document-types";
import { roleChoices } from "@/lib/forms/field-roles";
import { FIELD_ROLE_LABELS } from "@/lib/forms/labels";

/**
 * Which column of the operator's list of applications this field fills
 * (T-35). Shown only on a field some role fits; a role another field
 * already holds is listed but disabled with that field's label, so the
 * operator sees where to take it from instead of wondering why it is gone.
 */
export function RolePicker({
  document,
  field,
  onChange,
}: {
  document: FormDocument;
  field: FormField;
  onChange: (field: FormField) => void;
}) {
  const choices = roleChoices(document, field);

  if (choices.length === 0) {
    return null;
  }

  return (
    <label className="flex flex-col gap-1 text-sm">
      Pokazuj na liście wniosków jako
      <select
        className="rounded-sm border border-border-control px-2 py-1"
        value={field.role ?? ""}
        onChange={(event) => {
          const { role: _previous, ...rest } = field;
          onChange(
            event.target.value === ""
              ? rest
              : { ...rest, role: event.target.value as FieldRole },
          );
        }}
      >
        <option value="">Nie pokazuj</option>
        {choices.map(({ role, takenBy }) => (
          <option key={role} value={role} disabled={takenBy !== null}>
            {takenBy === null
              ? FIELD_ROLE_LABELS[role]
              : `${FIELD_ROLE_LABELS[role]} (ma już pole "${takenBy.label}")`}
          </option>
        ))}
      </select>
    </label>
  );
}
