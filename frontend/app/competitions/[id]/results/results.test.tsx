import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, within } from "@testing-library/react";

const fetchPublicResults = vi.fn();
const notFound = vi.fn(() => {
  throw new Error("NEXT_NOT_FOUND");
});

vi.mock("@/lib/competitions", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/competitions")>()),
  fetchPublicResults: (id: string) => fetchPublicResults(id),
}));
vi.mock("next/navigation", () => ({ notFound: () => notFound() }));

const { default: ResultsPage } = await import("./page");

const params = Promise.resolve({ id: "c1" });

function row(overrides: Record<string, unknown>) {
  return {
    rank: 1,
    number: "1/2026/1",
    entityName: "Stowarzyszenie Łąka",
    projectTitle: "Ławki w parku",
    totalScore: 43,
    awardedGrant: 6500,
    status: "Funded",
    ...overrides,
  };
}

afterEach(cleanup);

describe("wyniki konkursu", () => {
  it("lists the funded applications with the grant and the reserve list without one", async () => {
    fetchPublicResults.mockResolvedValue({
      competitionId: "c1",
      competitionNumber: "1/2026",
      competitionTitle: "Kierunek NOWE FIO",
      approvedAt: "2026-06-01T10:00:00Z",
      rows: [row({}), row({ rank: 2, number: "1/2026/2", entityName: "Grupa Sąsiedzi", awardedGrant: null, status: "Reserve" })],
    });

    render(await ResultsPage({ params }));

    const [funded, reserve] = screen.getAllByRole("table");
    expect(within(funded).getByText("Stowarzyszenie Łąka")).toBeDefined();
    expect(within(funded).getByText(/6\s?500,00/)).toBeDefined();
    expect(within(reserve).getByText("Grupa Sąsiedzi")).toBeDefined();
    expect(within(reserve).queryByText("Kwota dotacji")).toBeNull();
  });

  it("does not exist before the results are approved", async () => {
    fetchPublicResults.mockResolvedValue(null);

    await expect(ResultsPage({ params })).rejects.toThrow("NEXT_NOT_FOUND");
  });
});
