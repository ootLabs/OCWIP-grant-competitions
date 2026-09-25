"use client";

import { useEffect, useState } from "react";

import { EmptyState } from "@/components/empty-state";
import { fetchReviewers, type ReviewerSummary } from "@/lib/operator-evaluation";

/**
 * The expert accounts (T-41). Adding one is the grant-role command, never a
 * screen (README): whoever can grant a role here could also take one.
 */
export default function ReviewersPage() {
  const [reviewers, setReviewers] = useState<ReviewerSummary[] | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    fetchReviewers()
      .then(setReviewers)
      .catch(() => setFailed(true));
  }, []);

  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-2xl">Recenzenci</h1>

      {failed ? <p className="text-sm">Nie udało się pobrać listy recenzentów.</p> : null}
      {reviewers === null && !failed ? <p className="text-sm">Wczytywanie recenzentów…</p> : null}

      {reviewers !== null && reviewers.length === 0 ? (
        <EmptyState title="Nie ma jeszcze żadnego recenzenta">
          Recenzenci oceniają wnioski po zamknięciu naboru. Dodasz ich, zanim
          ocena się zacznie, i przypiszesz im wnioski do oceny.
        </EmptyState>
      ) : null}

      {reviewers !== null && reviewers.length > 0 ? (
        <ul className="flex flex-col gap-1 text-sm">
          {reviewers.map((reviewer) => (
            <li key={reviewer.id}>
              {reviewer.name ? `${reviewer.name} (${reviewer.email})` : reviewer.email}
            </li>
          ))}
        </ul>
      ) : null}
    </section>
  );
}
