"use client";

import { useMemo } from "react";
import {
  addFieldToSection,
  moveSection,
  removeSection,
  updateSection,
} from "@/lib/forms/document-edit";
import { newField } from "@/lib/forms/document-factory";
import { allFieldKeys } from "@/lib/forms/document-keys";
import { sectionMoveBlockers, sectionRemovalBlockers } from "@/lib/forms/section-guards";
import { ALL_FIELD_TYPES, type FormDocument, type FormSection } from "@/lib/forms/document-types";
import { VisibleWhenEditor } from "./visible-when-editor";
import { AddFieldControl } from "./add-field-control";
import { EditorGroup, inputClassName } from "./editor-group";

/** The open section's box, one at a time, so the focus can land on it. */
const sectionSettingsId = "kreator-ustawienia-sekcji";

/**
 * The open section, in the middle of the kreator: its title, its
 * description, its condition, a new field for it, and (T-26a) its own place
 * in the form. Its fields are opened from the outline. Moving and removing are guarded rather
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
  onFieldAdded,
  onRemoved,
}: {
  document: FormDocument;
  section: FormSection;
  index: number;
  onChange: (document: FormDocument) => void;
  /** Opens the new field, so the operator goes on with its settings. */
  onFieldAdded?: (fieldKey: string) => void;
  /** Opens the section that takes the removed one's place. */
  onRemoved?: (neighbourKey: string) => void;
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
    <section
      id={sectionSettingsId}
      tabIndex={-1}
      className="flex min-w-0 flex-col gap-4"
      aria-label={`Ustawienia sekcji: ${section.title}`}
    >
      <header className="flex flex-col gap-1">
        <p className="text-xs text-text-muted">Sekcja {index + 1} z {document.sections.length}</p>
        <h2 className="text-xl leading-tight [overflow-wrap:anywhere]">{section.title || "Sekcja bez tytułu"}</h2>
      </header>

      {isOnlySection ? (
        <p className="text-xs text-text-muted">
          To jedyna sekcja formularza. Formularz bez ani jednej sekcji nie istnieje,
          więc tej nie da się usunąć.
        </p>
      ) : null}

      {removalBlockers.length > 0 ? (
        <p className="text-xs text-text-muted">
          Odpowiedzi z tej sekcji czytają: {removalBlockers.join(", ")}. Zmień najpierw
          je, żeby móc usunąć sekcję.
        </p>
      ) : null}

      {reasons.length > 0 ? <p className="text-xs text-text-muted">{reasons.join(" ")}</p> : null}

      <EditorGroup title="Co widzi wnioskodawca">
      <label className="flex flex-col gap-1 text-sm">
        Tytuł sekcji
        <input
          className={inputClassName}
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
          className={`${inputClassName} min-h-20`}
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

      </EditorGroup>

      <EditorGroup title="Kiedy sekcja jest widoczna">
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
      </EditorGroup>

      <EditorGroup title="Nowe pole w tej sekcji">
      {section.fields.length === 0 ? (
        <p className="text-xs text-text-muted">
          Sekcja bez ani jednego pola nie ma treści i nie przejdzie publikacji.
          Dodaj pierwsze pole.
        </p>
      ) : null}

      <AddFieldControl
        types={ALL_FIELD_TYPES}
        onAdd={(type, label) => {
          const field = newField(type, label, allFieldKeys(document));
          onChange(addFieldToSection(document, section.key, field));
          onFieldAdded?.(field.key);
        }}
      />
      </EditorGroup>

      <footer className="flex flex-wrap items-center justify-end gap-3 border-t border-border-muted pt-4 text-sm">
        <span className="flex flex-wrap gap-2">
          <button
            type="button"
            className="rounded-sm border border-border-control px-3 py-1 disabled:opacity-40"
            disabled={index === 0 || upBlockers.length > 0}
            onClick={() => onChange(moveSection(document, section.key, "up"))}
            aria-label={`Przesuń sekcję w górę: ${section.title}`}
          >
            Wyżej
          </button>
          <button
            type="button"
            className="rounded-sm border border-border-control px-3 py-1 disabled:opacity-40"
            disabled={index === document.sections.length - 1 || downBlockers.length > 0}
            onClick={() => onChange(moveSection(document, section.key, "down"))}
            aria-label={`Przesuń sekcję w dół: ${section.title}`}
          >
            Niżej
          </button>
          <button
            type="button"
            className="rounded-sm border border-border-control px-3 py-1 text-brand-accent-text disabled:opacity-40"
            disabled={isOnlySection || removalBlockers.length > 0}
            onClick={() => {
              // O-01: the removed button took the focus with it to <body>, and
              // a keyboard user started again at the top of the page. The
              // section that takes this one's place opens with the focus.
              const neighbour = document.sections[index + 1] ?? document.sections[index - 1];
              onChange(removeSection(document, section.key));
              if (neighbour) {
                onRemoved?.(neighbour.key);
                setTimeout(() => window.document.getElementById(sectionSettingsId)?.focus(), 0);
              }
            }}
            aria-label={`Usuń sekcję: ${section.title}`}
          >
            Usuń sekcję
          </button>
        </span>
      </footer>
    </section>
  );
}

function moveReason(dependentLabel: string, sourceLabel: string): string {
  return `Po tym przesunięciu ${dependentLabel} czytałoby odpowiedź "${sourceLabel}" spod siebie, a warunek widoczności czyta wyłącznie odpowiedź wcześniejszą.`;
}
