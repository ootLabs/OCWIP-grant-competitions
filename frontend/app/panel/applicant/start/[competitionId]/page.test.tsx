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

function cardOf(id: string, name: string) {
  return {
    id,
    updatedAt: "2026-09-20T10:00:00Z",
    card: { ...emptyCard(), name, nip: "1111111111" },
    isFounder: true,
    members: [{ firstName: "Anna", lastName: "Testowa", isFounder: true, since: "2026-09-20T10:00:00Z" }],
  };
}

function summaryOf(card: ReturnType<typeof cardOf>) {
  return { id: card.id, type: "Organisation", name: card.card.name, isFounder: true, updatedAt: card.updatedAt };
}

const card = cardOf("e1", "Fundacja Testowa");

/**
 * The list of cards answers /me/entities, one card /me/entities/{id}, and a
 * correction its PUT. A fresh Response per call: a body can be read only once.
 */
function respondWith(cards: ReturnType<typeof cardOf>[]) {
  vi.stubGlobal(
    "fetch",
    vi.fn().mockImplementation(async (input: RequestInfo | URL) => {
      const url = String(input);
      const json = (body: unknown, status = 200) =>
        new Response(JSON.stringify(body), {
          status,
          headers: { "content-type": status >= 400 ? "application/problem+json" : "application/json" },
        });

      // "Co przygotować" reads the public competition; these tests are
      // about the card, so the competition is simply not found.
      if (url.includes("/public/competitions/")) {
        return json({ status: 404 }, 404);
      }

      const one = cards.find((item) => url.endsWith(`/me/entities/${item.id}`));
      return one ? json(one) : json(cards.map(summaryOf));
    }),
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
    respondWith([]);

    render(<StartApplicationPage />);

    expect(await screen.findByRole("button", { name: "Zapisz dane i przejdź do wniosku" })).toBeDefined();
    expect(createDraft).not.toHaveBeenCalled();
  });

  it("shows the only card filled in and starts the draft for it once the data are confirmed", async () => {
    respondWith([card]);
    createDraft.mockResolvedValue({ id: "app-1" });

    render(<StartApplicationPage />);

    expect(await screen.findByText("Fundacja Testowa")).toBeDefined();
    expect(screen.queryByText("W imieniu którego podmiotu składasz wniosek?")).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "Dane są aktualne" }));

    expect(createDraft).toHaveBeenCalledWith("c1", "e1");
    await vi.waitFor(() => expect(replace).toHaveBeenCalledWith("/panel/applicant/applications/app-1"));
  });

  it("asks on whose behalf when the person acts for several cards (T-93a)", async () => {
    const group = cardOf("e2", "Sąsiedzi z Zaodrza");
    respondWith([card, group]);
    createDraft.mockResolvedValue({ id: "app-2" });

    render(<StartApplicationPage />);

    expect(await screen.findByText("W imieniu którego podmiotu składasz wniosek?")).toBeDefined();
    expect(screen.queryByRole("button", { name: "Dane są aktualne" })).toBeNull();

    fireEvent.click(screen.getByRole("radio", { name: /Sąsiedzi z Zaodrza/ }));
    fireEvent.click(await screen.findByRole("button", { name: "Dane są aktualne" }));

    expect(createDraft).toHaveBeenCalledWith("c1", "e2");
  });

  it("lets somebody with a card add another organisation", async () => {
    respondWith([card]);

    render(<StartApplicationPage />);
    fireEvent.click(await screen.findByRole("button", { name: "Dodaj podmiot" }));

    expect(screen.getByRole("button", { name: "Zapisz dane i przejdź do wniosku" })).toBeDefined();
    expect((screen.getByLabelText("Pełna nazwa organizacji") as HTMLInputElement).value).toBe("");
  });

  it("opens the card for correction before the draft", async () => {
    respondWith([card]);

    render(<StartApplicationPage />);
    fireEvent.click(await screen.findByRole("button", { name: "Popraw" }));

    expect((screen.getByLabelText("Pełna nazwa organizacji") as HTMLInputElement).value).toBe("Fundacja Testowa");
    expect(screen.getByRole("button", { name: "Zapisz poprawki i przejdź do wniosku" })).toBeDefined();
    expect(createDraft).not.toHaveBeenCalled();
  });

  it("leaves no enabled button to start a second draft while a corrected card starts one", async () => {
    respondWith([card]);
    createDraft.mockReturnValue(new Promise(() => {}));

    render(<StartApplicationPage />);
    fireEvent.click(await screen.findByRole("button", { name: "Popraw" }));
    fireEvent.click(screen.getByRole("button", { name: "Zapisz poprawki i przejdź do wniosku" }));

    const starting = await screen.findByRole("button", { name: "Rozpoczynanie…" });
    expect((starting as HTMLButtonElement).disabled).toBe(true);
    expect(screen.queryByRole("button", { name: "Zapisz poprawki i przejdź do wniosku" })).toBeNull();
    expect(createDraft).toHaveBeenCalledTimes(1);
  });

  it("says why the draft could not start, for example a closed intake", async () => {
    respondWith([card]);
    const { ApiError } = await import("@/lib/api-client");
    createDraft.mockRejectedValue(new ApiError(409, "failed", {}, "Nabór został zamknięty."));

    render(<StartApplicationPage />);
    fireEvent.click(await screen.findByRole("button", { name: "Dane są aktualne" }));

    expect((await screen.findByRole("alert")).textContent).toBe("Nabór został zamknięty.");
    expect(replace).not.toHaveBeenCalled();
  });
});
