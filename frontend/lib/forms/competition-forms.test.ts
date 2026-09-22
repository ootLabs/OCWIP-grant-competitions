import { beforeEach, describe, expect, it, vi } from "vitest";

import {
  competitionsWithForms,
  fetchCurrentFormDocument,
  fetchOperatorCompetitions,
  type CompetitionSummary,
} from "./competition-forms";

function answer(status: number, body: unknown = null) {
  return {
    ok: status >= 200 && status < 300,
    status,
    headers: new Headers(),
    text: async () => (body === null ? "" : JSON.stringify(body)),
  } as unknown as Response;
}

function competition(overrides: Partial<CompetitionSummary>): CompetitionSummary {
  return {
    id: "c1",
    number: "1/2026",
    title: "Konkurs",
    description: null,
    status: "Draft",
    allowedTransitions: [],
    intake: {
      acceptsApplications: false,
      state: "Unavailable",
      opensAt: "2026-01-01T00:00:00Z",
      closesAt: null,
      message: "",
    },
    startDate: "2026-01-01T00:00:00Z",
    endDate: null,
    isContinuousIntake: false,
    maxGrantAmount: 1000,
    formDefinitionId: null,
    publishedAt: null,
    isActive: true,
    createdAt: "2026-01-01T00:00:00Z",
    updatedAt: "2026-01-01T00:00:00Z",
    expectedResults: null,
    rulesUrl: null,
    submissionNotice: null,
    submissionEmailBody: null,
    requiresPaperSubmission: false,
    paperSubmissionDeadline: null,
    paperSubmissionAddress: null,
    projectStartDate: null,
    projectEndDate: null,
    totalPoolAmount: null,
    minGrantAmount: null,
    maxIndirectCostPercent: null,
    maxInstitutionalDevelopmentPercent: null,
    percentageBasis: "GrantAmount",
    maxAverageAnnualRevenue: null,
    personalDataProcessedUntil: null,
    costCategories: [],
    maxAttachmentSizeInBytes: 0,
    maxApplicationSizeInBytes: 0,
    attachments: [],
    contacts: [],
    ...overrides,
  };
}

const fetchMock = vi.fn();

beforeEach(() => {
  fetchMock.mockReset();
  vi.stubGlobal("fetch", fetchMock);
});

describe("fetchOperatorCompetitions", () => {
  it("reads every competition, not only the published ones", async () => {
    fetchMock.mockResolvedValue(answer(200, [competition({ id: "a" })]));

    const competitions = await fetchOperatorCompetitions();

    expect(fetchMock.mock.calls[0][0]).toContain("/competitions");
    expect(competitions).toHaveLength(1);
  });
});

describe("competitionsWithForms", () => {
  it("keeps only competitions that carry a current form version", () => {
    const withForm = competition({ id: "a", formDefinitionId: "f1" });
    const withoutForm = competition({ id: "b", formDefinitionId: null });

    expect(competitionsWithForms([withForm, withoutForm])).toEqual([withForm]);
  });
});

describe("fetchCurrentFormDocument", () => {
  it("returns null when the competition has no form definition yet", async () => {
    fetchMock.mockResolvedValueOnce(answer(200, []));

    await expect(fetchCurrentFormDocument("c1")).resolves.toBeNull();
  });

  it("fetches the version marked current, by its version number", async () => {
    const document = { schemaVersion: 1, sections: [] };
    fetchMock
      .mockResolvedValueOnce(
        answer(200, [
          { id: "v1", competitionId: "c1", versionNumber: 1, isCurrent: false, createdAt: "2026-01-01T00:00:00Z" },
          { id: "v2", competitionId: "c1", versionNumber: 2, isCurrent: true, createdAt: "2026-01-02T00:00:00Z" },
        ]),
      )
      .mockResolvedValueOnce(
        answer(200, {
          id: "v2",
          competitionId: "c1",
          versionNumber: 2,
          definition: document,
          isCurrent: true,
          createdAt: "2026-01-02T00:00:00Z",
        }),
      );

    const result = await fetchCurrentFormDocument("c1");

    expect(result).toEqual(document);
    expect(fetchMock.mock.calls[1][0]).toMatch(/\/form-definitions\/2$/);
  });
});
