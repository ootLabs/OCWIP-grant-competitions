import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";

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

  it("publishes only after an explicit confirmation, not on the first click", async () => {
    await fillMinimum();
    fireEvent.click(screen.getByRole("button", { name: "1.7 Podsumowanie" }));
    await screen.findByText("Konkurs testowy");

    fireEvent.click(screen.getByRole("button", { name: "Opublikuj konkurs" }));

    // The first click only asks for confirmation: no publish request goes out
    // yet, so the success screen has not appeared.
    expect(screen.queryByText(/został opublikowany/)).toBeNull();

    fireEvent.click(screen.getByRole("button", { name: "Tak, opublikuj" }));

    expect(await screen.findByText(/został opublikowany/)).toBeDefined();
  });

  it("clears the draft from storage once publishing succeeds", async () => {
    await fillMinimum();
    fireEvent.click(screen.getByRole("button", { name: "1.7 Podsumowanie" }));
    await screen.findByText("Konkurs testowy");

    fireEvent.click(screen.getByRole("button", { name: "Opublikuj konkurs" }));
    fireEvent.click(screen.getByRole("button", { name: "Tak, opublikuj" }));

    await screen.findByText(/został opublikowany/);

    await waitFor(() =>
      expect(window.localStorage.getItem("ocwip:competition-wizard-draft")).toBeNull(),
    );
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
