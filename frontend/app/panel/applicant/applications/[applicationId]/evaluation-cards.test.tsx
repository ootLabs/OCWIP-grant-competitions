import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";

import { EvaluationCards } from "./evaluation-cards";

const document = {
  schemaVersion: 1,
  sections: [
    {
      key: "s",
      title: "Kryteria",
      description: "",
      fields: [{ key: "uzasadnienie", type: "longText", label: "Uzasadnienie", help: "", required: true, printed: true, maxLength: 500 }],
    },
  ],
};

function card(stage: "Formal" | "Merit", overrides: Record<string, unknown> = {}) {
  return {
    stage,
    applicantType: "Organisation",
    cardDefinition: document,
    answers: { uzasadnienie: `Uzasadnienie ${stage}` },
    formalPassed: stage === "Formal" ? true : null,
    meritScore: stage === "Merit" ? 40 : null,
    strategicScore: stage === "Merit" ? 1 : null,
    recommendedGrant: null,
    ...overrides,
  };
}

function respondWith(body: unknown, status = 200) {
  const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify(body), { status }));
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("EvaluationCards", () => {
  it("shows every shared card with its result and answers", async () => {
    respondWith({ shared: true, cards: [card("Formal"), card("Merit"), card("Merit", { meritScore: 44 })] });

    render(<EvaluationCards applicationId="a1" />);

    expect(await screen.findByRole("heading", { name: "Karty oceny wniosku" })).toBeDefined();
    expect(screen.getByRole("heading", { name: "Karta oceny formalnej" })).toBeDefined();
    expect(screen.getByRole("heading", { name: "Karta oceny merytorycznej 1" })).toBeDefined();
    expect(screen.getByRole("heading", { name: "Karta oceny merytorycznej 2" })).toBeDefined();
    expect(screen.getByText("Wynik oceny formalnej: spełnia wymogi formalne.")).toBeDefined();
    expect(screen.getByText(/Suma punktów: 44/)).toBeDefined();
    expect(screen.getAllByText("Uzasadnienie Merit")).toHaveLength(2);
  });

  it("shows nothing before the operator shares the cards", async () => {
    const fetchMock = respondWith({ shared: false, cards: [] });

    const { container } = render(<EvaluationCards applicationId="a1" />);

    await waitFor(() => expect(fetchMock).toHaveBeenCalled());
    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(container.textContent).toBe("");
  });
});
