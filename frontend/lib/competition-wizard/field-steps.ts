/**
 * Which wizard step owns a CompetitionRequestValidator field key, so a
 * backend error lands next to the input it is about instead of in a generic
 * banner (T-22).
 *
 * The keys are exactly the ones the validator writes (see
 * CompetitionRequestValidator.cs): flat names, plus `attachments[N].field`
 * for the indexed list. Kept as one explicit map rather than a naming
 * convention, so a field the backend renames stops compiling here instead of
 * silently landing on "summary".
 */
import type { WizardStepId } from "./types";

const FIELD_STEPS: Record<string, WizardStepId> = {
  number: "basics",
  title: "basics",
  startDate: "basics",
  endDate: "basics",
  isContinuousIntake: "basics",
  submissionNotice: "basics",

  description: "description",
  expectedResults: "description",
  rulesUrl: "description",

  requiresPaperSubmission: "paper",
  paperSubmissionDeadline: "paper",
  paperSubmissionAddress: "paper",

  projectStartDate: "limits",
  projectEndDate: "limits",
  totalPoolAmount: "limits",
  minGrantAmount: "limits",
  maxGrantAmount: "limits",
  maxIndirectCostPercent: "limits",
  maxInstitutionalDevelopmentPercent: "limits",
  percentageBasis: "limits",
  maxAverageAnnualRevenue: "limits",
  personalDataProcessedUntil: "limits",
  costCategories: "limits",

  maxAttachmentSizeInBytes: "attachments",
  maxApplicationSizeInBytes: "attachments",

  contactUserIds: "contacts",
  submissionEmailBody: "contacts",
};

export function stepForField(field: string): WizardStepId {
  if (field.startsWith("attachments")) {
    return "attachments";
  }

  return FIELD_STEPS[field] ?? "summary";
}

/**
 * Every step a set of backend field errors touches, in wizard order, so the
 * status bar can say "sprawdź kroki: 1.1, 1.4" instead of one at a time.
 */
export function stepsForFields(
  fields: Iterable<string>,
): WizardStepId[] {
  const steps = new Set<WizardStepId>();
  for (const field of fields) {
    steps.add(stepForField(field));
  }

  return Array.from(steps);
}
