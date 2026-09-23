/**
 * Turns a CompetitionDraft into the CompetitionRequest the backend accepts
 * (T-22).
 *
 * Two kinds of "not ready" are told apart on purpose:
 *
 * 1. STRUCTURALLY incomplete: a field the request cannot even be built
 *    without (number, title, the start date, an end date or continuous
 *    intake, the maximum grant amount). Caught here, before any network
 *    call, because sending a request with no start date at all would not
 *    come back as a friendly field error, it would fail to deserialize.
 * 2. Business incomplete: everything else. The wizard sends it anyway
 *    (proces.md, ścieżka 1: "kompletność sprawdzamy dopiero przy
 *    publikacji") and shows whatever CompetitionRequestValidator answers.
 *
 * Decimal amounts and percentages are sent as the STRING the operator typed,
 * not as a parsed number: CompetitionRequest.maxGrantAmount and its
 * neighbours accept both on the wire (see format.ts, formatAmount), and a
 * string is the one that keeps the last grosz instead of losing it to
 * floating point on the way through JSON.
 */
import { localWarsawToUtcIso } from "@/lib/local-time";
import type { CompetitionRequest } from "@/lib/operator-competitions";

import type { CompetitionDraft } from "./types";

export interface DraftConversion {
  readonly request: CompetitionRequest | null;
  /**
   * Polish sentences naming what is missing, when `request` is null. Not the
   * backend's business rules, only what this module needs to build a request
   * at all.
   */
  readonly structuralGaps: readonly string[];
}

function emptyToNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed === "" ? null : trimmed;
}

function megabytesToBytes(value: string): number | null {
  const trimmed = value.trim();
  if (trimmed === "") {
    return null;
  }

  const megabytes = Number(trimmed);
  return Number.isFinite(megabytes) ? Math.round(megabytes * 1024 * 1024) : null;
}

export function toCompetitionRequest(draft: CompetitionDraft): DraftConversion {
  const gaps: string[] = [];

  if (draft.number.trim() === "") {
    gaps.push("Numer konkursu");
  }

  if (draft.title.trim() === "") {
    gaps.push("Tytuł konkursu");
  }

  if (draft.startDateLocal === "") {
    gaps.push("Rozpoczęcie naboru wniosków");
  }

  if (!draft.isContinuousIntake && draft.endDateLocal === "") {
    gaps.push(
      "Zakończenie naboru wniosków (albo zaznacz nabór ciągły)",
    );
  }

  if (draft.maxGrantAmount.trim() === "") {
    gaps.push("Maksymalna dotacja na jeden wniosek");
  }

  if (gaps.length > 0) {
    return { request: null, structuralGaps: gaps };
  }

  const request: CompetitionRequest = {
    number: draft.number.trim(),
    title: draft.title.trim(),
    description: emptyToNull(draft.description),
    startDate: localWarsawToUtcIso(draft.startDateLocal),
    endDate: draft.isContinuousIntake
      ? null
      : localWarsawToUtcIso(draft.endDateLocal),
    isContinuousIntake: draft.isContinuousIntake,
    maxGrantAmount: draft.maxGrantAmount.trim(),
    formDefinitionId: null,

    expectedResults: emptyToNull(draft.expectedResults),
    rulesUrl: emptyToNull(draft.rulesUrl),

    submissionNotice: emptyToNull(draft.submissionNotice),
    submissionEmailBody: emptyToNull(draft.submissionEmailBody),

    requiresPaperSubmission: draft.requiresPaperSubmission,
    paperSubmissionDeadline:
      draft.requiresPaperSubmission && draft.paperSubmissionDeadlineLocal !== ""
        ? localWarsawToUtcIso(draft.paperSubmissionDeadlineLocal)
        : null,
    paperSubmissionAddress: draft.requiresPaperSubmission
      ? emptyToNull(draft.paperSubmissionAddress)
      : null,

    projectStartDate: emptyToNull(draft.projectStartDate),
    projectEndDate: emptyToNull(draft.projectEndDate),
    totalPoolAmount: emptyToNull(draft.totalPoolAmount),
    minGrantAmount: emptyToNull(draft.minGrantAmount),
    maxIndirectCostPercent: emptyToNull(draft.maxIndirectCostPercent),
    maxInstitutionalDevelopmentPercent: emptyToNull(
      draft.maxInstitutionalDevelopmentPercent,
    ),
    percentageBasis: draft.percentageBasis,
    maxAverageAnnualRevenue: emptyToNull(draft.maxAverageAnnualRevenue),
    personalDataProcessedUntil: emptyToNull(draft.personalDataProcessedUntil),
    costCategories: draft.costCategories,

    maxAttachmentSizeInBytes: megabytesToBytes(
      draft.maxAttachmentSizeInMegabytes,
    ),
    maxApplicationSizeInBytes: megabytesToBytes(
      draft.maxApplicationSizeInMegabytes,
    ),
    attachments: draft.attachments.map((attachment) => ({
      title: attachment.title.trim(),
      description: emptyToNull(attachment.description),
      requirement: attachment.requirement,
      allowedFormats: attachment.allowedFormats,
    })),
    contactUserIds: draft.contactUserIds,
  };

  return { request, structuralGaps: [] };
}
