import type { Report } from "./reports";

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
    ...overrides,
  } as Report;
}
