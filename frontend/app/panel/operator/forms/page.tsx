"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { EmptyState } from "@/components/empty-state";
import { statusActionClassName } from "@/components/status-page";
import {
  fetchOperatorCompetitions,
  type CompetitionSummary,
} from "@/lib/forms/competition-forms";

type Load =
  | { readonly status: "loading" }
  | { readonly status: "error" }
  | { readonly status: "ready"; readonly competitions: CompetitionSummary[] };

/**
 * Where an operator opens the creator for a competition (T-26).
 *
 * Every competition is listed, drafts included, because the form has to be
 * ready before a competition can be announced, not after. What differs is
 * the next step: a competition with a current form goes straight into
 * editing its own copy, one without goes through the source picker in
 * [competitionId]/page.tsx, because the creator never starts a document from
 * nothing (see docs/log.md, T-26 narrowing).
 */
export default function FormsPage() {
  const [load, setLoad] = useState<Load>({ status: "loading" });
  // Bumped by the retry button. The effect below depends on it so that
  // "Spróbuj ponownie" actually asks the network again instead of only
  // resetting the screen to a loading state that never leaves it, because
  // nothing else would ever call fetchOperatorCompetitions a second time.
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    let current = true;
    setLoad({ status: "loading" });

    fetchOperatorCompetitions()
      .then((competitions) => {
        if (current) {
          setLoad({ status: "ready", competitions });
        }
      })
      .catch(() => {
        if (current) {
          setLoad({ status: "error" });
        }
      });

    return () => {
      current = false;
    };
  }, [attempt]);

  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-2xl">Formularze</h1>

      {load.status === "loading" ? (
        <p className="text-sm">Wczytywanie konkursów…</p>
      ) : null}

      {load.status === "error" ? (
        <p className="text-sm">
          Nie udało się pobrać listy konkursów.{" "}
          <button
            type="button"
            className="underline"
            onClick={() => setAttempt((value) => value + 1)}
          >
            Spróbuj ponownie
          </button>
          .
        </p>
      ) : null}

      {load.status === "ready" && load.competitions.length === 0 ? (
        <EmptyState title="Nie ma jeszcze żadnego konkursu">
          Formularz podpina się do konkursu, więc zacznij od jego założenia.
        </EmptyState>
      ) : null}

      {load.status === "ready" && load.competitions.length > 0 ? (
        <ul className="divide-y divide-border-muted border-y border-border-muted">
          {load.competitions.map((competition) => (
            <li key={competition.id} className="flex items-center justify-between gap-4 py-3">
              <div>
                <p className="text-sm text-text">
                  {competition.number} - {competition.title}
                </p>
                <p className="text-sm">
                  {competition.formDefinitionId === null
                    ? "Brak formularza"
                    : "Ma formularz, kreator otwiera jego kopię"}
                </p>
              </div>
              <Link
                href={`/panel/operator/forms/${competition.id}`}
                className={statusActionClassName}
              >
                Otwórz kreator
              </Link>
            </li>
          ))}
        </ul>
      ) : null}
    </section>
  );
}
