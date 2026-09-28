import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";

import type { CompetitionAttachment } from "@/lib/competitions";
import { CompetitionAttachments } from "./competition-attachments";

afterEach(cleanup);

const base = { description: null, requirement: "Required", allowedFormats: ["Pdf"] } as const;

describe("CompetitionAttachments", () => {
  it("links the template of a requirement for download without signing in (T-102)", () => {
    const attachments = [
      { ...base, id: "r1", title: "Oświadczenie", template: { fileName: "wzor.pdf", format: "Pdf", sizeInBytes: 10 } },
      { ...base, id: "r2", title: "Statut", template: null },
    ] as unknown as CompetitionAttachment[];

    render(<CompetitionAttachments attachments={attachments} />);

    const link = screen.getByRole("link", { name: "Pobierz wzór: wzor.pdf" });
    expect(link.getAttribute("href")).toContain("/public/attachment-templates/r1");
    expect(screen.getAllByRole("link")).toHaveLength(1);
  });
});
