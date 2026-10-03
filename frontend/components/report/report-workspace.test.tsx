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

  // The button used to be clickable with a section already marked "(są
  // błędy)", so the refusal came after a confirmation dialog, two clicks
  // later; the application has had the list under a disabled button all
  // along (B-GUI-18).
  it("waits with the submission until nothing is missing and names what is", () => {
    const empty = reportFixture({ answers: { przebieg: "" } });

    render(<ReportWorkspace report={empty} onSubmitted={vi.fn()} />);

    expect((screen.getByRole("button", { name: "Złóż sprawozdanie" }) as HTMLButtonElement).disabled).toBe(true);
    const gap = screen.getByRole("button", { name: /Przebieg projektu/ });
    expect(gap.textContent).toContain("Przebieg");

    // The gap leads to its field, with the keyboard on the input.
    fireEvent.click(gap);
    expect(document.activeElement?.tagName.toLowerCase()).toBe("textarea");

    fireEvent.change(screen.getByLabelText(/Przebieg projektu/), {
      target: { value: "Zbudowaliśmy ławki." },
    });

    expect((screen.getByRole("button", { name: "Złóż sprawozdanie" }) as HTMLButtonElement).disabled).toBe(false);
    expect(screen.queryByText("Zanim złożysz sprawozdanie, uzupełnij:")).toBeNull();
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
