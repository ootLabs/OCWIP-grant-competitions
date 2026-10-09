"use client";

import Link from "next/link";
import { use, useCallback, useEffect, useState } from "react";

import { apiErrorMessage } from "@/lib/api-client";
import {
  asReviewers,
  fetchCompetitionExperts,
  withdrawExpert,
  type CompetitionExpert,
} from "@/lib/competition-experts";
import { resultsPath } from "@/lib/competitions";
import {
  approveResults,
  assignReviewer,
  fetchAssignments,
  fetchDeclarations,
  fetchEvaluationSettings,
  fetchRanking,
  rankingExportUrl,
  setGrantDecision,
  unassignReviewer,
  type CompetitionAssignment,
  type DeclarationRow,
  type EvaluationSettings,
  type Ranking,
  type ReviewerSummary,
} from "@/lib/operator-evaluation";

import { cardClassName, compactActionClassName } from "@/components/ui/styles";

import { operatorPanelRoot } from "../../navigation";
import { CardSharing } from "./card-sharing";
import { ContractBundle } from "./contract-bundle";
import { AppointExpertForm } from "./appoint-expert-form";
import { ContractTemplateEditor } from "./contract-template";
import { ExpertsTable } from "./experts-table";
import { RankingTable } from "./ranking-table";
import { ReportsList } from "./reports-list";
import { ResignationPanel } from "./resignation-panel";
import { ResultMails } from "./result-mails";
import { ResultsBar } from "./results-bar";
import { SettingsForm } from "./settings-form";

