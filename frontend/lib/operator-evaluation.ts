/**
 * The operator's side of evaluation (T-41): settings, the ranking list,
 * experts, their declarations and assignments. Every route is operator only
 * on the server.
 */
import type { ApiPath } from "./api-client";
import { apiFetch, fillPath } from "./api-client";
import type { components } from "./api-schema";
import type { Evaluation } from "./reviewer-work";

export type Ranking = components["schemas"]["RankingResponse"];
export type RankingRow = components["schemas"]["RankingRow"];
export type EvaluationSettings = components["schemas"]["EvaluationSettingsResponse"];
export type EvaluationSettingsRequest = components["schemas"]["EvaluationSettingsRequest"];
export type DeclarationRow = components["schemas"]["DeclarationRow"];
export type ReviewerSummary = components["schemas"]["ReviewerSummary"];
export type CompetitionAssignment = components["schemas"]["CompetitionAssignment"];
export type FormalStanding = components["schemas"]["FormalStanding"];
export type DeclarationStatus = components["schemas"]["DeclarationStatus"];

export const formalLabels: Record<FormalStanding, string> = {
  NotStarted: "Nierozpoczęta",
  InProgress: "W toku",
  Passed: "Pozytywna",
  Failed: "Negatywna",
};

export const declarationLabels: Record<DeclarationStatus, string> = {
  NotDecided: "Nie złożona",
  Accepted: "Złożona",
  Refused: "Odmowa",
};

export async function fetchRanking(competitionId: string): Promise<Ranking> {
  const template = "/competitions/{competitionId}/ranking" satisfies ApiPath;
  return apiFetch<Ranking>(fillPath(template, { competitionId }), { cache: "no-store" });
}

export async function fetchEvaluationSettings(competitionId: string): Promise<EvaluationSettings> {
  const template = "/competitions/{competitionId}/evaluation-settings" satisfies ApiPath;
  return apiFetch<EvaluationSettings>(fillPath(template, { competitionId }), { cache: "no-store" });
}

export async function saveEvaluationSettings(
  competitionId: string,
  settings: EvaluationSettingsRequest,
): Promise<EvaluationSettings> {
  const template = "/competitions/{competitionId}/evaluation-settings" satisfies ApiPath;
  return apiFetch<EvaluationSettings>(fillPath(template, { competitionId }), {
    method: "PUT",
    body: JSON.stringify(settings),
  });
}

export async function fetchDeclarations(competitionId: string): Promise<DeclarationRow[]> {
  const template = "/competitions/{competitionId}/declarations" satisfies ApiPath;
  return apiFetch<DeclarationRow[]>(fillPath(template, { competitionId }), { cache: "no-store" });
}

export async function fetchReviewers(): Promise<ReviewerSummary[]> {
  return apiFetch<ReviewerSummary[]>("/reviewers", { cache: "no-store" });
}

export async function fetchAssignments(competitionId: string): Promise<CompetitionAssignment[]> {
  const template = "/competitions/{competitionId}/assignments" satisfies ApiPath;
  return apiFetch<CompetitionAssignment[]>(fillPath(template, { competitionId }), { cache: "no-store" });
}

export async function assignReviewer(applicationId: string, reviewerId: string): Promise<void> {
  const template = "/applications/{id}/assignments" satisfies ApiPath;
  await apiFetch<unknown>(fillPath(template, { id: applicationId }), {
    method: "POST",
    body: JSON.stringify({ reviewerId }),
  });
}

export async function unassignReviewer(applicationId: string, reviewerId: string): Promise<void> {
  const template = "/applications/{id}/assignments/{reviewerId}" satisfies ApiPath;
  await apiFetch<unknown>(fillPath(template, { id: applicationId, reviewerId }), { method: "DELETE" });
}

export type ApplicationEvaluationItem = components["schemas"]["ApplicationEvaluationItem"];

/** Every card of one application with who filled it in, formal first (T-41a). */
export async function fetchApplicationEvaluations(applicationId: string): Promise<ApplicationEvaluationItem[]> {
  const template = "/applications/{applicationId}/evaluations" satisfies ApiPath;
  return apiFetch<ApplicationEvaluationItem[]>(fillPath(template, { applicationId }), { cache: "no-store" });
}

/** Opens the formal card of an application, or hands back the one already open (T-38, one per application). */
export async function openFormalCard(applicationId: string): Promise<Evaluation> {
  const template = "/applications/{applicationId}/evaluations/formal" satisfies ApiPath;
  return apiFetch<Evaluation>(fillPath(template, { applicationId }), { method: "POST" });
}

export type CardSharing = components["schemas"]["CardSharingResponse"];

export async function fetchCardSharing(competitionId: string): Promise<CardSharing> {
  const template = "/competitions/{competitionId}/card-sharing" satisfies ApiPath;
  return apiFetch<CardSharing>(fillPath(template, { competitionId }), { cache: "no-store" });
}

/** Shares the cards with the applicants, once for the whole competition (T-41b). */
export async function shareCards(competitionId: string): Promise<CardSharing> {
  const template = "/competitions/{competitionId}/card-sharing" satisfies ApiPath;
  return apiFetch<CardSharing>(fillPath(template, { competitionId }), { method: "POST" });
}
