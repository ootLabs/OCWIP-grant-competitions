import { Suspense } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, render, screen } from "@testing-library/react";

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
});
