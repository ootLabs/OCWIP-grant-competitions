import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import EvaluationPage from "./page";

function competition(overrides: Record<string, unknown>) {
  return {
    id: "c1",
    number: "1/2026",
    title: "Kierunek NOWE FIO",
    status: "Published",
    ...overrides,
  };
}

function respondWith(body: unknown, status = 200) {
  vi.stubGlobal(
    "fetch",
    vi
      .fn()
      .mockImplementation(
        async () => new Response(JSON.stringify(body), { status }),
      ),
  );
}

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("EvaluationPage", () => {
  it("leads into the evaluation of each announced competition and skips drafts", async () => {
    respondWith([
      competition({ id: "a" }),
      competition({ id: "b", title: "Szkic", status: "Draft" }),
    ]);

    render(<EvaluationPage />);

    const link = await screen.findByRole("link", {
      name: "Konkurs 1/2026: Kierunek NOWE FIO",
    });
    expect(link.getAttribute("href")).toBe("/panel/operator/evaluation/a");
    expect(screen.queryByText(/Szkic/)).toBeNull();
  });

  it("says what is missing when nothing is announced yet", async () => {
    respondWith([competition({ status: "Draft" })]);

    render(<EvaluationPage />);

    expect(
      await screen.findByText("Nie ma jeszcze konkursu do oceny"),
    ).toBeDefined();
  });
});
