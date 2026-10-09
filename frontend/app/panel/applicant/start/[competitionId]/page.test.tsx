import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";

const replace = vi.fn();
vi.mock("next/navigation", () => ({
  useParams: () => ({ competitionId: "c1" }),
  useRouter: () => ({ replace }),
}));

const createDraft = vi.fn();
vi.mock("@/lib/applicant-applications", () => ({
  createDraft: (...args: unknown[]) => createDraft(...args),
}));

import { emptyCard } from "@/lib/entity-card";

import StartApplicationPage from "./page";

const card = {
  id: "e1",
  updatedAt: "2026-09-20T10:00:00Z",
  card: { ...emptyCard(), name: "Fundacja Testowa", nip: "1111111111" },
};

function respondWith(body: unknown, status = 200) {
  vi.stubGlobal(
    "fetch",
    // A fresh Response per call: a body can be read only once, and the
    // correction test reads one for GET and another for PUT.
    vi.fn().mockImplementation(async (input: RequestInfo | URL) =>
      // "Co przygotować" reads the public competition; these tests are
      // about the card, so the competition is simply not found.
      String(input).includes("/public/competitions/")
        ? new Response(JSON.stringify({ status: 404 }), { status: 404, headers: { "content-type": "application/problem+json" } })
        : new Response(JSON.stringify(body), {
        status,
        headers: { "content-type": status >= 400 ? "application/problem+json" : "application/json" },
      }),
    ),
  );
}

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  replace.mockReset();
  createDraft.mockReset();
});

describe("Nowy wniosek: dane wnioskodawcy", () => {
  it("offers the empty card at the first application", async () => {
    respondWith({ title: "Not Found", status: 404 }, 404);

    render(<StartApplicationPage />);

    expect(await screen.findByRole("button", { name: "Zapisz dane i przejdź do wniosku" })).toBeDefined();
    expect(createDraft).not.toHaveBeenCalled();
  });

  it("shows the card filled in and starts the draft once the data are confirmed", async () => {
    respondWith(card);
    createDraft.mockResolvedValue({ id: "app-1" });

    render(<StartApplicationPage />);

    expect(await screen.findByText("Fundacja Testowa")).toBeDefined();
    fireEvent.click(screen.getByRole("button", { name: "Dane są aktualne" }));

    expect(createDraft).toHaveBeenCalledWith("c1");
    await vi.waitFor(() => expect(replace).toHaveBeenCalledWith("/panel/applicant/applications/app-1"));
  });

  it("opens the card for correction before the draft", async () => {
    respondWith(card);

    render(<StartApplicationPage />);
    fireEvent.click(await screen.findByRole("button", { name: /^Popraw/ }));

    expect((screen.getByLabelText("Pełna nazwa organizacji") as HTMLInputElement).value).toBe("Fundacja Testowa");
    expect(screen.getByRole("button", { name: "Zapisz poprawki i przejdź do wniosku" })).toBeDefined();
    expect(createDraft).not.toHaveBeenCalled();
  });

  it("leaves no enabled button to start a second draft while a corrected card starts one", async () => {
    respondWith(card);
    createDraft.mockReturnValue(new Promise(() => {}));

    render(<StartApplicationPage />);
    fireEvent.click(await screen.findByRole("button", { name: /^Popraw/ }));
    fireEvent.click(screen.getByRole("button", { name: "Zapisz poprawki i przejdź do wniosku" }));

    const starting = await screen.findByRole("button", { name: "Rozpoczynanie…" });
    expect((starting as HTMLButtonElement).disabled).toBe(true);
    expect(screen.queryByRole("button", { name: "Zapisz poprawki i przejdź do wniosku" })).toBeNull();
    expect(createDraft).toHaveBeenCalledTimes(1);
  });

  it("says why the draft could not start, for example a closed intake", async () => {
    respondWith(card);
    const { ApiError } = await import("@/lib/api-client");
    createDraft.mockRejectedValue(new ApiError(409, "failed", {}, "Nabór został zamknięty."));

    render(<StartApplicationPage />);
    fireEvent.click(await screen.findByRole("button", { name: "Dane są aktualne" }));

    expect((await screen.findByRole("alert")).textContent).toBe("Nabór został zamknięty.");
    expect(replace).not.toHaveBeenCalled();
  });
});
