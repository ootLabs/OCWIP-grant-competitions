import { Suspense } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";

import CompetitionEvaluationPage from "./page";

const settings = {
  evaluatorsPerApplication: 2,
  scoreAggregation: "Sum",
  meritThreshold: 50,
  thresholdIncludesStrategic: false,
  divergenceThresholdPercent: null,
};

const bodies: Record<string, unknown> = {
  "/competitions/c1/ranking": { competitionId: "c1", settings, rows: [] },
  "/competitions/c1/evaluation-settings": settings,
  "/competitions/c1/declarations": [
    {
      reviewerId: "r1",
      reviewerName: "Anna Ekspert",
      email: "anna@example.org",
      status: "Refused",
      refusalReason: "Znam wnioskodawcę",
      decidedAt: null,
    },
  ],
  "/reviewers": [
    { id: "r1", name: "Anna Ekspert", email: "anna@example.org" },
    { id: "r2", name: "Jan Wolny", email: "jan@example.org" },
  ],
  "/competitions/c1/assignments": [{ applicationId: "a1", reviewerId: "r1" }],
};

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

async function renderPage() {
  await act(async () => {
    render(
      <Suspense fallback={null}>
        <CompetitionEvaluationPage
          params={Promise.resolve({ competitionId: "c1" })}
        />
      </Suspense>,
    );
  });
}

describe("CompetitionEvaluationPage", () => {
  it("brings the settings, the declarations and the ranking onto one screen", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockImplementation(async (input: string) => {
        const path = new URL(
          String(input),
          "http://localhost",
        ).pathname.replace(/^\/api/, "");
        return new Response(JSON.stringify(bodies[path] ?? null), {
          status: path in bodies ? 200 : 404,
        });
      }),
    );

    await renderPage();

    expect(
      await screen.findByRole("heading", { name: "Ustawienia oceny" }),
    ).toBeDefined();
    expect(screen.getByText("Odmowa")).toBeDefined();
    expect(screen.getByText("Znam wnioskodawcę")).toBeDefined();
    const free = screen.getByText("Jan Wolny").closest("tr") as HTMLElement;
    expect(free.textContent).toContain("Brak przypisań w konkursie");
    const busy = screen.getByText("Anna Ekspert").closest("tr") as HTMLElement;
    expect(busy.textContent).toContain("1");
    expect(
      screen.getByText("W konkursie nie ma jeszcze złożonych wniosków."),
    ).toBeDefined();
  });

  it("says so when the evaluation cannot be read", async () => {
    vi.stubGlobal(
      "fetch",
      vi
        .fn()
        .mockImplementation(async () => new Response("{}", { status: 500 })),
    );

    await renderPage();

    expect(await screen.findByRole("alert")).toBeDefined();
  });

  it("reads everything again after a group assignment refused halfway", async () => {
    const rankingRow = (id: string, number: string) => ({
      rank: null, applicationId: id, number, entityName: "Podmiot", entityType: "Organization",
      projectTitle: "Projekt", requestedGrant: 7000, submittedAt: null, formal: "NotStarted",
      meritCardsFinished: 0, meritCardsRequired: 2, meritScore: null, strategicScore: null,
      totalScore: null, passesThreshold: null, diverges: false, recommendedGrant: null,
    });
    const assignments: { applicationId: string; reviewerId: string }[] = [];
    const fetchMock = vi.fn().mockImplementation(async (input: string, init?: RequestInit) => {
      const path = new URL(String(input), "http://localhost").pathname.replace(/^\/api/, "");
      if (init?.method === "POST") {
        const applicationId = path.split("/")[2];
        if (applicationId === "b2") {
          return new Response(JSON.stringify({ title: "Odmowa" }), {
            status: 409,
            headers: { "Content-Type": "application/problem+json" },
          });
        }
        assignments.push({ applicationId, reviewerId: "r2" });
        return new Response(null, { status: 204 });
      }
      if (path === "/competitions/c1/ranking") {
        return new Response(JSON.stringify({ competitionId: "c1", settings, rows: [rankingRow("b1", "1/2026/1"), rankingRow("b2", "1/2026/2")] }));
      }
      if (path === "/competitions/c1/assignments") return new Response(JSON.stringify(assignments));
      return new Response(JSON.stringify(bodies[path] ?? null), { status: path in bodies ? 200 : 404 });
    });
    vi.stubGlobal("fetch", fetchMock);

    await renderPage();

    fireEvent.click(await screen.findByLabelText("Zaznacz wszystkie wnioski"));
    fireEvent.change(screen.getByLabelText("Przypisz zaznaczone ekspertowi"), { target: { value: "r2" } });
    fireEvent.click(screen.getAllByRole("button", { name: "Przypisz" })[0]);

    expect(await screen.findByRole("alert")).toBeDefined();
    const expert = await screen.findByRole("button", { name: /Cofnij przypisanie eksperta Jan Wolny do wniosku 1\/2026\/1/ });
    expect(expert).toBeDefined();
    await waitFor(() => expect(screen.getByText("Zaznaczone wnioski: 2")).toBeDefined());
  });
});
