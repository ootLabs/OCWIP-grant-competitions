import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";

import type { Ranking } from "@/lib/operator-evaluation";

import { ResultsBar } from "./results-bar";

function ranking(overrides: Partial<Ranking>): Ranking {
  return {
    competitionId: "c1",
    settings: {} as Ranking["settings"],
    rows: [],
    totalPool: 100000,
    awardedTotal: 84000,
    resultsApprovedAt: null,
    ...overrides,
  } as Ranking;
}

afterEach(cleanup);

describe("ResultsBar", () => {
  it("says what is left of the pool", () => {
    render(<ResultsBar ranking={ranking({})} onApprove={vi.fn()} />);

    expect(screen.getByText(/zostało 16\s000,00\s*zł/)).toBeDefined();
  });

  it("says in words that the pool is exceeded, not only in colour", () => {
    render(<ResultsBar ranking={ranking({ awardedTotal: 104000 })} onApprove={vi.fn()} />);

    expect(screen.getByText(/pula przekroczona o 4\s?000,00\s*zł/)).toBeDefined();
  });

  it("approves only after the confirmation and shows why it could not", async () => {
    const onApprove = vi.fn().mockResolvedValue("Nie wszystkie wnioski mają zakończoną ocenę (czeka: 2).");

    render(<ResultsBar ranking={ranking({})} onApprove={onApprove} />);

    fireEvent.click(screen.getByRole("button", { name: "Zatwierdź wyniki konkursu" }));
    expect(onApprove).not.toHaveBeenCalled();

    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "Zatwierdź", hidden: true }));
    });

    expect(onApprove).toHaveBeenCalledTimes(1);
    expect(screen.getByText(/czeka: 2/)).toBeDefined();
  });

  it("offers no approval once the results are approved", () => {
    render(<ResultsBar ranking={ranking({ resultsApprovedAt: "2026-06-01T10:00:00Z" })} onApprove={vi.fn()} />);

    expect(screen.getByText(/Wyniki zatwierdzono/)).toBeDefined();
    expect(screen.queryByRole("button")).toBeNull();
  });
});
