/**
 * Resignation and the reserve list (T-109), the operator's side: the state
 * after the results, confirming a resignation, funding the next reserve
 * application. Every rule (status, pool) is the server's; this only calls it.
 */
import type { ApiPath } from "./api-client";
import { apiFetch, fillPath } from "./api-client";
import type { components } from "./api-schema";

export type Resignations = components["schemas"]["ResignationsResponse"];

export async function fetchResignations(competitionId: string): Promise<Resignations> {
  const template = "/competitions/{competitionId}/resignations" satisfies ApiPath;
  return apiFetch<Resignations>(fillPath(template, { competitionId }), { cache: "no-store" });
}

export async function confirmResignation(applicationId: string): Promise<void> {
  const template = "/applications/{applicationId}/resignation" satisfies ApiPath;
  await apiFetch<void>(fillPath(template, { applicationId }), { method: "POST" });
}

export async function promoteFromReserve(applicationId: string, awardedGrant: number): Promise<void> {
  const template = "/applications/{applicationId}/promotion" satisfies ApiPath;
  await apiFetch<void>(fillPath(template, { applicationId }), {
    method: "POST",
    body: JSON.stringify({ awardedGrant }),
  });
}
