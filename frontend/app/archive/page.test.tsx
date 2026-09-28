import { cleanup, render, screen, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

const fetchResultsArchive = vi.fn();

vi.mock("@/lib/competitions", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/competitions")>()),
  fetchResultsArchive: () => fetchResultsArchive(),
}));

const { default: ArchivePage } = await import("./page");

afterEach(cleanup);

describe("Archiwum wyników", () => {
  it("lists every resolved competition with its funded projects and a link to the full results", async () => {
    fetchResultsArchive.mockResolvedValue([
      {
        competitionId: "c2",
        competitionNumber: "2/2026",
        competitionTitle: "Kierunek NOWE FIO",
        approvedAt: "2026-06-01T10:00:00Z",
        projects: [
          { entityName: "Stowarzyszenie Łąka", projectTitle: "Ławki w parku", awardedGrant: 6500 },
          { entityName: "Grupa Sąsiedzi", projectTitle: "Wspólne podwórko", awardedGrant: 4000 },
        ],
      },
      { competitionId: "c1", competitionNumber: "1/2025", competitionTitle: "Mikrodotacje", approvedAt: "2025-06-01T10:00:00Z", projects: [] },
    ]);

    render(await ArchivePage());

    expect(screen.getByRole("heading", { level: 1, name: "Archiwum wyników" })).toBeDefined();
    const table = screen.getByRole("table");
    expect(within(table).getByText("Grupa Sąsiedzi")).toBeDefined();
    expect(within(table).getByText(/6\s?500,00/)).toBeDefined();
    expect(screen.getAllByRole("link", { name: "Pełne wyniki z listą rezerwową" })[0].getAttribute("href")).toBe(
      "/competitions/c2/results",
    );
    expect(screen.getByText("Żaden projekt nie otrzymał dofinansowania.")).toBeDefined();
  });

  it("says when nothing is resolved yet", async () => {
    fetchResultsArchive.mockResolvedValue([]);

    render(await ArchivePage());

    expect(screen.getByText("Nie ma jeszcze rozstrzygniętych konkursów")).toBeDefined();
  });
});
