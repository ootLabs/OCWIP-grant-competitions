import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";

import { ResignationPanel } from "./resignation-panel";

const state = {
  competitionId: "c1",
  resultsApprovedAt: "2026-10-01T10:00:00Z",
  contractDeadline: "2026-10-15T10:00:00Z",
  totalPool: 10000,
  awardedTotal: 6500,
  freePool: 3500,
  unsigned: [
    {
      applicationId: "a1",
      number: "001",
      entityName: "Stowarzyszenie A",
      awardedGrant: 6500,
      overdue: true,
      deadline: "2026-10-15T10:00:00Z",
    },
  ],
  nextReserve: {
    applicationId: "a2",
    rank: 2,
    number: "002",
    entityName: "Fundacja B",
    requestedGrant: 5000,
    proposedGrant: 3500,
  },
};

function stubFetch(mailSent = true, loaded: object = state) {
  const calls: { url: string; init?: RequestInit }[] = [];
  vi.stubGlobal(
    "fetch",
    vi.fn().mockImplementation(async (url: string, init?: RequestInit) => {
      calls.push({ url, init });
      return init?.method === "POST"
        ? new Response(JSON.stringify({ mailSent }), { status: 200 })
        : new Response(JSON.stringify(loaded), { status: 200 });
    }),
  );
  return calls;
}

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("ResignationPanel", () => {
  it("marks an overdue contract and confirms its resignation", async () => {
    const calls = stubFetch();
    const onChange = vi.fn();
    render(<ResignationPanel competitionId="c1" onChange={onChange} />);

    expect(await screen.findByText(/termin minął/)).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Potwierdź rezygnację 001" }));

    await waitFor(() => expect(onChange).toHaveBeenCalled());
    expect(calls.some((call) => call.init?.method === "POST" && call.url.includes("/applications/a1/resignation"))).toBe(true);
  });

  it("says when the change is stored but the applicant's mail did not go out", async () => {
    stubFetch(false);
    render(<ResignationPanel competitionId="c1" onChange={vi.fn()} />);

    fireEvent.click(await screen.findByRole("button", { name: "Potwierdź rezygnację 001" }));

    expect((await screen.findByRole("status")).textContent).toContain("mail do wnioskodawcy nie wyszedł");
  });

  it("shows the own deadline of an application funded from the reserve list", async () => {
    stubFetch(true, {
      ...state,
      unsigned: [{ ...state.unsigned[0], overdue: false, deadline: "2026-10-30T10:00:00Z" }],
    });
    render(<ResignationPanel competitionId="c1" onChange={vi.fn()} />);

    expect(await screen.findByText(/z listy rezerwowej, termin/)).toBeTruthy();
  });

  it("proposes the next reserve application with what the pool can give, and sends the amount", async () => {
    const calls = stubFetch();
    render(<ResignationPanel competitionId="c1" onChange={vi.fn()} />);

    const input = (await screen.findByLabelText("Kwota dotacji z listy rezerwowej")) as HTMLInputElement;
    expect(input.value).toBe("3500");
    fireEvent.change(input, { target: { value: "3000" } });
    fireEvent.click(screen.getByRole("button", { name: "Przyznaj dofinansowanie 002" }));

    await waitFor(() =>
      expect(calls.some((call) => call.init?.method === "POST" && call.url.includes("/applications/a2/promotion"))).toBe(true),
    );
    const post = calls.find((call) => call.url.includes("/promotion"))!;
    expect(JSON.parse(String(post.init!.body))).toEqual({ awardedGrant: 3000 });
  });
});
