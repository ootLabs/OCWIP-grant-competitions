/**
 * Operator-side reads and writes the creator (T-26) and the preview and
 * publish screen (T-27) need: which competitions exist, the document of a
 * competition's current published form, its limit settings for a live
 * preview, and publishing a new version.
 *
 * The creator itself still only ever reads: it edits a draft that stays in
 * the browser (lib/forms/draft-storage.ts) until this file's
 * publishFormDefinition sends it.
 */
import type { ApiPath } from "../api-client";
import { apiFetch } from "../api-client";
import type { components } from "../api-schema";
import type { FormDocument } from "./document-types";
import type { CompetitionLimitSettings } from "./limits";

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

/**
 * The six settings a limit inside the form may measure against (D12), read
 * straight off the competition: the same numbers the operator set in the
 * competition's own kreator, never copied into the form document itself
 * (docs/kontrakt-formularza.md, "Limity").
 */
export async function fetchCompetitionLimitSettings(
  competitionId: string,
): Promise<CompetitionLimitSettings> {
  const template = "/competitions/{id}" satisfies ApiPath;
  const path = template.replace("{id}", encodeURIComponent(competitionId)) as ApiPath;
  const competition = await apiFetch<CompetitionSummary>(path);

  const asNumber = (value: number | string | null | undefined): number | undefined =>
    value === null || value === undefined ? undefined : Number(value);

  return {
    maxGrantAmount: asNumber(competition.maxGrantAmount),
    minGrantAmount: asNumber(competition.minGrantAmount),
    totalPoolAmount: asNumber(competition.totalPoolAmount),
    maxIndirectCostPercent: asNumber(competition.maxIndirectCostPercent),
    maxInstitutionalDevelopmentPercent: asNumber(competition.maxInstitutionalDevelopmentPercent),
    maxAverageAnnualRevenue: asNumber(competition.maxAverageAnnualRevenue),
  };
}

/**
 * Publishes a competition's next version. Adds a row, never replaces one
 * (T-25): applications already started keep the version they were filled
 * against. A rejected document throws `ApiError` with `fieldErrors` keyed by
 * the JSON path `FormSchemaValidator` refused, one Polish message each.
 */
export async function publishFormDefinition(
  competitionId: string,
  document: FormDocument,
): Promise<components["schemas"]["FormDefinitionResponse"]> {
  const template = "/competitions/{competitionId}/form-definitions" satisfies ApiPath;
  const path = template.replace("{competitionId}", encodeURIComponent(competitionId)) as ApiPath;

  return apiFetch<components["schemas"]["FormDefinitionResponse"]>(path, {
    method: "POST",
    body: JSON.stringify({ definition: document }),
  });
}
