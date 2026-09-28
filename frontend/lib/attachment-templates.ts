/**
 * Templates of attachment requirements (T-102): the public download address,
 * and the operator's upload, replacement and withdrawal. The format and the
 * limit are the server's to check (the bytes decide, 10 MB).
 */
import type { ApiPath } from "./api-client";
import { apiBaseUrl, apiFetch, fillPath } from "./api-client";
import type { components } from "./api-schema";

export type AttachmentTemplate = components["schemas"]["AttachmentTemplateResponse"];

/** A plain link: anybody downloads the template of a public competition without signing in. */
export function templateDownloadUrl(requirementId: string): string {
  const template = "/public/attachment-templates/{requirementId}" satisfies ApiPath;
  return `${apiBaseUrl}${fillPath(template, { requirementId })}`;
}

/** The operator's link, which also works for a draft competition. */
export function operatorTemplateUrl(requirementId: string): string {
  const template = "/competition-attachments/{requirementId}/template" satisfies ApiPath;
  return `${apiBaseUrl}${fillPath(template, { requirementId })}`;
}

export async function uploadTemplate(requirementId: string, file: File): Promise<AttachmentTemplate> {
  const template = "/competition-attachments/{requirementId}/template" satisfies ApiPath;
  const body = new FormData();
  body.append("file", file);
  return apiFetch<AttachmentTemplate>(fillPath(template, { requirementId }), { method: "PUT", body });
}

export async function withdrawTemplate(requirementId: string): Promise<void> {
  const template = "/competition-attachments/{requirementId}/template/withdraw" satisfies ApiPath;
  await apiFetch<void>(fillPath(template, { requirementId }), { method: "POST" });
}
