"use client";

import { useMemo, useState } from "react";

import { changesLabel, documentChanges, type ChangeKind } from "@/lib/forms/document-changes";
import { addSection, moveField, removeField, updateField } from "@/lib/forms/document-edit";
import { newSection } from "@/lib/forms/document-factory";
import { allSectionKeys } from "@/lib/forms/document-keys";
import type { FormDocument } from "@/lib/forms/document-types";

import { AddSectionControl } from "./add-section-control";
import { ChangesList } from "./changes-list";
import { FieldInspector } from "./field-inspector";
import { LivePreview } from "./live-preview";
import { Outline, type Selection } from "./outline";
import { SectionEditor } from "./section-editor";

/**
 * The editing surface (T-26, T-26a), laid out for the way OCWIP is expected
 * to work: copy last year's form and correct it (report, "Formularz
 * wniosku"). The outline on the left shows the whole form and what changed;
 * the middle edits the one field or section that is open; the right shows it
 * the way the applicant will, on every keystroke.
 */
export function Builder({
  competitionId,
  document,
  baseline,
  baselineLabel,
  savedAt,
  canUndo,
  onChange,
  onUndo,
  onDiscard,
}: {
  competitionId: string;
  document: FormDocument;
  /** What this document is compared with: the copied form or the published version. */
  baseline: FormDocument | null;
  /** How to name that baseline on the bar: "kopia konkursu 1/2026", "opublikowana wersja". */
  baselineLabel: string | null;
  savedAt: string | null;
  canUndo: boolean;
  onChange: (document: FormDocument) => void;
  onUndo: () => void;
  onDiscard: () => void;
}) {
  const [picked, setPicked] = useState<Selection | "new-section" | null>(null);
  const [showChanges, setShowChanges] = useState(false);

  const changes = useMemo(() => documentChanges(baseline, document), [baseline, document]);
  const changeKinds = useMemo(
    () => new Map<string, ChangeKind>(changes.filter((c) => c.kind !== "removed").map((c) => [c.fieldKey, c.kind])),
    [changes],
  );

  const selection = resolve(document, picked);
  const section =
    selection !== "new-section" && selection !== null
      ? document.sections.find((candidate) => candidate.key === selection.sectionKey) ?? null
      : null;
  const sectionIndex = section ? document.sections.indexOf(section) : -1;
  const fieldIndex =
    section && selection !== "new-section" && selection?.kind === "field"
      ? section.fields.findIndex((candidate) => candidate.key === selection.fieldKey)
      : -1;
  const field = section && fieldIndex >= 0 ? section.fields[fieldIndex] : null;

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-x-5 gap-y-2 rounded-md border border-border-muted bg-surface-muted px-4 py-2.5 text-sm">
        <span>
          {savedAt === null
            ? "Szkic jeszcze nie zapisany."
            : `Szkic zapisany ${new Date(savedAt).toLocaleTimeString("pl-PL", { hour: "2-digit", minute: "2-digit" })}. Zostaje po zamknięciu przeglądarki.`}
        </span>
        {baselineLabel !== null ? (
          <button
            type="button"
            aria-expanded={showChanges}
            onClick={() => setShowChanges((value) => !value)}
            className={`rounded-full px-3 py-0.5 text-xs font-medium ${
              changes.length > 0 ? "bg-status-attention-bg text-status-attention-text" : "bg-status-neutral-bg text-status-neutral-text"
            }`}
          >
            {changes.length === 0 ? `Bez zmian względem: ${baselineLabel}` : `${changesLabel(changes.length)} względem: ${baselineLabel}`}
          </button>
        ) : null}
        <span className="ml-auto flex flex-wrap gap-4">
          <button type="button" className="underline disabled:no-underline disabled:opacity-40" disabled={!canUndo} onClick={onUndo}>
            Cofnij ostatnią zmianę
          </button>
          <button type="button" className="underline" onClick={onDiscard}>
            Odrzuć szkic i zacznij od nowa
          </button>
        </span>
      </div>

      {showChanges && baselineLabel !== null ? (
        <ChangesList
          changes={changes}
          baselineLabel={baselineLabel}
          onOpen={(sectionKey, fieldKey) => setPicked({ kind: "field", sectionKey, fieldKey })}
        />
      ) : null}

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-[15rem_minmax(0,1fr)] xl:grid-cols-[16rem_minmax(0,1fr)_minmax(0,24rem)]">
        <aside className="lg:sticky lg:top-4 lg:max-h-[calc(100vh-2rem)] lg:self-start lg:overflow-y-auto lg:overflow-x-hidden">
          <Outline
            document={document}
            selection={selection === "new-section" ? null : selection}
            changes={changeKinds}
            onSelect={setPicked}
            onAddSection={() => setPicked("new-section")}
            onMoveField={(sectionKey, fieldKey, toIndex) => onChange(moveTo(document, sectionKey, fieldKey, toIndex))}
          />
        </aside>

        <div className="min-w-0">
          {selection === "new-section" ? (
            <div className="flex flex-col gap-3">
              <h2 className="text-xl">Nowa sekcja</h2>
              <AddSectionControl
                onAdd={(title) => {
                  const created = newSection(title, allSectionKeys(document));
                  onChange(addSection(document, created));
                  setPicked({ kind: "section", sectionKey: created.key });
                }}
              />
            </div>
          ) : null}

          {section && field ? (
            <FieldInspector
              key={field.key}
              document={document}
              sectionKey={section.key}
              sectionTitle={section.title}
              field={field}
              index={fieldIndex}
              count={section.fields.length}
              change={changes.find((change) => change.fieldKey === field.key)}
              onChange={(updated) => onChange(updateField(document, { sectionKey: section.key, fieldKey: field.key }, () => updated))}
              onMove={(direction) => onChange(moveField(document, { sectionKey: section.key, fieldKey: field.key }, direction))}
              onRemove={() => {
                onChange(removeField(document, { sectionKey: section.key, fieldKey: field.key }));
                setPicked({ kind: "section", sectionKey: section.key });
              }}
            />
          ) : null}

          {section && !field ? (
            <SectionEditor
              document={document}
              section={section}
              index={sectionIndex}
              onChange={onChange}
              onFieldAdded={(fieldKey) => setPicked({ kind: "field", sectionKey: section.key, fieldKey })}
            />
          ) : null}
        </div>

        <aside
          data-preview-scroll
          className="min-w-0 lg:col-span-2 xl:col-span-1 xl:sticky xl:top-4 xl:max-h-[calc(100vh-2rem)] xl:self-start xl:overflow-y-auto"
        >
          <LivePreview
            competitionId={competitionId}
            document={document}
            sectionKey={section?.key ?? null}
            fieldKey={field?.key ?? null}
            onSectionChange={(sectionKey) => {
              if (sectionKey !== section?.key) {
                setPicked({ kind: "section", sectionKey });
              }
            }}
          />
        </aside>
      </div>
    </div>
  );
}

