"use client";

import Link from "next/link";
import { useEffect, useState } from "react";

import { EmptyState } from "@/components/empty-state";
import { statusActionClassName } from "@/components/status-page";
import { StatusBadge } from "@/components/ui/status-badge";
import { cardClassName, primaryActionClassName } from "@/components/ui/styles";
import {
  fetchMyApplications,
  type ApplicationOverview,
} from "@/lib/applicant-applications";
import { formatMoment } from "@/lib/format";
import { applicationStatusLabels, applicationStatusTones } from "@/lib/operator-applications";

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
  const [filter, setFilter] = useState<Filter>("all");

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

  const applications = load.status === "ready" ? load.applications : [];
  const shown = applications.filter((application) => filter === "all" || groupOf(application.status) === filter);
  const toCorrect = applications.filter((application) => application.status === "Returned").length;

  return (
    <section className="flex flex-col gap-6">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div className="flex flex-col gap-1">
          <h1 className="text-4xl">Moje wnioski</h1>
          {toCorrect > 0 ? (
            <p className="text-text-muted">
              {toCorrect === 1 ? "Jeden wniosek czeka na Twoją poprawę." : `Wnioski czekające na Twoją poprawę: ${toCorrect}.`}
            </p>
          ) : null}
        </div>
        <Link className={primaryActionClassName} href={`${applicantPanelRoot}/competitions`}>
          Nowy wniosek
        </Link>
      </div>

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

      {load.status === "ready" && applications.length === 0 ? (
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

      {load.status === "ready" && applications.length > 0 ? (
        <>
          {/* Toggle buttons, not tabs: they narrow one list in place and
              there is no panel for each of them to own. */}
          <div aria-label="Pokaż wnioski" className="flex flex-wrap gap-2" role="group">
            {filters.map((option) => (
              <button
                aria-pressed={filter === option.id}
                className="min-h-10 rounded-pill border border-border-control px-4 text-sm font-semibold aria-pressed:border-active-border aria-pressed:bg-active-bg aria-pressed:text-active-text"
                key={option.id}
                onClick={() => setFilter(option.id)}
                type="button"
              >
                {option.label}
              </button>
            ))}
          </div>

          {shown.length === 0 ? (
            <p className="text-sm text-text-muted">Nie masz wniosków w tej grupie.</p>
          ) : (
            <ul className="grid list-none gap-5 md:grid-cols-2">
              {shown.map((application) => (
                <li key={application.id} className={`${cardClassName} flex flex-col gap-4 p-6`}>
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <StatusBadge tone={applicationStatusTones[application.status]}>
                      {applicationStatusLabels[application.status]}
                    </StatusBadge>
                    {application.number ? (
                      <span className="text-sm text-text-muted">{`nr ${application.number}`}</span>
                    ) : null}
                  </div>
                  <div>
                    <p className="text-sm text-text-muted">Konkurs {application.competitionNumber}</p>
                    <h2 className="text-2xl leading-tight">{application.competitionTitle}</h2>
                  </div>
                  <p className="text-sm">
                    {application.status !== "Draft"
                      ? `złożono ${formatMoment(application.submittedAt!)}`
                      : `zapisano ${formatMoment(application.lastSavedAt)}`}
                  </p>
                  <p className="mt-auto border-t border-border-muted pt-4">
                    <Link
                      href={`${applicantPanelRoot}/applications/${application.id}`}
                      className={
                        application.status === "Draft" || application.status === "Returned"
                          ? primaryActionClassName
                          : statusActionClassName
                      }
                    >
                      {application.status === "Draft"
                        ? "Wypełnij dalej"
                        : application.status === "Returned"
                          ? "Popraw wniosek"
                          : "Zobacz wniosek"}
                    </Link>
                  </p>
                </li>
              ))}
            </ul>
          )}
        </>
      ) : null}
    </section>
  );
}

type Filter = "all" | "draft" | "active" | "closed";

const filters: readonly { readonly id: Filter; readonly label: string }[] = [
  { id: "all", label: "Wszystkie" },
  { id: "draft", label: "Robocze" },
  { id: "active", label: "W toku" },
  { id: "closed", label: "Zakończone" },
];

/** Which of the filters an application falls under. A full map, so a new status has to be placed. */
const groups: Record<ApplicationOverview["status"], Exclude<Filter, "all">> = {
  Draft: "draft",
  Submitted: "active",
  Returned: "active",
  Funded: "active",
  Reserve: "active",
  ContractSigned: "active",
  Settled: "closed",
  Rejected: "closed",
  Resigned: "closed",
};

function groupOf(status: ApplicationOverview["status"]): Exclude<Filter, "all"> {
  return groups[status];
}
