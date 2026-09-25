import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, within } from "@testing-library/react";
import ReviewerHome from "./page";

function respondWith(body: unknown, status = 200) {
  vi.stubGlobal(
    "fetch",
    vi.fn().mockImplementation(async () => new Response(JSON.stringify(body), { status })),
  );
}

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("ReviewerHome", () => {
  it("lists the assigned applications with the expert's own card and the three sums", async () => {
    respondWith({
      competitions: [
        {
          competitionId: "c1",
          number: "1/2026",
          title: "Kierunek NOWE FIO 2026",
          totalPoolAmount: 100000,
          requestedTotal: 14000,
          recommendedTotal: 6000,
          applications: [
            {
              applicationId: "a1",
              number: "001",
              entityType: "Organisation",
              projectTitle: "Sąsiedzka biblioteka",
              requestedGrant: 7000,
              card: "Draft",
              evaluationId: "e1",
              recommendedGrant: 6000,
            },
          ],
        },
      ],
    });

    render(<ReviewerHome />);

    const link = await screen.findByRole("link", { name: "001" });
    expect(link.getAttribute("href")).toBe("/panel/reviewer/applications/a1");

    const row = link.closest("tr")!;
    expect(within(row).getByText("W toku")).toBeDefined();
    expect(within(row).getByText("Organizacja")).toBeDefined();

    expect(screen.getByText("Twoje rekomendacje razem")).toBeDefined();
    expect(screen.getByText("Pula konkursu")).toBeDefined();
  });

  it("says where the applications come from when there are none yet", async () => {
    respondWith({ competitions: [] });

    render(<ReviewerHome />);

    expect(await screen.findByText("Nie masz jeszcze wniosków do oceny")).toBeDefined();
  });
});
