import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import ApplicationsPage from "./page";

function overview(overrides: Record<string, unknown>) {
  return {
    id: "a1",
    competitionId: "c1",
    competitionNumber: "1/2026",
    competitionTitle: "Granty na inicjatywy",
    status: "Draft",
    number: null,
    submittedAt: null,
    lastSavedAt: "2026-09-12T10:32:00Z",
    ...overrides,
  };
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

describe("ApplicationsPage (applicant)", () => {
  it("lists a draft and a submitted application as two separate rows, D9 included", async () => {
    respondWith([
      overview({ id: "draft-1", competitionId: "c1", status: "Draft" }),
      overview({
        id: "submitted-1",
        competitionId: "c1",
        status: "Submitted",
        number: "001",
        submittedAt: "2026-09-13T09:00:00Z",
      }),
    ]);

    render(<ApplicationsPage />);

    const continueLink = await screen.findByRole("link", { name: "Wypełnij dalej" });
    expect(continueLink.getAttribute("href")).toBe("/panel/applicant/applications/draft-1");

    const viewLink = screen.getByRole("link", { name: "Zobacz wniosek" });
    expect(viewLink.getAttribute("href")).toBe("/panel/applicant/applications/submitted-1");

    expect(screen.getByText(/nr 001/)).toBeDefined();
  });

  it("shows the result of an approved competition and still opens the submitted application", async () => {
    respondWith([
      overview({ status: "Funded", number: "007", submittedAt: "2026-04-10T09:00:00Z" }),
    ]);

    render(<ApplicationsPage />);

    expect(await screen.findByText(/Dofinansowany, umowa niepodpisana/)).toBeDefined();
    expect(screen.getByText(/złożono/)).toBeDefined();
    expect(screen.getByRole("link", { name: "Zobacz wniosek" })).toBeDefined();
  });

  it("invites a correction of an application returned for it (T-103)", async () => {
    respondWith([overview({ id: "r-1", status: "Returned", number: "004", submittedAt: "2026-09-13T09:00:00Z" })]);

    render(<ApplicationsPage />);

    expect(await screen.findByText(/Zwrócony do poprawy/)).toBeDefined();
    expect(screen.getByText("Wnioski zwrócone do poprawy: 1.")).toBeDefined();
    expect(screen.getByRole("link", { name: "Popraw wniosek" }).getAttribute("href")).toBe(
      "/panel/applicant/applications/r-1",
    );
  });

  it("says where to start when there is nothing yet", async () => {
    respondWith([]);

    render(<ApplicationsPage />);

    expect(await screen.findByText("Nie masz jeszcze żadnego wniosku")).toBeDefined();
    expect(
      screen.getByRole("link", { name: "Zobacz aktualne konkursy" }).getAttribute("href"),
    ).toBe("/panel/applicant/competitions");
  });

  it("offers a retry that asks the network again", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(new Response("boom", { status: 500 }))
      .mockResolvedValueOnce(new Response(JSON.stringify([overview({})]), { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);

    render(<ApplicationsPage />);

    await screen.findByText(/Nie udało się pobrać listy wniosków/);
    fireEvent.click(screen.getByRole("button", { name: "Spróbuj ponownie" }));

    await screen.findByRole("link", { name: "Wypełnij dalej" });
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });
  it("narrows the list to one group and says so when the group is empty", async () => {
    respondWith([
      overview({ id: "draft-1", status: "Draft" }),
      overview({ id: "submitted-1", status: "Submitted", number: "001", submittedAt: "2026-09-13T09:00:00Z" }),
    ]);

    render(<ApplicationsPage />);
    await screen.findByRole("link", { name: "Wypełnij dalej" });

    fireEvent.click(screen.getByRole("button", { name: "Robocze" }));
    expect(screen.getByRole("button", { name: "Robocze" }).getAttribute("aria-pressed")).toBe("true");
    expect(screen.getByRole("link", { name: "Wypełnij dalej" })).toBeDefined();
    expect(screen.queryByRole("link", { name: "Zobacz wniosek" })).toBeNull();

    fireEvent.click(screen.getByRole("button", { name: "Zakończone" }));
    expect(screen.getByText("Nie masz wniosków w tej grupie.")).toBeDefined();

    fireEvent.click(screen.getByRole("button", { name: "Wszystkie" }));
    expect(screen.getByRole("link", { name: "Zobacz wniosek" })).toBeDefined();
  });
});
