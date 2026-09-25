import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";

import type { Evaluation } from "@/lib/reviewer-work";
import { AUTOSAVE_DELAY_MS, EvaluationWorkspace } from "./evaluation-workspace";

const card = {
  schemaVersion: 1,
  sections: [
    {
      key: "s",
      title: "Kryteria",
      description: "",
      fields: [
        { key: "pomysl", type: "number", label: "Pomysł", help: "", required: true, printed: true, minValue: 0, maxValue: 20 },
        {
          key: "z_patronem",
          type: "yesNo",
          label: "Grupa z patronem",
          help: "",
          required: true,
          printed: true,
          points: 1,
          appliesTo: ["InformalGroup", "PatronInformalGroup"],
        },
      ],
    },
  ],
};

function evaluation(overrides: Partial<Evaluation> = {}): Evaluation {
  return {
    id: "e1",
    applicationId: "a1",
    competitionId: "c1",
    applicantType: "Organisation",
    stage: "Merit",
    cardDefinitionId: "d1",
    cardVersionNumber: 1,
    cardDefinition: card,
    authorUserId: "r1",
    authorName: null,
    enteredByUserId: "r1",
    answers: {},
    status: "Draft",
    finishedAt: null,
    updatedAt: "2026-09-25T10:00:00Z",
    formalPassed: null,
    meritScore: 0,
    strategicScore: 0,
    recommendedGrant: null,
    ...overrides,
  } as Evaluation;
}

function respondWith(body: unknown) {
  const fetchMock = vi.fn().mockImplementation(async () => new Response(JSON.stringify(body), { status: 200 }));
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

afterEach(() => {
  cleanup();
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

describe("EvaluationWorkspace", () => {
  it("hides the criterion an organisation is not asked", () => {
    render(<EvaluationWorkspace evaluation={evaluation()} />);

    expect(screen.getByRole("spinbutton", { name: /Pomysł/ })).toBeDefined();
    expect(screen.queryByText(/Grupa z patronem/)).toBeNull();
  });

  it("saves a second after the last change, not on every keystroke", async () => {
    vi.useFakeTimers();
    const fetchMock = respondWith(evaluation({ answers: { pomysl: 12 }, meritScore: 12 }));
    render(<EvaluationWorkspace evaluation={evaluation()} />);

    fireEvent.change(screen.getByRole("spinbutton", { name: /Pomysł/ }), { target: { value: "12" } });
    expect(fetchMock).not.toHaveBeenCalled();

    await act(async () => {
      vi.advanceTimersByTime(AUTOSAVE_DELAY_MS);
    });

    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url, init] = fetchMock.mock.calls[0];
    expect(String(url)).toContain("/evaluations/e1");
    expect((init as RequestInit).method).toBe("PUT");
  });

  it("finishes only through the confirmation and then shows the card read only", async () => {
    const fetchMock = respondWith(evaluation({ status: "Finished", finishedAt: "2026-09-25T11:00:00Z" }));
    render(<EvaluationWorkspace evaluation={evaluation()} />);

    fireEvent.click(screen.getByRole("button", { name: "Zakończ ocenę" }));
    expect(fetchMock).not.toHaveBeenCalled();

    const confirm = screen.getAllByRole("button", { name: "Zakończ ocenę", hidden: true }).at(-1)!;
    await act(async () => {
      fireEvent.click(confirm);
    });

    expect(String(fetchMock.mock.calls[0][0])).toContain("/evaluations/e1/finish");
    expect(await screen.findByText("Ocena zakończona. Karty nie można już zmienić.")).toBeDefined();
  });
});
