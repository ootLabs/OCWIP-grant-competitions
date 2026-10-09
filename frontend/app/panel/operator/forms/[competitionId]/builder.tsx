"use client";

import { useState } from "react";

import { ConfirmDialog } from "@/components/confirm-dialog";
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
  // O-02: one click threw the whole draft away with no way back, while the
  // publication next to it asks first. Undo cannot bring a discard back.
  const [confirmingDiscard, setConfirmingDiscard] = useState(false);

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
          <button type="button" className="underline" onClick={() => setConfirmingDiscard(true)}>
            Odrzuć szkic i zacznij od nowa
          </button>
        </span>
      </div>

      {confirmingDiscard ? (
        <ConfirmDialog
          title="Odrzucić cały szkic formularza? Wrócisz do wersji w mocy, a tej zmiany nie da się cofnąć."
          confirmLabel="Odrzuć szkic"
          busyLabel="Odrzucanie…"
          busy={false}
          error={null}
          onCancel={() => setConfirmingDiscard(false)}
          onConfirm={() => {
            setConfirmingDiscard(false);
            onDiscard();
          }}
        />
      ) : null}

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
