/**
 * The evaluation cards and the report form of a competition, as the
 * competition page manages them (T-96): which version is in force, and
 * copying the ones in force from another competition. The same routes the
 * API always had for them (T-38, T-50a); publishing a copy is an ordinary
 * new version, checked by the same contract gate.
 */
import { apiFetch, fillPath, type ApiPath } from "./api-client";
import type { components } from "./api-schema";

type Summary = components["schemas"]["FormDefinitionSummaryResponse"];
type Definition = components["schemas"]["FormDefinitionResponse"];

export type ContentPart = "formal" | "merit" | "report";

export const contentParts: readonly { readonly part: ContentPart; readonly label: string }[] = [
  { part: "formal", label: "Karta oceny formalnej" },
  { part: "merit", label: "Karta oceny merytorycznej" },
  { part: "report", label: "Wzór sprawozdania" },
];

function listPath(competitionId: string, part: ContentPart): ApiPath {
  return part === "report"
    ? fillPath("/competitions/{competitionId}/report-form" satisfies ApiPath, { competitionId })
    : fillPath("/competitions/{competitionId}/evaluation-cards/{stage}" satisfies ApiPath, {
        competitionId,
        stage: part,
      });
}

function versionPath(competitionId: string, part: ContentPart, version: string): ApiPath {
  return part === "report"
    ? fillPath("/competitions/{competitionId}/report-form/{version}" satisfies ApiPath, { competitionId, version })
    : fillPath("/competitions/{competitionId}/evaluation-cards/{stage}/{version}" satisfies ApiPath, {
        competitionId,
        stage: part,
        version,
      });
}

/** The version in force, or null when the competition has none of this part. */
export async function fetchPartInForce(competitionId: string, part: ContentPart): Promise<number | null> {
  const versions = await apiFetch<Summary[]>(listPath(competitionId, part), { cache: "no-store" });
  const current = versions.find((version) => version.isCurrent);
  return current ? Number(current.versionNumber) : null;
}

/**
 * Publishes the source competition's version in force as the next version
 * here. False when the source has none of this part, so nothing is sent.
 */
export async function copyPart(fromCompetitionId: string, toCompetitionId: string, part: ContentPart): Promise<boolean> {
  const version = await fetchPartInForce(fromCompetitionId, part);
  if (version === null) {
    return false;
  }

  const source = await apiFetch<Definition>(versionPath(fromCompetitionId, part, String(version)), {
    cache: "no-store",
  });
  await apiFetch<Definition>(listPath(toCompetitionId, part), {
    method: "POST",
    body: JSON.stringify({ definition: source.definition }),
  });
  return true;
}
