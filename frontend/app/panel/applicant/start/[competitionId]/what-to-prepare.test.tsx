import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";

import type { PublicCompetition } from "@/lib/competitions";

import { WhatToPrepare } from "./what-to-prepare";

afterEach(cleanup);

const base = {
  intake: { message: "Nabór trwa do 30 października 2026, 12:00 czasu polskiego." },
  maxGrantAmount: 7000,
  maxAttachmentSizeInBytes: 10 * 1024 * 1024,
  maxApplicationSizeInBytes: 50 * 1024 * 1024,
  requiresPaperSubmission: false,
  paperSubmissionDeadline: null,
  paperSubmissionAddress: null,
  rulesUrl: null,
  attachments: [],
} as unknown as PublicCompetition;

describe("Co przygotować", () => {
  it("names the deadline, the ceiling and every required attachment with its formats", () => {
    render(
      <WhatToPrepare
        competition={{
          ...base,
          attachments: [
            { id: "a1", title: "Odpis z KRS", description: null, requirement: "Required", allowedFormats: ["Pdf"] },
          ],
        }}
      />,
    );

    expect(screen.getByRole("heading", { name: "Co przygotować" })).toBeDefined();
    expect(screen.getByText("Nabór trwa do 30 października 2026, 12:00 czasu polskiego.")).toBeDefined();
    expect(screen.getByText(/Odpis z KRS \(wymagany, PDF\)/)).toBeDefined();
  });

  it("says plainly when nothing is to be attached and when paper is required", () => {
    render(
      <WhatToPrepare
        competition={{ ...base, requiresPaperSubmission: true, paperSubmissionAddress: "ul. Damrota 4/36, Opole" }}
      />,
    );

    expect(screen.getByText("Konkurs nie wymaga załączników.")).toBeDefined();
    expect(screen.getByText(/Wersję papierową trzeba dostarczyć na adres: ul. Damrota 4\/36, Opole/)).toBeDefined();
  });
});
