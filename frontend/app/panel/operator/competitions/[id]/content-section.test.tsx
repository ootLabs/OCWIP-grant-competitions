import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";

import type { OperatorCompetition } from "@/lib/operator-competitions";

import { ContentSection } from "./content-section";

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { "content-type": "application/json" } });

const competition = { id: "target", isActive: true } as OperatorCompetition;

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("Karty oceny i wzór sprawozdania", () => {
  it("names the version in force of each part, and what is missing", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async (input: RequestInfo | URL) => {
        const url = String(input);
        if (url.endsWith("/target/evaluation-cards/formal")) return json([{ versionNumber: 2, isCurrent: true }]);
        return json([]);
      }),
    );

    render(<ContentSection competition={competition} onCopied={vi.fn()} />);

    expect(await screen.findByText("Karta oceny formalnej: wersja 2")).toBeDefined();
    expect(screen.getByText("Karta oceny merytorycznej: brak")).toBeDefined();
    expect(screen.getByText("Wzór sprawozdania: brak")).toBeDefined();
  });

  it("copies the versions in force from another competition as new versions here", async () => {
    const posted: string[] = [];
    vi.stubGlobal(
      "fetch",
      vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
        const url = String(input);
        const method = init?.method ?? "GET";
        if (method === "POST") {
          posted.push(url);
          return json({ versionNumber: 1 }, 201);
        }
        if (url.endsWith("/competitions")) return json([{ id: "source", number: "1/2026", title: "Poprzedni" }, competition]);
        if (url.includes("/source/evaluation-cards/") && !url.match(/\/\d+$/)) return json([{ versionNumber: 1, isCurrent: true }]);
        if (url.match(/\/source\/evaluation-cards\/(formal|merit)\/1$/)) return json({ definition: { schemaVersion: 1, sections: [] } });
        return json([]);
      }),
    );
    const onCopied = vi.fn();

    render(<ContentSection competition={competition} onCopied={onCopied} />);
    fireEvent.change(await screen.findByLabelText("Skopiuj z konkursu"), { target: { value: "source" } });
    fireEvent.click(screen.getByRole("button", { name: "Skopiuj karty i wzór" }));

    expect(await screen.findByText("Skopiowano: karta oceny formalnej, karta oceny merytorycznej.")).toBeDefined();
    expect(posted.map((url) => url.replace(/^.*\/competitions\//, ""))).toEqual([
      "target/evaluation-cards/formal",
      "target/evaluation-cards/merit",
    ]);
    expect(onCopied).toHaveBeenCalled();
  });
});
