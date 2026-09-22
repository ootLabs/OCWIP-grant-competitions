"use client";

import { createContext, useContext } from "react";
import type { FormAnswers, TableRowAnswers } from "@/lib/forms/answer-types";
import type { CompetitionLimitSettings } from "@/lib/forms/limits";
import type { FormDocument } from "@/lib/forms/document-types";

/**
 * Everything a field deep inside the renderer needs, in one place instead of
 * threaded prop by prop through section-view -> field-view -> table-field ->
 * one cell (T-28). Every field component only ever needs its own `field` (and
 * for a column, its row context) plus this.
 */
export interface RendererContextValue {
  readonly document: FormDocument;
  readonly answers: FormAnswers;
  readonly competitionSettings: CompetitionLimitSettings;
  /** Field keys (or "table[row].column" for a cell) whose error may show. */
  readonly touched: ReadonlySet<string>;
  readonly onAnswer: (fieldKey: string, value: FormAnswers[string]) => void;
  readonly onTouch: (key: string) => void;
  readonly onAnswerCell: (
    tableKey: string,
    rowIndex: number,
    columnKey: string,
    value: TableRowAnswers[string],
  ) => void;
  readonly onAddRow: (tableKey: string) => void;
  readonly onRemoveRow: (tableKey: string, rowIndex: number) => void;
  readonly onMoveRow: (tableKey: string, rowIndex: number, direction: "up" | "down") => void;
}

const RendererContext = createContext<RendererContextValue | null>(null);

export function RendererProvider({
  value,
  children,
}: {
  value: RendererContextValue;
  children: React.ReactNode;
}) {
  return <RendererContext.Provider value={value}>{children}</RendererContext.Provider>;
}

export function useRenderer(): RendererContextValue {
  const value = useContext(RendererContext);
  if (value === null) {
    throw new Error("useRenderer used outside a RendererProvider.");
  }
  return value;
}

/** The key touched-tracking and cell answers use for one table cell. */
export function cellKey(tableKey: string, rowIndex: number, columnKey: string): string {
  return `${tableKey}[${rowIndex}].${columnKey}`;
}

const CELL_KEY_PATTERN = /^([a-z][a-z0-9_]*)\[(\d+)\]\.([a-z][a-z0-9_]*)$/;

/**
 * The reverse of `cellKey`. A field key can never contain `[`, `]` or `.`
 * (document-keys.ts's `isValidKey`), so this parse is unambiguous.
 */
export function parseCellKey(
  key: string,
): { tableKey: string; rowIndex: number; columnKey: string } | null {
  const match = CELL_KEY_PATTERN.exec(key);
  if (match === null) {
    return null;
  }
  return { tableKey: match[1], rowIndex: Number(match[2]), columnKey: match[3] };
}

/**
 * Touched-state follow-up for removing a table row: that row's own cells
 * stop being tracked, and every later row's cells shift down one index, the
 * same way the row data itself shifts (T-28's touched-by-index gap).
 */
export function touchedAfterRowRemoved(
  touched: ReadonlySet<string>,
  tableKey: string,
  removedIndex: number,
): ReadonlySet<string> {
  const next = new Set<string>();

  for (const key of touched) {
    const parsed = parseCellKey(key);
    if (parsed === null || parsed.tableKey !== tableKey) {
      next.add(key);
      continue;
    }
    if (parsed.rowIndex === removedIndex) {
      continue;
    }
    const rowIndex = parsed.rowIndex > removedIndex ? parsed.rowIndex - 1 : parsed.rowIndex;
    next.add(cellKey(tableKey, rowIndex, parsed.columnKey));
  }

  return next;
}

/** Touched-state follow-up for swapping two rows: their cells' touched state swaps with them. */
export function touchedAfterRowsSwapped(
  touched: ReadonlySet<string>,
  tableKey: string,
  indexA: number,
  indexB: number,
): ReadonlySet<string> {
  const next = new Set<string>();

  for (const key of touched) {
    const parsed = parseCellKey(key);
    if (parsed === null || parsed.tableKey !== tableKey) {
      next.add(key);
      continue;
    }
    const rowIndex =
      parsed.rowIndex === indexA ? indexB : parsed.rowIndex === indexB ? indexA : parsed.rowIndex;
    next.add(cellKey(tableKey, rowIndex, parsed.columnKey));
  }

  return next;
}
