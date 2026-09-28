import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";

vi.mock("next/navigation", () => ({ useParams: () => ({ id: "comp-1" }) }));

import OperatorCompetitionPage from "./page";

function competition(overrides: Record<string, unknown> = {}) {
  return {
    id: "comp-1",
    number: "1/2026",
    title: "Konkurs testowy",
    description: null,
    status: "Draft",
    allowedTransitions: ["Published"],
    intake: {
      acceptsApplications: false,
      state: "NotYetOpen",
      opensAt: "2026-09-01T06:00:00Z",
      closesAt: "2026-09-30T10:00:00Z",
      message: "Nabór jeszcze się nie rozpoczął.",
    },
    startDate: "2026-09-01T06:00:00Z",
    endDate: "2026-09-30T10:00:00Z",
    isContinuousIntake: false,
    maxGrantAmount: 5000,
    formDefinitionId: null,
    publishedAt: null,
    isActive: true,
    createdAt: "2026-01-01T00:00:00Z",
    updatedAt: "2026-01-01T00:00:00Z",
    expectedResults: null,
    rulesUrl: null,
    submissionNotice: null,
    submissionEmailBody: null,
    requiresPaperSubmission: false,
    paperSubmissionDeadline: null,
    paperSubmissionAddress: null,
    projectStartDate: null,
    projectEndDate: null,
    totalPoolAmount: null,
    minGrantAmount: null,
    maxIndirectCostPercent: null,
    maxInstitutionalDevelopmentPercent: null,
    percentageBasis: "GrantAmount",
    maxAverageAnnualRevenue: null,
    personalDataProcessedUntil: null,
    costCategories: ["DirectCosts", "InstitutionalDevelopment", "IndirectCosts"],
    maxAttachmentSizeInBytes: 10 * 1024 * 1024,
    maxApplicationSizeInBytes: 50 * 1024 * 1024,
    attachments: [],
    contacts: [],
    publicationGaps: [],
    ...overrides,
  };
}

type Handler = (method: string, url: string) => Response | undefined;

function stubApi(handler: Handler) {
  const fetch = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const method = init?.method ?? "GET";
    const url = String(input);
    // The cards section (T-96) asks for the versions in force and the other
    // competitions; these tests are about the rest of the page.
    if (method === "GET" && (url.includes("/evaluation-cards/") || url.endsWith("/report-form") || url.endsWith("/competitions"))) {
      return json([]);
    }
    const answer = handler(method, url);
    if (!answer) {
      throw new Error(`Unexpected fetch: ${method} ${String(input)}`);
    }
    return answer;
  });
  vi.stubGlobal("fetch", fetch);
  return fetch;
}

const json = (body: unknown, status = 200, type = "application/json") =>
  new Response(JSON.stringify(body), { status, headers: { "content-type": type } });

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("Strona konkursu (operator)", () => {
  it("lists what a draft lacks and keeps publishing shut until nothing does", async () => {
    stubApi((method) =>
      method === "GET"
        ? json(competition({ publicationGaps: ["Brak karty oceny formalnej."] }))
        : undefined,
    );

    render(<OperatorCompetitionPage />);

    expect(await screen.findByText("Brak karty oceny formalnej.")).toBeDefined();
    expect((screen.getByRole("button", { name: "Opublikuj konkurs" }) as HTMLButtonElement).disabled).toBe(true);
    expect(screen.getByRole("link", { name: "Wnioski" }).getAttribute("href")).toBe(
      "/panel/operator/applications/comp-1",
    );
    expect(screen.getByRole("link", { name: "Edytuj ogłoszenie" }).getAttribute("href")).toBe(
      "/panel/operator/competitions/comp-1/edit",
    );
  });

  it("publishes after the confirmation and shows the new state", async () => {
    const fetch = stubApi((method, url) => {
      if (method === "GET") return json(competition());
      if (method === "POST" && url.endsWith("/competitions/comp-1/status"))
        return json(competition({ status: "Published", allowedTransitions: [] }));
      return undefined;
    });

    render(<OperatorCompetitionPage />);
    fireEvent.click(await screen.findByRole("button", { name: "Opublikuj konkurs" }));
    expect(fetch.mock.calls.some(([, init]) => init?.method === "POST")).toBe(false);

    fireEvent.click(screen.getByRole("button", { name: "Tak", hidden: true }));

    expect(await screen.findByText(/Ogłoszony/)).toBeDefined();
    const post = fetch.mock.calls.find(([, init]) => init?.method === "POST")!;
    expect(JSON.parse(String(post[1]!.body))).toEqual({ status: "Published" });
  });

  it("names the gaps the server found when publication is refused", async () => {
    stubApi((method) =>
      method === "GET"
        ? json(competition())
        : json(
            {
              status: 409,
              detail: "Konkursu nie można jeszcze opublikować: Brak opublikowanego formularza wniosku.",
              errors: { publication: ["Brak opublikowanego formularza wniosku."] },
            },
            409,
            "application/problem+json",
          ),
    );

    render(<OperatorCompetitionPage />);
    fireEvent.click(await screen.findByRole("button", { name: "Opublikuj konkurs" }));
    fireEvent.click(screen.getByRole("button", { name: "Tak", hidden: true }));

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("Brak opublikowanego formularza wniosku.");
  });

  it("restores a deactivated competition", async () => {
    stubApi((method, url) => {
      if (method === "GET") return json(competition({ isActive: false, allowedTransitions: [] }));
      if (method === "POST" && url.endsWith("/competitions/comp-1/restore")) return json(competition());
      return undefined;
    });

    render(<OperatorCompetitionPage />);
    fireEvent.click(await screen.findByRole("button", { name: "Przywróć konkurs" }));
    fireEvent.click(screen.getByRole("button", { name: "Tak", hidden: true }));

    expect(await screen.findByRole("button", { name: "Dezaktywuj konkurs" })).toBeDefined();
  });
});
