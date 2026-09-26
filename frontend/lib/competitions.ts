/**
 * The public competition, read in one place (T-23).
 *
 * Both routes behind this are anonymous by design (D6): an applicant finds out
 * what OCWIP is running without creating an account, and without seeing the
 * competitions of every other operator in the country.
 *
 * Nothing here decides whether a competition may still be applied to. That is
 * the T-21 rule and it travels inside `intake` with every response, so a page
 * draws the button and the countdown from it instead of comparing the two
 * dates sitting next to it a second time.
 */

import type { ApiPath } from "./api-client";
import { ApiError, apiFetch, serverApiBaseUrl } from "./api-client";
import type { components } from "./api-schema";

export type PublicCompetition = components["schemas"]["PublicCompetitionResponse"];
export type CompetitionIntake = components["schemas"]["CompetitionIntakeResponse"];
export type CompetitionAttachment =
  components["schemas"]["CompetitionAttachmentResponse"];
export type CompetitionContact =
  components["schemas"]["CompetitionContactResponse"];
export type AttachmentRequirement = components["schemas"]["AttachmentRequirement"];
export type AllowedFileFormat = components["schemas"]["AllowedFileFormat"];
export type CostCategory = components["schemas"]["CostCategory"];

/** Where a competition lives publicly. The one place that spells this out. */
export function competitionPath(id: string): string {
  return `/competitions/${id}`;
}

/**
 * Read fresh, every time.
 *
 * The intake state and the countdown are the two things this page exists for
 * and both of them change with the clock alone, with no write anywhere to
 * invalidate a cache. A page cached for even a minute would keep offering
 * "Wypełnij wniosek" past a deadline that D7 says is cut to the minute.
 */
const readOptions = { cache: "no-store" } as const;

export async function fetchPublicCompetitions(): Promise<PublicCompetition[]> {
  return apiFetch<PublicCompetition[]>("/public/competitions", {
    ...readOptions,
    baseUrl: serverApiBaseUrl(),
  });
}

/**
 * One competition, or null when it has no public address.
 *
 * Null covers both "never existed" and "is still a draft", because the backend
 * deliberately answers 404 to both: telling somebody who guessed an identifier
 * that they guessed right is the leak that answer exists to prevent. The page
 * turns this into its own 404 and asks no further questions.
 */
export async function fetchPublicCompetition(
  id: string,
): Promise<PublicCompetition | null> {
  // The template carries the typo check, because `satisfies` fails to compile
  // the day this route leaves the API document. The identifier is filled in
  // afterwards, which is the one thing the generated union cannot express, so
  // the cast is on the filled address and not on the route.
  const template = "/public/competitions/{id}" satisfies ApiPath;
  const path = template.replace("{id}", encodeURIComponent(id)) as ApiPath;

  try {
    return await apiFetch<PublicCompetition>(path, {
      ...readOptions,
      baseUrl: serverApiBaseUrl(),
    });
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) {
      return null;
    }

    throw error;
  }
}

export type PublicResults = components["schemas"]["PublicResultsResponse"];

/** Where the published results of a competition live (T-42a). */
export function resultsPath(id: string): string {
  return `${competitionPath(id)}/results`;
}

/**
 * The approved results, or null while there are none: the backend answers
 * 404 before approval, the same as for no competition at all.
 */
export async function fetchPublicResults(id: string): Promise<PublicResults | null> {
  const template = "/public/competitions/{competitionId}/results" satisfies ApiPath;
  const path = template.replace("{competitionId}", encodeURIComponent(id)) as ApiPath;

  try {
    return await apiFetch<PublicResults>(path, { ...readOptions, baseUrl: serverApiBaseUrl() });
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) {
      return null;
    }

    throw error;
  }
}
