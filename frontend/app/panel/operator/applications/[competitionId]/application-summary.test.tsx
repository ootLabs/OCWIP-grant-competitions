import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";

import { ApplicationSummary } from "./application-summary";

afterEach(cleanup);

function item(id: string, overrides: Record<string, unknown> = {}) {
  return {
    id,
    number: id,
    entityName: "Stowarzyszenie",
    entityType: "Organisation" as const,
    projectTitle: "Projekt",
    totalCost: 1000,
    requestedGrant: 500,
    status: "Submitted" as const,
    submittedAt: "2026-09-15T10:30:00Z",
    formal: "NotStarted" as const,
    ...overrides,
  };
}

function term(name: string): string {
  const dt = screen.getByText(name);
  return dt.nextElementSibling?.textContent ?? "";
}

describe("ApplicationSummary", () => {
  it("counts the tiles from the same list the table shows", () => {
    render(
      <ApplicationSummary
        list={{
          competitionId: "c1",
          competitionNumber: "1/2026",
          competitionTitle: "Granty",
          totalPoolAmount: 100000,
          requestedTotal: 1500,
          poolRemaining: 98500,
          applications: [
            item("1", { formal: "Passed" }),
            item("2", { formal: "Failed", status: "Returned" }),
            item("3", { formal: "InProgress" }),
          ],
        }}
      />,
    );

    expect(term("Złożone wnioski")).toBe("3");
    expect(term("Ocena formalna zakończona")).toBe("2 z 3");
    expect(term("Zwrócone do poprawy")).toBe("1");
    expect(term("Wnioskowane razem")).toMatch(/1500,00\szł.*Pula konkursu: 100\s000,00\szł/);
  });

  it("says when the pool is not set rather than comparing with nothing", () => {
    render(
      <ApplicationSummary
        list={{
          competitionId: "c1",
          competitionNumber: "1/2026",
          competitionTitle: "Granty",
          totalPoolAmount: null,
          requestedTotal: 0,
          poolRemaining: null,
          applications: [],
        }}
      />,
    );

    expect(term("Ocena formalna zakończona")).toBe("0 z 0");
    expect(screen.getByText("Pula konkursu nie jest ustawiona.")).toBeDefined();
  });
});
