"use client";

import {
  addFieldToSection,
  moveField,
  removeField,
  updateField,
} from "@/lib/forms/document-edit";
import { newField } from "@/lib/forms/document-factory";
import { allFieldKeys } from "@/lib/forms/document-keys";
import { ALL_FIELD_TYPES, type FormDocument, type FormSection } from "@/lib/forms/document-types";
import { VisibleWhenEditor } from "./visible-when-editor";
import { AddFieldControl } from "./add-field-control";
import { FieldRow } from "./field-row";

/**
 * One section of the form: its own title and description are editable, its
 * fields are addable and removable, but the section itself cannot be
 * renamed into existence or reordered against its neighbours here. That is
 * the card's own narrowing (docs/log.md, T-26): sections come from the
 * competition being copied from, and building the section list itself is
 * T-26a.
 */
export function SectionEditor({
  document,
  section,
  onChange,
}: {
  document: FormDocument;
  section: FormSection;
  onChange: (section: FormSection) => void;
}) {
  return (
    <section className="flex flex-col gap-3 rounded-sm border border-border p-4">
      <label className="flex flex-col gap-1 text-sm">
        Tytuł sekcji
        <input
          className="rounded-sm border border-border px-2 py-1 text-base"
          value={section.title}
          onChange={(event) => onChange({ ...section, title: event.target.value })}
        />
      </label>

      <label className="flex flex-col gap-1 text-sm">
        Opis sekcji (opcjonalny)
        <textarea
          className="rounded-sm border border-border px-2 py-1"
          value={section.description}
          onChange={(event) => onChange({ ...section, description: event.target.value })}
        />
      </label>

      <div className="flex flex-col gap-1 border-y border-border-muted py-3">
        <span className="text-sm">Warunek widoczności sekcji</span>
        <VisibleWhenEditor
          document={document}
          sectionKey={section.key}
          value={section.visibleWhen}
          onChange={(visibleWhen) => onChange({ ...section, visibleWhen })}
        />
      </div>

      <ul className="flex flex-col gap-2">
        {section.fields.map((field, index) => {
          const path = { sectionKey: section.key, fieldKey: field.key };

          return (
            <FieldRow
              key={field.key}
              document={document}
              sectionKey={section.key}
              fieldKey={field.key}
              field={field}
              canMoveUp={index > 0}
              canMoveDown={index < section.fields.length - 1}
              onChange={(updated) =>
                onChange(sectionOf(updateField(document, path, () => updated), section.key))
              }
              onMove={(direction) =>
                onChange(sectionOf(moveField(document, path, direction), section.key))
              }
              onRemove={() =>
                onChange(sectionOf(removeField(document, path), section.key))
              }
            />
          );
        })}
      </ul>

      <AddFieldControl
        types={ALL_FIELD_TYPES}
        onAdd={(type, label) => {
          const taken = allFieldKeys(document);
          const field = newField(type, label, taken);
          onChange(sectionOf(addFieldToSection(document, section.key, field), section.key));
        }}
      />
    </section>
  );
}

/** Pulls one section back out of a whole-document edit, to hand to `onChange`. */
function sectionOf(document: FormDocument, sectionKey: string): FormSection {
  return document.sections.find((s) => s.key === sectionKey)!;
}
