/**
 * The applicant's own side of an application (T-34): "Moje wnioski", the one
 * being filled in right now, its attachments, and submitting it.
 *
 * Shares wire types and a couple of labels with lib/operator-applications.ts
 * (T-35), but not the reads themselves: the operator reads everybody's
 * submitted offers of one competition, this reads exactly one Podmiot's own,
 * draft included, across every competition at once.
 */
import type { ApiPath } from "./api-client";
import { apiBaseUrl, apiFetch } from "./api-client";
import type { components } from "./api-schema";
import type { PublicCompetition } from "./competitions";
import type { FormAnswers } from "./forms/answer-types";
import type { FormDocument } from "./forms/document-types";
import type { CompetitionLimitSettings } from "./forms/limits";

export type ApplicationOverview = components["schemas"]["ApplicationOverviewResponse"];
export type Application = components["schemas"]["ApplicationResponse"];
export type Attachment = components["schemas"]["AttachmentResponse"];

export interface ApplicationForm {
  readonly versionNumber: number;
  readonly document: FormDocument;
}

/**
 * The six settings a limit inside the form may measure against (D12), read
 * straight off the public competition the applicant already has in hand.
 * Unlike lib/forms/competition-forms.ts's fetchCompetitionLimitSettings
 * (T-27, operator only), this never asks the API again: PublicCompetitionResponse
 * already carries every one of these fields for anybody, logged in or not.
 */
export function limitSettingsFrom(competition: PublicCompetition): CompetitionLimitSettings {
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

function fill(template: string, values: Record<string, string>): ApiPath {
  return Object.entries(values).reduce(
    (path, [key, value]) => path.replace(`{${key}}`, encodeURIComponent(value)),
    template,
  ) as ApiPath;
}

export async function fetchMyApplications(): Promise<ApplicationOverview[]> {
  return apiFetch<ApplicationOverview[]>("/applications", { cache: "no-store" });
}

export async function createDraft(competitionId: string): Promise<Application> {
  const template = "/competitions/{competitionId}/applications" satisfies ApiPath;
  return apiFetch<Application>(fill(template, { competitionId }), {
    method: "POST",
    body: JSON.stringify({}),
  });
}

export async function fetchApplication(id: string): Promise<Application> {
  const template = "/applications/{id}" satisfies ApiPath;
  return apiFetch<Application>(fill(template, { id }), { cache: "no-store" });
}

export async function fetchApplicationForm(id: string): Promise<ApplicationForm> {
  const template = "/applications/{id}/form-definition" satisfies ApiPath;
  const response = await apiFetch<components["schemas"]["ApplicationFormResponse"]>(
    fill(template, { id }),
    { cache: "no-store" },
  );

  return {
    versionNumber: Number(response.versionNumber),
    document: response.definition as FormDocument,
  };
}

export async function saveDraft(id: string, answers: FormAnswers): Promise<Application> {
  const template = "/applications/{id}" satisfies ApiPath;
  return apiFetch<Application>(fill(template, { id }), {
    method: "PUT",
    body: JSON.stringify({ answers }),
  });
}

export async function submitApplication(id: string): Promise<Application> {
  const template = "/applications/{id}/submit" satisfies ApiPath;
  return apiFetch<Application>(fill(template, { id }), {
    method: "POST",
    body: JSON.stringify({}),
  });
}

export function confirmationPdfUrl(id: string): string {
  const template = "/applications/{id}/confirmation" satisfies ApiPath;
  return `${apiBaseUrl}${fill(template, { id })}`;
}

export async function fetchAttachments(applicationId: string): Promise<Attachment[]> {
  const template = "/applications/{applicationId}/attachments" satisfies ApiPath;
  return apiFetch<Attachment[]>(fill(template, { applicationId }), { cache: "no-store" });
}

export async function uploadAttachment(
  applicationId: string,
  file: File,
): Promise<Attachment> {
  const template = "/applications/{applicationId}/attachments" satisfies ApiPath;
  return uploadForm(fill(template, { applicationId }), "POST", file);
}

export async function replaceAttachment(
  attachmentId: string,
  file: File,
): Promise<Attachment> {
  const template = "/attachments/{id}" satisfies ApiPath;
  return uploadForm(fill(template, { id: attachmentId }), "PUT", file);
}

function uploadForm(path: ApiPath, method: "POST" | "PUT", file: File): Promise<Attachment> {
  const body = new FormData();
  body.append("file", file);

  return apiFetch<Attachment>(path, { method, body });
}
