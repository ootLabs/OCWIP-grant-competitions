"use client";

import { useMemo } from "react";
import { isSectionVisible } from "@/lib/forms/evaluate";
import { sectionStatus, type SectionStatus } from "@/lib/forms/validate";
import type { FormDocument } from "@/lib/forms/document-types";
import { useRenderer } from "./renderer-context";

const STATUS_LABELS: Record<SectionStatus, string> = {
  ready: "gotowa",
  inProgress: "w toku",
  hasErrors: "są błędy",
};

/**
 * "Wnioskodawca wie, gdzie jest" (proces.md rule 9): every section, its
 * status, which one is current, in one row. A hidden section (its own
 * visibleWhen not met) is left out entirely rather than shown disabled,
 * the same choice field-view.tsx makes for a single field.
 */
export function SectionNav({
  document,
  currentSectionKey,
  onSelect,
}: {
  document: FormDocument;
  currentSectionKey: string;
  onSelect: (sectionKey: string) => void;
}) {
  const { answers, competitionSettings } = useRenderer();

  // Recomputed only when the answers actually change, not on every render:
  // blurring a field (which only changes `touched`, read elsewhere) would
  // otherwise re-validate every section on the nav for nothing.
  const statuses = useMemo(
    () =>
      document.sections
        .filter((section) => isSectionVisible(section, answers))
        .map((section) => ({
          section,
          status: sectionStatus(document, answers, section, competitionSettings),
        })),
    [document, answers, competitionSettings],
  );

  return (
    <nav aria-label="Sekcje wniosku">
      <ol className="flex flex-wrap gap-2">
        {statuses.map(({ section, status }, index) => {
          const isCurrent = section.key === currentSectionKey;

          return (
            <li key={section.key}>
              <button
                type="button"
                aria-current={isCurrent ? "step" : undefined}
                onClick={() => onSelect(section.key)}
                className={`rounded-sm border px-3 py-1.5 text-sm ${
                  isCurrent ? "border-brand-accent" : "border-border"
                }`}
              >
                {index + 1}. {section.title}
                <span className="ml-2 text-xs">({STATUS_LABELS[status]})</span>
              </button>
            </li>
          );
        })}
      </ol>
    </nav>
  );
}
