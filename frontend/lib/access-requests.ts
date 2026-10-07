/**
 * Joining a Podmiot card that already exists (T-93a, report step 2.2): the
 * applicant asks by NIP, the founder decides, and after seven days without
 * an answer an operator decides with a note on how the person was checked.
 */
import { apiFetch, fillPath, type ApiPath } from "./api-client";
import type { components } from "./api-schema";

export type MyAccessRequest = components["schemas"]["MyEntityAccessRequest"];
export type PendingAccessRequest = components["schemas"]["PendingEntityAccessRequest"];
export type EscalatedAccessRequest = components["schemas"]["EscalatedEntityAccessRequest"];
export type AccessRequestStatus = MyAccessRequest["status"];

export const accessRequestStatusLabels: Record<AccessRequestStatus, string> = {
  Pending: "czeka na decyzję",
  Approved: "przyznany",
  Rejected: "odrzucona",
};

export async function requestEntityAccess(nip: string): Promise<MyAccessRequest> {
  return apiFetch<MyAccessRequest>("/me/access-requests" satisfies ApiPath, {
    method: "POST",
    body: JSON.stringify({ nip }),
  });
}

export async function fetchMyAccessRequests(): Promise<MyAccessRequest[]> {
  return apiFetch<MyAccessRequest[]>("/me/access-requests" satisfies ApiPath, { cache: "no-store" });
}

/** Requests waiting for the founder of this card; the founder only. */
export async function fetchPendingAccessRequests(entityId: string): Promise<PendingAccessRequest[]> {
  const template = "/me/entities/{id}/access-requests" satisfies ApiPath;
  return apiFetch<PendingAccessRequest[]>(fillPath(template, { id: entityId }), { cache: "no-store" });
}

export async function decideAsFounder(entityId: string, requestId: string, approve: boolean): Promise<void> {
  const template = "/me/entities/{id}/access-requests/{requestId}/decision" satisfies ApiPath;
  await apiFetch<void>(fillPath(template, { id: entityId, requestId }), {
    method: "POST",
    body: JSON.stringify({ approve }),
  });
}

export async function fetchEscalatedAccessRequests(): Promise<EscalatedAccessRequest[]> {
  return apiFetch<EscalatedAccessRequest[]>("/access-requests/escalated" satisfies ApiPath, { cache: "no-store" });
}

export async function decideAsOperator(requestId: string, approve: boolean, note: string): Promise<void> {
  const template = "/access-requests/{requestId}/decision" satisfies ApiPath;
  await apiFetch<void>(fillPath(template, { requestId }), {
    method: "POST",
    body: JSON.stringify({ approve, note }),
  });
}
