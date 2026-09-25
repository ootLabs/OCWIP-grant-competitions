import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import ApplicationsPage from "./page";

function competition(overrides: Record<string, unknown>) {
  return { id: "c1", number: "1/2026", title: "Granty na inicjatywy", status: "Published", ...overrides };
}

function respondWith(body: unknown, status = 200) {
  vi.stubGlobal(
    "fetch",
    vi.fn().mockImplementation(async () => new Response(JSON.stringify(body), { status })),
  );
}

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("ApplicationsPage", () => {
  it("leads into the list of each announced competition and skips drafts", async () => {
    respondWith([
      competition({ id: "a", number: "1/2026", status: "Published" }),
      competition({ id: "b", number: "2/2026", title: "Szkic konkursu", status: "Draft" }),
    ]);

    render(<ApplicationsPage />);

    const link = await screen.findByRole("link", { name: "Lista wniosków" });
    expect(link.getAttribute("href")).toBe("/panel/operator/applications/a");
    expect(screen.queryByText(/Szkic konkursu/)).toBeNull();
  });

  it("says why there is nothing yet and where to go when no competition is announced", async () => {
    respondWith([competition({ status: "Draft" })]);

    render(<ApplicationsPage />);

    expect(await screen.findByText("Nie ma jeszcze żadnego wniosku")).toBeDefined();
    expect(screen.getByRole("link", { name: "Przejdź do konkursów" })).toBeDefined();
  });

  it("offers a retry that asks the network again", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(new Response("boom", { status: 500 }))
      .mockResolvedValueOnce(new Response(JSON.stringify([competition({})]), { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);

    render(<ApplicationsPage />);

    await screen.findByText(/Nie udało się pobrać listy konkursów/);
    fireEvent.click(screen.getByRole("button", { name: "Spróbuj ponownie" }));

    await screen.findByRole("link", { name: "Lista wniosków" });
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });
});
