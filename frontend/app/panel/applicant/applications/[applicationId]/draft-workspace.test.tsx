import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";

import type { FormDocument } from "@/lib/forms/document-types";
import type { PublicCompetition } from "@/lib/competitions";

const saveDraft = vi.fn();
const submitApplication = vi.fn();

vi.mock("@/lib/applicant-applications", async () => {
  const actual = await vi.importActual<typeof import("@/lib/applicant-applications")>(
    "@/lib/applicant-applications",
  );
  return {
    ...actual,
    saveDraft: (...args: unknown[]) => saveDraft(...args),
    submitApplication: (...args: unknown[]) => submitApplication(...args),
  };
});

vi.mock("./attachments-panel", () => ({
  AttachmentsPanel: () => <div data-testid="attachments-panel" />,
}));

import { DraftWorkspace } from "./draft-workspace";
import type { Application } from "@/lib/applicant-applications";

const formDocument: FormDocument = {
  schemaVersion: 1,
  sections: [
    {
      key: "s1",
      title: "Projekt",
      description: "",
      fields: [
        {
          key: "tytul",
          type: "shortText",
          label: "Tytuł projektu",
          help: "",
          required: true,
          printed: true,
          maxLength: 100,
        },
      ],
    },
  ],
};

function application(overrides: Partial<Application> = {}): Application {
  return {
    id: "app-1",
    competitionId: "c1",
    formDefinitionId: "f1",
    status: "Draft",
    answers: {},
    number: null,
    submittedAt: null,
    lastSavedAt: "2026-09-12T10:32:00Z",
    checksum: "0a55-22c2-b414",
    isActive: true,
    ...overrides,
  };
}

function competition(): PublicCompetition {
  return {
    id: "c1",
    number: "1/2026",
    title: "Konkurs testowy",
    description: null,
    status: "OpenForApplications",
    intake: {
      acceptsApplications: true,
      state: "Open",
      opensAt: "2026-09-01T08:00:00Z",
      closesAt: "2026-10-25T10:00:00Z",
      message: "Nabór trwa.",
    },
    startDate: "2026-09-01T08:00:00Z",
    endDate: "2026-10-25T10:00:00Z",
    isContinuousIntake: false,
    maxGrantAmount: 15000,
    expectedResults: null,
    rulesUrl: null,
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
    costCategories: [],
    maxAttachmentSizeInBytes: 10 * 1024 * 1024,
    maxApplicationSizeInBytes: 50 * 1024 * 1024,
    attachments: [],
    contacts: [],
  };
}

function renderWorkspace(overrides: Partial<Application> = {}) {
  const onSubmitted = vi.fn();
  render(
    <DraftWorkspace
      application={application(overrides)}
      form={{ versionNumber: 1, document: formDocument }}
      competition={competition()}
      initialAttachments={[]}
      onSubmitted={onSubmitted}
    />,
  );
  return { onSubmitted };
}

beforeEach(() => {
  vi.useFakeTimers();
  // jsdom has no <dialog> support at all: showModal is the guarded no-op
  // confirm-submit-dialog.tsx falls back to, so without this the dialog
  // never gets its `open` attribute and never becomes queryable by role.
  HTMLDialogElement.prototype.showModal = function (this: HTMLDialogElement) {
    this.setAttribute("open", "");
  };
});

afterEach(() => {
  cleanup();
  vi.useRealTimers();
  saveDraft.mockReset();
  submitApplication.mockReset();
});

describe("DraftWorkspace", () => {
  it("keeps Złóż wniosek disabled while a required field is empty, and names it in the gap list", () => {
    renderWorkspace();

    const button = screen.getByRole("button", { name: "Złóż wniosek" });
    expect(button).toHaveProperty("disabled", true);
    expect(screen.getByText(/Projekt: Tytuł projektu/)).toBeDefined();
  });

  it("autosaves a second after the applicant stops typing, not on every keystroke", async () => {
    saveDraft.mockResolvedValue(
      application({ lastSavedAt: "2026-09-12T11:00:00Z", checksum: "next" }),
    );
    renderWorkspace();

    fireEvent.change(screen.getByLabelText(/Tytuł projektu/), {
      target: { value: "Nasz projekt" },
    });

    expect(saveDraft).not.toHaveBeenCalled();

    await vi.advanceTimersByTimeAsync(1000);

    expect(saveDraft).toHaveBeenCalledWith("app-1", { tytul: "Nasz projekt" });
  });

  it("enables Złóż wniosek once the required field is filled, and reaches the summary screen", () => {
    renderWorkspace();

    fireEvent.change(screen.getByLabelText(/Tytuł projektu/), {
      target: { value: "Nasz projekt" },
    });

    const button = screen.getByRole("button", { name: "Złóż wniosek" });
    expect(button).toHaveProperty("disabled", false);

    fireEvent.click(button);

    expect(screen.getByText("Podsumowanie wniosku")).toBeDefined();
  });

  it("submits only after the one confirmation dialog is itself confirmed", async () => {
    submitApplication.mockResolvedValue(application({ status: "Submitted", number: "001" }));
    const { onSubmitted } = renderWorkspace({ answers: { tytul: "Nasz projekt" } });

    // First click: the always visible bar's own button, filling to summary.
    fireEvent.click(screen.getByRole("button", { name: "Złóż wniosek" }));
    expect(screen.getByText("Podsumowanie wniosku")).toBeDefined();
    expect(submitApplication).not.toHaveBeenCalled();

    // Second click of that same bar, now on the summary screen: opens the
    // one confirmation dialog, still without submitting anything.
    fireEvent.click(screen.getByRole("button", { name: "Złóż wniosek" }));
    const dialog = screen.getByRole("dialog");
    expect(within(dialog).getByText(/Po złożeniu wniosku nie będzie można go już edytować/)).toBeDefined();
    expect(submitApplication).not.toHaveBeenCalled();

    // Only the dialog's own confirm button actually submits. vi.waitFor
    // rather than testing-library's: this test runs under fake timers for
    // the autosave debounce elsewhere in the suite, and only vitest's own
    // waitFor advances them while it polls.
    fireEvent.click(within(dialog).getByRole("button", { name: "Złóż wniosek" }));

    await vi.waitFor(() => expect(submitApplication).toHaveBeenCalledWith("app-1"));
    await vi.waitFor(() => expect(onSubmitted).toHaveBeenCalled());
  });

  it("jumps back to the field a gap names and moves keyboard focus onto it", () => {
    renderWorkspace();

    fireEvent.click(screen.getByRole("button", { name: /Projekt: Tytuł projektu/ }));

    expect(document.activeElement).toBe(screen.getByLabelText(/Tytuł projektu/));
  });
});
