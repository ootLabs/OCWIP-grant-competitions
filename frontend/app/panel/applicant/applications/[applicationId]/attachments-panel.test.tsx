import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";

import { ApiError } from "@/lib/api-client";
import type { CompetitionAttachment } from "@/lib/competitions";

const uploadAttachment = vi.fn();
const replaceAttachment = vi.fn();

vi.mock("@/lib/applicant-applications", () => ({
  uploadAttachment: (...args: unknown[]) => uploadAttachment(...args),
  replaceAttachment: (...args: unknown[]) => replaceAttachment(...args),
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

afterEach(() => {
  cleanup();
  uploadAttachment.mockReset();
  replaceAttachment.mockReset();
});

describe("AttachmentsPanel", () => {
  it("separates required from optional requirements", () => {
    render(
      <AttachmentsPanel
        applicationId="app-1"
        requirements={[
          requirement({ id: "r1", title: "Statut", requirement: "Required" }),
          requirement({ id: "r2", title: "Zdjęcie z wydarzenia", requirement: "Optional" }),
        ]}
        attachments={[]}
        onUploaded={vi.fn()}
        onReplaced={vi.fn()}
      />,
    );

    expect(screen.getByText("Wymagane")).toBeDefined();
    expect(screen.getByText("Nieobowiązkowe")).toBeDefined();
    expect(screen.getByText("Statut")).toBeDefined();
    expect(screen.getByText("Zdjęcie z wydarzenia")).toBeDefined();
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
    expect(uploadAttachment).toHaveBeenCalledWith("app-1", file);
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

  it("replaces an already uploaded file and reports the replacement", async () => {
    replaceAttachment.mockResolvedValue(attachment({ id: "a2", fileName: "statut-v2.pdf" }));
    const onReplaced = vi.fn();

    render(
      <AttachmentsPanel
        applicationId="app-1"
        requirements={[]}
        attachments={[attachment()]}
        onUploaded={vi.fn()}
        onReplaced={onReplaced}
      />,
    );

    expect(screen.getByText(/statut\.pdf/)).toBeDefined();

    const inputs = document.querySelectorAll<HTMLInputElement>('input[type="file"]');
    // The second file input is the row's own "Zastąp", after the upload area's.
    const replaceInput = inputs[1];
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
});
