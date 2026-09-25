import { describe, expect, it } from "vitest";
import {
  defaultListView,
  exportUrl,
  requestedSum,
  visibleRows,
  type ApplicationListItem,
} from "./operator-applications";

function item(overrides: Partial<ApplicationListItem> & Pick<ApplicationListItem, "number">): ApplicationListItem {
  return {
    id: overrides.number,
    entityName: "Podmiot",
    entityType: "Organisation",
    projectTitle: "Tytuł",
    totalCost: 100,
    requestedGrant: 100,
    status: "Submitted",
    submittedAt: "2026-09-15T10:30:00Z",
    ...overrides,
  };
}

const numbers = (rows: ApplicationListItem[]) => rows.map((row) => row.number);

describe("visibleRows", () => {
  it("orders numbers by value, not as text", () => {
    const rows = visibleRows([item({ number: "1000" }), item({ number: "999" })], defaultListView);

    expect(numbers(rows)).toEqual(["999", "1000"]);
  });

  it("sorts names the Polish way and keeps blanks last in both directions", () => {
    const items = [
      item({ number: "001", projectTitle: "Żagle" }),
      item({ number: "002", projectTitle: null }),
      item({ number: "003", projectTitle: "Łódź" }),
      item({ number: "004", projectTitle: "Zabawa" }),
    ];

    const ascending = visibleRows(items, { ...defaultListView, sortKey: "projectTitle" });
    const descending = visibleRows(items, {
      ...defaultListView,
      sortKey: "projectTitle",
      descending: true,
    });

    expect(numbers(ascending)).toEqual(["003", "004", "001", "002"]);
    expect(numbers(descending)).toEqual(["001", "004", "003", "002"]);
  });

  it("sorts amounts that arrive as text by value", () => {
    const items = [
      item({ number: "001", requestedGrant: "9000.5" }),
      item({ number: "002", requestedGrant: 12000 }),
      item({ number: "003", requestedGrant: "800" }),
    ];

    const rows = visibleRows(items, { ...defaultListView, sortKey: "requestedGrant" });

    expect(numbers(rows)).toEqual(["003", "001", "002"]);
  });

  it("filters by the kind of applicant", () => {
    const items = [
      item({ number: "001", entityType: "InformalGroup" }),
      item({ number: "002", entityType: "Organisation" }),
    ];

    const rows = visibleRows(items, { ...defaultListView, entityType: "InformalGroup" });

    expect(numbers(rows)).toEqual(["001"]);
  });
});

describe("requestedSum", () => {
  it("adds what is asked for and skips rows that ask for nothing named", () => {
    expect(requestedSum([item({ number: "001", requestedGrant: "0.1" }), item({ number: "002", requestedGrant: null })])).toBeCloseTo(0.1);
  });
});

describe("exportUrl", () => {
  it("points at the API, not at the frontend", () => {
    expect(exportUrl("abc", "csv")).toMatch(/^http.*\/competitions\/abc\/applications\/export\/csv$/);
  });
});
