import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import ReviewersPage from "./page";

function respondWith(body: unknown, status = 200) {
  vi.stubGlobal(
    "fetch",
    // The team list below (T-104) asks for its own address; it is not what these tests are about.
    vi.fn().mockImplementation(async (url: string) =>
      String(url).includes("/accounts/team")
        ? new Response(JSON.stringify([]), { status: 200 })
        : new Response(JSON.stringify(body), { status }),
    ),
  );
}

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("ReviewersPage", () => {
  it("lists the expert accounts by name and address", async () => {
    respondWith([{ id: "r1", name: "Anna Ekspert", email: "anna@example.org" }]);

    render(<ReviewersPage />);

    expect(await screen.findByText("Anna Ekspert (anna@example.org)")).toBeDefined();
  });

  it("says what is missing and what happens next when there is no expert", async () => {
    respondWith([]);

    render(<ReviewersPage />);

    expect(await screen.findByText("Nie ma jeszcze żadnego recenzenta")).toBeDefined();
    expect(screen.getByText(/przypiszesz im wnioski/)).toBeDefined();
  });

  it("says so when the list cannot be read", async () => {
    respondWith({ title: "boom" }, 500);

    render(<ReviewersPage />);

    expect(await screen.findByText("Nie udało się pobrać listy recenzentów.")).toBeDefined();
  });
});
