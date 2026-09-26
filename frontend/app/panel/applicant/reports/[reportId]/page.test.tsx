import { Suspense } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, render, screen } from "@testing-library/react";

import { reportFixture } from "@/lib/reports.fixtures";

import ApplicantReportPage from "./page";

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

async function renderWith(report: ReturnType<typeof reportFixture>) {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(JSON.stringify(report))));
  await act(async () => {
    render(
      <Suspense fallback={null}>
        <ApplicantReportPage params={Promise.resolve({ reportId: "r1" })} />
      </Suspense>,
    );
  });
}

describe("ApplicantReportPage", () => {
  it("puts the return reason on top of an editable report", async () => {
    await renderWith(reportFixture({ status: "Returned", submittedAt: "2026-06-02T10:00:00Z", returnReason: "Brakuje opisu promocji." }));

    expect(await screen.findByText("Brakuje opisu promocji.")).toBeDefined();
    expect(screen.getByRole("button", { name: "Złóż sprawozdanie" })).toBeDefined();
  });

  it("shows a submitted report read only", async () => {
    await renderWith(reportFixture({ status: "Submitted", submittedAt: "2026-06-02T10:00:00Z" }));

    expect(await screen.findByText(/Czeka na sprawdzenie przez operatora/)).toBeDefined();
    expect(screen.queryByRole("button", { name: "Złóż sprawozdanie" })).toBeNull();
    expect(screen.getByText("Zbudowaliśmy ławki.")).toBeDefined();
  });
});
