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
  requirementAnchorId: (id: string) => `zalaczniki-${id}`,
  AttachmentsPanel: (props: {
    onUploaded: (attachment: Attachment) => void;
  }) => (
    <div data-testid="attachments-panel">
      <button
        type="button"
        onClick={() =>
          props.onUploaded({
            id: "uploaded-1",
            applicationId: "app-1",
            fileName: "statut.pdf",
            contentType: "application/pdf",
            sizeInBytes: 2048,
            createdAt: "2026-09-12T10:05:00Z",
          })
        }
      >
        mock-upload
      </button>
    </div>
  ),
  attachmentsAnchorId: "zalaczniki",
}));

import { DraftWorkspace } from "./draft-workspace";
import type { Application, Attachment } from "@/lib/applicant-applications";
import { ApiError } from "@/lib/api-client";

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

function competition(overrides: Partial<PublicCompetition> = {}): PublicCompetition {
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
    ...overrides,
  };
}

function renderWorkspace(
  overrides: Partial<Application> = {},
  competitionOverrides: Partial<PublicCompetition> = {},
  initialAttachments: Attachment[] = [],
) {
  const onSubmitted = vi.fn();
  const onAttachmentsChange = vi.fn();
  render(
    <DraftWorkspace
      application={application(overrides)}
      form={{ versionNumber: 1, document: formDocument }}
      competition={competition(competitionOverrides)}
      initialAttachments={initialAttachments}
      onSubmitted={onSubmitted}
      onAttachmentsChange={onAttachmentsChange}
    />,
  );
  return { onSubmitted, onAttachmentsChange };
}

const requiredAttachment = {
  id: "req-1",
  title: "Statut organizacji",
  description: null,
  requirement: "Required" as const,
  allowedFormats: [],
};

