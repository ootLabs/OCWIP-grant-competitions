"use client";

import type { FormDocument } from "@/lib/forms/document-types";
import { SectionEditor } from "./section-editor";

/**
 * The editing surface itself, once a document exists to edit (T-26). Section
 * add/remove/reorder deliberately is not here: see section-editor.tsx.
 */
export function Builder({
  document,
  savedAt,
  canUndo,
  onChange,
  onUndo,
  onDiscard,
}: {
  document: FormDocument;
  savedAt: string | null;
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
            : `Szkic zapisany ${new Date(savedAt).toLocaleString("pl-PL")}. Zostaje po zamknięciu przeglądarki.`}
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

      {document.sections.map((section) => (
        <SectionEditor
          key={section.key}
          document={document}
          section={section}
          onChange={(updated) => {
            onChange({
              ...document,
              sections: document.sections.map((s) => (s.key === section.key ? updated : s)),
            });
          }}
        />
      ))}
    </div>
  );
}
