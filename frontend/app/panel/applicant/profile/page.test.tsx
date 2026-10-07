import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";

import { emptyCard } from "@/lib/entity-card";

import ProfilePage from "./page";

type Routes = Record<string, { body: unknown; status?: number }>;

/** Answers by the end of the path; anything else is a 404. A fresh Response per call. */
function respondWith(routes: Routes) {
  const fetch = vi.fn().mockImplementation(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    const key = Object.keys(routes)
      .sort((a, b) => b.length - a.length)
      .find((path) => url.endsWith(path) && (!path.startsWith("POST ") || init?.method === "POST"));
    const route = key ? routes[key] : { body: { status: 404 }, status: 404 };
    const status = route.status ?? 200;
    return status === 204
      ? new Response(null, { status })
      : new Response(JSON.stringify(route.body), {
          status,
          headers: { "content-type": status >= 400 ? "application/problem+json" : "application/json" },
        });
  });
  vi.stubGlobal("fetch", fetch);
  return fetch;
}

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

const founderCard = {
  id: "e1",
  updatedAt: "2026-09-20T10:00:00Z",
  card: { ...emptyCard(), name: "Fundacja Testowa", bankAccount: "73111111111111111111111111" },
  isFounder: true,
  members: [
    { firstName: "Anna", lastName: "Testowa", isFounder: true, since: "2026-09-20T10:00:00Z" },
    { firstName: "Jan", lastName: "Skarbnik", isFounder: false, since: "2026-09-25T10:00:00Z" },
  ],
};

const summary = { id: "e1", type: "Organisation", name: "Fundacja Testowa", isFounder: true, updatedAt: "2026-09-20T10:00:00Z" };

describe("Mój profil", () => {
  it("says the card comes with the first application while there is none", async () => {
    respondWith({ "/me/entities": { body: [] }, "/me/access-requests": { body: [] } });

    render(<ProfilePage />);

    expect(await screen.findByText("Nie mamy jeszcze danych Twojego podmiotu")).toBeDefined();
    expect(screen.getByRole("link", { name: "Zobacz aktualne konkursy" })).toBeDefined();
  });

  it("shows the card with who has access and lets it be corrected at any time", async () => {
    respondWith({
      "/me/entities": { body: [summary] },
      "/me/entities/e1": { body: founderCard },
      "/me/entities/e1/access-requests": { body: [] },
      "/me/access-requests": { body: [] },
    });

    render(<ProfilePage />);

    expect(await screen.findByRole("heading", { name: "Fundacja Testowa" })).toBeDefined();
    expect(screen.getByText("73 1111 1111 1111 1111 1111 1111")).toBeDefined();
    const members = screen.getByRole("region", { name: "Osoby z dostępem" });
    expect(within(members).getByText(/Jan Skarbnik/)).toBeDefined();

    fireEvent.click(screen.getByRole("button", { name: "Popraw dane" }));
    expect(screen.getByRole("button", { name: "Zapisz poprawki" })).toBeDefined();
  });

  it("lets the founder approve a request to join (T-93a)", async () => {
    const fetch = respondWith({
      "/me/entities": { body: [summary] },
      "/me/entities/e1": { body: founderCard },
      "/me/entities/e1/access-requests": {
        body: [{ id: "r1", firstName: "Ola", lastName: "Nowa", email: "ola@example.org", requestedAt: "2026-10-01T10:00:00Z" }],
      },
      "/me/entities/e1/access-requests/r1/decision": { body: null, status: 204 },
      "/me/access-requests": { body: [] },
    });

    render(<ProfilePage />);

    const requests = await screen.findByRole("region", { name: "Prośby o dostęp" });
    expect(within(requests).getByText(/ola@example.org/)).toBeDefined();
    fireEvent.click(within(requests).getByRole("button", { name: "Zatwierdź" }));

    await vi.waitFor(() =>
      expect(fetch.mock.calls.some(([url, init]) => String(url).endsWith("/r1/decision") && init?.method === "POST")).toBe(true),
    );
    const decision = fetch.mock.calls.find(([url]) => String(url).endsWith("/r1/decision"))!;
    expect(JSON.parse(String(decision[1]!.body))).toEqual({ approve: true });
  });

  it("does not offer requests to a member who did not found the card", async () => {
    const fetch = respondWith({
      "/me/entities": { body: [{ ...summary, isFounder: false }] },
      "/me/entities/e1": { body: { ...founderCard, isFounder: false } },
      "/me/access-requests": { body: [] },
    });

    render(<ProfilePage />);

    expect(await screen.findByRole("heading", { name: "Fundacja Testowa" })).toBeDefined();
    expect(screen.queryByRole("region", { name: "Prośby o dostęp" })).toBeNull();
    expect(fetch.mock.calls.some(([url]) => String(url).endsWith("/access-requests") && String(url).includes("/me/entities/"))).toBe(false);
  });

  it("lists the person's own requests and where they stand", async () => {
    respondWith({
      "/me/entities": { body: [] },
      "/me/access-requests": {
        body: [{ id: "r1", entityName: "Fundacja Cudza", status: "Pending", requestedAt: "2026-10-01T10:00:00Z", decidedAt: null }],
      },
    });

    render(<ProfilePage />);

    const mine = await screen.findByRole("region", { name: "Twoje prośby o dostęp" });
    expect(within(mine).getByText(/Fundacja Cudza: czeka na decyzję/)).toBeDefined();
  });
});