type Data = {
  readonly ranking: Ranking;
  readonly settings: EvaluationSettings;
  readonly declarations: DeclarationRow[];
  readonly reviewers: ReviewerSummary[];
  readonly experts: CompetitionExpert[];
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
      const [ranking, settings, declarations, experts, assignments] =
        await Promise.all([
          fetchRanking(competitionId),
          fetchEvaluationSettings(competitionId),
          fetchDeclarations(competitionId),
          fetchCompetitionExperts(competitionId),
          fetchAssignments(competitionId),
        ]);
      // The committee of this competition (R-44) is who may be assigned.
      setData({ ranking, settings, declarations, reviewers: asReviewers(experts), experts, assignments });
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
    <section className="flex flex-col gap-6">
      <p className="text-sm">
        <Link className="underline" href={`${operatorPanelRoot}/evaluation`}>
          Wróć do listy konkursów
        </Link>
      </p>
      <h1 className="text-3xl">Ocena konkursu</h1>

      {error !== null ? (
        <p role="alert" className="text-sm">
          {error}
        </p>
      ) : null}
      {data === null && error === null ? (
        <p className="text-sm">Wczytywanie oceny…</p>
      ) : null}

      {data !== null ? (
        <div className="grid items-start gap-8 xl:grid-cols-[14rem_minmax(0,1fr)]">
          {/* The stages of evaluation, in the order they are done. A long page
              of eight parts needs a way to jump, and the list doubles as the
              checklist of what the operator has ahead. */}
          <nav aria-label="Etapy oceny" className="xl:sticky xl:top-36 xl:max-h-[calc(100vh-10rem)] xl:overflow-y-auto">
            <ol className="flex flex-wrap gap-2 xl:flex-col xl:gap-1">
              {stages
                .filter((stage) => stage.id !== "rezygnacje" || Boolean(data.ranking.resultsApprovedAt))
                .map((stage) => (
                  <li key={stage.id}>
                    <a
                      className="block rounded-sm px-3 py-2 text-sm no-underline hover:bg-surface-warm hover:underline"
                      href={`#${stage.id}`}
                    >
                      {stage.label}
                    </a>
                  </li>
                ))}
            </ol>
          </nav>
          <div className="flex min-w-0 flex-col gap-6">
          <section aria-labelledby="ustawienia" className={sectionClassName}>
            <h2 id="ustawienia" className="scroll-mt-36 text-2xl">
              Ustawienia oceny
            </h2>
            <SettingsForm
              competitionId={competitionId}
              settings={data.settings}
              onSaved={() => void load()}
            />
          </section>
          <section aria-labelledby="eksperci" className={sectionClassName}>
            <h2 id="eksperci" className="scroll-mt-36 text-2xl">
              Komisja i deklaracje bezstronności
            </h2>
            <AppointExpertForm competitionId={competitionId} onAppointed={() => void load()} />
            {data.reviewers.length === 0 ? (
              <p className="text-sm">
                Komisja jest pusta. Powołaj ekspertów po adresie e-mail; osobę
                bez konta zaprosisz, podając jej imię i nazwisko.
              </p>
            ) : (
              <ExpertsTable
                reviewers={data.reviewers}
                declarations={data.declarations}
                assignments={data.assignments}
                invited={new Set(data.experts.filter((expert) => expert.invitationPending).map((expert) => expert.userId))}
                onWithdraw={(reviewerId) =>
                  void change(async () => {
                    await withdrawExpert(competitionId, reviewerId);
                  }, "Nie udało się odwołać eksperta.")
                }
              />
            )}
          </section>
          <section aria-labelledby="ranking" className={sectionClassName}>
            <h2 id="ranking" className="scroll-mt-36 text-2xl">
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
            <p className="flex flex-wrap items-center gap-2 text-sm">
              <span className="mr-1 text-text-muted">Pobierz listę:</span>
              <a className={compactActionClassName} href={rankingExportUrl(competitionId, "pdf")}>PDF do publikacji</a>
              <a className={compactActionClassName} href={rankingExportUrl(competitionId, "xlsx")}>XLSX</a>
              <a className={compactActionClassName} href={rankingExportUrl(competitionId, "csv")}>CSV</a>
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
          <section aria-labelledby="udostepnienie" className={sectionClassName}>
            <h2 id="udostepnienie" className="scroll-mt-36 text-2xl">
              Udostępnienie kart wnioskodawcom
            </h2>
            <CardSharing competitionId={competitionId} />
          </section>
          <section aria-labelledby="powiadomienia" className={sectionClassName}>
            <h2 id="powiadomienia" className="scroll-mt-36 text-2xl">
              Powiadomienia o wynikach
            </h2>
            <ResultMails competitionId={competitionId} approved={Boolean(data.ranking.resultsApprovedAt)} />
          </section>
          <section aria-labelledby="wzor-umowy" className={sectionClassName}>
            <h2 id="wzor-umowy" className="scroll-mt-36 text-2xl">
              Wzór umowy
            </h2>
            <ContractTemplateEditor competitionId={competitionId} />
            <ContractBundle competitionId={competitionId} />
          </section>
          {data.ranking.resultsApprovedAt ? (
            <section aria-labelledby="rezygnacje" className={sectionClassName}>
              <h2 id="rezygnacje" className="scroll-mt-36 text-2xl">
                Umowy, rezygnacje i lista rezerwowa
              </h2>
              <ResignationPanel competitionId={competitionId} onChange={() => void load()} />
            </section>
          ) : null}
          <section aria-labelledby="sprawozdania" className={sectionClassName}>
            <h2 id="sprawozdania" className="scroll-mt-36 text-2xl">
              Sprawozdania
            </h2>
            <ReportsList competitionId={competitionId} />
          </section>
          </div>
        </div>
      ) : null}
    </section>
  );
}

/** Each stage on its own card, so eight parts read as eight parts. */
const sectionClassName = `${cardClassName} flex flex-col gap-4 p-5 sm:p-6`;

const stages: readonly { readonly id: string; readonly label: string }[] = [
  { id: "ustawienia", label: "Ustawienia oceny" },
  { id: "eksperci", label: "Eksperci i deklaracje" },
  { id: "ranking", label: "Lista rankingowa" },
  { id: "udostepnienie", label: "Udostępnienie kart" },
  { id: "powiadomienia", label: "Powiadomienia o wynikach" },
  { id: "wzor-umowy", label: "Wzór umowy" },
  { id: "rezygnacje", label: "Umowy i lista rezerwowa" },
  { id: "sprawozdania", label: "Sprawozdania" },
];
