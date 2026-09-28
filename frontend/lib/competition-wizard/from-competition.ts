/**
 * A saved competition as a wizard draft again (T-97), so the wizard edits
 * what the server holds rather than what one browser remembers. The inverse
 * of to-request.ts: instants back to the Warsaw wall clock, amounts and
 * percentages back to the text an input shows, bytes back to megabytes.
 */
import { utcIsoToLocalWarsaw } from "@/lib/local-time";
import type { OperatorCompetition } from "@/lib/operator-competitions";

import type { CompetitionDraft } from "./types";

const text = (value: number | string | null | undefined): string =>
  value === null || value === undefined ? "" : String(value);

const megabytes = (bytes: number | string): string => {
  const value = Number(bytes) / (1024 * 1024);
  return Number.isInteger(value) ? String(value) : value.toFixed(2);
};

export function fromCompetition(competition: OperatorCompetition): CompetitionDraft {
  return {
    number: competition.number,
    title: competition.title,
    startDateLocal: utcIsoToLocalWarsaw(competition.startDate),
    endDateLocal: competition.endDate ? utcIsoToLocalWarsaw(competition.endDate) : "",
    isContinuousIntake: competition.isContinuousIntake,
    submissionNotice: text(competition.submissionNotice),

    description: text(competition.description),
    expectedResults: text(competition.expectedResults),
    rulesUrl: text(competition.rulesUrl),

    requiresPaperSubmission: competition.requiresPaperSubmission,
    paperSubmissionDeadlineLocal: competition.paperSubmissionDeadline
      ? utcIsoToLocalWarsaw(competition.paperSubmissionDeadline)
      : "",
    paperSubmissionAddress: text(competition.paperSubmissionAddress),

    projectStartDate: text(competition.projectStartDate),
    projectEndDate: text(competition.projectEndDate),
    totalPoolAmount: text(competition.totalPoolAmount),
    minGrantAmount: text(competition.minGrantAmount),
    maxGrantAmount: text(competition.maxGrantAmount),
    maxIndirectCostPercent: text(competition.maxIndirectCostPercent),
    maxInstitutionalDevelopmentPercent: text(competition.maxInstitutionalDevelopmentPercent),
    percentageBasis: competition.percentageBasis,
    maxAverageAnnualRevenue: text(competition.maxAverageAnnualRevenue),
    personalDataProcessedUntil: text(competition.personalDataProcessedUntil),
    costCategories: [...competition.costCategories],

    maxAttachmentSizeInMegabytes: megabytes(competition.maxAttachmentSizeInBytes),
    maxApplicationSizeInMegabytes: megabytes(competition.maxApplicationSizeInBytes),
    attachments: competition.attachments.map((attachment) => ({
      title: attachment.title,
      description: text(attachment.description),
      requirement: attachment.requirement,
      allowedFormats: [...attachment.allowedFormats],
    })),

    contactUserIds: competition.contacts.map((contact) => contact.userId),
    submissionEmailBody: text(competition.submissionEmailBody),
  };
}
