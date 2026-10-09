import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";

import { ApiError } from "@/lib/api-client";
import type { CompetitionAttachment } from "@/lib/competitions";

const uploadAttachment = vi.fn();
const replaceAttachment = vi.fn();
const withdrawAttachment = vi.fn();

vi.mock("@/lib/applicant-applications", () => ({
  uploadAttachment: (...args: unknown[]) => uploadAttachment(...args),
  replaceAttachment: (...args: unknown[]) => replaceAttachment(...args),
  withdrawAttachment: (...args: unknown[]) => withdrawAttachment(...args),
}));

import { AttachmentsPanel } from "./attachments-panel";

function requirement(overrides: Partial<CompetitionAttachment> = {}): CompetitionAttachment {
  return {
    id: "r1",
    title: "Statut organizacji",
    description: "Aktualny statut podpisany przez zarząd.",
    requirement: "Required",
    allowedFormats: ["Pdf"],
    ...overrides,
  };
}

function attachment(overrides: Record<string, unknown> = {}) {
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

function selectFile(input: HTMLElement, file: File) {
  Object.defineProperty(input, "files", { value: [file], configurable: true });
  fireEvent.change(input);
}

function selectFiles(input: HTMLElement, files: File[]) {
  Object.defineProperty(input, "files", { value: files, configurable: true });
  fireEvent.change(input);
}

afterEach(() => {
  cleanup();
  uploadAttachment.mockReset();
  replaceAttachment.mockReset();
  withdrawAttachment.mockReset();
});

describe("AttachmentsPanel", () => {
  it("withdraws a file added by mistake only after a confirmation (P4-14)", async () => {
    // jsdom has no <dialog>: the guarded showModal in ConfirmDialog needs this
    // to give the dialog its open attribute.
    HTMLDialogElement.prototype.showModal = function (this: HTMLDialogElement) {
      this.setAttribute("open", "");
    };
    withdrawAttachment.mockResolvedValue(undefined);
    const onWithdrawn = vi.fn();
    render(
      <AttachmentsPanel
        applicationId="app-1"
        requirements={[requirement()]}
        attachments={[attachment({ requirementId: "r1", fileName: "pomylka.pdf" })]}
        onUploaded={vi.fn()}
        onReplaced={vi.fn()}
        onWithdrawn={onWithdrawn}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: "Wycofaj pomylka.pdf" }));
    expect(withdrawAttachment).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole("button", { name: "Wycofaj plik" }));

    await waitFor(() => expect(onWithdrawn).toHaveBeenCalledWith("a1"));
    expect(withdrawAttachment).toHaveBeenCalledWith("a1");
  });

  it("shows one tile per requirement, saying which already has its file", () => {
    render(
      <AttachmentsPanel
        applicationId="app-1"
        requirements={[
          requirement({ id: "r1", title: "Statut", requirement: "Required" }),
          requirement({ id: "r2", title: "Zdjęcie z wydarzenia", requirement: "Optional" }),
        ]}
        attachments={[attachment({ requirementId: "r1" })]}
        onUploaded={vi.fn()}
        onReplaced={vi.fn()}
      />,
    );

    const statut = screen.getByRole("listitem", { name: "Statut" });
    expect(statut.textContent).toContain("Wymagany");
    expect(statut.textContent).toContain("dodano");
    expect(statut.textContent).toContain("statut.pdf");
    const photo = screen.getByRole("listitem", { name: "Zdjęcie z wydarzenia" });
    expect(photo.textContent).toContain("jeszcze nie dodano");
  });

  it("sends a file picked in a tile as answering that requirement (T-101)", async () => {
    uploadAttachment.mockResolvedValue(attachment({ requirementId: "r1" }));
    const onUploaded = vi.fn();

    render(
      <AttachmentsPanel
        applicationId="app-1"
        requirements={[requirement({ id: "r1", title: "Statut" })]}
        attachments={[]}
        onUploaded={onUploaded}
        onReplaced={vi.fn()}
      />,
    );

    const input = screen.getByRole("listitem", { name: "Statut" }).querySelector<HTMLInputElement>('input[type="file"]')!;
    const file = new File(["tresc"], "statut.pdf", { type: "application/pdf" });
    selectFile(input, file);

    await waitFor(() => expect(onUploaded).toHaveBeenCalled());
    expect(uploadAttachment).toHaveBeenCalledWith("app-1", file, "r1");
  });

  it("lists a file that answers no requirement apart, as counting for nothing", () => {
    render(
      <AttachmentsPanel
        applicationId="app-1"
        requirements={[requirement({ id: "r1", title: "Statut" })]}
        attachments={[attachment({ requirementId: null })]}
        onUploaded={vi.fn()}
        onReplaced={vi.fn()}
      />,
    );

    expect(screen.getByRole("heading", { name: "Inne pliki" })).toBeDefined();
    expect(screen.getByRole("listitem", { name: "Statut" }).textContent).toContain("jeszcze nie dodano");
  });

  it("says nothing is required when the competition asks for no attachment", () => {
    render(
      <AttachmentsPanel
        applicationId="app-1"
        requirements={[]}
        attachments={[]}
        onUploaded={vi.fn()}
        onReplaced={vi.fn()}
      />,
    );

    expect(
      screen.getByText("Ten konkurs nie wymaga żadnych załączników poza samym wnioskiem."),
    ).toBeDefined();
  });

  it("uploads a picked file and reports it to the caller", async () => {
    uploadAttachment.mockResolvedValue(attachment());
    const onUploaded = vi.fn();

    render(
      <AttachmentsPanel
        applicationId="app-1"
        requirements={[]}
        attachments={[]}
        onUploaded={onUploaded}
        onReplaced={vi.fn()}
      />,
    );

    const input = document.querySelector<HTMLInputElement>('input[type="file"]')!;
    const file = new File(["tresc"], "statut.pdf", { type: "application/pdf" });
    selectFile(input, file);

    await waitFor(() => expect(onUploaded).toHaveBeenCalledWith(attachment()));
    expect(uploadAttachment).toHaveBeenCalledWith("app-1", file, undefined);
  });

  it("shows the backend's own message when an upload is refused", async () => {
    uploadAttachment.mockRejectedValue(
      new ApiError(400, "Request failed.", {}, "Niedozwolony format pliku."),
    );

    render(
      <AttachmentsPanel
        applicationId="app-1"
        requirements={[]}
        attachments={[]}
        onUploaded={vi.fn()}
        onReplaced={vi.fn()}
      />,
    );

    const input = document.querySelector<HTMLInputElement>('input[type="file"]')!;
    selectFile(input, new File(["x"], "zly.exe"));

    expect(await screen.findByText("Niedozwolony format pliku.")).toBeDefined();
  });

  it("uploads every valid file even when others in the same drop are refused", async () => {
    const good = attachment({ id: "a-good", fileName: "dobry.pdf" });
    uploadAttachment.mockImplementation((_applicationId: string, file: File) =>
      file.name.endsWith(".exe")
        ? Promise.reject(
            new ApiError(400, "Request failed.", {}, `Niedozwolony format pliku: ${file.name}.`),
          )
        : Promise.resolve(good),
    );
    const onUploaded = vi.fn();

    render(
      <AttachmentsPanel
        applicationId="app-1"
        requirements={[]}
        attachments={[]}
        onUploaded={onUploaded}
        onReplaced={vi.fn()}
      />,
    );

    const input = document.querySelector<HTMLInputElement>('input[type="file"]')!;
    selectFiles(input, [
      new File(["x"], "zly.exe"),
      new File(["tresc"], "dobry.pdf", { type: "application/pdf" }),
      new File(["y"], "tez-zly.exe"),
    ]);

    // The one valid file among three still gets through...
    await waitFor(() => expect(onUploaded).toHaveBeenCalledWith(good));
    expect(onUploaded).toHaveBeenCalledTimes(1);
    // ...and with two refused, each keeps its own name next to its own
    // backend message, rather than one generic line losing which was which.
    expect(
      await screen.findByText(
        "zly.exe: Niedozwolony format pliku: zly.exe. tez-zly.exe: Niedozwolony format pliku: tez-zly.exe.",
      ),
    ).toBeDefined();
  });

  it("replaces an already uploaded file and reports the replacement", async () => {
    replaceAttachment.mockResolvedValue(attachment({ id: "a2", fileName: "statut-v2.pdf" }));
    const onReplaced = vi.fn();

    render(
      <AttachmentsPanel
        applicationId="app-1"
        requirements={[requirement({ id: "r1", title: "Statut" })]}
        attachments={[attachment({ requirementId: "r1" })]}
        onUploaded={vi.fn()}
        onReplaced={onReplaced}
      />,
    );

    expect(screen.getByText(/statut\.pdf \(/)).toBeDefined();

    // Named after the file it replaces, so a screen reader can tell the rows'
    // "Zastąp" apart (T-46).
    const replaceInput = screen.getByLabelText<HTMLInputElement>("Zastąp statut.pdf");
    const file = new File(["nowa tresc"], "statut-v2.pdf", { type: "application/pdf" });
    selectFile(replaceInput, file);

    await waitFor(() =>
      expect(onReplaced).toHaveBeenCalledWith(
        "a1",
        attachment({ id: "a2", fileName: "statut-v2.pdf" }),
      ),
    );
    expect(replaceAttachment).toHaveBeenCalledWith("a1", file);
  });

  it("drops a tile's refusal once its files are put right another way (O-09)", async () => {
    uploadAttachment.mockRejectedValue(new ApiError(400, "Request failed.", {}, "Załącznik przyjmuje tylko: PDF."));
    const props = {
      applicationId: "app-1",
      requirements: [requirement({ id: "r1", title: "Statut" })],
      onUploaded: vi.fn(),
      onReplaced: vi.fn(),
    };
    const { rerender } = render(<AttachmentsPanel {...props} attachments={[attachment({ requirementId: "r1" })]} />);

    const tile = screen.getByRole("listitem", { name: "Statut" });
    const drop = Array.from(tile.querySelectorAll<HTMLInputElement>('input[type="file"]')).at(-1)!;
    selectFile(drop, new File(["x"], "statut.docx"));
    expect(await screen.findByText("Załącznik przyjmuje tylko: PDF.")).toBeDefined();

    // "Zastąp" on the row went through: the tile now holds another file.
    rerender(<AttachmentsPanel {...props} attachments={[attachment({ id: "a2", requirementId: "r1" })]} />);

    expect(screen.queryByText("Załącznik przyjmuje tylko: PDF.")).toBeNull();
  });
});
