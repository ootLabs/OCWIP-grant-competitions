"use client";

import { useState } from "react";

import { apiErrorMessage } from "@/lib/api-client";
import { formatAmount } from "@/lib/format";
import { costRowLabel, reviewReportCosts, type Report, type ReportSettlement } from "@/lib/reports";

interface Draft {
  readonly refused: string;
  readonly reason: string;
}

/** "12,50" and "12.50" both mean twelve złoty fifty; an empty box means nothing refused. */
function amountOf(text: string): number {
  const trimmed = text.trim().replace(/\s/g, "").replace(",", ".");
  return trimmed === "" ? 0 : Number(trimmed);
}

/**
 * The operator's review of the budget costs of a submitted report (T-50b):
 * next to each row, how much of its grant spending is not accepted and why.
 * The whole review is saved at once; a row left at zero is accepted in full.
 */
export function CostReviewForm({
  report,
  settlement,
  onChange,
}: {
  report: Report;
  settlement: ReportSettlement;
  onChange: (report: Report) => void;
}) {
  const [drafts, setDrafts] = useState<Draft[]>(() =>
    settlement.rows.map((row) => ({
      refused: Number(row.refused) > 0 ? String(row.refused).replace(".", ",") : "",
      reason: row.reason ?? "",
    })),
  );
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);

  function edit(index: number, change: Partial<Draft>) {
    setSaved(false);
    setDrafts((current) => current.map((draft, i) => (i === index ? { ...draft, ...change } : draft)));
  }

  async function save() {
    setBusy(true);
    setError(null);
    try {
      const items = drafts
        // T-95: a report may split its budget into tables, so every item
        // names its table and its row within it.
        .map((draft, index) => {
          const { row, budget } = settlement.rows[index]!;
          return { row: Number(row), budget, refused: amountOf(draft.refused), reason: draft.reason };
        })
        .filter((item) => item.refused !== 0);
      if (items.some((item) => Number.isNaN(item.refused))) {
        setError("Kwota nieuznana musi być liczbą, na przykład 120,50.");
        return;
      }
      onChange(await reviewReportCosts(report.id, items));
      setSaved(true);
    } catch (failure) {
      setError(apiErrorMessage(failure, "Nie udało się zapisać oceny kosztów."));
    } finally {
      setBusy(false);
    }
  }

  if (settlement.rows.length === 0) {
    return <p className="text-sm">Budżet sprawozdania nie ma żadnej pozycji do oceny.</p>;
  }

  return (
    <section aria-labelledby="ocena-kosztow" className="flex flex-col gap-3 text-sm">
      <h2 id="ocena-kosztow" className="text-xl">
        Ocena kosztów
      </h2>
      <p>Wpisz kwotę nieuznaną i powód tylko przy pozycjach, których nie uznajesz w całości. Wnioskodawca przeczyta powód.</p>
      <table className="w-full border-collapse">
        <thead>
          <tr>
            <th scope="col" className="border-b border-border-control py-1 text-left">Pozycja budżetu</th>
            <th scope="col" className="border-b border-border-control py-1 text-right">Wydatek z dotacji</th>
            <th scope="col" className="border-b border-border-control py-1 pl-4 text-left">Kwota nieuznana</th>
            <th scope="col" className="border-b border-border-control py-1 pl-4 text-left">Powód</th>
          </tr>
        </thead>
        <tbody>
          {settlement.rows.map((row, index) => {
            const label = costRowLabel(report, row.budget, Number(row.row));
            const draft = drafts[index]!;
            return (
              <tr key={`${row.budget}-${String(row.row)}`}>
                <th scope="row" className="py-1 text-left font-normal">{label}</th>
                <td className="py-1 text-right">{formatAmount(row.spent)}</td>
                <td className="py-1 pl-4">
                  <input
                    type="text"
                    inputMode="decimal"
                    aria-label={`Kwota nieuznana: ${label}`}
                    className="w-28 rounded-sm border border-border-control px-2 py-1"
                    value={draft.refused}
                    onChange={(event) => edit(index, { refused: event.target.value })}
                  />
                </td>
                <td className="py-1 pl-4">
                  <input
                    type="text"
                    maxLength={1000}
                    aria-label={`Powód nieuznania: ${label}`}
                    className="w-full rounded-sm border border-border-control px-2 py-1"
                    value={draft.reason}
                    onChange={(event) => edit(index, { reason: event.target.value })}
                  />
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
      <div className="flex items-center gap-4">
        <button
          type="button"
          className="rounded-sm border border-border-control px-4 py-2 disabled:opacity-40"
          disabled={busy}
          onClick={() => void save()}
        >
          {busy ? "Zapisywanie…" : "Zapisz ocenę kosztów"}
        </button>
        {saved ? <p role="status">Ocena kosztów zapisana.</p> : null}
      </div>
      {error !== null ? <p role="alert">{error}</p> : null}
    </section>
  );
}
