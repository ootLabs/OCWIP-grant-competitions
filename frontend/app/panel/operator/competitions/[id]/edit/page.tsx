"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useEffect, useState } from "react";

import { ApiError } from "@/lib/api-client";
import { fromCompetition } from "@/lib/competition-wizard/from-competition";
import type { CompetitionDraft } from "@/lib/competition-wizard/types";
import { fetchApplicationList } from "@/lib/operator-applications";
import { fetchOperatorCompetition, type OperatorCompetition } from "@/lib/operator-competitions";

import { CompetitionWizard } from "../../new/competition-wizard";

type Load =
  | { readonly status: "loading" }
  | { readonly status: "missing" }
  | { readonly status: "error" }
  | {
      readonly status: "ready";
      readonly competition: OperatorCompetition;
      readonly draft: CompetitionDraft;
      readonly submitted: number;
    };

/**
 * A saved competition in the wizard (T-97), read from the server, so a draft
 * started in one browser opens in any other. Editing stays open in every
 * state, the decision recorded at CompetitionService.UpdateAsync; once
 * applications have arrived the wizard says so above the steps.
 */
export default function EditCompetitionPage() {
  const { id } = useParams<{ id: string }>();
  const [load, setLoad] = useState<Load>({ status: "loading" });

  useEffect(() => {
    let current = true;

    Promise.all([
      fetchOperatorCompetition(id),
      // The count is a warning, not a condition: without it the wizard
      // still opens.
      fetchApplicationList(id)
        .then((list) => list.applications.length)
        .catch(() => 0),
    ])
      .then(([competition, submitted]) => {
        if (current) {
          setLoad({ status: "ready", competition, draft: fromCompetition(competition), submitted });
        }
      })
      .catch((error: unknown) => {
        if (current) {
          setLoad({ status: error instanceof ApiError && error.status === 404 ? "missing" : "error" });
        }
      });

    return () => {
      current = false;
    };
  }, [id]);

  if (load.status === "loading") {
    return <p className="text-sm">Wczytywanie konkursu…</p>;
  }

  if (load.status !== "ready") {
    return (
      <p className="text-sm">
        {load.status === "missing" ? "Nie ma takiego konkursu." : "Nie udało się wczytać konkursu."}{" "}
        <Link className="underline" href="/panel/operator">
          Wróć do listy konkursów
        </Link>
      </p>
    );
  }

  return (
    <CompetitionWizard
      initialDraft={load.draft}
      initialCompetition={load.competition}
      submittedApplications={load.submitted}
    />
  );
}
