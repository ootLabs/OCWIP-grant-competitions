/**
 * Competitions as an operator writes and reads them (T-20, T-22), plus the
 * staff directory the contact picker in step 1.6 reads.
 *
 * Kept apart from lib/competitions.ts on purpose: that module is the public,
 * anonymous read (T-23, D6) and always talks to serverApiBaseUrl from a
 * server component. Everything here runs from the operator panel, in the
 * browser, with the session cookie apiFetch already sends.
 */

import type { ApiPath } from "./api-client";
import { apiFetch, fillPath } from "./api-client";
import type { components } from "./api-schema";

export type OperatorCompetition = components["schemas"]["CompetitionResponse"];
export type CompetitionRequest = components["schemas"]["CompetitionRequest"];
export type OperatorAccount = components["schemas"]["OperatorAccountResponse"];
export type CompetitionStatus = components["schemas"]["CompetitionStatus"];

function competitionPath(id: string): ApiPath {
  const template = "/competitions/{id}" satisfies ApiPath;
  return template.replace("{id}", encodeURIComponent(id)) as ApiPath;
}

/** Every competition an operator manages, drafts and inactive ones included. */
export async function fetchOperatorCompetitions(): Promise<OperatorCompetition[]> {
  return apiFetch<OperatorCompetition[]>("/competitions", { cache: "no-store" });
}

export async function fetchOperatorCompetition(
  id: string,
): Promise<OperatorCompetition> {
  return apiFetch<OperatorCompetition>(competitionPath(id), {
    cache: "no-store",
  });
}

/** Always comes back a draft: publishing is the separate, confirmed step. */
export async function createCompetition(
  request: CompetitionRequest,
): Promise<OperatorCompetition> {
  return apiFetch<OperatorCompetition>("/competitions", {
    method: "POST",
    body: JSON.stringify(request),
  });
}

export async function updateCompetition(
  id: string,
  request: CompetitionRequest,
): Promise<OperatorCompetition> {
  return apiFetch<OperatorCompetition>(competitionPath(id), {
    method: "PUT",
    body: JSON.stringify(request),
  });
}

/**
 * One deliberate move through the lifecycle (T-20's transition table), from
 * the competition page (T-97). The targets on offer are the competition's own
 * `allowedTransitions`; resolving is not one of them, it comes with
 * approving the results on the evaluation screen.
 */
export async function changeCompetitionStatus(
  id: string,
  status: CompetitionStatus,
): Promise<OperatorCompetition> {
  const template = "/competitions/{id}/status" satisfies ApiPath;
  return apiFetch<OperatorCompetition>(fillPath(template, { id }), {
    method: "POST",
    body: JSON.stringify({ status }),
  });
}

export async function publishCompetition(id: string): Promise<OperatorCompetition> {
  return changeCompetitionStatus(id, "Published");
}

/** Marks the competition inactive; nothing is deleted (retention). */
export async function deactivateCompetition(id: string): Promise<OperatorCompetition> {
  return apiFetch<OperatorCompetition>(competitionPath(id), { method: "DELETE" });
}

/** Undoes a deactivation (R-26); 409 when the number was taken meanwhile. */
export async function restoreCompetition(id: string): Promise<OperatorCompetition> {
  const template = "/competitions/{id}/restore" satisfies ApiPath;
  return apiFetch<OperatorCompetition>(fillPath(template, { id }), { method: "POST" });
}

/** Active OCWIP staff, for the "osoby kontaktowe" picker in step 1.6. */
export async function fetchOperators(): Promise<OperatorAccount[]> {
  return apiFetch<OperatorAccount[]>("/accounts/operators", {
    cache: "no-store",
  });
}

export type CompetitionCopyRequest = components["schemas"]["CompetitionCopyRequest"];

/** "Skopiuj konkurs" (T-98): a new draft from this one, with its own number and dates. */
export async function copyCompetition(id: string, request: CompetitionCopyRequest): Promise<OperatorCompetition> {
  const template = "/competitions/{id}/copy" satisfies ApiPath;
  return apiFetch<OperatorCompetition>(fillPath(template, { id }), {
    method: "POST",
    body: JSON.stringify(request),
  });
}
