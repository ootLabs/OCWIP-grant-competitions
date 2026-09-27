import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen, within } from "@testing-library/react";

import { settledReportFixture } from "@/lib/reports.fixtures";

import { SettlementView } from "./settlement-view";

afterEach(cleanup);

describe("SettlementView", () => {
  it("shows the refund and each refused cost with its reason at its row", () => {
    const report = settledReportFixture();
    render(<SettlementView report={report} settlement={report.settlement!} />);

    expect(screen.getByText("Kwota do zwrotu").nextElementSibling?.textContent).toMatch(/150,00/);
    const row = screen.getByRole("row", { name: /Farba/ });
    expect(within(row).getByText("Faktura bez opisu.")).toBeDefined();
    expect(screen.queryByRole("row", { name: /Deski/ })).toBeNull();
    expect(screen.getByText(/Wyliczenie wstępne/)).toBeDefined();
  });

  it("says when every cost is accepted and the count is final", () => {
    const report = settledReportFixture(
      { status: "Accepted", acceptedAt: "2026-06-03T10:00:00Z" },
      { refused: 0, accepted: 1490, refund: 110, rows: [{ row: 0, spent: 1400, refused: 0, reason: null }] },
    );
    render(<SettlementView report={report} settlement={report.settlement!} />);

    expect(screen.getByText("Wszystkie koszty są uznane.")).toBeDefined();
    expect(screen.queryByText(/Wyliczenie wstępne/)).toBeNull();
  });
});
