import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";

const replace = vi.fn();
vi.mock("next/navigation", () => ({ useRouter: () => ({ replace }) }));

import { saveWizardDraft } from "@/lib/competition-wizard/draft-storage";
import { fromCompetition } from "@/lib/competition-wizard/from-competition";
import type { OperatorCompetition } from "@/lib/operator-competitions";

import { CompetitionWizard } from "./competition-wizard";
import CompetitionWizardPage from "./page";

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status });
}

function competitionResponse(overrides: Record<string, unknown> = {}) {
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

function stubApi(overrides: { onCreate?: () => Response } = {}) {
  vi.stubGlobal(
    "fetch",
    vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      const method = init?.method ?? "GET";

      if (url.endsWith("/accounts/operators")) {
        return jsonResponse([]);
      }

      if (method === "POST" && url.endsWith("/competitions")) {
        return overrides.onCreate
          ? overrides.onCreate()
          : jsonResponse(competitionResponse(), 201);
      }

      // A step-summary visit re-saves before showing the preview, and
      // publish() re-saves again before publishing: once a competition
      // exists, further saves are PUT, not a second POST.
      if (method === "PUT" && url.endsWith("/competitions/comp-1")) {
        return jsonResponse(competitionResponse());
      }

      if (method === "POST" && url.endsWith("/competitions/comp-1/status")) {
        return jsonResponse(
          competitionResponse({
            status: "Published",
            publishedAt: "2026-01-02T00:00:00Z",
            allowedTransitions: [],
          }),
        );
      }

      throw new Error(`Unexpected fetch: ${method} ${url}`);
    }),
  );
}

beforeEach(() => {
  window.localStorage.clear();
  stubApi();
});

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  replace.mockReset();
  window.history.replaceState(null, "", "/");
});

async function fillMinimum() {
  render(<CompetitionWizardPage />);

  fireEvent.change(screen.getByLabelText("Numer konkursu"), {
    target: { value: "1/2026" },
  });
  fireEvent.change(screen.getByLabelText("Tytuł konkursu"), {
    target: { value: "Konkurs testowy" },
  });
  fireEvent.change(screen.getByLabelText("Rozpoczęcie naboru wniosków"), {
    target: { value: "2026-09-01T08:00" },
  });
  fireEvent.change(screen.getByLabelText("Zakończenie naboru wniosków"), {
    target: { value: "2026-09-30T12:00" },
  });

  fireEvent.click(screen.getByRole("button", { name: "1.4 Limity" }));
  fireEvent.change(
    screen.getByLabelText("Maksymalna dotacja na jeden wniosek"),
    { target: { value: "5000" } },
  );
}

