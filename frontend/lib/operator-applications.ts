/**
 * The applications a competition received, as the operator tracks them
 * (T-35): the list, one submitted offer, the exports, and the sorting and
 * filtering the table does in the browser.
 *
 * Sorting and filtering stay here rather than on the backend: one
 * competition brings in about 120 offers, the whole list is one response,
 * and a round trip per click on a column heading would be the slowest way
 * to reorder a table that is already on screen.
 */

import type { ApiPath } from "./api-client";
import { apiBaseUrl, apiFetch } from "./api-client";
import type { components } from "./api-schema";

export type ApplicationList = components["schemas"]["ApplicationListResponse"];
export type ApplicationListItem = components["schemas"]["ApplicationListItem"];
export type SubmittedApplication = components["schemas"]["SubmittedApplicationResponse"];
export type EntityType = components["schemas"]["EntityType"];
export type ApplicationStatus = components["schemas"]["ApplicationStatus"];

/** docs/reguly-biznesowe.md, "Typy podmiotów"; the same words as the export. */
export const entityTypeLabels: Record<EntityType, string> = {
  InformalGroup: "Grupa nieformalna",
  PatronInformalGroup: "Grupa nieformalna pod patronatem",
  Organisation: "Organizacja",
};

/** Draft is in the map although the list never shows one: see app/competitions/labels.ts. */
export const applicationStatusLabels: Record<ApplicationStatus, string> = {
  Draft: "Wersja robocza",
  Submitted: "Złożony",
};

function fill(template: string, values: Record<string, string>): ApiPath {
  return Object.entries(values).reduce(
    (path, [key, value]) => path.replace(`{${key}}`, encodeURIComponent(value)),
    template,
  ) as ApiPath;
}

export async function fetchApplicationList(competitionId: string): Promise<ApplicationList> {
  const template = "/competitions/{competitionId}/applications" satisfies ApiPath;
  return apiFetch<ApplicationList>(fill(template, { competitionId }), { cache: "no-store" });
}

export async function fetchSubmittedApplication(
  competitionId: string,
  id: string,
): Promise<SubmittedApplication> {
  const template = "/competitions/{competitionId}/applications/{id}" satisfies ApiPath;
  return apiFetch<SubmittedApplication>(fill(template, { competitionId, id }), {
    cache: "no-store",
  });
}

/**
 * Plain links rather than fetches: the browser downloads the file under the
 * name the backend gives it, with the session cookie it already sends to the
 * API origin.
 */
export function exportUrl(competitionId: string, format: "csv" | "pdf"): string {
  const template =
    format === "csv"
      ? ("/competitions/{competitionId}/applications/export/csv" satisfies ApiPath)
      : ("/competitions/{competitionId}/applications/export/pdf" satisfies ApiPath);
  return `${apiBaseUrl}${fill(template, { competitionId })}`;
}

export function attachmentUrl(id: string): string {
  const template = "/attachments/{id}" satisfies ApiPath;
  return `${apiBaseUrl}${fill(template, { id })}`;
}

export type SortKey =
  | "number"
  | "entityName"
  | "entityType"
  | "projectTitle"
  | "totalCost"
  | "requestedGrant"
  | "status"
  | "submittedAt";

export interface ListView {
  readonly sortKey: SortKey;
  readonly descending: boolean;
  readonly status: ApplicationStatus | "all";
  readonly entityType: EntityType | "all";
}

export const defaultListView: ListView = {
  sortKey: "number",
  descending: false,
  status: "all",
  entityType: "all",
};

type Comparable = string | number | null;

function sortValue(item: ApplicationListItem, key: SortKey): Comparable {
  switch (key) {
    case "number":
      // Zero padded to three digits, so "1000" has to sort by value, not text.
      return Number(item.number);
    case "entityName":
      return item.entityName;
    case "entityType":
      return entityTypeLabels[item.entityType];
    case "projectTitle":
      return item.projectTitle;
    case "totalCost":
      return item.totalCost === null ? null : Number(item.totalCost);
    case "requestedGrant":
      return item.requestedGrant === null ? null : Number(item.requestedGrant);
    case "status":
      return applicationStatusLabels[item.status];
    case "submittedAt":
      return item.submittedAt;
  }
}

const collator = new Intl.Collator("pl", { sensitivity: "base", numeric: true });

function compare(a: Comparable, b: Comparable): number {
  if (typeof a === "number" && typeof b === "number") {
    return a - b;
  }
  return collator.compare(String(a), String(b));
}

/**
 * The rows the table shows. An empty value always sorts last, in either
 * direction: a missing title is not "smaller" than any title, and flipping
 * the order should not bring every blank to the top. Ties keep the number
 * order, so rows do not reshuffle among themselves on every click.
 */
export function visibleRows(
  items: readonly ApplicationListItem[],
  view: ListView,
): ApplicationListItem[] {
  const direction = view.descending ? -1 : 1;

  return items
    .filter((item) => view.status === "all" || item.status === view.status)
    .filter((item) => view.entityType === "all" || item.entityType === view.entityType)
    .map((item, index) => ({ item, index }))
    .sort((left, right) => {
      const a = sortValue(left.item, view.sortKey);
      const b = sortValue(right.item, view.sortKey);

      if (a === null || b === null) {
        return a === b ? left.index - right.index : a === null ? 1 : -1;
      }

      return compare(a, b) * direction || left.index - right.index;
    })
    .map(({ item }) => item);
}

/** The requested total of the rows on screen, for the footer under a filter. */
export function requestedSum(items: readonly ApplicationListItem[]): number {
  return items.reduce((sum, item) => sum + Number(item.requestedGrant ?? 0), 0);
}
