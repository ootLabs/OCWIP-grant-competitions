"use client";

import { useId, useState } from "react";

import type { ReviewerSummary } from "@/lib/operator-evaluation";

/**
 * One expert onto every selected application at once (T-41). With about 120
 * applications a click per row is not a way of working, so the table offers
 * a selection and this bar acts on it.
 */
export function GroupAssign({
  selectedCount,
  reviewers,
  onAssign,
}: {
  selectedCount: number;
  reviewers: readonly ReviewerSummary[];
  onAssign: (reviewerId: string) => Promise<unknown>;
}) {
  const [choice, setChoice] = useState("");
  const [busy, setBusy] = useState(false);
  const selectId = useId();

  async function assign() {
    setBusy(true);
    try {
      await onAssign(choice);
      setChoice("");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="flex flex-wrap items-center gap-2 text-sm">
      <span>Zaznaczone wnioski: {selectedCount}</span>
      <label htmlFor={selectId}>Przypisz zaznaczone ekspertowi</label>
      <select
        id={selectId}
        className="rounded-sm border border-border-control px-2 py-1"
        value={choice}
        onChange={(event) => setChoice(event.target.value)}
      >
        <option value="">wybierz eksperta</option>
        {reviewers.map((reviewer) => (
          <option key={reviewer.id} value={reviewer.id}>
            {reviewer.name || reviewer.email}
          </option>
        ))}
      </select>
      <button
        type="button"
        className="rounded-sm bg-brand-accent px-3 py-1 text-bg hover:bg-brand-accent-hover disabled:opacity-40"
        disabled={busy || choice === "" || selectedCount === 0}
        onClick={() => void assign()}
      >
        Przypisz
      </button>
    </div>
  );
}
