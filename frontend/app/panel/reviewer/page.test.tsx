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
          declaration: "Accepted",
          assignedCount: 1,
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

  it("shows the declaration and how many applications wait, never the applications, before it is accepted", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockImplementation(async (url: string) =>
        String(url).includes("/declaration")
          ? new Response(
              JSON.stringify({
                competitionId: "c1",
                status: "NotDecided",
                text: "Oświadczam, że nie jestem związany z wnioskodawcami.",
                refusalReason: null,
                decidedAt: null,
              }),
              { status: 200 },
            )
          : new Response(
              JSON.stringify({
                competitions: [
                  {
                    competitionId: "c1",
                    number: "1/2026",
                    title: "Kierunek NOWE FIO 2026",
                    declaration: "NotDecided",
                    assignedCount: 3,
                    totalPoolAmount: 100000,
                    requestedTotal: 0,
                    recommendedTotal: 0,
                    applications: [],
                  },
                ],
              }),
              { status: 200 },
            ),
      ),
    );

    render(<ReviewerHome />);

    expect(await screen.findByText(/Oświadczam, że nie jestem związany/)).toBeDefined();
    expect(screen.getByText(/Masz przydzielonych wniosków do oceny: 3/)).toBeDefined();
    expect(screen.getByRole("button", { name: "Składam deklarację" })).toBeDefined();
    expect(screen.queryByRole("table")).toBeNull();
  });

  it("says where the applications come from when there are none yet", async () => {
    respondWith({ competitions: [] });

    render(<ReviewerHome />);

    expect(await screen.findByText("Nie masz jeszcze wniosków do oceny")).toBeDefined();
  });
});
