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

import { operatorPanelRoot } from "../navigation";

type Load =
  | { readonly status: "loading" }
  | { readonly status: "error" }
  | { readonly status: "ready"; readonly competitions: OperatorCompetition[] };

/**
 * Wnioski (T-35): the applications arrive per competition, so this screen
 * asks which one first. A competition still in draft is left out: nothing
 * can have been submitted to a call that was never announced.
 */
export default function ApplicationsPage() {
  const [load, setLoad] = useState<Load>({ status: "loading" });
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    let current = true;
    setLoad({ status: "loading" });

    fetchOperatorCompetitions()
      .then((competitions) => {
        if (current) {
          setLoad({
            status: "ready",
            competitions: competitions.filter((competition) => competition.status !== "Draft"),
          });
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
      <h1 className="text-2xl">Wnioski</h1>

      {load.status === "loading" ? <p className="text-sm">Wczytywanie konkursów…</p> : null}

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
        <EmptyState
          title="Nie ma jeszcze żadnego wniosku"
          action={{ href: operatorPanelRoot, label: "Przejdź do konkursów" }}
        >
          Wnioski trafiają tutaj z ogłoszonych konkursów. Dopóki żaden konkurs
          nie został ogłoszony, nie ma czego składać.
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
                <p className="text-sm">{statusLabels[competition.status]}</p>
              </div>
              <Link
                href={`/panel/operator/applications/${competition.id}`}
                className={statusActionClassName}
              >
                Lista wniosków
              </Link>
            </li>
          ))}
        </ul>
      ) : null}
    </section>
  );
}
