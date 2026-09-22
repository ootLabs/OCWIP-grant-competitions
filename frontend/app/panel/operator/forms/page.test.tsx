import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import FormsPage from "./page";

function respondWith(body: unknown, status = 200) {
  vi.stubGlobal(
    "fetch",
    vi.fn().mockImplementation(async () => new Response(JSON.stringify(body), { status })),
  );
}

function competition(overrides: Record<string, unknown>) {
  return {
    id: "c1",
    number: "1/2026",
    title: "Granty na inicjatywy",
    formDefinitionId: null,
    ...overrides,
  };
}

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("FormsPage", () => {
  it("shows an empty state when there are no competitions yet", async () => {
    respondWith([]);

    render(<FormsPage />);

    expect(await screen.findByText("Nie ma jeszcze żadnego konkursu")).toBeDefined();
  });

  it("lists competitions and tells apart the ones with a form", async () => {
    respondWith([
      competition({ id: "a", number: "1/2026", formDefinitionId: null }),
      competition({ id: "b", number: "2/2026", formDefinitionId: "f1" }),
    ]);

    render(<FormsPage />);

    await waitFor(() => expect(screen.getByText(/1\/2026/)).toBeDefined());
    expect(screen.getByText("Brak formularza")).toBeDefined();
    expect(screen.getByText("Ma formularz, kreator otwiera jego kopię")).toBeDefined();

    const links = screen.getAllByRole("link", { name: "Otwórz kreator" });
    expect(links).toHaveLength(2);
    expect(links[0].getAttribute("href")).toBe("/panel/operator/forms/a");
  });

  it("offers a retry when the competitions cannot be read", async () => {
    respondWith(null, 500);

    render(<FormsPage />);

    expect(await screen.findByText(/Nie udało się pobrać listy konkursów/)).toBeDefined();
  });
});
