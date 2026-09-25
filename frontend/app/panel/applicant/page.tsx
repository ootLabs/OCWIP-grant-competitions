"use client";

import Link from "next/link";
import { useEffect, useState } from "react";

import { EmptyState } from "@/components/empty-state";
import { statusActionClassName } from "@/components/status-page";
import {
  fetchMyApplications,
  type ApplicationOverview,
} from "@/lib/applicant-applications";
import { formatMoment } from "@/lib/format";
import { applicationStatusLabels } from "@/lib/operator-applications";

import { applicantPanelRoot } from "./navigation";

type Load =
  | { readonly status: "loading" }
  | { readonly status: "error" }
  | { readonly status: "ready"; readonly applications: ApplicationOverview[] };

/**
 * Moje wnioski (T-34): every application the signed in Podmiot has started
 * or submitted, across every competition, newest first. A second offer in
 * the same competition (D9) is just another row: nothing here groups by
 * competition or hides one behind the other.
 */
export default function ApplicationsPage() {
  const [load, setLoad] = useState<Load>({ status: "loading" });
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    let current = true;
    setLoad({ status: "loading" });

    fetchMyApplications()
      .then((applications) => {
        if (current) {
          setLoad({ status: "ready", applications });
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
      <h1 className="text-2xl">Moje wnioski</h1>

      {load.status === "loading" ? <p className="text-sm">Wczytywanie wniosków…</p> : null}

      {load.status === "error" ? (
        <p className="text-sm">
          Nie udało się pobrać listy wniosków.{" "}
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

      {load.status === "ready" && load.applications.length === 0 ? (
        <EmptyState
          title="Nie masz jeszcze żadnego wniosku"
          action={{
            href: `${applicantPanelRoot}/competitions`,
            label: "Zobacz aktualne konkursy",
          }}
        >
          Wniosek zaczyna się od wybrania konkursu, do którego chcesz go
          złożyć. Rozpoczęty wniosek zapisuje się jako roboczy i możesz do
          niego wracać aż do zamknięcia naboru.
        </EmptyState>
      ) : null}

      {load.status === "ready" && load.applications.length > 0 ? (
        <ul className="divide-y divide-border-muted border-y border-border-muted">
          {load.applications.map((application) => (
            <li
              key={application.id}
              className="flex items-center justify-between gap-4 py-3"
            >
              <div>
                <p className="text-sm text-text">
                  {application.competitionNumber} - {application.competitionTitle}
                </p>
                <p className="text-sm">
                  {applicationStatusLabels[application.status]}
                  {application.number ? ` · nr ${application.number}` : ""}
                  {" · "}
                  {application.status === "Submitted"
                    ? `złożono ${formatMoment(application.submittedAt!)}`
                    : `zapisano ${formatMoment(application.lastSavedAt)}`}
                </p>
              </div>
              <Link
                href={`${applicantPanelRoot}/applications/${application.id}`}
                className={statusActionClassName}
              >
                {application.status === "Submitted" ? "Zobacz wniosek" : "Wypełnij dalej"}
              </Link>
            </li>
          ))}
        </ul>
      ) : null}
    </section>
  );
}
