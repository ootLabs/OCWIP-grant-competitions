import { describe, expect, it } from "vitest";

import type { OperatorCompetition } from "@/lib/operator-competitions";

import { fromCompetition } from "./from-competition";
import { toCompetitionRequest } from "./to-request";

const saved = {
  id: "c1",
  number: "3/2026",
  title: "Inicjatywy lokalne",
  description: "Opis",
  status: "Draft",
  // Both sides of the October change: +02:00 before, +01:00 after.
  startDate: "2026-10-01T06:00:00Z",
  endDate: "2026-10-30T11:00:00Z",
  isContinuousIntake: false,
  maxGrantAmount: "7000.50",
  submissionNotice: null,
  expectedResults: null,
  rulesUrl: null,
  requiresPaperSubmission: true,
  paperSubmissionDeadline: "2026-11-02T14:30:00Z",
  paperSubmissionAddress: "ul. Kościuszki 1, Opole",
  projectStartDate: "2027-01-01",
  projectEndDate: "2027-06-30",
  totalPoolAmount: 140000,
  minGrantAmount: null,
  maxIndirectCostPercent: 10,
  maxInstitutionalDevelopmentPercent: null,
  percentageBasis: "GrantAmount",
  maxAverageAnnualRevenue: null,
  personalDataProcessedUntil: null,
  costCategories: ["DirectCosts"],
  maxAttachmentSizeInBytes: 10 * 1024 * 1024,
  maxApplicationSizeInBytes: 50 * 1024 * 1024,
  attachments: [{ id: "a1", title: "Statut", description: null, requirement: "Required", allowedFormats: ["Pdf"] }],
  contacts: [{ userId: "u1", name: "Anna", email: "anna@example.org" }],
  submissionEmailBody: "Dziękujemy",
} as unknown as OperatorCompetition;

describe("fromCompetition", () => {
  it("shows the saved instants as the Warsaw wall clock, on either side of the time change", () => {
    const draft = fromCompetition(saved);

    expect(draft.startDateLocal).toBe("2026-10-01T08:00");
    expect(draft.endDateLocal).toBe("2026-10-30T12:00");
    expect(draft.paperSubmissionDeadlineLocal).toBe("2026-11-02T15:30");
    expect(draft.maxAttachmentSizeInMegabytes).toBe("10");
    expect(draft.contactUserIds).toEqual(["u1"]);
  });

  it("round trips: saving an opened competition unchanged sends what is stored", () => {
    const { request } = toCompetitionRequest(fromCompetition(saved));

    expect(request).not.toBeNull();
    expect(new Date(request!.startDate).toISOString()).toBe(new Date(saved.startDate).toISOString());
    expect(new Date(request!.endDate!).toISOString()).toBe(new Date(saved.endDate!).toISOString());
    expect(new Date(request!.paperSubmissionDeadline!).toISOString()).toBe(
      new Date(saved.paperSubmissionDeadline!).toISOString(),
    );
    expect(request!.maxGrantAmount).toBe("7000.50");
    expect(request!.totalPoolAmount).toBe("140000");
    expect(request!.maxAttachmentSizeInBytes).toBe(saved.maxAttachmentSizeInBytes);
    expect(request!.attachments).toEqual([
      { title: "Statut", description: null, requirement: "Required", allowedFormats: ["Pdf"] },
    ]);
  });
});
