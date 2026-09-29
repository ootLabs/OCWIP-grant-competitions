"use client";

import { useState } from "react";

/**
 * "Dodaj sekcję": a title, nothing else (T-26a). The key is derived from the
 * title the same way a field's key is derived from its label, so the operator
 * never meets one.
 */
export function AddSectionControl({ onAdd }: { onAdd: (title: string) => void }) {
  const [open, setOpen] = useState(false);
  const [title, setTitle] = useState("");

  if (!open) {
    return (
      <button
        type="button"
        className="self-start text-sm underline"
        onClick={() => setOpen(true)}
      >
        Dodaj sekcję
      </button>
    );
  }

  return (
    <form
      className="flex flex-wrap items-end gap-2 rounded-sm border border-dashed border-border p-3"
      onSubmit={(event) => {
        event.preventDefault();
        const trimmed = title.trim();
        if (trimmed.length === 0) {
          return;
        }
        onAdd(trimmed);
        setTitle("");
        setOpen(false);
      }}
    >
      <label className="flex flex-1 flex-col gap-1 text-sm">
        Tytuł nowej sekcji
        <input
          className="rounded-sm border border-border-control px-2 py-1"
          value={title}
          onChange={(event) => setTitle(event.target.value)}
          placeholder="Na przykład: Budżet projektu"
          autoFocus
        />
      </label>

      <button
        type="submit"
        className="rounded-sm border border-brand-accent px-3 py-1.5 text-sm text-brand-accent-text hover:bg-brand-accent hover:text-bg"
      >
        Dodaj
      </button>
      <button type="button" className="text-sm underline" onClick={() => setOpen(false)}>
        Anuluj
      </button>
    </form>
  );
}
