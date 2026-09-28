/**
 * "Zwrot do poprawy" (T-103): the operator's return, and what the
 * applicant's screen does with it. The server enforces every rule here
 * again (LockedSections, ApplicationEditWindow); this module only keeps the
 * screen from offering what the server would refuse.
 */
import type { ApiPath } from "./api-client";
import { apiFetch, fillPath } from "./api-client";
import type { components } from "./api-schema";
import type { FormDocument, FormField } from "./forms/document-types";

export type ApplicationCorrections = components["schemas"]["ApplicationCorrectionsResponse"];
export type ApplicationReturn = components["schemas"]["ApplicationReturnResponse"];
export type ApplicationReturnRequest = components["schemas"]["ApplicationReturnRequest"];

export async function fetchCorrections(applicationId: string): Promise<ApplicationCorrections> {
  const template = "/applications/{id}/corrections" satisfies ApiPath;
  return apiFetch<ApplicationCorrections>(fillPath(template, { id: applicationId }), { cache: "no-store" });
}

export async function returnApplication(
  applicationId: string,
  request: ApplicationReturnRequest,
): Promise<ApplicationReturn> {
  const template = "/applications/{id}/return" satisfies ApiPath;
  return apiFetch<ApplicationReturn>(fillPath(template, { id: applicationId }), {
    method: "POST",
    body: JSON.stringify(request),
  });
}

/** The return still waiting for the correction: the one without resolvedAt. */
export function openReturn(corrections: ApplicationCorrections): ApplicationReturn | null {
  return corrections.returns.find((item) => !item.resolvedAt) ?? null;
}

/**
 * The form with every field outside the unlocked sections shown and never
 * an input, through the same read only rendering the report's "było" uses.
 */
export function lockOutside(document: FormDocument, unlocked: readonly string[]): FormDocument {
  const lock = (field: FormField): FormField => ({
    ...field,
    readOnly: true,
    ...(field.table ? { table: { ...field.table, columns: field.table.columns.map(lock) } } : {}),
  });

  return {
    ...document,
    sections: document.sections.map((section) =>
      unlocked.includes(section.key) ? section : { ...section, fields: section.fields.map(lock) },
    ),
  };
}
