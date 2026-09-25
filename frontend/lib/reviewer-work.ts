/**
 * The expert's side of evaluation (T-40): their list of assigned
 * applications and their own merit card. Every call goes to a route that
 * checks the assignment on the server (T-37, T-38); nothing here decides who
 * may see what.
 */
import type { ApiPath } from "./api-client";
import { apiFetch, fillPath } from "./api-client";
import type { components } from "./api-schema";
import type { FormAnswers } from "./forms/answer-types";
import type { ApplicantKind, FormDocument } from "./forms/document-types";

export type ReviewerWork = components["schemas"]["ReviewerWorkResponse"];
export type ReviewerCompetition = components["schemas"]["ReviewerCompetition"];
export type ReviewerApplication = components["schemas"]["ReviewerApplication"];
export type OwnCardStanding = components["schemas"]["OwnCardStanding"];
export type Evaluation = components["schemas"]["EvaluationResponse"];

export const ownCardLabels: Record<OwnCardStanding, string> = {
  NotStarted: "Nierozpoczęta",
  Draft: "W toku",
  Finished: "Zakończona",
};

export async function fetchReviewerWork(): Promise<ReviewerWork> {
  return apiFetch<ReviewerWork>("/reviewer/applications", { cache: "no-store" });
}

/** Opens the caller's own merit card, starting it if there is none yet. */
export async function openMeritCard(applicationId: string): Promise<Evaluation> {
  const template = "/applications/{applicationId}/evaluations/merit" satisfies ApiPath;
  return apiFetch<Evaluation>(fillPath(template, { applicationId }), {
    method: "POST",
    body: JSON.stringify({}),
  });
}

export async function saveEvaluation(evaluationId: string, answers: FormAnswers): Promise<Evaluation> {
  const template = "/evaluations/{evaluationId}" satisfies ApiPath;
  return apiFetch<Evaluation>(fillPath(template, { evaluationId }), {
    method: "PUT",
    body: JSON.stringify({ answers }),
  });
}

export async function finishEvaluation(evaluationId: string): Promise<Evaluation> {
  const template = "/evaluations/{evaluationId}/finish" satisfies ApiPath;
  return apiFetch<Evaluation>(fillPath(template, { evaluationId }), {
    method: "POST",
    body: JSON.stringify({}),
  });
}

/** The card and who it is about, in the shapes the renderer takes. */
export function cardOf(evaluation: Pick<Evaluation, "cardDefinition" | "answers" | "applicantType">): {
  document: FormDocument;
  answers: FormAnswers;
  applicant: ApplicantKind;
} {
  return {
    document: evaluation.cardDefinition as FormDocument,
    answers: evaluation.answers as FormAnswers,
    applicant: evaluation.applicantType,
  };
}

/** Amounts arrive as numbers or numeric strings, like everywhere in this API. */
export function amount(value: number | string | null | undefined): number | null {
  return value === null || value === undefined ? null : Number(value);
}

export type Declaration = components["schemas"]["DeclarationResponse"];

/** The expert's impartiality declaration for one competition, with its text (T-40a). */
export async function fetchDeclaration(competitionId: string): Promise<Declaration> {
  const template = "/reviewer/competitions/{competitionId}/declaration" satisfies ApiPath;
  return apiFetch<Declaration>(fillPath(template, { competitionId }), { cache: "no-store" });
}

/** Accepts the declaration, or refuses it with a reason. Decided once. */
export async function decideDeclaration(
  competitionId: string,
  accept: boolean,
  refusalReason: string | null,
): Promise<Declaration> {
  const template = "/reviewer/competitions/{competitionId}/declaration" satisfies ApiPath;
  return apiFetch<Declaration>(fillPath(template, { competitionId }), {
    method: "POST",
    body: JSON.stringify({ accept, refusalReason }),
  });
}
