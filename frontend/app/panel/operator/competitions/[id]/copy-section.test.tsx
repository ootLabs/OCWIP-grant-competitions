import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";

import type { OperatorCompetition } from "@/lib/operator-competitions";

const push = vi.fn();
vi.mock("next/navigation", () => ({ useRouter: () => ({ push }) }));

import { CopySection } from "./copy-section";

const competition = { id: "c1", title: "Konkurs 2026", isContinuousIntake: false } as OperatorCompetition;

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  push.mockReset();
});

describe("CopySection", () => {
  it("sends the new number, title and Polish dates as UTC, then opens the copy", async () => {
    const calls: { url: string; init?: RequestInit }[] = [];
    vi.stubGlobal(
      "fetch",
      vi.fn().mockImplementation(async (url: string, init?: RequestInit) => {
        calls.push({ url, init });
        return new Response(JSON.stringify({ id: "c2" }), { status: 201 });
      }),
    );
    render(<CopySection competition={competition} />);

    fireEvent.change(screen.getByLabelText("Numer nowego konkursu"), { target: { value: "2/2027" } });
    fireEvent.change(screen.getByLabelText("Nazwa nowego konkursu"), { target: { value: "Konkurs 2027" } });
    fireEvent.change(screen.getByLabelText("Początek naboru"), { target: { value: "2027-03-01T08:00" } });
    fireEvent.change(screen.getByLabelText("Koniec naboru"), { target: { value: "2027-03-31T12:00" } });
    fireEvent.click(screen.getByRole("button", { name: "Skopiuj konkurs" }));

    await waitFor(() => expect(push).toHaveBeenCalledWith("/panel/operator/competitions/c2"));
    expect(calls[0].url).toContain("/competitions/c1/copy");
    expect(JSON.parse(String(calls[0].init!.body))).toEqual({
      number: "2/2027",
      title: "Konkurs 2027",
      // March, before the change: Warsaw is UTC+1.
      startDate: "2027-03-01T07:00:00.000Z",
      endDate: "2027-03-31T10:00:00.000Z",
      isContinuousIntake: false,
    });
  });

  it("shows the refusal and stays on the page", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockImplementation(async () =>
        new Response(JSON.stringify({ title: "Numer zajęty", detail: "Konkurs o tym numerze już istnieje." }), {
          status: 409,
          headers: { "Content-Type": "application/problem+json" },
        }),
      ),
    );
    render(<CopySection competition={competition} />);

    fireEvent.click(screen.getByRole("button", { name: "Skopiuj konkurs" }));

    expect(await screen.findByRole("alert")).toBeTruthy();
    expect(push).not.toHaveBeenCalled();
  });
});
