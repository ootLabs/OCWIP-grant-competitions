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
import { apiFetch } from "./api-client";
import type { components } from "./api-schema";

export type OperatorCompetition = components["schemas"]["CompetitionResponse"];
export type CompetitionRequest = components["schemas"]["CompetitionRequest"];
export type OperatorAccount = components["schemas"]["OperatorAccountResponse"];

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
 * One deliberate move through the lifecycle (T-20's transition table). The
 * wizard only ever asks for "Published": every other transition belongs to
 * screens this card does not build (T-21's clock, and closing/archiving by an
 * operator elsewhere).
 */
export async function publishCompetition(
  id: string,
): Promise<OperatorCompetition> {
  const template = "/competitions/{id}/status" satisfies ApiPath;
  const path = template.replace("{id}", encodeURIComponent(id)) as ApiPath;

  return apiFetch<OperatorCompetition>(path, {
    method: "POST",
    body: JSON.stringify({ status: "Published" }),
  });
}

/** Active OCWIP staff, for the "osoby kontaktowe" picker in step 1.6. */
export async function fetchOperators(): Promise<OperatorAccount[]> {
  return apiFetch<OperatorAccount[]>("/accounts/operators", {
    cache: "no-store",
  });
}
