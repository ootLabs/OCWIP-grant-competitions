"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useEffect, useState } from "react";

import { EmptyState } from "@/components/empty-state";
import { statusActionClassName } from "@/components/status-page";
import { ApiError } from "@/lib/api-client";
import {
  exportUrl,
  fetchApplicationList,
  type ApplicationList,
} from "@/lib/operator-applications";

import { ApplicationTable } from "./application-table";

type Load =
  | { readonly status: "loading" }
  | { readonly status: "error" }
  | { readonly status: "missing" }
  | { readonly status: "ready"; readonly list: ApplicationList };

/**
 * The applications of one competition (T-35). The exports are plain links:
 * the file carries the whole list in number order, whatever the table on
 * screen is sorted or filtered by, because a list sent on to a commission
 * should not depend on where the operator last clicked.
 */
export default function CompetitionApplicationsPage() {
  const { competitionId } = useParams<{ competitionId: string }>();
  const [load, setLoad] = useState<Load>({ status: "loading" });
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    let current = true;
    setLoad({ status: "loading" });

    fetchApplicationList(competitionId)
      .then((list) => {
        if (current) {
          setLoad({ status: "ready", list });
        }
      })
      .catch((error: unknown) => {
        if (current) {
          setLoad({
            status: error instanceof ApiError && error.status === 404 ? "missing" : "error",
          });
        }
      });

    return () => {
      current = false;
    };
  }, [competitionId, attempt]);

  return (
    <section className="flex flex-col gap-4">
      <Link href="/panel/operator/applications" className="text-sm underline">
        Wszystkie konkursy
      </Link>

      <div className="flex flex-wrap items-center justify-between gap-2">
        <h1 className="text-2xl">
          {load.status === "ready"
            ? `Wnioski: ${load.list.competitionNumber} - ${load.list.competitionTitle}`
            : "Wnioski"}
        </h1>
        {load.status === "ready" && load.list.applications.length > 0 ? (
          <div className="flex flex-wrap gap-2">
            <a href={exportUrl(competitionId, "csv")} className={statusActionClassName}>
              Pobierz arkusz (CSV)
            </a>
            <a href={exportUrl(competitionId, "pdf")} className={statusActionClassName}>
              Pobierz PDF
            </a>
          </div>
        ) : null}
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

      {load.status === "missing" ? (
        <EmptyState
          title="Nie ma takiego konkursu"
          action={{ href: "/panel/operator/applications", label: "Wróć do listy konkursów" }}
        >
          Ten adres nie prowadzi do żadnego konkursu. Wybierz konkurs z listy,
          żeby zobaczyć wnioski, które do niego wpłynęły.
        </EmptyState>
      ) : null}

      {load.status === "ready" && load.list.applications.length === 0 ? (
        <EmptyState title="Do tego konkursu nie wpłynął jeszcze żaden wniosek">
          Wniosek pojawia się tutaj w chwili złożenia. Wersje robocze, które
          wnioskodawcy dopiero wypełniają, nie są widoczne na tej liście.
        </EmptyState>
      ) : null}

      {load.status === "ready" && load.list.applications.length > 0 ? (
        <ApplicationTable list={load.list} />
      ) : null}
    </section>
  );
}
