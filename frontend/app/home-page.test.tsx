import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, within } from "@testing-library/react";

vi.mock("next/navigation", () => ({ usePathname: () => "/", useRouter: () => ({ push: vi.fn() }) }));

import HomePage from "./page";

const competition = (overrides: Record<string, unknown>) => ({
  id: "c1",
  number: "1/2026",
  title: "Kierunek NOWE FIO 2026",
  description: null,
  status: "OpenForApplications",
  intake: { acceptsApplications: true, state: "Open", opensAt: "2026-09-01T06:00:00Z", closesAt: "2026-10-30T11:00:00Z", message: "Nabór trwa." },
  startDate: "2026-09-01T06:00:00Z",
  endDate: "2026-10-30T11:00:00Z",
  isContinuousIntake: false,
  maxGrantAmount: 7000,
  attachments: [],
  contacts: [],
  ...overrides,
});

function respondWith(list: unknown[]) {
  vi.stubGlobal(
    "fetch",
    vi.fn(async (input: RequestInfo | URL) =>
      String(input).endsWith("/me")
        ? new Response(null, { status: 401 })
        : new Response(JSON.stringify(list), { status: 200, headers: { "content-type": "application/json" } }),
    ),
  );
}

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("Strona główna", () => {
  it("leads to sign in, registration, the competitions and the results without typing an address", async () => {
    respondWith([
      competition({}),
      competition({ id: "c2", number: "1/2025", title: "Poprzednia edycja", status: "Resolved", intake: { acceptsApplications: false, state: "Closed", opensAt: "2025-09-01T06:00:00Z", closesAt: "2025-10-30T11:00:00Z", message: "Nabór zamknięty." } }),
    ]);

    render(await HomePage());

    const main = screen.getByRole("main");
    expect(within(main).getByRole("link", { name: "Załóż konto" }).getAttribute("href")).toBe("/register");
    expect(within(main).getByRole("link", { name: "Zaloguj się" }).getAttribute("href")).toBe("/login");
    expect(screen.getByRole("link", { name: "Kierunek NOWE FIO 2026" }).getAttribute("href")).toBe("/competitions/c1");
    expect(screen.getByRole("link", { name: "Wyniki konkursu 1/2025: Poprzednia edycja" }).getAttribute("href")).toBe(
      "/competitions/c2/results",
    );
    expect(screen.getByRole("link", { name: "Wszystkie konkursy, także zakończone" }).getAttribute("href")).toBe("/competitions");
    expect(screen.getByRole("link", { name: "Archiwum wyników" }).getAttribute("href")).toBe("/archive");
    // Each open call sits under "Otwarte nabory", one level down.
    expect(screen.getByRole("heading", { level: 3, name: "Kierunek NOWE FIO 2026" })).toBeDefined();
    // The closed one is not offered as open.
    expect(screen.queryByRole("link", { name: "Poprzednia edycja" })).toBeNull();
  });

  it("says there is no open call rather than showing an empty page", async () => {
    respondWith([]);

    render(await HomePage());

    expect(screen.getByText("Nie ma teraz otwartego naboru")).toBeDefined();
    expect(screen.queryByRole("heading", { name: "Wyniki" })).toBeNull();
  });
});
