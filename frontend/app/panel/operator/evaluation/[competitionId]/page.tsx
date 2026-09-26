"use client";

import Link from "next/link";
import { use, useCallback, useEffect, useState } from "react";

import { apiErrorMessage } from "@/lib/api-client";
import { resultsPath } from "@/lib/competitions";
import {
  approveResults,
  assignReviewer,
  fetchAssignments,
  fetchDeclarations,
  fetchEvaluationSettings,
  fetchRanking,
  fetchReviewers,
  rankingExportUrl,
  setGrantDecision,
  unassignReviewer,
  type CompetitionAssignment,
  type DeclarationRow,
  type EvaluationSettings,
  type Ranking,
  type ReviewerSummary,
} from "@/lib/operator-evaluation";

import { operatorPanelRoot } from "../../navigation";
import { CardSharing } from "./card-sharing";
import { ExpertsTable } from "./experts-table";
import { RankingTable } from "./ranking-table";
import { ReportsList } from "./reports-list";
import { ResultMails } from "./result-mails";
import { ResultsBar } from "./results-bar";
import { SettingsForm } from "./settings-form";

type Data = {
  readonly ranking: Ranking;
  readonly settings: EvaluationSettings;
  readonly declarations: DeclarationRow[];
  readonly reviewers: ReviewerSummary[];
  readonly assignments: CompetitionAssignment[];
};

/**
 * Evaluation of one competition for the operator (T-41): how it is scored,
 * who evaluates and whether they declared impartiality, and the ranking list
 * with every application's progress and experts.
 */
export default function CompetitionEvaluationPage({
  params,
}: {
  params: Promise<{ competitionId: string }>;
}) {
  const { competitionId } = use(params);
  const [data, setData] = useState<Data | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      const [ranking, settings, declarations, reviewers, assignments] =
        await Promise.all([
          fetchRanking(competitionId),
          fetchEvaluationSettings(competitionId),
          fetchDeclarations(competitionId),
          fetchReviewers(),
          fetchAssignments(competitionId),
        ]);
      setData({ ranking, settings, declarations, reviewers, assignments });
      setError(null);
    } catch (failure) {
      setError(
        apiErrorMessage(failure, "Nie udało się pobrać oceny konkursu."),
      );
    }
  }, [competitionId]);

  useEffect(() => {
    void load();
  }, [load]);

  /** True when every step went through. Reads everything again either way:
   * a group assignment refused halfway keeps what went before the refusal. */
  async function change(
    action: () => Promise<unknown>,
    fallback = "Nie udało się zmienić przypisania.",
  ): Promise<boolean> {
    setActionError(null);
    try {
      await action();
      return true;
    } catch (failure) {
      setActionError(apiErrorMessage(failure, fallback));
      return false;
    } finally {
      await load();
    }
  }

  return (
    <section className="flex flex-col gap-8">
      <p className="text-sm">
        <Link className="underline" href={`${operatorPanelRoot}/evaluation`}>
          Wróć do listy konkursów
        </Link>
      </p>
      <h1 className="text-2xl">Ocena konkursu</h1>

      {error !== null ? (
        <p role="alert" className="text-sm">
          {error}
        </p>
      ) : null}
      {data === null && error === null ? (
        <p className="text-sm">Wczytywanie oceny…</p>
      ) : null}

      {data !== null ? (
        <>
          <section aria-labelledby="ustawienia" className="flex flex-col gap-3">
            <h2 id="ustawienia" className="text-xl">
              Ustawienia oceny
            </h2>
            <SettingsForm
              competitionId={competitionId}
              settings={data.settings}
              onSaved={() => void load()}
            />
          </section>

          <section aria-labelledby="eksperci" className="flex flex-col gap-3">
            <h2 id="eksperci" className="text-xl">
              Eksperci i deklaracje bezstronności
            </h2>
            {data.reviewers.length === 0 ? (
              <p className="text-sm">
                Nie ma jeszcze kont ekspertów. Rolę recenzenta nadaje
                administrator systemu.
              </p>
            ) : (
              <ExpertsTable
                reviewers={data.reviewers}
                declarations={data.declarations}
                assignments={data.assignments}
              />
            )}
          </section>

          <section aria-labelledby="udostepnienie" className="flex flex-col gap-3">
            <h2 id="udostepnienie" className="text-xl">
              Udostępnienie kart wnioskodawcom
            </h2>
            <CardSharing competitionId={competitionId} />
          </section>

          <section aria-labelledby="powiadomienia" className="flex flex-col gap-3">
            <h2 id="powiadomienia" className="text-xl">
              Powiadomienia o wynikach
            </h2>
            <ResultMails competitionId={competitionId} approved={Boolean(data.ranking.resultsApprovedAt)} />
          </section>

          <section aria-labelledby="sprawozdania" className="flex flex-col gap-3">
            <h2 id="sprawozdania" className="text-xl">
              Sprawozdania
            </h2>
            <ReportsList competitionId={competitionId} />
          </section>

          <section aria-labelledby="ranking" className="flex flex-col gap-3">
            <h2 id="ranking" className="text-xl">
              Lista rankingowa
            </h2>
            {actionError !== null ? (
              <p role="alert" className="text-sm text-brand-accent-text">
                {actionError}
              </p>
            ) : null}
            <ResultsBar
              ranking={data.ranking}
              onApprove={async () => {
                try {
                  await approveResults(competitionId);
                  await load();
                  return null;
                } catch (failure) {
                  return apiErrorMessage(failure, "Nie udało się zatwierdzić wyników.");
                }
              }}
            />
            <p className="flex flex-wrap gap-4 text-sm">
              <span>Pobierz listę:</span>
              <a className="underline" href={rankingExportUrl(competitionId, "pdf")}>PDF do publikacji</a>
              <a className="underline" href={rankingExportUrl(competitionId, "xlsx")}>XLSX</a>
              <a className="underline" href={rankingExportUrl(competitionId, "csv")}>CSV</a>
              {data.ranking.resultsApprovedAt ? (
                <a className="underline" href={resultsPath(competitionId)}>Opublikowane wyniki</a>
              ) : null}
            </p>
            {data.ranking.rows.length === 0 ? (
              <p className="text-sm">
                W konkursie nie ma jeszcze złożonych wniosków.
              </p>
            ) : (
              <RankingTable
                competitionId={competitionId}
                rows={data.ranking.rows}
                reviewers={data.reviewers}
                assignments={data.assignments}
                onAssign={(applicationIds, reviewerId) =>
                  change(async () => {
                    // One after another, not all at once: the first refusal
                    // stops the run and is shown, and what went through stays.
                    for (const applicationId of applicationIds) {
                      await assignReviewer(applicationId, reviewerId);
                    }
                  })
                }
                locked={Boolean(data.ranking.resultsApprovedAt)}
                onDecide={(applicationId, awardedGrant, note) =>
                  change(
                    () => setGrantDecision(applicationId, awardedGrant, note),
                    "Nie udało się zapisać decyzji.",
                  )
                }
                onUnassign={(applicationId, reviewerId) =>
                  change(() => unassignReviewer(applicationId, reviewerId))
                }
              />
            )}
          </section>
        </>
      ) : null}
    </section>
  );
}
