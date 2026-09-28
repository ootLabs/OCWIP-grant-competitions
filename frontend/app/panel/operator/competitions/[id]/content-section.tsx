"use client";

import { useCallback, useEffect, useState } from "react";

import { apiErrorMessage } from "@/lib/api-client";
import { contentParts, copyPart, fetchPartInForce, type ContentPart } from "@/lib/competition-content";
import { fetchOperatorCompetitions, type OperatorCompetition } from "@/lib/operator-competitions";

type Versions = Record<ContentPart, number | null>;

/**
 * "Karty oceny i wzór sprawozdania" on the competition page (T-96): which
 * version of each is in force, and copying the ones in force from another
 * competition, the usual way to start next year's call from this year's.
 * A new competition on a fresh system gets them from the import command
 * (docs/wdrozenie.md) instead.
 */
export function ContentSection({
  competition,
  onCopied,
}: {
  competition: OperatorCompetition;
  onCopied: () => void;
}) {
  const [versions, setVersions] = useState<Versions | null>(null);
  const [others, setOthers] = useState<OperatorCompetition[]>([]);
  const [source, setSource] = useState("");
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  const load = useCallback(async () => {
    const entries = await Promise.all(
      contentParts.map(async ({ part }) => [part, await fetchPartInForce(competition.id, part)] as const),
    );
    setVersions(Object.fromEntries(entries) as Versions);
  }, [competition.id]);

  useEffect(() => {
    load().catch(() => setVersions(null));
    fetchOperatorCompetitions()
      .then((list) => setOthers(list.filter((other) => other.id !== competition.id)))
      .catch(() => setOthers([]));
  }, [competition.id, load]);

  async function copy() {
    setBusy(true);
    setMessage(null);
    try {
      const copied: string[] = [];
      for (const { part, label } of contentParts) {
        if (await copyPart(source, competition.id, part)) {
          copied.push(label.toLowerCase());
        }
      }
      setMessage(copied.length > 0 ? `Skopiowano: ${copied.join(", ")}.` : "Wybrany konkurs nie ma kart ani wzoru sprawozdania.");
      await load();
      onCopied();
    } catch (error) {
      setMessage(apiErrorMessage(error, "Nie udało się skopiować. Spróbuj ponownie."));
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="flex flex-col gap-2">
      <h2 className="text-xl">Karty oceny i wzór sprawozdania</h2>
      {versions === null ? (
        <p className="text-sm">Wczytywanie…</p>
      ) : (
        <ul className="text-sm">
          {contentParts.map(({ part, label }) => (
            <li key={part}>
              {label}: {versions[part] === null ? "brak" : `wersja ${versions[part]}`}
            </li>
          ))}
        </ul>
      )}

      {competition.isActive && others.length > 0 ? (
        <div className="flex flex-col gap-2 text-sm sm:flex-row sm:items-end">
          <label className="flex flex-col gap-1">
            Skopiuj z konkursu
            <select
              className="rounded-sm border border-border-control px-2 py-2"
              value={source}
              onChange={(event) => setSource(event.target.value)}
            >
              <option value="">Wybierz konkurs…</option>
              {others.map((other) => (
                <option key={other.id} value={other.id}>
                  {other.number} - {other.title}
                </option>
              ))}
            </select>
          </label>
          <button
            type="button"
            className="self-start rounded-sm border border-brand-accent px-4 py-2 text-brand-accent-text hover:bg-brand-accent hover:text-bg disabled:opacity-40"
            disabled={busy || source === ""}
            onClick={() => void copy()}
          >
            {busy ? "Kopiowanie…" : "Skopiuj karty i wzór"}
          </button>
        </div>
      ) : null}

      {message !== null ? (
        <p role="status" className="text-sm">
          {message}
        </p>
      ) : null}
    </section>
  );
}
