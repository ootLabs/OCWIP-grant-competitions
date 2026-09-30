"use client";

import { useState } from "react";
import { EmptyState } from "@/components/empty-state";
import type { CompetitionSummary } from "@/lib/forms/competition-forms";

/**
 * Where a competition without a form of its own gets a starting point (T-26,
 * T-26a): a copy of a competition that already has one, or nothing at all.
 *
 * Copying stays the first offer even though building from scratch now works.
 * The report reads OCWIP's real way of working as "last year's form with
 * corrections", and a blank page is the harder of the two starting points for
 * the person this creator is for.
 */
export function SourcePicker({
  sources,
  onCopy,
  onStartBlank,
}: {
  sources: readonly CompetitionSummary[];
  onCopy: (competitionId: string) => void;
  onStartBlank: () => void;
}) {
  const [selected, setSelected] = useState(sources[0]?.id ?? "");

  if (sources.length === 0) {
    return (
      <div className="flex flex-col items-center gap-3">
        <EmptyState title="Żaden konkurs nie ma jeszcze formularza do skopiowania">
          To pierwszy formularz w tym systemie, więc nie ma jeszcze czego skopiować.
          Zbuduj go od zera: zaczniesz od jednej pustej sekcji.
        </EmptyState>
        <button
          type="button"
          className="rounded-sm border border-brand-accent px-4 py-2 text-sm text-brand-accent-text hover:bg-brand-accent hover:text-bg"
          onClick={onStartBlank}
        >
          Zacznij od zera
        </button>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-3">
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
            className="rounded-sm border border-border-control px-2 py-1"
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

      <p className="text-sm">
        Albo zbuduj formularz od zera, zaczynając od jednej pustej sekcji:{" "}
        <button type="button" className="underline" onClick={onStartBlank}>
          Zacznij od zera
        </button>
      </p>
    </div>
  );
}
