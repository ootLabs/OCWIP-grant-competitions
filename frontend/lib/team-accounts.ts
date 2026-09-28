/**
 * The OCWIP team (T-104): operators and experts with their role and state,
 * read only. Roles and deactivation are server commands (grant-role,
 * deactivate-account), never a screen.
 */
import { apiFetch } from "./api-client";
import type { components } from "./api-schema";

export type TeamAccount = components["schemas"]["TeamAccountResponse"];

export async function fetchTeam(): Promise<TeamAccount[]> {
  return apiFetch<TeamAccount[]>("/accounts/team", { cache: "no-store" });
}

export const teamRoleLabels: Record<TeamAccount["role"], string> = {
  Applicant: "Wnioskodawca",
  Operator: "Operator",
  Reviewer: "Ekspert",
};
