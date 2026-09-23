"use client";

import Link from "next/link";
import { useEffect, useState } from "react";

import { statusLabels } from "@/app/competitions/labels";
import { EmptyState } from "@/components/empty-state";
import { statusActionClassName } from "@/components/status-page";
import {
  fetchOperatorCompetitions,
  type OperatorCompetition,
} from "@/lib/operator-competitions";

type Load =
  | { readonly status: "loading" }
  | { readonly status: "error" }
  | { readonly status: "ready"; readonly competitions: OperatorCompetition[] };

/**
 * Konkursy (T-22): every competition an operator manages, drafts and
 * inactive ones included (ListCompetitions on the backend hides nothing),
 * and the entry point into the announcement wizard.
 */
export default function CompetitionsPage() {
  const [load, setLoad] = useState<Load>({ status: "loading" });
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
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h1 className="text-2xl">Konkursy</h1>
        <Link
          href="/panel/operator/competitions/new"
          className={statusActionClassName}
        >
          Nowy konkurs
        </Link>
      </div>

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
          Konkurs zaczyna się od ogłoszenia: tytuł, terminy naboru, budżet i
          formularz wniosku, który wnioskodawcy wypełnią.
        </EmptyState>
      ) : null}

      {load.status === "ready" && load.competitions.length > 0 ? (
        <ul className="divide-y divide-border-muted border-y border-border-muted">
          {load.competitions.map((competition) => (
            <li
              key={competition.id}
              className="flex items-center justify-between gap-4 py-3"
            >
              <div>
                <p className="text-sm text-text">
                  {competition.number} - {competition.title}
                </p>
                <p className="text-sm">{statusLabels[competition.status]}</p>
              </div>
              <Link
                href={`/panel/operator/forms/${competition.id}`}
                className={statusActionClassName}
              >
                Formularz wniosku
              </Link>
            </li>
          ))}
        </ul>
      ) : null}
    </section>
  );
}
