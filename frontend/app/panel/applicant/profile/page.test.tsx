import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";

import { emptyCard } from "@/lib/entity-card";

import ProfilePage from "./page";

function respondWith(body: unknown, status = 200) {
  vi.stubGlobal(
    "fetch",
    vi.fn().mockResolvedValue(
      new Response(JSON.stringify(body), {
        status,
        headers: { "content-type": status >= 400 ? "application/problem+json" : "application/json" },
      }),
    ),
  );
}

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("Mój profil", () => {
  it("says the card comes with the first application while there is none", async () => {
    respondWith({ title: "Not Found", status: 404 }, 404);

    render(<ProfilePage />);

    expect(await screen.findByText("Nie mamy jeszcze danych Twojego podmiotu")).toBeDefined();
    expect(screen.getByRole("link", { name: "Zobacz aktualne konkursy" })).toBeDefined();
  });

  it("shows the card and lets it be corrected at any time", async () => {
    respondWith({
      id: "e1",
      updatedAt: "2026-09-20T10:00:00Z",
      card: { ...emptyCard(), name: "Fundacja Testowa", bankAccount: "73111111111111111111111111" },
    });

    render(<ProfilePage />);

    expect(await screen.findByText("Fundacja Testowa")).toBeDefined();
    expect(screen.getByText("73 1111 1111 1111 1111 1111 1111")).toBeDefined();

    fireEvent.click(screen.getByRole("button", { name: "Popraw dane" }));
    expect(screen.getByRole("button", { name: "Zapisz poprawki" })).toBeDefined();
  });
});
