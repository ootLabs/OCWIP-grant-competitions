"use client";

import { useId, useState } from "react";

import type { ReviewerSummary } from "@/lib/operator-evaluation";

/** The experts of one application in the ranking list, with adding and taking off one (T-41). */
export function AssignedExperts({
  applicationId,
  number,
  assigned,
  names,
  reviewers,
  onAssign,
  onUnassign,
}: {
  applicationId: string;
  number: string;
  assigned: readonly string[];
  names: ReadonlyMap<string, string>;
  reviewers: readonly ReviewerSummary[];
  onAssign: (applicationId: string, reviewerId: string) => Promise<void>;
  onUnassign: (applicationId: string, reviewerId: string) => Promise<void>;
}) {
  const [choice, setChoice] = useState("");
  const [busy, setBusy] = useState(false);
  const selectId = useId();
  const free = reviewers.filter((reviewer) => !assigned.includes(reviewer.id));

  async function run(action: () => Promise<void>) {
    setBusy(true);
    try {
      await action();
      setChoice("");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="flex flex-col gap-1">
      <ul className="flex flex-col gap-1">
        {assigned.map((reviewerId) => (
          <li key={reviewerId} className="flex items-center gap-2">
            <span>{names.get(reviewerId) ?? "ekspert"}</span>
            <button
              type="button"
              className="text-xs underline"
              disabled={busy}
              onClick={() =>
                void run(() => onUnassign(applicationId, reviewerId))
              }
            >
              Cofnij
              <span className="sr-only">
                {" "}
                przypisanie eksperta {names.get(reviewerId)} do wniosku {number}
              </span>
            </button>
          </li>
        ))}
      </ul>
      {free.length > 0 ? (
        <div className="flex items-center gap-1">
          <label htmlFor={selectId} className="sr-only">
            Ekspert do przypisania do wniosku {number}
          </label>
          <select
            id={selectId}
            className="rounded-sm border border-border-control px-1 py-0.5 text-xs"
            value={choice}
            onChange={(event) => setChoice(event.target.value)}
          >
            <option value="">wybierz eksperta</option>
            {free.map((reviewer) => (
              <option key={reviewer.id} value={reviewer.id}>
                {reviewer.name || reviewer.email}
              </option>
            ))}
          </select>
          <button
            type="button"
            className="text-xs underline disabled:opacity-40"
            disabled={busy || choice === ""}
            onClick={() => void run(() => onAssign(applicationId, choice))}
          >
            Przypisz
          </button>
        </div>
      ) : null}
    </div>
  );
}
