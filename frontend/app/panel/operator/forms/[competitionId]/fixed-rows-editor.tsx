"use client";

import { useState } from "react";
import type { FormTableRow } from "@/lib/forms/document-types";
import { newTableRow } from "@/lib/forms/document-factory";

/**
 * The named rows of a table of fixed size (T-26a): the three members of an
 * informal group, the named lines of a cost table. Until this existed the
 * creator could add such a table and had no way to give it a single row, and
 * a table of fixed size without rows is refused on publication.
 *
 * Renaming keeps the row's key, because the key is the row's identity inside
 * the definition: a condition or a calculation written against it stays
 * pointed at the same row. It is NOT what an applicant's answer is filed
 * under, which is the position (lib/forms/answer-types.ts reads
 * `stored[index]`). Reordering and removing rows here are safe anyway,
 * because answers only ever exist against a PUBLISHED version and publishing
 * writes a new one (T-25): the applications already filled in keep the
 * version they were filled against, rows and all.
 */
export function FixedRowsEditor({
  rows,
  onAdd,
  onRename,
  onRemove,
  onMove,
}: {
  rows: readonly FormTableRow[];
  onAdd: (row: FormTableRow) => void;
  onRename: (rowKey: string, label: string) => void;
  onRemove: (rowKey: string) => void;
  onMove: (rowKey: string, direction: "up" | "down") => void;
}) {
  const [label, setLabel] = useState("");
  const isOnlyRow = rows.length === 1;

  return (
    <div className="flex flex-col gap-2">
      <span className="text-sm">Wiersze tabeli</span>

      {rows.length === 0 ? (
        <p className="text-xs">
          Tabela o stałej liczbie wierszy musi mieć co najmniej jeden wiersz.
          Dodaj pierwszy, inaczej formularz nie przejdzie publikacji.
        </p>
      ) : null}

      <ul className="flex flex-col gap-2">
        {rows.map((row, index) => (
          <li key={row.key} className="flex flex-wrap items-center gap-2">
            <input
              className="flex-1 rounded-sm border border-border-control px-2 py-1 text-sm"
              value={row.label}
              aria-label={`Nazwa wiersza ${index + 1}`}
              onChange={(event) => onRename(row.key, event.target.value)}
            />
            <button
              type="button"
              className="text-sm underline disabled:no-underline disabled:opacity-40"
              disabled={index === 0}
              onClick={() => onMove(row.key, "up")}
              aria-label={`Przesuń wiersz w górę: ${row.label}`}
            >
              Góra
            </button>
            <button
              type="button"
              className="text-sm underline disabled:no-underline disabled:opacity-40"
              disabled={index === rows.length - 1}
              onClick={() => onMove(row.key, "down")}
              aria-label={`Przesuń wiersz w dół: ${row.label}`}
            >
              Dół
            </button>
            <button
              type="button"
              className="text-sm underline disabled:no-underline disabled:opacity-40"
              disabled={isOnlyRow}
              onClick={() => onRemove(row.key)}
              aria-label={`Usuń wiersz: ${row.label}`}
            >
              Usuń
            </button>
          </li>
        ))}
      </ul>

      {isOnlyRow ? (
        <p className="text-xs">
          To jedyny wiersz. Tabela o stałej liczbie wierszy bez ani jednego wiersza
          nie przeszłaby publikacji.
        </p>
      ) : null}

      <div className="flex flex-wrap items-end gap-2">
        <label className="flex flex-1 flex-col gap-1 text-sm">
          Nazwa nowego wiersza
          <input
            className="rounded-sm border border-border-control px-2 py-1"
            value={label}
            onChange={(event) => setLabel(event.target.value)}
            placeholder="Na przykład: Pierwszy członek grupy"
          />
        </label>
        <button
          type="button"
          className="rounded-sm border border-brand-accent px-3 py-1.5 text-sm text-brand-accent-text hover:bg-brand-accent hover:text-bg"
          onClick={() => {
            const trimmed = label.trim();
            if (trimmed.length === 0) {
              return;
            }
            onAdd(newTableRow(trimmed, new Set(rows.map((row) => row.key))));
            setLabel("");
          }}
        >
          Dodaj wiersz
        </button>
      </div>
    </div>
  );
}
