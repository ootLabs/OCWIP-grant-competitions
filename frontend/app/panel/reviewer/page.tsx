"use client";

import Link from "next/link";
import { useEffect, useState } from "react";

import { EmptyState } from "@/components/empty-state";
import { formatAmount } from "@/lib/format";
import { entityTypeLabels } from "@/lib/operator-applications";
import { amount, fetchReviewerWork, ownCardLabels, type ReviewerWork } from "@/lib/reviewer-work";

import { reviewerPanelRoot } from "./navigation";

type Load =
  | { readonly status: "loading" }
  | { readonly status: "error" }
  | { readonly status: "ready"; readonly work: ReviewerWork };

/**
 * What the expert has to evaluate (T-40), one table per competition with the
 * three sums of the report above it: requested, recommended by this expert
 * and the competition's pool, so an expert sees at once that they have
 * recommended more than there is to give.
 */
export default function ReviewerHome() {
  const [load, setLoad] = useState<Load>({ status: "loading" });
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    let current = true;
    setLoad({ status: "loading" });

    fetchReviewerWork()
      .then((work) => {
        if (current) setLoad({ status: "ready", work });
      })
      .catch(() => {
        if (current) setLoad({ status: "error" });
      });

    return () => {
      current = false;
    };
  }, [attempt]);

  return (
    <section className="flex flex-col gap-6">
      <h1 className="text-2xl">Wnioski do oceny</h1>

      {load.status === "loading" ? <p className="text-sm">Wczytywanie wniosków…</p> : null}

      {load.status === "error" ? (
        <p className="text-sm">
          Nie udało się pobrać listy wniosków.{" "}
          <button type="button" className="underline" onClick={() => setAttempt((value) => value + 1)}>
            Spróbuj ponownie
          </button>
          .
        </p>
      ) : null}

      {load.status === "ready" && load.work.competitions.length === 0 ? (
        <EmptyState title="Nie masz jeszcze wniosków do oceny">
          Wnioski przydziela operator OCWIP. Gdy to zrobi, pojawią się tutaj.
        </EmptyState>
      ) : null}

      {load.status === "ready"
        ? load.work.competitions.map((competition) => (
            <section key={competition.competitionId} className="flex flex-col gap-3">
              <h2 className="text-xl">
                Konkurs {competition.number}: {competition.title}
              </h2>

              <dl className="grid gap-2 text-sm sm:grid-cols-3">
                <Sum label="Wnioskowane razem" value={amount(competition.requestedTotal)} />
                <Sum label="Twoje rekomendacje razem" value={amount(competition.recommendedTotal)} />
                <Sum label="Pula konkursu" value={amount(competition.totalPoolAmount)} />
              </dl>

              <div className="overflow-x-auto">
                <table className="w-full border-collapse text-sm">
                  <caption className="sr-only">Wnioski przydzielone do oceny w konkursie {competition.number}</caption>
                  <thead>
                    <tr className="text-left">
                      <th scope="col" className="border-b border-border px-2 py-1">Numer</th>
                      <th scope="col" className="border-b border-border px-2 py-1">Tytuł projektu</th>
                      <th scope="col" className="border-b border-border px-2 py-1">Rodzaj</th>
                      <th scope="col" className="border-b border-border px-2 py-1 text-right">Wnioskowana</th>
                      <th scope="col" className="border-b border-border px-2 py-1">Twoja karta</th>
                      <th scope="col" className="border-b border-border px-2 py-1 text-right">Rekomendowana</th>
                    </tr>
                  </thead>
                  <tbody>
                    {competition.applications.map((application) => (
                      <tr key={application.applicationId}>
                        <td className="border-b border-border-muted px-2 py-1">
                          <Link className="underline" href={`${reviewerPanelRoot}/applications/${application.applicationId}`}>
                            {application.number ?? "bez numeru"}
                          </Link>
                        </td>
                        <td className="border-b border-border-muted px-2 py-1">{application.projectTitle ?? ""}</td>
                        <td className="border-b border-border-muted px-2 py-1">{entityTypeLabels[application.entityType]}</td>
                        <td className="border-b border-border-muted px-2 py-1 text-right">
                          <Money value={amount(application.requestedGrant)} />
                        </td>
                        <td className="border-b border-border-muted px-2 py-1">{ownCardLabels[application.card]}</td>
                        <td className="border-b border-border-muted px-2 py-1 text-right">
                          <Money value={amount(application.recommendedGrant)} />
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </section>
          ))
        : null}
    </section>
  );
}

function Sum({ label, value }: { label: string; value: number | null }) {
  return (
    <div className="rounded-sm border border-border-muted bg-surface-muted px-3 py-2">
      <dt>{label}</dt>
      <dd className="font-semibold">{value === null ? "nie ustawiono" : formatAmount(value)}</dd>
    </div>
  );
}

function Money({ value }: { value: number | null }) {
  return <>{value === null ? "" : formatAmount(value)}</>;
}
