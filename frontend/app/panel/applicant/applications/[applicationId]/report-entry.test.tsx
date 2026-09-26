import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";

import { reportFixture } from "@/lib/reports.fixtures";

import { ReportEntry } from "./report-entry";

const push = vi.fn();
vi.mock("next/navigation", () => ({ useRouter: () => ({ push }) }));

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  push.mockReset();
});

describe("ReportEntry", () => {
  it("opens the report started or already there", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(JSON.stringify(reportFixture({ id: "r9" })), { status: 201 })));

    render(<ReportEntry applicationId="a1" />);
    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "Przejdź do sprawozdania" }));
    });

    expect(push).toHaveBeenCalledWith("/panel/applicant/reports/r9");
  });

  it("says why when the competition has no report form yet", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify({ title: "Konkurs nie ma jeszcze opublikowanego wzoru sprawozdania." }), {
          status: 409,
          headers: { "Content-Type": "application/problem+json" },
        }),
      ),
    );

    render(<ReportEntry applicationId="a1" />);
    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "Przejdź do sprawozdania" }));
    });

    expect(screen.getByRole("alert")).toBeDefined();
    expect(push).not.toHaveBeenCalled();
  });
});
