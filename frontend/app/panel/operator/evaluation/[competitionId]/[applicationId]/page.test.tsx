import { Suspense } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";

import ApplicationEvaluationPage from "./page";

const document = {
  schemaVersion: 1,
  sections: [
    {
      key: "s",
      title: "Kryteria",
      description: "",
      fields: [{ key: "w_terminie", type: "yesNo", label: "Złożony w terminie", help: "", required: true, printed: true, role: "formalCriterion" }],
    },
  ],
};

const offerDocument = {
  schemaVersion: 1,
  sections: [
    {
      key: "o",
      title: "Opis projektu",
      description: "",
      fields: [{ key: "opis", type: "longText", label: "Opis", help: "", required: true, printed: true, maxLength: 100 }],
    },
  ],
};

const offer = {
  id: "a1",
  competitionId: "c1",
  competitionTitle: "Kierunek NOWE FIO",
  number: "1/2026/1",
  entityName: "Stowarzyszenie Przykład",
  entityType: "Organisation",
  status: "Submitted",
  submittedAt: "2026-04-10T10:00:00Z",
  checksum: "abc",
  formVersion: 1,
  definition: offerDocument,
  answers: { opis: "Ławki w parku" },
  attachments: [],
};

function card(overrides: Record<string, unknown>) {
  return {
    id: "e1",
    applicationId: "a1",
    competitionId: "c1",
    applicantType: "Organisation",
    stage: "Merit",
    cardDefinitionId: "d1",
    cardVersionNumber: 1,
    cardDefinition: document,
    authorUserId: "r1",
    authorName: null,
    enteredByUserId: "r1",
    answers: {},
    status: "Finished",
    finishedAt: "2026-05-01T10:00:00Z",
    updatedAt: "2026-05-01T10:00:00Z",
    formalPassed: null,
    meritScore: 41,
    strategicScore: 1,
    recommendedGrant: 6500,
    ...overrides,
  };
}

function serve(evaluations: unknown[], formal = card({ id: "f1", stage: "Formal", status: "Draft", finishedAt: null })) {
  const fetchMock = vi.fn().mockImplementation(async (input: string, init?: RequestInit) => {
    const path = new URL(String(input), "http://localhost").pathname.replace(/^\/api/, "");
    if (path === "/competitions/c1/applications/a1") return new Response(JSON.stringify(offer));
    if (path === "/applications/a1/evaluations") return new Response(JSON.stringify(evaluations));
    if (path === "/applications/a1/evaluations/formal" && init?.method === "POST") {
      return new Response(JSON.stringify(formal), { status: 201 });
    }
    return new Response("{}", { status: 404 });
  });
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

async function renderPage() {
  await act(async () => {
    render(
      <Suspense fallback={null}>
        <ApplicationEvaluationPage params={Promise.resolve({ competitionId: "c1", applicationId: "a1" })} />
      </Suspense>,
    );
  });
}

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("ApplicationEvaluationPage", () => {
  it("shows every expert's card by name and the application under them", async () => {
    serve([{ evaluation: card({}), author: "Anna Ekspert" }]);

    await renderPage();

    expect(await screen.findByRole("heading", { name: "Anna Ekspert" })).toBeDefined();
    expect(screen.getByText(/Suma punktów: 41, kryteria strategiczne: 1/)).toBeDefined();
    expect(screen.getByText("Ławki w parku")).toBeDefined();
    expect(screen.getByRole("link", { name: "Wróć do listy rankingowej" }).getAttribute("href")).toBe(
      "/panel/operator/evaluation/c1",
    );
  });

  it("starts the formal card only on request", async () => {
    const fetchMock = serve([]);

    await renderPage();

    expect(await screen.findByText("Żaden ekspert nie otworzył jeszcze karty tego wniosku.")).toBeDefined();
    const posts = () => fetchMock.mock.calls.filter(([, init]) => (init as RequestInit | undefined)?.method === "POST");
    expect(posts()).toHaveLength(0);

    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "Rozpocznij ocenę formalną" }));
    });

    expect(posts()).toHaveLength(1);
    expect(await screen.findByRole("button", { name: "Zakończ ocenę" })).toBeDefined();
    expect(screen.getByText(/Złożony w terminie/)).toBeDefined();
  });

  it("says so when the application cannot be read", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response("{}", { status: 500 })));

    await renderPage();

    expect(await screen.findByRole("alert")).toBeDefined();
  });
});
