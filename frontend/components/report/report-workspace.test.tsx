import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";

import { reportFixture } from "@/lib/reports.fixtures";

import { ReportWorkspace } from "./report-workspace";

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("ReportWorkspace", () => {
  it("lists what is missing when the server refuses the submission", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify({ title: "x", errors: { przebieg: ["To pole jest wymagane."] } }), {
          status: 400,
          headers: { "Content-Type": "application/problem+json" },
        }),
      ),
    );
    const onSubmitted = vi.fn();

    render(<ReportWorkspace report={reportFixture()} onSubmitted={onSubmitted} />);

    fireEvent.click(screen.getByRole("button", { name: "Złóż sprawozdanie" }));
    await act(async () => {
      fireEvent.click(screen.getAllByRole("button", { name: "Złóż sprawozdanie", hidden: true }).at(-1)!);
    });

    expect(screen.getByRole("alert").textContent).toContain("To pole jest wymagane.");
    expect(onSubmitted).not.toHaveBeenCalled();
  });

  it("submits only after the confirmation", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify(reportFixture({ status: "Submitted", submittedAt: "2026-06-02T10:00:00Z" }))));
    vi.stubGlobal("fetch", fetchMock);
    const onSubmitted = vi.fn();

    render(<ReportWorkspace report={reportFixture()} onSubmitted={onSubmitted} />);

    fireEvent.click(screen.getByRole("button", { name: "Złóż sprawozdanie" }));
    expect(fetchMock).not.toHaveBeenCalled();
    await act(async () => {
      fireEvent.click(screen.getAllByRole("button", { name: "Złóż sprawozdanie", hidden: true }).at(-1)!);
    });

    expect(onSubmitted).toHaveBeenCalledWith(expect.objectContaining({ status: "Submitted" }));
  });
});
