"use client";

import { useCallback, useMemo, useState } from "react";
import { allTopLevelFields, isFieldVisible, isSectionVisible } from "@/lib/forms/evaluate";
import { resolveTableRows, type FormAnswers, type TableRowAnswers } from "@/lib/forms/answer-types";
import { reorder } from "@/lib/forms/document-edit";
import type { CompetitionLimitSettings } from "@/lib/forms/limits";
import { TABLE_TYPES, type FormDocument, type FormSection } from "@/lib/forms/document-types";
import {
  RendererProvider,
  cellKey,
  touchedAfterRowRemoved,
  touchedAfterRowsSwapped,
} from "./renderer-context";
import { SectionNav } from "./section-nav";
import { SectionView } from "./section-view";

/**
 * The whole point of T-24 having a document instead of hand written pages:
 * this component turns any FormDocument into a working, paginated,
 * validating form (card T-28). Used as is by T-27's preview and, with real
 * persistence wrapped around it, by T-34's applicant flow: neither builds
 * its own copy of a form.
 */
export function FormRenderer({
  document,
  initialAnswers,
  competitionSettings,
  onChange,
  activeSectionKey,
  onActiveSectionChange,
}: {
  document: FormDocument;
  initialAnswers?: FormAnswers;
  competitionSettings: CompetitionLimitSettings;
  onChange?: (answers: FormAnswers) => void;
  /**
   * Lets a caller outside this component jump to a specific section, for
   * example T-34's list of what is missing before submission. Uncontrolled
   * (the renderer keeps its own current section) when omitted, which is
   * every caller before T-34: T-27's preview and a bare FormRenderer have no
   * outside reason to move the section themselves.
   */
  activeSectionKey?: string;
  onActiveSectionChange?: (sectionKey: string) => void;
}) {
  const [answers, setAnswers] = useState<FormAnswers>(initialAnswers ?? {});
  const [touched, setTouched] = useState<ReadonlySet<string>>(new Set());
  const visibleSections = useMemo(
    () => document.sections.filter((section) => isSectionVisible(section, answers)),
    [document, answers],
  );
  const [internalSectionKey, setInternalSectionKey] = useState(
    visibleSections[0]?.key ?? document.sections[0]?.key,
  );
  const currentSectionKey = activeSectionKey ?? internalSectionKey;
  const setCurrentSectionKey = onActiveSectionChange ?? setInternalSectionKey;

  const update = useCallback(
    (next: FormAnswers) => {
      setAnswers(next);
      onChange?.(next);
    },
    [onChange],
  );

  const onAnswer = useCallback(
    (fieldKey: string, value: FormAnswers[string]) => {
      update({ ...answers, [fieldKey]: value });
    },
    [answers, update],
  );

  const onTouch = useCallback((key: string) => {
    setTouched((previous) => new Set(previous).add(key));
  }, []);

  const tableField = useCallback(
    (tableKey: string) => allTopLevelFields(document).find((f) => f.key === tableKey)!,
    [document],
  );

  const onAnswerCell = useCallback(
    (tableKey: string, rowIndex: number, columnKey: string, value: TableRowAnswers[string]) => {
      // Through resolveTableRows, not `[...(answers[tableKey] ?? [])]`: a
      // fixedTable's rows do not exist in `answers` until a cell is edited,
      // and editing row 2 before row 0 would otherwise leave row 0 as a hole
      // that later iteration (touchWholeSection) silently skips.
      const rows = [...resolveTableRows(tableField(tableKey), answers)];
      rows[rowIndex] = { ...rows[rowIndex], [columnKey]: value };
      update({ ...answers, [tableKey]: rows });
    },
    [answers, tableField, update],
  );

  const onAddRow = useCallback(
    (tableKey: string) => {
      const rows = resolveTableRows(tableField(tableKey), answers);
      update({ ...answers, [tableKey]: [...rows, {}] });
    },
    [answers, tableField, update],
  );

  const onRemoveRow = useCallback(
    (tableKey: string, rowIndex: number) => {
      const rows = resolveTableRows(tableField(tableKey), answers);
      update({ ...answers, [tableKey]: rows.filter((_, index) => index !== rowIndex) });
      // The removed row's own touched cells stop being tracked, and every
      // later row's cells shift down one index along with its data:
      // otherwise a genuinely-invalid, already-flagged cell can silently
      // stop showing its error once the rows around it move.
      setTouched((previous) => touchedAfterRowRemoved(previous, tableKey, rowIndex));
    },
    [answers, tableField, update],
  );

  const onMoveRow = useCallback(
    (tableKey: string, rowIndex: number, direction: "up" | "down") => {
      const rows = resolveTableRows(tableField(tableKey), answers);
      const offset = direction === "up" ? -1 : 1;
      const target = rowIndex + offset;
      if (target < 0 || target >= rows.length) {
        return;
      }
      const targetRow = rows[rowIndex];
      update({
        ...answers,
        [tableKey]: reorder(rows, (row) => row === targetRow, offset),
      });
      setTouched((previous) => touchedAfterRowsSwapped(previous, tableKey, rowIndex, target));
    },
    [answers, tableField, update],
  );

  const currentSection =
    visibleSections.find((section) => section.key === currentSectionKey) ?? visibleSections[0];

  const goToSection = useCallback(
    (sectionKey: string) => {
      if (currentSection) {
        setTouched((previous) => touchWholeSection(previous, currentSection, answers));
      }
      setCurrentSectionKey(sectionKey);
    },
    [answers, currentSection],
  );

  if (currentSection === undefined) {
    return null;
  }

  const currentIndex = visibleSections.findIndex((section) => section.key === currentSection.key);

  return (
    <RendererProvider
      value={{
        document,
        answers,
        competitionSettings,
        touched,
        onAnswer,
        onTouch,
        onAnswerCell,
        onAddRow,
        onRemoveRow,
        onMoveRow,
      }}
    >
      <div className="flex flex-col gap-6">
        <SectionNav document={document} currentSectionKey={currentSection.key} onSelect={goToSection} />

        <h2 className="text-xl">{currentSection.title}</h2>
        {/* The asterisk is hidden from screen readers (field-view.tsx says
            "wymagane" to them in words), so it has to be explained to the
            eye in words too, once per section that uses it (T-46). */}
        {currentSection.fields.some((field) => field.required) ? (
          <p className="text-sm">Pola oznaczone gwiazdką (*) są wymagane.</p>
        ) : null}
        <SectionView section={currentSection} />

        <div className="flex justify-between">
          <button
            type="button"
            className="text-sm underline disabled:no-underline disabled:opacity-40"
            disabled={currentIndex <= 0}
            onClick={() => goToSection(visibleSections[currentIndex - 1].key)}
          >
            Wstecz
          </button>
          <button
            type="button"
            className="text-sm underline disabled:no-underline disabled:opacity-40"
            disabled={currentIndex >= visibleSections.length - 1}
            onClick={() => goToSection(visibleSections[currentIndex + 1].key)}
          >
            Dalej
          </button>
        </div>
      </div>
    </RendererProvider>
  );
}

/** Marks every currently visible field (and table cell) of a section touched, on the way out of it. */
function touchWholeSection(
  previous: ReadonlySet<string>,
  section: FormSection,
  answers: FormAnswers,
): ReadonlySet<string> {
  const next = new Set(previous);

  for (const field of section.fields) {
    if (!isFieldVisible(field, answers)) {
      continue;
    }

    if (TABLE_TYPES.has(field.type) && field.table) {
      // resolveTableRows, not `answers[field.key]` directly: a fixedTable
      // whose cells have never been touched has no rows in `answers` at
      // all, and reading it raw would leave every required cell in every
      // row untouched and silently error free forever.
      const rows = resolveTableRows(field, answers);
      rows.forEach((_, rowIndex) => {
        field.table!.columns.forEach((column) => {
          next.add(cellKey(field.key, rowIndex, column.key));
        });
      });
      next.add(field.key);
      continue;
    }

    next.add(field.key);
  }

  return next;
}
