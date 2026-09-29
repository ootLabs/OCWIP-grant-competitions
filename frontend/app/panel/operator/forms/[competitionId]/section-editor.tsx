"use client";

import { useMemo } from "react";
import {
  addFieldToSection,
  moveField,
  moveSection,
  removeField,
  removeSection,
  updateField,
  updateSection,
} from "@/lib/forms/document-edit";
import { newField } from "@/lib/forms/document-factory";
import { allFieldKeys } from "@/lib/forms/document-keys";
import { sectionMoveBlockers, sectionRemovalBlockers } from "@/lib/forms/section-guards";
import { ALL_FIELD_TYPES, type FormDocument, type FormSection } from "@/lib/forms/document-types";
import { VisibleWhenEditor } from "./visible-when-editor";
import { AddFieldControl } from "./add-field-control";
import { FieldRow } from "./field-row";

/**
 * One section: its title, its description, its condition, its fields, and
 * (T-26a) its own place in the form. Moving and removing are guarded rather
 * than free, because both can break a visibility condition somewhere else in
 * the document; the button says what stands in the way instead of letting the
 * operator find out at publication, in a message written as a JSON path.
 *
 * Everything here speaks in whole documents. A section-level edit would have
 * to be lifted back into the document by the caller anyway, and a move is not
 * expressible as "this section changed" at all.
 */
export function SectionEditor({
  document,
  section,
  index,
  onChange,
}: {
  document: FormDocument;
  section: FormSection;
  index: number;
  onChange: (document: FormDocument) => void;
}) {
  // Each of these walks the whole document, and every section on screen runs
  // its own: without memoizing, typing one letter in a title re-scans the
  // form once per section per keystroke.
  const upBlockers = useMemo(
    () => (index === 0 ? [] : sectionMoveBlockers(document, section.key, "up")),
    [document, section.key, index],
  );
  const downBlockers = useMemo(
    () =>
      index === document.sections.length - 1
        ? []
        : sectionMoveBlockers(document, section.key, "down"),
    [document, section.key, index],
  );
  const removalBlockers = useMemo(
    () => sectionRemovalBlockers(document, section),
    [document, section],
  );

  const isOnlySection = document.sections.length === 1;
  const reasons = [
    ...upBlockers.map((blocker) => moveReason(blocker.dependentLabel, blocker.sourceLabel)),
    ...downBlockers.map((blocker) => moveReason(blocker.dependentLabel, blocker.sourceLabel)),
  ];

  return (
    <section className="flex flex-col gap-3 rounded-sm border border-border p-4">
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-sm">Sekcja {index + 1} z {document.sections.length}</span>
        <span className="ml-auto flex gap-2">
          <button
            type="button"
            className="text-sm underline disabled:no-underline disabled:opacity-40"
            disabled={index === 0 || upBlockers.length > 0}
            onClick={() => onChange(moveSection(document, section.key, "up"))}
            aria-label={`Przesuń sekcję w górę: ${section.title}`}
          >
            Góra
          </button>
          <button
            type="button"
            className="text-sm underline disabled:no-underline disabled:opacity-40"
            disabled={index === document.sections.length - 1 || downBlockers.length > 0}
            onClick={() => onChange(moveSection(document, section.key, "down"))}
            aria-label={`Przesuń sekcję w dół: ${section.title}`}
          >
            Dół
          </button>
          <button
            type="button"
            className="text-sm underline disabled:no-underline disabled:opacity-40"
            disabled={isOnlySection || removalBlockers.length > 0}
            onClick={() => onChange(removeSection(document, section.key))}
            aria-label={`Usuń sekcję: ${section.title}`}
          >
            Usuń sekcję
          </button>
        </span>
      </div>

      {isOnlySection ? (
        <p className="text-xs">
          To jedyna sekcja formularza. Formularz bez ani jednej sekcji nie istnieje,
          więc tej nie da się usunąć.
        </p>
      ) : null}

      {removalBlockers.length > 0 ? (
        <p className="text-xs">
          Odpowiedzi z tej sekcji czytają: {removalBlockers.join(", ")}. Zmień najpierw
          je, żeby móc usunąć sekcję.
        </p>
      ) : null}

      {reasons.length > 0 ? <p className="text-xs">{reasons.join(" ")}</p> : null}

      <label className="flex flex-col gap-1 text-sm">
        Tytuł sekcji
        <input
          className="rounded-sm border border-border-control px-2 py-1 text-base"
          value={section.title}
          onChange={(event) =>
            onChange(
              updateSection(document, section.key, (current) => ({
                ...current,
                title: event.target.value,
              })),
            )
          }
        />
      </label>

      <label className="flex flex-col gap-1 text-sm">
        Opis sekcji (opcjonalny)
        <textarea
          className="rounded-sm border border-border-control px-2 py-1"
          value={section.description}
          onChange={(event) =>
            onChange(
              updateSection(document, section.key, (current) => ({
                ...current,
                description: event.target.value,
              })),
            )
          }
        />
      </label>

      <div className="flex flex-col gap-1 border-y border-border-muted py-3">
        <span className="text-sm">Warunek widoczności sekcji</span>
        <VisibleWhenEditor
          document={document}
          sectionKey={section.key}
          value={section.visibleWhen}
          onChange={(visibleWhen) =>
            onChange(
              updateSection(document, section.key, (current) => ({
                ...current,
                visibleWhen,
              })),
            )
          }
        />
      </div>

      <ul className="flex flex-col gap-2">
        {section.fields.map((field, fieldIndex) => {
          const path = { sectionKey: section.key, fieldKey: field.key };

          return (
            <FieldRow
              key={field.key}
              document={document}
              sectionKey={section.key}
              fieldKey={field.key}
              field={field}
              canMoveUp={fieldIndex > 0}
              canMoveDown={fieldIndex < section.fields.length - 1}
              onChange={(updated) => onChange(updateField(document, path, () => updated))}
              onMove={(direction) => onChange(moveField(document, path, direction))}
              onRemove={() => onChange(removeField(document, path))}
            />
          );
        })}
      </ul>

      {section.fields.length === 0 ? (
        <p className="text-xs">
          Sekcja bez ani jednego pola nie ma treści i nie przejdzie publikacji.
          Dodaj pierwsze pole.
        </p>
      ) : null}

      <AddFieldControl
        types={ALL_FIELD_TYPES}
        onAdd={(type, label) => {
          const field = newField(type, label, allFieldKeys(document));
          onChange(addFieldToSection(document, section.key, field));
        }}
      />
    </section>
  );
}

function moveReason(dependentLabel: string, sourceLabel: string): string {
  return `Po tym przesunięciu ${dependentLabel} czytałoby odpowiedź "${sourceLabel}" spod siebie, a warunek widoczności czyta wyłącznie odpowiedź wcześniejszą.`;
}
