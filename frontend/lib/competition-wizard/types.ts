/**
 * The competition wizard's own working shape (T-22), separate from
 * CompetitionRequest.
 *
 * Every field is a plain string, exactly as an input gives it back: a date
 * picker's "YYYY-MM-DD", a datetime-local's "YYYY-MM-DDTHH:mm" in the
 * operator's own local reading, a number input's decimal text. Nothing here
 * is parsed or converted while the operator is still typing, which is what
 * lets "walidacja nie blokuje przechodzenia między krokami" (proces.md,
 * ścieżka 1) hold: a half filled step is just a draft with some empty
 * strings in it, never an invalid value. Conversion into CompetitionRequest
 * happens once, in to-request.ts, when the draft is actually sent.
 */

import type {
  AllowedFileFormat,
  AttachmentRequirement,
  CostCategory,
} from "@/lib/competitions";
import type { components } from "@/lib/api-schema";

export type PercentageBasis = components["schemas"]["PercentageBasis"];

export const WIZARD_STEPS = [
  "basics",
  "description",
  "paper",
  "limits",
  "attachments",
  "contacts",
  "summary",
] as const;

export type WizardStepId = (typeof WIZARD_STEPS)[number];

export const STEP_LABELS: Record<WizardStepId, string> = {
  basics: "1.1 Dane konkursu",
  description: "1.2 Opis konkursu",
  paper: "1.3 Forma dostarczenia",
  limits: "1.4 Limity",
  attachments: "1.5 Załączniki do oferty",
  contacts: "1.6 Osoby kontaktowe",
  summary: "1.7 Podsumowanie",
};

export interface AttachmentDraft {
  title: string;
  description: string;
  requirement: AttachmentRequirement;
  allowedFormats: AllowedFileFormat[];
}

export interface CompetitionDraft {
  // 1.1 Dane konkursu
  number: string;
  title: string;
  startDateLocal: string;
  endDateLocal: string;
  isContinuousIntake: boolean;
  submissionNotice: string;

  // 1.2 Opis konkursu
  description: string;
  expectedResults: string;
  rulesUrl: string;

  // 1.3 Forma dostarczenia
  requiresPaperSubmission: boolean;
  paperSubmissionDeadlineLocal: string;
  paperSubmissionAddress: string;

  // 1.4 Limity
  projectStartDate: string;
  projectEndDate: string;
  totalPoolAmount: string;
  minGrantAmount: string;
  maxGrantAmount: string;
  maxIndirectCostPercent: string;
  maxInstitutionalDevelopmentPercent: string;
  percentageBasis: PercentageBasis;
  maxAverageAnnualRevenue: string;
  personalDataProcessedUntil: string;
  costCategories: CostCategory[];

  // 1.5 Załączniki do oferty
  maxAttachmentSizeInMegabytes: string;
  maxApplicationSizeInMegabytes: string;
  attachments: AttachmentDraft[];

  // 1.6 Osoby kontaktowe
  contactUserIds: string[];
  submissionEmailBody: string;
}

/**
 * What the wizard has saved to the backend so far, alongside the draft. Null
 * until the first successful save, at which point the wizard is editing a
 * real row rather than only a local one: see draft-storage.ts and
 * lib/operator-competitions.ts.
 */
export interface SavedCompetition {
  id: string;
}
