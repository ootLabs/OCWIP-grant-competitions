import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import CompetitionApplicationsPage from "./page";

vi.mock("next/navigation", () => ({
  useParams: () => ({ competitionId: "c1" }),
}));

function item(index: number, overrides: Record<string, unknown> = {}) {
  return {
    id: `a${index}`,
    number: String(index).padStart(3, "0"),
    entityName: `Stowarzyszenie ${index}`,
    entityType: "Organisation",
    projectTitle: `Projekt ${index}`,
    totalCost: 1000,
    requestedGrant: 500,
    status: "Submitted",
    submittedAt: "2026-09-15T10:30:00Z",
    ...overrides,
  };
}

function list(applications: unknown[], overrides: Record<string, unknown> = {}) {
  return {
    competitionId: "c1",
    competitionNumber: "1/2026",
    competitionTitle: "Granty na inicjatywy",
    totalPoolAmount: 100000,
    requestedTotal: applications.length * 500,
    poolRemaining: 100000 - applications.length * 500,
    applications,
    ...overrides,
  };
}

function respondWith(body: unknown, status = 200) {
  vi.stubGlobal(
    "fetch",
    vi.fn().mockImplementation(async () => new Response(JSON.stringify(body), { status })),
  );
}

function bodyRows() {
  const [, body] = screen.getAllByRole("rowgroup");
  return within(body).getAllByRole("row");
}

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("CompetitionApplicationsPage", () => {
  it("shows every one of 120 offers with the columns the report lists and the totals below", async () => {
    respondWith(list(Array.from({ length: 120 }, (_, i) => item(i + 1))));

    render(<CompetitionApplicationsPage />);

    await screen.findByText("Wniosków: 120.");
    const headings = screen.getAllByRole("columnheader").map((cell) => cell.textContent);
    expect(headings).toEqual([
      "Lp.",
      "Numer wniosku ▲",
      "Nazwa podmiotu",
      "Rodzaj wnioskodawcy",
      "Tytuł projektu",
      "Całkowity koszt zadania",
      "Wnioskowana kwota",
      "Status",
      "Data złożenia",
    ]);
    expect(bodyRows()).toHaveLength(120);

    // A fixed layout: the widths do not follow the content, so neither
    // loading the rows nor re-sorting them can make the columns jump.
    expect(screen.getByRole("table").className).toContain("table-fixed");

    const footer = screen.getAllByRole("rowgroup")[2];
    expect(within(footer).getByText(/60\s000,00/)).toBeDefined();
    expect(within(footer).getByText(/40\s000,00/)).toBeDefined();
  });

  it("links every number to the submitted offer", async () => {
    respondWith(list([item(7)]));

    render(<CompetitionApplicationsPage />);

    const link = await screen.findByRole("link", { name: "007" });
    expect(link.getAttribute("href")).toBe("/panel/operator/applications/c1/a7");
  });

  it("sorts by a column and turns the order round on a second click", async () => {
    respondWith(
      list([
        item(1, { requestedGrant: 800 }),
        item(2, { requestedGrant: 200 }),
        item(3, { requestedGrant: 500 }),
      ]),
    );

    render(<CompetitionApplicationsPage />);

    const sort = await screen.findByRole("button", { name: "Wnioskowana kwota" });
    fireEvent.click(sort);
    expect(bodyRows().map((row) => within(row).getAllByRole("cell")[1].textContent)).toEqual([
      "002",
      "003",
      "001",
    ]);
    expect(sort.closest("th")?.getAttribute("aria-sort")).toBe("ascending");

    fireEvent.click(sort);
    expect(bodyRows().map((row) => within(row).getAllByRole("cell")[1].textContent)).toEqual([
      "001",
      "003",
      "002",
    ]);
    // The ordinal counts rows as shown, not the number they came in with.
    expect(within(bodyRows()[0]).getAllByRole("cell")[0].textContent).toBe("1");
  });

  it("filters by the kind of applicant and totals only what is left on screen", async () => {
    respondWith(
      list([
        item(1, { entityType: "InformalGroup", requestedGrant: 300 }),
        item(2, { entityType: "Organisation", requestedGrant: 700 }),
      ]),
    );

    render(<CompetitionApplicationsPage />);

    fireEvent.change(await screen.findByLabelText("Rodzaj wnioskodawcy"), {
      target: { value: "InformalGroup" },
    });

    expect(bodyRows()).toHaveLength(1);
    expect(screen.getByText("Widać 1 z 2 wniosków.")).toBeDefined();
    expect(screen.getByText("Suma wnioskowanych kwot (widoczne wiersze)")).toBeDefined();
    const footer = screen.getAllByRole("rowgroup")[2];
    expect(within(footer).getByText(/^300,00/)).toBeDefined();
  });

  it("says a value is missing rather than showing a zero", async () => {
    respondWith(list([item(1, { projectTitle: null, requestedGrant: null })], { totalPoolAmount: null, poolRemaining: null }));

    render(<CompetitionApplicationsPage />);

    expect(await screen.findAllByText("brak")).toHaveLength(2);
    expect(screen.getByText("pula nieustawiona")).toBeDefined();
  });

  it("offers both exports straight from the API", async () => {
    respondWith(list([item(1)]));

    render(<CompetitionApplicationsPage />);

    const csv = await screen.findByRole("link", { name: "Pobierz arkusz (CSV)" });
    const pdf = screen.getByRole("link", { name: "Pobierz PDF" });
    expect(csv.getAttribute("href")).toMatch(/\/competitions\/c1\/applications\/export\/csv$/);
    expect(pdf.getAttribute("href")).toMatch(/\/competitions\/c1\/applications\/export\/pdf$/);
  });

  it("explains an empty list without offering exports of nothing", async () => {
    respondWith(list([]));

    render(<CompetitionApplicationsPage />);

    expect(await screen.findByText("Do tego konkursu nie wpłynął jeszcze żaden wniosek")).toBeDefined();
    expect(screen.queryByRole("link", { name: "Pobierz PDF" })).toBeNull();
  });

  it("says the competition does not exist when the address leads nowhere", async () => {
    respondWith({ title: "Nie ma takiego konkursu." }, 404);

    render(<CompetitionApplicationsPage />);

    expect(await screen.findByText("Nie ma takiego konkursu")).toBeDefined();
  });
});
