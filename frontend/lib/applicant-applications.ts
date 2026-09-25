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
import type { FormAnswers } from "./forms/answer-types";
import type { FormDocument } from "./forms/document-types";

export type ApplicationOverview = components["schemas"]["ApplicationOverviewResponse"];
export type Application = components["schemas"]["ApplicationResponse"];
export type Attachment = components["schemas"]["AttachmentResponse"];

export interface ApplicationForm {
  readonly versionNumber: number;
  readonly document: FormDocument;
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
