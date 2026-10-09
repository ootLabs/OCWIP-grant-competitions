"use client";

import { useMemo } from "react";

import type { FieldChange } from "@/lib/forms/document-changes";
import { findReferencesTo } from "@/lib/forms/document-references";
import { fieldMoveBlockers } from "@/lib/forms/section-guards";
import type { FormDocument, FormField } from "@/lib/forms/document-types";
import { FIELD_TYPE_LABELS, fieldDisplayName } from "@/lib/forms/labels";

import { FieldEditor } from "./field-editor";

const chip = "inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium";

/**
 * The open field, in the middle of the kreator: what it is, what changed in
 * it, what depends on it, and every setting. Moving and deleting live here
 * rather than as three links on every row of the list (T-26), and a delete
 * another field depends on is refused with the list of those fields, each
 * named once with how many times it repeats.
 */
export function FieldInspector({
  document,
  sectionKey,
  sectionTitle,
  field,
  index,
  count,
  change,
  onChange,
  onMove,
  onRemove,
}: {
  document: FormDocument;
  sectionKey: string;
  sectionTitle: string;
  field: FormField;
  index: number;
  count: number;
  change: FieldChange | undefined;
  onChange: (field: FormField) => void;
  onMove: (direction: "up" | "down") => void;
  onRemove: () => void;
}) {
  const references = useMemo(() => findReferencesTo(document, field.key), [document, field.key]);
  const dependents = useMemo(() => {
    const counts = new Map<string, number>();
    for (const reference of references) {
      counts.set(reference.fieldLabel, (counts.get(reference.fieldLabel) ?? 0) + 1);
    }
    return [...counts];
  }, [references]);

  // R-42: a move that would leave a condition reading the answer below it
  // is refused here, with the reason, as a section move already is.
  const upBlockers = useMemo(
    () => (index === 0 ? [] : fieldMoveBlockers(document, sectionKey, field.key, "up")),
    [document, sectionKey, field.key, index],
  );
  const downBlockers = useMemo(
    () => (index === count - 1 ? [] : fieldMoveBlockers(document, sectionKey, field.key, "down")),
    [document, sectionKey, field.key, index, count],
  );
  const moveReasons = [...upBlockers, ...downBlockers].map(
    (blocker) =>
      `Po tym przesunięciu ${blocker.dependentLabel} czytałoby odpowiedź "${blocker.sourceLabel}" spod siebie, a warunek widoczności czyta wyłącznie odpowiedź wcześniejszą.`,
  );

  return (
    <article className="flex min-w-0 flex-col gap-4" aria-label={`Ustawienia pola: ${fieldDisplayName(field)}`}>
      <header className="flex flex-col gap-2">
        <p className="text-xs text-text-muted">{sectionTitle}</p>
        <div className="flex flex-wrap items-center gap-2">
          <h2 className="min-w-0 text-xl leading-tight [overflow-wrap:anywhere]">{fieldDisplayName(field) || "(bez etykiety)"}</h2>
          <span className={`${chip} bg-status-neutral-bg text-status-neutral-text`}>{FIELD_TYPE_LABELS[field.type]}</span>
          {change?.kind === "new" ? (
            <span className={`${chip} bg-status-info-bg text-status-info-text`}>nowe pole</span>
          ) : null}
          {change?.kind === "changed" ? (
            <span className={`${chip} bg-status-attention-bg text-status-attention-text`}>
              zmienione: {change.what.join(", ")}
            </span>
          ) : null}
        </div>
        {change?.wasLabel ? (
          <p className="text-xs text-text-muted">
            Wcześniej: <s>{change.wasLabel}</s>
          </p>
        ) : null}
      </header>

      {dependents.length > 0 ? (
        <details className="rounded-md border border-border-muted bg-surface-muted px-4 py-3 text-sm">
          <summary className="cursor-pointer">
            Od tego pola zależy {references.length} {references.length === 1 ? "inne pole" : "innych pól"}
          </summary>
          <ul className="mt-2 list-disc pl-5 text-text-muted">
            {dependents.map(([label, times]) => (
              <li key={label}>
                {label}
                {times > 1 ? ` (${times} pola)` : ""}
              </li>
            ))}
          </ul>
        </details>
      ) : null}

      <FieldEditor
        document={document}
        sectionKey={sectionKey}
        fieldKey={field.key}
        field={field}
        onChange={onChange}
      />

      {moveReasons.length > 0 ? (
        <ul className="list-disc pl-5 text-xs">
          {moveReasons.map((reason) => (
            <li key={reason}>{reason}</li>
          ))}
        </ul>
      ) : null}

      <footer className="flex flex-wrap items-center justify-between gap-3 border-t border-border-muted pt-4 text-sm">
        <span className="text-text-muted">
          Pole {index + 1} z {count} w sekcji. Kolejność zmienisz też, przeciągając pole w spisie.
        </span>
        <span className="flex flex-wrap gap-2">
          <button
            type="button"
            className="rounded-sm border border-border-control px-3 py-1 disabled:opacity-40"
            disabled={index === 0 || upBlockers.length > 0}
            onClick={() => onMove("up")}
            aria-label={`Przesuń w górę: ${field.label}`}
          >
            Wyżej
          </button>
          <button
            type="button"
            className="rounded-sm border border-border-control px-3 py-1 disabled:opacity-40"
            disabled={index === count - 1 || downBlockers.length > 0}
            onClick={() => onMove("down")}
            aria-label={`Przesuń w dół: ${field.label}`}
          >
            Niżej
          </button>
          <button
            type="button"
            className="rounded-sm border border-border-control px-3 py-1 text-brand-accent-text disabled:opacity-40"
            disabled={references.length > 0}
            onClick={onRemove}
            aria-label={`Usuń: ${field.label}`}
            title={references.length > 0 ? "Najpierw zmień pola, które od niego zależą" : undefined}
          >
            Usuń pole
          </button>
        </span>
      </footer>
      {references.length > 0 ? (
        <p className="text-xs text-text-muted">Usunięcie zablokowane, dopóki inne pola od niego zależą.</p>
      ) : null}
    </article>
  );
}
