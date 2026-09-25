"use client";

import { useId, useState } from "react";

import { formatAmount } from "@/lib/format";
import type { RankingRow } from "@/lib/operator-evaluation";

/** "5 000,50", "5000.50" and "5000,5" all mean the same amount; empty is no grant. */
export function parseAmount(text: string): number | null | "invalid" {
  const cleaned = text.replace(/\s/g, "").replace(",", ".");
  if (cleaned === "") return null;
  if (!/^\d+(\.\d{1,2})?$/.test(cleaned)) return "invalid";
  const value = Number(cleaned);
  return value > 0 ? value : "invalid";
}

const toText = (value: number | string | null | undefined) =>
  value === null || value === undefined ? "" : String(value).replace(".", ",");

/**
 * The two cells of the operator's decision on a ranking row (T-42): the
 * awarded amount, which is what funds the application (report), and a note.
 * Saved when the field is left; read only once the results are approved.
 */
export function DecisionCells({
  row,
  locked,
  onSave,
}: {
  row: RankingRow;
  locked: boolean;
  onSave: (applicationId: string, awardedGrant: number | null, note: string | null) => Promise<unknown>;
}) {
  const [amount, setAmount] = useState(toText(row.awardedGrant));
  const [note, setNote] = useState(row.decisionNote ?? "");
  const [error, setError] = useState<string | null>(null);
  const ids = { amount: useId(), note: useId(), error: useId() };
  const cell = "border-b border-border-muted px-2 py-1";

  if (locked) {
    return (
      <>
        <td className={`${cell} text-right`}>
          {row.awardedGrant === null || row.awardedGrant === undefined ? "" : formatAmount(row.awardedGrant)}
        </td>
        <td className={cell}>{row.decisionNote ?? ""}</td>
      </>
    );
  }

  function save() {
    const parsed = parseAmount(amount);
    if (parsed === "invalid") {
      setError("Wpisz kwotę większą od zera, najwyżej z groszami, albo zostaw pole puste.");
      return;
    }
    setError(null);
    const trimmed = note.trim() === "" ? null : note.trim();
    const unchanged =
      parsed === (row.awardedGrant === null || row.awardedGrant === undefined ? null : Number(row.awardedGrant)) &&
      trimmed === (row.decisionNote ?? null);
    if (!unchanged) void onSave(row.applicationId, parsed, trimmed);
  }

  return (
    <>
      <td className={cell}>
        <label htmlFor={ids.amount} className="sr-only">
          Kwota przyznana dla wniosku {row.number ?? ""}
        </label>
        <input
          id={ids.amount}
          inputMode="decimal"
          className="w-28 rounded-sm border border-border-control px-1 py-0.5 text-right"
          value={amount}
          aria-invalid={error !== null}
          aria-describedby={error !== null ? ids.error : undefined}
          onChange={(event) => setAmount(event.target.value)}
          onBlur={save}
        />
        {error !== null ? (
          <p id={ids.error} className="text-xs text-brand-accent-text">
            {error}
          </p>
        ) : null}
      </td>
      <td className={cell}>
        <label htmlFor={ids.note} className="sr-only">
          Uwagi do decyzji dla wniosku {row.number ?? ""}
        </label>
        <input
          id={ids.note}
          maxLength={2000}
          className="w-40 rounded-sm border border-border-control px-1 py-0.5"
          value={note}
          onChange={(event) => setNote(event.target.value)}
          onBlur={save}
        />
      </td>
    </>
  );
}
