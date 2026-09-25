"use client";

import { useState } from "react";
import type { FormFieldType } from "@/lib/forms/document-types";
import { FIELD_TYPE_LABELS } from "@/lib/forms/labels";

/**
 * "Dodaj pole": pick a kind by its Polish name, type a label, done. No key,
 * no JSON, no mention of the wire type anywhere (card T-26).
 */
export function AddFieldControl({
  types,
  onAdd,
}: {
  types: readonly FormFieldType[];
  onAdd: (type: FormFieldType, label: string) => void;
}) {
  const [open, setOpen] = useState(false);
  const [type, setType] = useState<FormFieldType>(types[0]);
  const [label, setLabel] = useState("");

  if (!open) {
    return (
      <button type="button" className="self-start text-sm underline" onClick={() => setOpen(true)}>
        Dodaj pole
      </button>
    );
  }

  return (
    <form
      className="flex flex-wrap items-end gap-2 rounded-sm border border-dashed border-border p-3"
      onSubmit={(event) => {
        event.preventDefault();
        const trimmed = label.trim();
        if (trimmed.length === 0) {
          return;
        }
        onAdd(type, trimmed);
        setLabel("");
        setOpen(false);
      }}
    >
      <label className="flex flex-col gap-1 text-sm">
        Rodzaj pola
        <select
          className="rounded-sm border border-border-control px-2 py-1"
          value={type}
          onChange={(event) => setType(event.target.value as FormFieldType)}
        >
          {types.map((option) => (
            <option key={option} value={option}>
              {FIELD_TYPE_LABELS[option]}
            </option>
          ))}
        </select>
      </label>

      <label className="flex flex-1 flex-col gap-1 text-sm">
        Etykieta pola
        <input
          className="rounded-sm border border-border-control px-2 py-1"
          value={label}
          onChange={(event) => setLabel(event.target.value)}
          placeholder="Na przykład: Tytuł projektu"
          autoFocus
        />
      </label>

      <button type="submit" className="rounded-sm border border-brand-accent px-3 py-1.5 text-sm text-brand-accent-text hover:bg-brand-accent hover:text-bg">
        Dodaj
      </button>
      <button type="button" className="text-sm underline" onClick={() => setOpen(false)}>
        Anuluj
      </button>
    </form>
  );
}
