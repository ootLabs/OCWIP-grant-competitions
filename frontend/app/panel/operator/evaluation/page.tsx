"use client";

import Link from "next/link";
import { useEffect, useState } from "react";

import { EmptyState } from "@/components/empty-state";
import {
  fetchOperatorCompetitions,
  type OperatorCompetition,
} from "@/lib/operator-competitions";

import { operatorPanelRoot } from "../navigation";

type Load =
  | { readonly status: "loading" }
  | { readonly status: "error" }
  | { readonly status: "ready"; readonly competitions: OperatorCompetition[] };

/** Which competition to evaluate (T-41): every announced one, drafts left out. */
export default function EvaluationPage() {
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
            competitions: competitions.filter((c) => c.status !== "Draft"),
          });
        }
      })
      .catch(() => {
        if (current) setLoad({ status: "error" });
      });

    return () => {
      current = false;
    };
  }, [attempt]);

  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-2xl">Ocena</h1>

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
        <EmptyState title="Nie ma jeszcze konkursu do oceny">
          Ocena zaczyna się po ogłoszeniu konkursu i złożeniu pierwszych
          wniosków.
        </EmptyState>
      ) : null}

      {load.status === "ready" && load.competitions.length > 0 ? (
        <ul className="flex flex-col gap-2">
          {load.competitions.map((competition) => (
            <li key={competition.id}>
              <Link
                className="underline"
                href={`${operatorPanelRoot}/evaluation/${competition.id}`}
              >
                Konkurs {competition.number}: {competition.title}
              </Link>
            </li>
          ))}
        </ul>
      ) : null}
    </section>
  );
}
