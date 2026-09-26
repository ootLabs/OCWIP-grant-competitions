import { Suspense } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";

import { reportFixture } from "@/lib/reports.fixtures";

import OperatorReportPage from "./page";

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

function serve() {
  const fetchMock = vi.fn().mockImplementation(async (input: string, init?: RequestInit) => {
    const path = new URL(String(input), "http://localhost").pathname;
    if (path.endsWith("/accept")) return new Response(JSON.stringify(reportFixture({ status: "Accepted", submittedAt: "2026-06-02T10:00:00Z", acceptedAt: "2026-06-03T10:00:00Z" })));
    if (path.endsWith("/return")) {
      const reason = JSON.parse(init!.body as string).reason as string;
      return new Response(JSON.stringify(reportFixture({ status: "Returned", submittedAt: "2026-06-02T10:00:00Z", returnReason: reason })));
    }
    return new Response(JSON.stringify(reportFixture({ status: "Submitted", submittedAt: "2026-06-02T10:00:00Z" })));
  });
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

async function renderPage() {
  await act(async () => {
    render(
      <Suspense fallback={null}>
        <OperatorReportPage params={Promise.resolve({ competitionId: "c1", reportId: "r1" })} />
      </Suspense>,
    );
  });
}

describe("OperatorReportPage", () => {
  it("accepts a submitted report after the confirmation", async () => {
    serve();
    await renderPage();

    fireEvent.click(await screen.findByRole("button", { name: "Przyjmij sprawozdanie" }));
    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "Przyjmij", hidden: true }));
    });

    expect(await screen.findByText(/Stan: Przyjęte/)).toBeDefined();
    expect(screen.queryByRole("button", { name: "Przyjmij sprawozdanie" })).toBeNull();
  });

  it("sends back only with a reason, and sends that reason", async () => {
    const fetchMock = serve();
    await renderPage();

    const back = await screen.findByRole("button", { name: "Zwróć do poprawy" });
    expect((back as HTMLButtonElement).disabled).toBe(true);
    fireEvent.change(screen.getByLabelText(/Powód zwrotu/), { target: { value: "Brakuje opisu promocji." } });
    fireEvent.click(back);
    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "Zwróć", hidden: true }));
    });

    const call = fetchMock.mock.calls.find(([input]) => String(input).endsWith("/return"))!;
    expect(JSON.parse((call[1] as RequestInit).body as string)).toEqual({ reason: "Brakuje opisu promocji." });
    expect(await screen.findByText("Powód zwrotu: Brakuje opisu promocji.")).toBeDefined();
  });
});
