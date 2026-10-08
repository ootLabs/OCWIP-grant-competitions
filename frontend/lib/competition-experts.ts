/**
 * The committee of one competition (R-44, report step 5.1): who is
 * appointed, appointing by address or inviting, and withdrawing.
 */
import { ApiError, apiFetch, fillPath, type ApiPath } from "./api-client";
import type { components } from "./api-schema";
import type { ReviewerSummary } from "./operator-evaluation";

export type CompetitionExpert = components["schemas"]["CompetitionExpertResponse"];

export async function fetchCompetitionExperts(competitionId: string): Promise<CompetitionExpert[]> {
  const template = "/competitions/{id}/experts" satisfies ApiPath;
  return apiFetch<CompetitionExpert[]>(fillPath(template, { id: competitionId }), { cache: "no-store" });
}

export async function appointExpert(
  competitionId: string,
  person: { email: string; firstName?: string; lastName?: string },
): Promise<CompetitionExpert> {
  const template = "/competitions/{id}/experts" satisfies ApiPath;
  return apiFetch<CompetitionExpert>(fillPath(template, { id: competitionId }), {
    method: "POST",
    body: JSON.stringify(person),
  });
}

export async function withdrawExpert(competitionId: string, userId: string): Promise<void> {
  const template = "/competitions/{id}/experts/{userId}" satisfies ApiPath;
  await apiFetch<void>(fillPath(template, { id: competitionId, userId }), { method: "DELETE" });
}

/** No account with this address: the operator gives a name and invites the person. */
export function needsName(error: unknown): boolean {
  return error instanceof ApiError && error.status === 404;
}

/** The committee in the shape the assignment screens already read. */
export function asReviewers(experts: readonly CompetitionExpert[]): ReviewerSummary[] {
  return experts.map((expert) => ({
    id: expert.userId,
    name: `${expert.firstName} ${expert.lastName}`.trim(),
    email: expert.email,
  }));
}
