"use client";

import { useState } from "react";

import type { ChangeKind } from "@/lib/forms/document-changes";
import type { FormDocument } from "@/lib/forms/document-types";
import { FIELD_TYPE_LABELS, fieldDisplayName } from "@/lib/forms/labels";

export type Selection =
  | { readonly kind: "section"; readonly sectionKey: string }
  | { readonly kind: "field"; readonly sectionKey: string; readonly fieldKey: string };

/**
 * The whole form at a glance, on the left of the kreator: every section and
 * every field, which one is open, and which ones changed against the form
 * this one started from. Clicking opens it in the inspector; dragging a
 * field moves it within its section (the inspector's own buttons do the same
 * from the keyboard).
 */
export function Outline({
  document,
  selection,
  changes,
  onSelect,
  onMoveField,
  onAddSection,
}: {
  document: FormDocument;
  selection: Selection | null;
  changes: ReadonlyMap<string, ChangeKind>;
  onSelect: (selection: Selection) => void;
  onMoveField: (sectionKey: string, fieldKey: string, toIndex: number) => void;
  onAddSection: () => void;
}) {
  const [dragging, setDragging] = useState<{ sectionKey: string; fieldKey: string } | null>(null);

  return (
    <nav aria-label="Spis formularza" className="flex flex-col gap-4">
      {document.sections.map((section, sectionIndex) => {
        const sectionOpen = selection?.kind === "section" && selection.sectionKey === section.key;
        return (
          <div key={section.key} className="flex flex-col gap-1">
            <button
              type="button"
              aria-current={sectionOpen ? "true" : undefined}
              onClick={() => onSelect({ kind: "section", sectionKey: section.key })}
              className={`flex items-baseline gap-2 rounded-sm px-2 py-1.5 text-left text-xs font-semibold uppercase tracking-wide text-text-muted hover:bg-surface-muted ${
                sectionOpen ? "bg-surface-warm text-text shadow-[inset_3px_0_0_var(--color-brand-accent)]" : ""
              }`}
            >
              <span className="min-w-0 flex-1 truncate">
                {sectionIndex + 1}. {section.title || "Sekcja bez tytułu"}
              </span>
              <span className="font-normal tabular-nums">{section.fields.length}</span>
            </button>

            <ol className="flex flex-col gap-0.5">
              {section.fields.map((field, fieldIndex) => {
                const open = selection?.kind === "field" && selection.fieldKey === field.key;
                const change = changes.get(field.key);
                return (
                  <li
                    key={field.key}
                    draggable
                    onDragStart={() => setDragging({ sectionKey: section.key, fieldKey: field.key })}
                    onDragEnd={() => setDragging(null)}
                    onDragOver={(event) => {
                      if (dragging?.sectionKey === section.key) {
                        event.preventDefault();
                      }
                    }}
                    onDrop={(event) => {
                      event.preventDefault();
                      if (dragging?.sectionKey === section.key && dragging.fieldKey !== field.key) {
                        onMoveField(section.key, dragging.fieldKey, fieldIndex);
                      }
                      setDragging(null);
                    }}
                    className={dragging?.fieldKey === field.key ? "opacity-50" : undefined}
                  >
                    <button
                      type="button"
                      aria-current={open ? "true" : undefined}
                      onClick={() => onSelect({ kind: "field", sectionKey: section.key, fieldKey: field.key })}
                      className={`grid w-full grid-cols-[0.9rem_minmax(0,1fr)_auto] items-center gap-2 rounded-sm px-2 py-1.5 text-left text-sm hover:bg-surface-muted ${
                        open ? "bg-surface-warm shadow-[inset_3px_0_0_var(--color-brand-accent)]" : ""
                      }`}
                    >
                      <span aria-hidden="true" className="cursor-grab select-none text-xs tracking-tighter text-text-muted">
                        ⋮⋮
                      </span>
                      <span className="truncate">
                        {/* Thirteen statements read "Oświadczenie" (P4-09): the start of the text tells them apart. */}
                        {fieldDisplayName(field) || "(bez etykiety)"}
                        <span className="sr-only">, {FIELD_TYPE_LABELS[field.type]}</span>
                      </span>
                      {change ? <ChangeDot kind={change} /> : <span />}
                    </button>
                  </li>
                );
              })}
            </ol>
          </div>
        );
      })}

      <button type="button" className="self-start px-2 text-sm text-brand-accent-text underline" onClick={onAddSection}>
        + Dodaj sekcję
      </button>
    </nav>
  );
}

function ChangeDot({ kind }: { kind: ChangeKind }) {
  const label = kind === "new" ? "nowe pole" : "zmienione";
  return (
    <span
      title={label}
      className={`size-2 rounded-full ${kind === "new" ? "bg-status-info-text" : "bg-status-attention-text"}`}
    >
      <span className="sr-only">{label}</span>
    </span>
  );
}
