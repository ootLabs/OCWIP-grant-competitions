/**
 * The operator-side reads the creator needs (T-26): which competitions exist
 * and, for a chosen one, the document of its current published form.
 *
 * Nothing here writes anything. Publishing a version is T-27's endpoint
 * (POST /competitions/{id}/form-definitions), not this card's job: the
 * creator only ever reads a source to copy from and edits a draft that stays
 * in the browser (lib/forms/draft-storage.ts) until T-27 sends it.
 */
import type { ApiPath } from "../api-client";
import { apiFetch } from "../api-client";
import type { components } from "../api-schema";
import type { FormDocument } from "./document-types";

export type CompetitionSummary = components["schemas"]["CompetitionResponse"];

export async function fetchOperatorCompetitions(): Promise<CompetitionSummary[]> {
  return apiFetch<CompetitionSummary[]>("/competitions");
}

/** Competitions that already carry a published, current form: valid copy sources. */
export function competitionsWithForms(
  competitions: readonly CompetitionSummary[],
): CompetitionSummary[] {
  return competitions.filter((competition) => competition.formDefinitionId !== null);
}

/**
 * The document of the version currently in force for a competition, or null
 * when it has none yet. `versionNumber` comes from ListFormDefinitions
 * because CompetitionResponse only carries the version's id, not its number,
 * and GetFormDefinition is addressed by number (docs/kontrakt-formularza.md).
 */
export async function fetchCurrentFormDocument(
  competitionId: string,
): Promise<FormDocument | null> {
  const listTemplate = "/competitions/{competitionId}/form-definitions" satisfies ApiPath;
  const listPath = listTemplate.replace(
    "{competitionId}",
    encodeURIComponent(competitionId),
  ) as ApiPath;

  const versions = await apiFetch<
    components["schemas"]["FormDefinitionSummaryResponse"][]
  >(listPath);

  const current = versions.find((version) => version.isCurrent);
  if (current === undefined) {
    return null;
  }

  const versionTemplate =
    "/competitions/{competitionId}/form-definitions/{version}" satisfies ApiPath;
  const versionPath = versionTemplate
    .replace("{competitionId}", encodeURIComponent(competitionId))
    .replace("{version}", encodeURIComponent(String(current.versionNumber))) as ApiPath;

  const response =
    await apiFetch<components["schemas"]["FormDefinitionResponse"]>(versionPath);

  return response.definition as FormDocument;
}
