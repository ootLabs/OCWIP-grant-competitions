import type { Report, ReportSettlement } from "./reports";

/** One report as the API sends it, for the tests of the report screens. */
export function reportFixture(overrides: Partial<Report> = {}): Report {
  return {
    id: "r1",
    applicationId: "a1",
    competitionId: "c1",
    applicationNumber: "1/2026/1",
    entityName: "Stowarzyszenie Łąka",
    applicantType: "Organisation",
    formVersion: 1,
    formDefinition: {
      schemaVersion: 1,
      sections: [
        {
          key: "s",
          title: "Przebieg",
          description: "",
          fields: [{ key: "przebieg", type: "longText", label: "Przebieg projektu", help: "", required: true, printed: true, maxLength: 2000 }],
        },
      ],
    },
    answers: { przebieg: "Zbudowaliśmy ławki." },
    status: "Draft",
    submittedAt: null,
    returnReason: null,
    acceptedAt: null,
    updatedAt: "2026-06-01T10:00:00Z",
    settlement: null,
    ...overrides,
  } as Report;
}

/**
 * A two row budget settled against a grant of 1600: 1490 spent, 40 of the
 * paint refused. The answers name the rows the way costRowLabel reads them.
 */
export function settledReportFixture(overrides: Partial<Report> = {}, settlement: Partial<ReportSettlement> = {}): Report {
  return reportFixture({
    status: "Submitted",
    submittedAt: "2026-06-02T10:00:00Z",
    answers: { przebieg: "Zbudowaliśmy ławki.", budzet: [{ pozycja: "Deski", wykonana: 1400 }, { pozycja: "Farba", wykonana: 90 }] },
    settlement: {
      budgetKey: "budzet",
      awardedGrant: 1600,
      grantSpent: 1490,
      refused: 40,
      accepted: 1450,
      refund: 150,
      rows: [
        { row: 0, spent: 1400, refused: 0, reason: null },
        { row: 1, spent: 90, refused: 40, reason: "Faktura bez opisu." },
      ],
      ...settlement,
    },
    ...overrides,
  });
}
