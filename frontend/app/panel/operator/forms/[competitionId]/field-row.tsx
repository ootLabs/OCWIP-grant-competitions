"use client";

import { useState } from "react";
import { findReferencesTo } from "@/lib/forms/document-references";
import type { FormDocument, FormField } from "@/lib/forms/document-types";
import { FIELD_TYPE_LABELS } from "@/lib/forms/labels";
import { FieldEditor } from "./field-editor";

/**
 * One field, collapsed to a summary row until an operator asks to edit it.
 * Also the row for a table column, told apart only by `columnKey` being set
 * on the caller's path (card T-26). Deleting or moving a field it never lets
 * happen quietly when something else in the document depends on it: the
 * button explains why instead of breaking the reference.
 */
export function FieldRow({
  document,
  sectionKey,
  fieldKey,
  columnKey,
  field,
  canMoveUp,
  canMoveDown,
  onChange,
  onMove,
  onRemove,
}: {
  document: FormDocument;
  sectionKey: string;
  fieldKey: string;
  columnKey?: string;
  field: FormField;
  canMoveUp: boolean;
  canMoveDown: boolean;
  onChange: (field: FormField) => void;
  onMove: (direction: "up" | "down") => void;
  onRemove: () => void;
}) {
  const [expanded, setExpanded] = useState(false);

  const references = findReferencesTo(document, columnKey ?? fieldKey);

  return (
    <li className="rounded-sm border border-border-muted">
      <div className="flex flex-wrap items-center gap-3 px-3 py-2">
        <button
          type="button"
          className="text-left text-sm underline"
          onClick={() => setExpanded((value) => !value)}
          aria-expanded={expanded}
        >
          {field.label || "(bez etykiety)"}
        </button>
        <span className="text-xs">{FIELD_TYPE_LABELS[field.type]}</span>
        {field.required ? <span className="text-xs">wymagane</span> : null}
        {!field.printed ? <span className="text-xs">nie na wydruku</span> : null}

        <span className="ml-auto flex gap-2">
          <button
            type="button"
            className="text-sm underline disabled:no-underline disabled:opacity-40"
            disabled={!canMoveUp}
            onClick={() => onMove("up")}
            aria-label={`Przesuń w górę: ${field.label}`}
          >
            Góra
          </button>
          <button
            type="button"
            className="text-sm underline disabled:no-underline disabled:opacity-40"
            disabled={!canMoveDown}
            onClick={() => onMove("down")}
            aria-label={`Przesuń w dół: ${field.label}`}
          >
            Dół
          </button>
          <button
            type="button"
            className="text-sm underline"
            onClick={() => {
              if (references.length > 0) {
                return;
              }
              onRemove();
            }}
            disabled={references.length > 0}
            title={
              references.length > 0
                ? `Pole jest używane przez: ${references.map((r) => r.fieldLabel).join(", ")}`
                : undefined
            }
          >
            Usuń
          </button>
        </span>
      </div>

      {references.length > 0 ? (
        <p className="px-3 pb-2 text-xs">
          Tego pola używają: {references.map((r) => r.fieldLabel).join(", ")}. Zmień
          najpierw je, żeby móc usunąć to pole.
        </p>
      ) : null}

      {expanded ? (
        <div className="border-t border-border-muted px-3 py-3">
          <FieldEditor
            document={document}
            sectionKey={sectionKey}
            fieldKey={fieldKey}
            columnKey={columnKey}
            field={field}
            onChange={onChange}
          />
        </div>
      ) : null}
    </li>
  );
}
