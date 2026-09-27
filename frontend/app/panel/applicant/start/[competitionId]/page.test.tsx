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
    fireEvent.click(await screen.findByRole("button", { name: "Popraw" }));

    expect((screen.getByLabelText("Pełna nazwa organizacji") as HTMLInputElement).value).toBe("Fundacja Testowa");
    expect(screen.getByRole("button", { name: "Zapisz poprawki i przejdź do wniosku" })).toBeDefined();
    expect(createDraft).not.toHaveBeenCalled();
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