/**
 * What is open, checked against the document as it is now: a field that was
 * deleted, or undone out of existence, falls back to its section, and an
 * empty choice opens the first field of the form.
 */
function resolve(document: FormDocument, picked: Selection | "new-section" | null): Selection | "new-section" | null {
  if (picked === "new-section") {
    return picked;
  }

  const section = picked ? document.sections.find((candidate) => candidate.key === picked.sectionKey) : undefined;
  if (picked && section) {
    if (picked.kind === "section" || section.fields.some((candidate) => candidate.key === picked.fieldKey)) {
      return picked;
    }
    return { kind: "section", sectionKey: section.key };
  }

  const first = document.sections[0];
  if (!first) {
    return null;
  }
  return first.fields[0]
    ? { kind: "field", sectionKey: first.key, fieldKey: first.fields[0].key }
    : { kind: "section", sectionKey: first.key };
}

/** A drop in the outline: one step at a time through moveField, so every move is the same move as the buttons. */
function moveTo(document: FormDocument, sectionKey: string, fieldKey: string, toIndex: number): FormDocument {
  const from = document.sections.find((s) => s.key === sectionKey)?.fields.findIndex((f) => f.key === fieldKey) ?? -1;
  if (from < 0 || from === toIndex) {
    return document;
  }
  let next = document;
  const direction = toIndex > from ? "down" : "up";
  for (let step = 0; step < Math.abs(toIndex - from); step++) {
    next = moveField(next, { sectionKey, fieldKey }, direction);
  }
  return next;
}
