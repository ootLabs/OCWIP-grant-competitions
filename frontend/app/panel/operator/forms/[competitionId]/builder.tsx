"use client";

import { addSection } from "@/lib/forms/document-edit";
import { newSection } from "@/lib/forms/document-factory";
import { allSectionKeys } from "@/lib/forms/document-keys";
import type { FormDocument } from "@/lib/forms/document-types";
import { AddSectionControl } from "./add-section-control";
import { SectionEditor } from "./section-editor";

/** The editing surface itself, once a document exists to edit (T-26, T-26a). */
export function Builder({
  document,
  savedAt,
  copiedFrom,
  canUndo,
  onChange,
  onUndo,
  onDiscard,
}: {
  document: FormDocument;
  savedAt: string | null;
  /** The competition this document was copied from, or null for an own form. */
  copiedFrom: string | null;
  canUndo: boolean;
  onChange: (document: FormDocument) => void;
  onUndo: () => void;
  onDiscard: () => void;
}) {
  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-2 rounded-sm border border-border-muted bg-surface-muted px-3 py-2 text-sm">
        <p>
          {savedAt === null
            ? "Szkic jeszcze nie zapisany."
            : `Szkic zapisany ${new Date(savedAt).toLocaleString("pl-PL")}. Zostaje po zamknięciu przeglądarki.`}{" "}
          {copiedFrom !== null ? "Ten formularz jest kopią innego konkursu." : null}
        </p>
        <span className="flex gap-3">
          <button type="button" className="underline disabled:no-underline disabled:opacity-40" disabled={!canUndo} onClick={onUndo}>
            Cofnij ostatnią zmianę
          </button>
          <button type="button" className="underline" onClick={onDiscard}>
            Odrzuć szkic i zacznij od nowa
          </button>
        </span>
      </div>

      {document.sections.map((section, index) => (
        <SectionEditor
          key={section.key}
          document={document}
          section={section}
          index={index}
          onChange={onChange}
        />
      ))}

      <AddSectionControl
        onAdd={(title) =>
          onChange(addSection(document, newSection(title, allSectionKeys(document))))
        }
      />
    </div>
  );
}
