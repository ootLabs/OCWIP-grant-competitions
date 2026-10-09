import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";

import AccessRequestsPage from "./page";

const overdue = {
  id: "r1",
  entityName: "Fundacja Testowa",
  nip: "5260001246",
  requesterFirstName: "Ola",
  requesterLastName: "Nowa",
  requesterEmail: "ola@example.org",
  founderFirstName: "Anna",
  founderLastName: "Testowa",
  founderEmail: "anna@example.org",
  requestedAt: "2026-09-20T10:00:00Z",
};

function respondWith(list: unknown[]) {
  const fetch = vi.fn().mockImplementation(async (input: RequestInfo | URL, init?: RequestInit) =>
    init?.method === "POST"
      ? new Response(null, { status: 204 })
      : new Response(JSON.stringify(String(input).endsWith("/access-requests/escalated") ? list : []), {
          status: 200,
          headers: { "content-type": "application/json" },
        }),
  );
  vi.stubGlobal("fetch", fetch);
  return fetch;
}

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("Prośby o dostęp (T-93a)", () => {
  it("says nothing waits when no request is seven days old", async () => {
    respondWith([]);

    render(<AccessRequestsPage />);

    expect(await screen.findByText("Nie ma próśb czekających dłużej niż 7 dni")).toBeDefined();
  });

  it("shows who asks and who founded the card, so both can be phoned", async () => {
    respondWith([overdue]);

    render(<AccessRequestsPage />);

    expect(await screen.findByText(/Fundacja Testowa, NIP 5260001246/)).toBeDefined();
    expect(screen.getByText(/ola@example.org/)).toBeDefined();
    expect(screen.getByText(/anna@example.org/)).toBeDefined();
  });

  it("decides only with a note on how the person was checked", async () => {
    const fetch = respondWith([overdue]);

    render(<AccessRequestsPage />);
    fireEvent.click(await screen.findByRole("button", { name: "Zatwierdź dostęp" }));

    expect((await screen.findByRole("alert")).textContent).toBe("Zapisz, jak sprawdzono osobę proszącą o dostęp.");
    expect(fetch.mock.calls.some(([, init]) => init?.method === "POST")).toBe(false);

    fireEvent.change(screen.getByLabelText("Jak sprawdzono osobę proszącą"), { target: { value: " Telefon do zarządu " } });
    fireEvent.click(screen.getByRole("button", { name: "Zatwierdź dostęp" }));

    await vi.waitFor(() => expect(fetch.mock.calls.some(([, init]) => init?.method === "POST")).toBe(true));
    const [url, init] = fetch.mock.calls.find(([, call]) => call?.method === "POST")!;
    expect(String(url)).toContain("/access-requests/r1/decision");
    expect(JSON.parse(String(init!.body))).toEqual({ approve: true, note: "Telefon do zarządu" });
  });
});
