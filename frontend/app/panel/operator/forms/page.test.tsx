import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
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

    const { container } = render(<FormsPage />);

    await waitFor(() => expect(screen.getByText(/1\/2026/)).toBeDefined());
    expect(screen.getByText("Brak formularza")).toBeDefined();
    expect(screen.getByText("Ma formularz, kreator otwiera jego kopię")).toBeDefined();

    const links = screen.getAllByRole("link", { name: "Otwórz kreator" });
    expect(links).toHaveLength(2);
    expect(links[0].getAttribute("href")).toBe("/panel/operator/forms/a");

    // Nothing technical leaks into a screen the client reads first, the same
    // guarantee app/panel/empty-screens.test.tsx checks for every other panel
    // screen (this page dropped out of that shared list in T-26 because it
    // reads real data instead of standing empty).
    expect(container.textContent).not.toMatch(/null|undefined/);
  });

  it("offers a retry that actually asks the network again", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(new Response("boom", { status: 500 }))
      .mockResolvedValueOnce(
        new Response(JSON.stringify([competition({ id: "a" })]), { status: 200 }),
      );
    vi.stubGlobal("fetch", fetchMock);

    render(<FormsPage />);

    await screen.findByText(/Nie udało się pobrać listy konkursów/);
    fireEvent.click(screen.getByRole("button", { name: "Spróbuj ponownie" }));

    await screen.findByText(/Granty na inicjatywy/);
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });
});
