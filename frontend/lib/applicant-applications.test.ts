import { afterEach, describe, expect, it, vi } from "vitest";
import { confirmationPdfUrl, fetchApplicationForm, limitSettingsFrom } from "./applicant-applications";
import type { PublicCompetition } from "./competitions";

function competition(overrides: Partial<PublicCompetition> = {}): PublicCompetition {
  return {
    id: "c1",
    number: "1/2026",
    title: "Konkurs testowy",
    description: null,
    status: "OpenForApplications",
    intake: {
      acceptsApplications: true,
      state: "Open",
      opensAt: "2026-09-01T08:00:00Z",
      closesAt: "2026-10-25T10:00:00Z",
      message: "Nabór trwa.",
    },
    startDate: "2026-09-01T08:00:00Z",
    endDate: "2026-10-25T10:00:00Z",
    isContinuousIntake: false,
    maxGrantAmount: 15000,
    expectedResults: null,
    rulesUrl: null,
    requiresPaperSubmission: false,
    paperSubmissionDeadline: null,
    paperSubmissionAddress: null,
    projectStartDate: null,
    projectEndDate: null,
    totalPoolAmount: 120000,
    minGrantAmount: 2000,
    maxIndirectCostPercent: 20,
    maxInstitutionalDevelopmentPercent: null,
    percentageBasis: "GrantAmount",
    maxAverageAnnualRevenue: null,
    personalDataProcessedUntil: null,
    costCategories: ["DirectCosts", "IndirectCosts"],
    maxAttachmentSizeInBytes: 10 * 1024 * 1024,
    maxApplicationSizeInBytes: 50 * 1024 * 1024,
    attachments: [],
    contacts: [],
    ...overrides,
  };
}

describe("limitSettingsFrom", () => {
  it("reads the six basis settings straight off the public competition", () => {
    const settings = limitSettingsFrom(
      competition({
        maxGrantAmount: 9000,
        minGrantAmount: "500",
        totalPoolAmount: 50000,
        maxIndirectCostPercent: 10,
        maxInstitutionalDevelopmentPercent: null,
        maxAverageAnnualRevenue: 200000,
      }),
    );

    expect(settings).toEqual({
      maxGrantAmount: 9000,
      minGrantAmount: 500,
      totalPoolAmount: 50000,
      maxIndirectCostPercent: 10,
      maxInstitutionalDevelopmentPercent: undefined,
      maxAverageAnnualRevenue: 200000,
    });
  });
});

describe("confirmationPdfUrl", () => {
  it("points at the API, not at the frontend", () => {
    expect(confirmationPdfUrl("abc")).toMatch(/^http.*\/applications\/abc\/confirmation$/);
  });
});

describe("fetchApplicationForm", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("coerces the OpenAPI number-or-string version into a plain number", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({
            versionNumber: "2",
            definition: { schemaVersion: 1, sections: [] },
          }),
          { status: 200 },
        ),
      ),
    );

    const form = await fetchApplicationForm("abc");

    expect(form.versionNumber).toBe(2);
    expect(form.document).toEqual({ schemaVersion: 1, sections: [] });
  });
});
