import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import CompetitionsPage from "./page";

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
    status: "Draft",
    ...overrides,
  };
}

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("CompetitionsPage", () => {
  it("shows an empty state when there are no competitions yet", async () => {
    respondWith([]);

    render(<CompetitionsPage />);

    expect(await screen.findByText("Nie ma jeszcze żadnego konkursu")).toBeDefined();
  });

  it("always offers the entry point into the announcement wizard", async () => {
    respondWith([]);

    render(<CompetitionsPage />);

    const link = await screen.findByRole("link", { name: "Nowy konkurs" });
    expect(link.getAttribute("href")).toBe("/panel/operator/competitions/new");
  });

  it("lists competitions with their status in Polish", async () => {
    respondWith([
      competition({ id: "a", number: "1/2026", status: "Draft" }),
      competition({ id: "b", number: "2/2026", status: "Published" }),
    ]);

    render(<CompetitionsPage />);

    await waitFor(() => expect(screen.getByText(/1\/2026/)).toBeDefined());
    expect(screen.getByText("Roboczy")).toBeDefined();
    expect(screen.getByText("Ogłoszony")).toBeDefined();

    const links = screen.getAllByRole("link", { name: "Formularz wniosku" });
    expect(links[0].getAttribute("href")).toBe("/panel/operator/forms/a");
  });

  it("offers a retry that actually asks the network again", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(new Response("boom", { status: 500 }))
      .mockResolvedValueOnce(
        new Response(JSON.stringify([competition({ id: "a" })]), { status: 200 }),
      );
    vi.stubGlobal("fetch", fetchMock);

    render(<CompetitionsPage />);

    await screen.findByText(/Nie udało się pobrać listy konkursów/);
    fireEvent.click(screen.getByRole("button", { name: "Spróbuj ponownie" }));

    await screen.findByText(/Granty na inicjatywy/);
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });
});
