"use client";

import { useEffect, useRef, useState } from "react";

import { FormRenderer } from "@/components/form-renderer/form-renderer";
import { fetchCompetitionLimitSettings } from "@/lib/forms/competition-forms";
import type { FormDocument } from "@/lib/forms/document-types";
import { fieldAnchorId } from "@/lib/forms/field-anchor";
import type { CompetitionLimitSettings } from "@/lib/forms/limits";

/**
 * "Tak zobaczy to wnioskodawca", beside the settings: the same FormRenderer
 * the applicant gets (T-28), on the draft in hand, opened on the section
 * being edited, with the field being edited marked. A change of a label or a
 * limit shows here on the next keystroke, which is what the separate preview
 * tab could not do: it had to be switched to, and the field found again.
 *
 * Nothing typed here is kept; the renderer has nowhere to save to.
 */
export function LivePreview({
  competitionId,
  document,
  sectionKey,
  fieldKey,
  onSectionChange,
}: {
  competitionId: string;
  document: FormDocument;
  sectionKey: string | null;
  fieldKey: string | null;
  /** The preview's own section steps open that section in the editor too. */
  onSectionChange: (sectionKey: string) => void;
}) {
  const [settings, setSettings] = useState<CompetitionLimitSettings | null>(null);
  const [failed, setFailed] = useState(false);
  const frame = useRef<HTMLDivElement>(null);

  useEffect(() => {
    let current = true;
    fetchCompetitionLimitSettings(competitionId)
      .then((found) => current && setSettings(found))
      .catch(() => current && setFailed(true));
    return () => {
      current = false;
    };
  }, [competitionId]);

  // The mark is a style on the rendered field, set from outside the
  // renderer, so the renderer the applicant uses carries no kreator code.
  useEffect(() => {
    const root = frame.current;
    if (root === null || fieldKey === null) {
      return;
    }
    // By id within the frame: the field keys are the contract's own, and
    // getElementById needs no escaping of them.
    const element = root.ownerDocument.getElementById(fieldAnchorId(fieldKey));
    if (element === null || !root.contains(element)) {
      return;
    }
    const target = element;
    target.style.outline = "2px solid var(--color-brand-accent)";
    target.style.outlineOffset = "6px";
    target.style.borderRadius = "4px";
    // Scroll the preview column only, never the page: the operator is
    // typing in the middle column and must not be moved away from it.
    const column = root.closest<HTMLElement>("[data-preview-scroll]");
    if (column !== null && column.scrollHeight > column.clientHeight) {
      const offset = target.getBoundingClientRect().top - column.getBoundingClientRect().top;
      if (offset < 0 || offset > column.clientHeight - 80) {
        column.scrollTop += offset - 80;
      }
    }
    return () => {
      target.style.outline = "";
      target.style.outlineOffset = "";
      target.style.borderRadius = "";
    };
  });

  return (
    <section aria-label="Podgląd dla wnioskodawcy" className="flex min-w-0 flex-col gap-3">
      <p className="text-xs text-text-muted">Tak zobaczy to wnioskodawca. Nic tu wpisane nie jest zapisywane.</p>
      {failed ? <p className="text-sm">Nie udało się wczytać ustawień konkursu dla podglądu.</p> : null}
      {settings === null && !failed ? <p className="text-sm">Wczytywanie podglądu…</p> : null}
      {settings !== null ? (
        <div
          ref={frame}
          className="rounded-md border border-border-muted bg-bg p-4 [&_h1]:text-2xl [&_h2]:text-xl [&_h3]:text-lg"
        >
          <FormRenderer
            document={document}
            competitionSettings={settings}
            activeSectionKey={sectionKey ?? undefined}
            onActiveSectionChange={onSectionChange}
          />
        </div>
      ) : null}
    </section>
  );
}