describe("CompetitionWizardPage", () => {
  it("shows the deadline in plain words as soon as an end date is typed", async () => {
    render(<CompetitionWizardPage />);

    fireEvent.change(screen.getByLabelText("Rozpoczęcie naboru wniosków"), {
      target: { value: "2026-09-01T08:00" },
    });
    fireEvent.change(screen.getByLabelText("Zakończenie naboru wniosków"), {
      target: { value: "2026-09-12T12:00" },
    });

    expect(
      await screen.findByText(/Nabór zamyka się 12 września/),
    ).toBeDefined();
  });

  it("spells the amount out in words under the grant limit field", async () => {
    render(<CompetitionWizardPage />);

    fireEvent.click(screen.getByRole("button", { name: "1.4 Limity" }));
    fireEvent.change(
      screen.getByLabelText("Maksymalna dotacja na jeden wniosek"),
      { target: { value: "100" } },
    );

    expect(await screen.findByText("sto złotych")).toBeDefined();
  });

  it("refuses to save and names what is missing when required fields are empty", async () => {
    render(<CompetitionWizardPage />);

    fireEvent.click(screen.getByRole("button", { name: "Zapisz" }));

    expect(
      await screen.findByText(/Żeby zapisać, uzupełnij najpierw/),
    ).toBeDefined();
  });

  it("saves a draft once the structurally required fields are filled", async () => {
    await fillMinimum();

    fireEvent.click(screen.getByRole("button", { name: "Zapisz" }));

    expect(await screen.findByText(/Zapisano jako roboczy/)).toBeDefined();
  });

  it("shows the exact preview an applicant would see when reaching the summary", async () => {
    await fillMinimum();

    fireEvent.click(screen.getByRole("button", { name: "1.7 Podsumowanie" }));

    expect(await screen.findByText("Konkurs testowy")).toBeDefined();
    expect(screen.getByText(/Nr 1\/2026/)).toBeDefined();
  });

  it("sends the operator to the competition page to publish, instead of publishing here", async () => {
    await fillMinimum();
    fireEvent.click(screen.getByRole("button", { name: "1.7 Podsumowanie" }));
    await screen.findByText("Konkurs testowy");

    // Publication needs the form and both cards (T-97), which only a saved
    // competition can have, so the wizard ends with the draft saved.
    expect(screen.queryByRole("button", { name: "Opublikuj konkurs" })).toBeNull();
    expect(
      screen.getByRole("link", { name: "Przejdź do strony konkursu" }).getAttribute("href"),
    ).toBe("/panel/operator/competitions/comp-1");
  });

  it("moves to the saved competition's address after the first save, and keeps no copy of it", async () => {
    await fillMinimum();
    expect(window.localStorage.getItem("ocwip:competition-wizard-draft")).not.toBeNull();

    fireEvent.click(screen.getByRole("button", { name: "Zapisz" }));
    await screen.findByText(/Zapisano jako roboczy/);

    // A reload now opens the competition from the server, in any browser.
    expect(window.location.pathname).toBe("/panel/operator/competitions/comp-1/edit");
    expect(window.localStorage.getItem("ocwip:competition-wizard-draft")).toBeNull();
    expect(window.localStorage.getItem("ocwip:competition-wizard-draft:comp-1")).toBeNull();
  });

  it("opens a buffer left from before T-97 at the saved competition", () => {
    // The one slot of T-22, which kept the id of the competition it saved.
    window.localStorage.setItem(
      "ocwip:competition-wizard-draft",
      JSON.stringify({
        draft: fromCompetition(competitionResponse() as OperatorCompetition),
        savedAt: "2026-01-01T00:00:00Z",
        competitionId: "comp-1",
      }),
    );

    render(<CompetitionWizardPage />);

    expect(replace).toHaveBeenCalledWith("/panel/operator/competitions/comp-1/edit");
  });

  it("maps a validation error from the backend to the step it belongs to", async () => {
    stubApi({
      onCreate: () =>
        new Response(
          JSON.stringify({
            title: "Bad Request",
            status: 400,
            errors: { number: ["Ten numer jest już zajęty."] },
          }),
          { status: 400, headers: { "content-type": "application/problem+json" } },
        ),
    });
    await fillMinimum();

    fireEvent.click(screen.getByRole("button", { name: "Zapisz" }));

    // The operator is on 1.4 Limity (fillMinimum leaves them there), and the
    // status bar names 1.1 as the step to check without moving them there.
    expect(
      await screen.findByText(/Sprawdź kroki:.*1\.1 Dane konkursu/),
    ).toBeDefined();
    expect(screen.queryByText("Ten numer jest już zajęty.")).toBeNull();

    fireEvent.click(
      screen.getByRole("button", { name: /1\.1 Dane konkursu/ }),
    );

    expect(screen.getByText("Ten numer jest już zajęty.")).toBeDefined();
  });
});

describe("CompetitionWizard on a saved competition", () => {
  const saved = () => competitionResponse({ description: "Z serwera" }) as OperatorCompetition;

  it("offers back what was typed in this browser on top of the same version", () => {
    const competition = saved();
    saveWizardDraft({ ...fromCompetition(competition), title: "Niezapisany tytuł" }, "comp-1", competition.updatedAt);

    render(<CompetitionWizard initialDraft={fromCompetition(competition)} initialCompetition={competition} />);

    expect((screen.getByLabelText("Tytuł konkursu") as HTMLInputElement).value).toBe("Niezapisany tytuł");
    fireEvent.click(screen.getByRole("button", { name: "Odrzuć je" }));
    expect((screen.getByLabelText("Tytuł konkursu") as HTMLInputElement).value).toBe("Konkurs testowy");
  });

  it("recognises the same version however many fractional digits it carries", () => {
    const competition = competitionResponse({ updatedAt: "2026-01-01T10:00:00.123456+00:00" }) as OperatorCompetition;
    saveWizardDraft(
      { ...fromCompetition(competition), title: "Po zapisie" },
      "comp-1",
      // What the answer to a save said: one digit more than the read.
      "2026-01-01T10:00:00.1234567+00:00",
    );

    render(<CompetitionWizard initialDraft={fromCompetition(competition)} initialCompetition={competition} />);

    expect((screen.getByLabelText("Tytuł konkursu") as HTMLInputElement).value).toBe("Po zapisie");
  });

  it("drops a buffer typed on top of an older version rather than undo a later save", () => {
    const competition = saved();
    saveWizardDraft({ ...fromCompetition(competition), title: "Stary tytuł" }, "comp-1", "2025-12-31T00:00:00Z");

    render(<CompetitionWizard initialDraft={fromCompetition(competition)} initialCompetition={competition} />);

    expect((screen.getByLabelText("Tytuł konkursu") as HTMLInputElement).value).toBe("Konkurs testowy");
    expect(window.localStorage.getItem("ocwip:competition-wizard-draft:comp-1")).toBeNull();
  });

  it("warns that applications have already arrived", () => {
    const competition = saved();

    render(
      <CompetitionWizard
        initialDraft={fromCompetition(competition)}
        initialCompetition={competition}
        submittedApplications={3}
      />,
    );

    expect(screen.getByText(/wpłynęło już wniosków: 3/)).toBeDefined();
  });
});
