import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";

import { settledReportFixture } from "@/lib/reports.fixtures";

import { CostReviewForm } from "./cost-review-form";

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("CostReviewForm", () => {
  it("sends only the refused rows, with a decimal comma read as a point", async () => {
    const report = settledReportFixture();
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify(report)));
    vi.stubGlobal("fetch", fetchMock);
    const onChange = vi.fn();
    render(<CostReviewForm report={report} settlement={report.settlement!} onChange={onChange} />);

    expect((screen.getByLabelText("Kwota nieuznana: Farba") as HTMLInputElement).value).toBe("40");
    fireEvent.change(screen.getByLabelText("Kwota nieuznana: Deski"), { target: { value: "100,50" } });
    fireEvent.change(screen.getByLabelText("Powód nieuznania: Deski"), { target: { value: "Ponad plan." } });
    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "Zapisz ocenę kosztów" }));
    });

    const [input, init] = fetchMock.mock.calls[0]!;
    expect(String(input)).toMatch(/\/reports\/r1\/cost-review$/);
    expect(JSON.parse((init as RequestInit).body as string)).toEqual({
      items: [
        { row: 0, refused: 100.5, reason: "Ponad plan." },
        { row: 1, refused: 40, reason: "Faktura bez opisu." },
      ],
    });
    expect(onChange).toHaveBeenCalled();
    expect(screen.getByRole("status").textContent).toBe("Ocena kosztów zapisana.");
  });

  it("refuses an amount that is not a number without asking the server", async () => {
    const report = settledReportFixture();
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);
    render(<CostReviewForm report={report} settlement={report.settlement!} onChange={vi.fn()} />);

    fireEvent.change(screen.getByLabelText("Kwota nieuznana: Deski"), { target: { value: "sto" } });
    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "Zapisz ocenę kosztów" }));
    });

    expect(fetchMock).not.toHaveBeenCalled();
    expect(screen.getByRole("alert").textContent).toMatch(/musi być liczbą/);
  });
});
