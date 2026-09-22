"use client";

import { useState } from "react";
import { EmptyState } from "@/components/empty-state";
import type { CompetitionSummary } from "@/lib/forms/competition-forms";

/**
 * Where a competition without a form of its own gets a starting point (T-26).
 * The creator never opens on an empty document: the very first form a
 * competition on this system ever gets is set up once, outside the creator,
 * and every one after that is a copy an operator adjusts. See docs/log.md
 * for why (T-26 narrowing) and docs/runbook/kolejka.md for T-26a, which picks
 * building one up from nothing back up.
 */
export function SourcePicker({
  sources,
  onCopy,
}: {
  sources: readonly CompetitionSummary[];
  onCopy: (competitionId: string) => void;
}) {
  const [selected, setSelected] = useState(sources[0]?.id ?? "");

  if (sources.length === 0) {
    return (
      <EmptyState title="Żaden konkurs nie ma jeszcze formularza do skopiowania">
        Pierwszy formularz w systemie zakłada zespół wdrożeniowy razem z OCWIP,
        poza kreatorem. Gdy powstanie, każdy kolejny konkurs zacznie od jego
        kopii.
      </EmptyState>
    );
  }

  return (
    <form
      className="flex flex-wrap items-end gap-3"
      onSubmit={(event) => {
        event.preventDefault();
        if (selected !== "") {
          onCopy(selected);
        }
      }}
    >
      <label className="flex flex-col gap-1 text-sm">
        Skopiuj formularz z konkursu
        <select
          className="rounded-sm border border-border px-2 py-1"
          value={selected}
          onChange={(event) => setSelected(event.target.value)}
        >
          {sources.map((competition) => (
            <option key={competition.id} value={competition.id}>
              {competition.number} - {competition.title}
            </option>
          ))}
        </select>
      </label>
      <button
        type="submit"
        className="rounded-sm border border-brand-accent px-4 py-2 text-sm text-brand-accent-text hover:bg-brand-accent hover:text-bg"
      >
        Kopiuj formularz
      </button>
    </form>
  );
}
