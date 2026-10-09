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
  const { answers, competitionSettings, applicant } = useRenderer();

  // Recomputed only when the answers actually change, not on every render:
  // blurring a field (which only changes `touched`, read elsewhere) would
  // otherwise re-validate every section on the nav for nothing.
  const statuses = useMemo(
    () =>
      document.sections
        .filter((section) => isSectionVisible(section, answers))
        .map((section) => ({
          section,
          status: sectionStatus(document, answers, section, competitionSettings, applicant),
        })),
    [document, answers, competitionSettings],
  );

  return (
    <nav aria-label="Sekcje wniosku">
      <ol className="flex flex-wrap gap-2">
        {statuses.map(({ section, status }, index) => {
          const isCurrent = section.key === currentSectionKey;
          // A correction locks every section it does not return (O-11): they
          // read "gotowa" like the one to fix. Grey and named instead, the
          // "reszta szara" the scenario promises.
          const locked = section.fields.length > 0 && section.fields.every((field) => field.readOnly);

          return (
            <li key={section.key}>
              <button
                type="button"
                aria-current={isCurrent ? "step" : undefined}
                onClick={() => onSelect(section.key)}
                className={`flex min-h-11 items-center gap-2 rounded-pill border py-1.5 pl-1.5 pr-4 text-left text-sm ${
                  isCurrent
                    ? "border-active-border bg-surface-warm font-semibold"
                    : "border-border hover:border-border-control"
                } ${locked ? "text-text-muted" : ""}`}
              >
                {/* The number in a circle, filled once the section is ready:
                    the status is also spelled out after the title, so the
                    circle is only a second, faster way to see it. */}
                <span
                  aria-hidden="true"
                  className={`grid size-7 shrink-0 place-items-center rounded-full border-2 text-xs font-semibold ${
                    locked ? "border-border text-text-muted" : MARKS[status]
                  }`}
                >
                  {locked ? index + 1 : status === "ready" ? "✓" : status === "hasErrors" ? "!" : index + 1}
                </span>
                <span>
                  {index + 1}. {section.title}
                  <span className="ml-2 text-xs font-normal text-text-muted">
                    ({locked ? "zablokowana" : STATUS_LABELS[status]})
                  </span>
                </span>
              </button>
            </li>
          );
        })}
      </ol>
    </nav>
  );
}

const MARKS: Record<SectionStatus, string> = {
  ready: "border-text bg-text text-bg",
  inProgress: "border-border-control text-text",
  hasErrors: "border-status-negative-text bg-status-negative-bg text-status-negative-text",
};
