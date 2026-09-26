import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";

import { ReportsList } from "./reports-list";

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("ReportsList", () => {
  it("leads from each report to its own screen", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify([
          { id: "r1", applicationId: "a1", applicationNumber: "1/2026/1", entityName: "Stowarzyszenie Łąka", status: "Submitted", submittedAt: "2026-06-02T10:00:00Z" },
        ])),
      ),
    );

    render(<ReportsList competitionId="c1" />);

    const link = await screen.findByRole("link", { name: "1/2026/1" });
    expect(link.getAttribute("href")).toBe("/panel/operator/evaluation/c1/reports/r1");
    expect(screen.getByText("Złożone, czeka na sprawdzenie")).toBeDefined();
  });

  it("says where reports come from when there is none", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response("[]")));

    render(<ReportsList competitionId="c1" />);

    expect(await screen.findByText(/Nikt jeszcze nie zaczął sprawozdania/)).toBeDefined();
  });
});
