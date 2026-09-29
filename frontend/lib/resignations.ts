/**
 * Resignation and the reserve list (T-109), the operator's side: the state
 * after the results, confirming a resignation, funding the next reserve
 * application. Every rule (status, pool) is the server's; this only calls it.
 */
import type { ApiPath } from "./api-client";
import { apiFetch, fillPath } from "./api-client";
import type { components } from "./api-schema";

export type Resignations = components["schemas"]["ResignationsResponse"];
export type ResignationAction = components["schemas"]["ResignationActionResponse"];

export async function fetchResignations(competitionId: string): Promise<Resignations> {
  const template = "/competitions/{competitionId}/resignations" satisfies ApiPath;
  return apiFetch<Resignations>(fillPath(template, { competitionId }), { cache: "no-store" });
}

/** The change is stored either way; mailSent says whether the applicant was told. */
export async function confirmResignation(applicationId: string): Promise<ResignationAction> {
  const template = "/applications/{applicationId}/resignation" satisfies ApiPath;
  return apiFetch<ResignationAction>(fillPath(template, { applicationId }), { method: "POST" });
}

export async function promoteFromReserve(applicationId: string, awardedGrant: number): Promise<ResignationAction> {
  const template = "/applications/{applicationId}/promotion" satisfies ApiPath;
  return apiFetch<ResignationAction>(fillPath(template, { applicationId }), {
    method: "POST",
    body: JSON.stringify({ awardedGrant }),
  });
}