function attachment(overrides: Partial<Attachment> = {}): Attachment {
  return {
    id: "a1",
    applicationId: "app-1",
    fileName: "statut.pdf",
    contentType: "application/pdf",
    sizeInBytes: 1024,
    createdAt: "2026-09-12T10:00:00Z",
    ...overrides,
  };
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
  window.localStorage.clear();
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

  it("says a save lost to an ended session in its own words, with a way back in (P4-15)", async () => {
    saveDraft.mockRejectedValue(new ApiError(401, "Zaloguj się, żeby zobaczyć tę stronę.", {}, "Zaloguj się, żeby zobaczyć tę stronę."));
    renderWorkspace();

    fireEvent.change(screen.getByLabelText(/Tytuł projektu/), { target: { value: "Nasz projekt" } });
    await vi.advanceTimersByTimeAsync(1000);
    // The rejection settles a few promise hops after the timer fires.
    await vi.advanceTimersByTimeAsync(0);

    const alert = screen.getByRole("alert");
    expect(alert.textContent).toMatch(/Nie zapisano ostatnich zmian, bo sesja się zakończyła/);
    expect(alert.textContent).not.toMatch(/żeby zobaczyć tę stronę/);
    const link = within(alert).getByRole("link", { name: /Zaloguj się ponownie/ });
    expect(link.getAttribute("target")).toBe("_blank");
    expect(link.getAttribute("href")).toContain(encodeURIComponent("/panel/applicant/applications/app-1"));

    saveDraft.mockReset();
    saveDraft.mockResolvedValue(application({ lastSavedAt: "2026-09-12T11:00:00Z" }));
    fireEvent.click(within(alert).getByRole("button", { name: "zapisz zmiany" }));
    await vi.advanceTimersByTimeAsync(0);

    expect(saveDraft).toHaveBeenCalledWith("app-1", { tytul: "Nasz projekt" });
    expect(screen.queryByText(/Nie zapisano ostatnich zmian/)).toBeNull();
  });

  it("opens again at the section the applicant was last in (P4-11)", () => {
    const twoSections: FormDocument = {
      schemaVersion: 1,
      sections: [
        formDocument.sections[0],
        { key: "s2", title: "Budżet", description: "", fields: [] },
      ],
    };
    const props = {
      application: application(),
      form: { versionNumber: 1, document: twoSections },
      competition: competition(),
      initialAttachments: [],
      onSubmitted: vi.fn(),
      onAttachmentsChange: vi.fn(),
    };
    render(<DraftWorkspace {...props} />);
    fireEvent.click(screen.getByRole("button", { name: "Dalej" }));
    expect(screen.getByRole("heading", { level: 2, name: "Budżet" })).toBeDefined();
    cleanup();

    render(<DraftWorkspace {...props} />);

    expect(screen.getByRole("heading", { level: 2, name: "Budżet" })).toBeDefined();
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

  it("offers no second try once the intake closed under the open page (O-19)", async () => {
    const closed = "Nabór został zamknięty 08.10.2026 o godzinie 13:35 czasu polskiego. Wniosku nie można już złożyć.";
    submitApplication.mockRejectedValue(new ApiError(409, closed, {}, closed));
    renderWorkspace({ answers: { tytul: "Nasz projekt" } });

    fireEvent.click(screen.getByRole("button", { name: "Złóż wniosek" }));
    fireEvent.click(screen.getByRole("button", { name: "Złóż wniosek" }));
    const dialog = screen.getByRole("dialog");
    fireEvent.click(within(dialog).getByRole("button", { name: "Złóż wniosek" }));

    await vi.waitFor(() => expect(within(dialog).getByRole("alert").textContent).toContain("Nabór został zamknięty"));
    expect(within(dialog).queryByRole("button", { name: "Złóż wniosek" })).toBeNull();
    fireEvent.click(within(dialog).getByRole("button", { name: "Zamknij" }));
    expect(screen.getByText(/Wersja robocza zostaje zapisana/)).toBeDefined();
  });

  it("keeps the second try for a refusal that asks for one, also a 409", async () => {
    const changed = "Wniosek zmienił się w trakcie składania, na przykład w drugiej karcie. Sprawdź go i złóż ponownie.";
    submitApplication.mockRejectedValue(new ApiError(409, changed, {}, changed));
    renderWorkspace({ answers: { tytul: "Nasz projekt" } });

    fireEvent.click(screen.getByRole("button", { name: "Złóż wniosek" }));
    fireEvent.click(screen.getByRole("button", { name: "Złóż wniosek" }));
    const dialog = screen.getByRole("dialog");
    fireEvent.click(within(dialog).getByRole("button", { name: "Złóż wniosek" }));

    await vi.waitFor(() => expect(within(dialog).getByRole("alert").textContent).toContain("złóż ponownie"));
    expect(within(dialog).getByRole("button", { name: "Złóż wniosek" })).toBeDefined();
    expect(screen.queryByText(/Wersja robocza zostaje zapisana/)).toBeNull();
  });

  it("jumps back to the field a gap names and moves keyboard focus onto it", () => {
    renderWorkspace();

    fireEvent.click(screen.getByRole("button", { name: /Projekt: Tytuł projektu/ }));

    expect(document.activeElement).toBe(screen.getByLabelText(/Tytuł projektu/));
  });

  it("blocks submission naming each required attachment without its file (T-101)", () => {
    renderWorkspace(
      { answers: { tytul: "Nasz projekt" } },
      { attachments: [requiredAttachment] },
    );

    const button = screen.getByRole("button", { name: "Złóż wniosek" });
    expect(button).toHaveProperty("disabled", true);
    expect(screen.getByText(new RegExp(`Brakuje wymaganego załącznika: ${requiredAttachment.title}`))).toBeDefined();
  });

  it("does not block once a file answers the requirement, but a file for none counts for nothing", () => {
    renderWorkspace(
      { answers: { tytul: "Nasz projekt" } },
      { attachments: [requiredAttachment] },
      [attachment({ requirementId: requiredAttachment.id })],
    );
    expect(screen.getByRole("button", { name: "Złóż wniosek" })).toHaveProperty("disabled", false);
    cleanup();

    renderWorkspace(
      { answers: { tytul: "Nasz projekt" } },
      { attachments: [requiredAttachment] },
      [attachment({ requirementId: null })],
    );
    expect(screen.getByRole("button", { name: "Złóż wniosek" })).toHaveProperty("disabled", true);
  });

  it("never blocks on attachments when the competition asks for none", () => {
    renderWorkspace({ answers: { tytul: "Nasz projekt" } });

    const button = screen.getByRole("button", { name: "Złóż wniosek" });
    expect(button).toHaveProperty("disabled", false);
  });

  it("flushes a pending autosave before submitting, instead of finalizing whatever the server still has", async () => {
    saveDraft.mockResolvedValue(
      application({
        answers: { tytul: "Najnowsza wersja" },
        lastSavedAt: "2026-09-12T12:00:00Z",
        checksum: "flushed",
      }),
    );
    submitApplication.mockResolvedValue(application({ status: "Submitted", number: "001" }));
    const { onSubmitted } = renderWorkspace();

    fireEvent.change(screen.getByLabelText(/Tytuł projektu/), {
      target: { value: "Najnowsza wersja" },
    });

    // Reaches the confirmation dialog and confirms it before the 1s debounce
    // would otherwise have fired on its own: without the flush, saveDraft is
    // never called here at all.
    fireEvent.click(screen.getByRole("button", { name: "Złóż wniosek" }));
    fireEvent.click(screen.getByRole("button", { name: "Złóż wniosek" }));
    const dialog = screen.getByRole("dialog");
    fireEvent.click(within(dialog).getByRole("button", { name: "Złóż wniosek" }));

    await vi.waitFor(() =>
      expect(saveDraft).toHaveBeenCalledWith("app-1", { tytul: "Najnowsza wersja" }),
    );
    await vi.waitFor(() => expect(submitApplication).toHaveBeenCalledWith("app-1"));
    await vi.waitFor(() => expect(onSubmitted).toHaveBeenCalled());
  });

  it("reports every attachment change upward as it happens, not only the list it was handed at mount", async () => {
    const { onAttachmentsChange } = renderWorkspace();

    expect(onAttachmentsChange).toHaveBeenCalledWith([]);

    fireEvent.click(screen.getByRole("button", { name: "mock-upload" }));

    await vi.waitFor(() =>
      expect(onAttachmentsChange).toHaveBeenLastCalledWith([
        expect.objectContaining({ id: "uploaded-1" }),
      ]),
    );
  });
});

describe("DraftWorkspace in a correction (T-103)", () => {
  const twoSections: FormDocument = {
    schemaVersion: 1,
    sections: [
      {
        key: "dane",
        title: "Dane projektu",
        description: "",
        fields: [
          { key: "opis", type: "shortText", label: "Opis", help: "", required: true, printed: true, maxLength: 100 },
        ],
      },
      {
        key: "budzet",
        title: "Budżet",
        description: "",
        fields: [
          { key: "kwota", type: "shortText", label: "Kwota", help: "", required: true, printed: true, maxLength: 20 },
        ],
      },
    ],
  };

  function renderCorrection(unlocksAttachments: boolean) {
    render(
      <DraftWorkspace
        application={application({ status: "Returned", answers: { opis: "Stary opis", kwota: "100" } })}
        form={{ versionNumber: 1, document: twoSections }}
        competition={competition()}
        initialAttachments={[]}
        onSubmitted={vi.fn()}
        onAttachmentsChange={vi.fn()}
        correction={{
          id: "r1",
          sections: ["budzet"],
          unlocksAttachments,
          message: "Popraw kwotę w budżecie.",
          deadline: "2026-10-05T10:00:00Z",
          returnedAt: "2026-10-01T10:00:00Z",
          resolvedAt: null,
        }}
      />,
    );
  }

  it("names what to correct, opens on the unlocked section and keeps the rest read only", () => {
    renderCorrection(false);

    expect(screen.getByRole("heading", { name: "Wniosek zwrócony do poprawy" })).toBeTruthy();
    expect(screen.getByText("Popraw kwotę w budżecie.")).toBeTruthy();
    expect(screen.getByText(/Zmienić możesz tylko sekcje: Budżet\./)).toBeTruthy();
    expect(screen.getByLabelText(/Kwota/)).toBeTruthy();
    expect(screen.getByText("Załączniki nie są odblokowane do poprawy.")).toBeTruthy();
    expect(screen.queryByTestId("attachments-panel")).toBeNull();
    // O-11: the bar tells the locked part from the one to correct.
    expect(screen.getByRole("button", { name: /Dane projektu \(zablokowana\)/ })).toBeTruthy();
    expect(screen.getByRole("button", { name: /Budżet/ }).textContent).not.toMatch(/zablokowana/);
  });

  it("shows the attachments when the return unlocks them", () => {
    renderCorrection(true);

    expect(screen.getByTestId("attachments-panel")).toBeTruthy();
  });
});
