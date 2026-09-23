import type { CompetitionDraft } from "./types";

/**
 * An empty competition draft, with the same defaults CompetitionRequest gets
 * on the backend when a field is left out (CompetitionConfiguration.cs,
 * Competition.cs), written out explicitly here so the operator sees the
 * limit that already applies instead of a blank field that looks unset.
 */
export function newDraft(): CompetitionDraft {
  return {
    number: "",
    title: "",
    startDateLocal: "",
    endDateLocal: "",
    isContinuousIntake: false,
    submissionNotice: "",

    description: "",
    expectedResults: "",
    rulesUrl: "",

    requiresPaperSubmission: false,
    paperSubmissionDeadlineLocal: "",
    paperSubmissionAddress: "",

    projectStartDate: "",
    projectEndDate: "",
    totalPoolAmount: "",
    minGrantAmount: "",
    maxGrantAmount: "",
    maxIndirectCostPercent: "",
    maxInstitutionalDevelopmentPercent: "",
    percentageBasis: "GrantAmount",
    maxAverageAnnualRevenue: "",
    personalDataProcessedUntil: "",
    costCategories: ["DirectCosts", "InstitutionalDevelopment", "IndirectCosts"],

    // Competition.DefaultMaxAttachmentSizeInBytes / DefaultMaxApplicationSizeInBytes.
    maxAttachmentSizeInMegabytes: "10",
    maxApplicationSizeInMegabytes: "50",
    attachments: [],

    contactUserIds: [],
    submissionEmailBody: "",
  };
}
