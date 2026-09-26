/**
 * The report of a funded project (T-50a): the applicant starts, fills in and
 * submits it; the operator lists, accepts or sends it back.
 */

import type { ApiPath } from "./api-client";
import { apiFetch, fillPath } from "./api-client";
import type { components } from "./api-schema";
import type { FormAnswers } from "./forms/answer-types";
import type { ApplicantKind, FormDocument } from "./forms/document-types";

export type Report = components["schemas"]["ReportResponse"];
export type ReportListItem = components["schemas"]["ReportListItem"];
export type ReportStatus = components["schemas"]["ReportStatus"];

export const reportStatusLabels: Record<ReportStatus, string> = {
  Draft: "W przygotowaniu",
  Submitted: "Złożone, czeka na sprawdzenie",
  Returned: "Zwrócone do poprawy",
  Accepted: "Przyjęte",
};

/** Starts the report of a funded application, or hands back the one started. */
export async function startReport(applicationId: string): Promise<Report> {
  const template = "/applications/{applicationId}/report" satisfies ApiPath;
  return apiFetch<Report>(fillPath(template, { applicationId }), { method: "POST" });
}

export async function fetchReport(reportId: string): Promise<Report> {
  const template = "/reports/{reportId}" satisfies ApiPath;
  return apiFetch<Report>(fillPath(template, { reportId }), { cache: "no-store" });
}

export async function saveReport(reportId: string, answers: FormAnswers): Promise<Report> {
  const template = "/reports/{reportId}" satisfies ApiPath;
  return apiFetch<Report>(fillPath(template, { reportId }), {
    method: "PUT",
    body: JSON.stringify({ answers }),
  });
}

export async function submitReport(reportId: string): Promise<Report> {
  const template = "/reports/{reportId}/submit" satisfies ApiPath;
  return apiFetch<Report>(fillPath(template, { reportId }), { method: "POST" });
}

export async function fetchCompetitionReports(competitionId: string): Promise<ReportListItem[]> {
  const template = "/competitions/{competitionId}/reports" satisfies ApiPath;
  return apiFetch<ReportListItem[]>(fillPath(template, { competitionId }), { cache: "no-store" });
}

export async function returnReport(reportId: string, reason: string): Promise<Report> {
  const template = "/reports/{reportId}/return" satisfies ApiPath;
  return apiFetch<Report>(fillPath(template, { reportId }), {
    method: "POST",
    body: JSON.stringify({ reason }),
  });
}

export async function acceptReport(reportId: string): Promise<Report> {
  const template = "/reports/{reportId}/accept" satisfies ApiPath;
  return apiFetch<Report>(fillPath(template, { reportId }), { method: "POST" });
}

/** The report and who it is about, in the shapes the renderer takes. */
export function reportFormOf(report: Report): {
  document: FormDocument;
  answers: FormAnswers;
  applicant: ApplicantKind;
} {
  return {
    document: report.formDefinition as FormDocument,
    answers: report.answers as FormAnswers,
    applicant: report.applicantType,
  };
}
