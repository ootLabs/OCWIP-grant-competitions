import { afterEach, describe, expect, it, vi } from "vitest";
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";

import type { RankingRow } from "@/lib/operator-evaluation";

import { RankingTable } from "./ranking-table";

function row(overrides: Partial<RankingRow>): RankingRow {
  return {
    rank: 1,
    applicationId: "a1",
    number: "1/2026/1",
    entityName: "Stowarzyszenie Przykład",
    entityType: "Organization",
    projectTitle: "Ławki w parku",
    requestedGrant: 7000,
    submittedAt: "2026-04-10T10:00:00Z",
    formal: "Passed",
    meritCardsFinished: 2,
    meritCardsRequired: 2,
    meritScore: 81,
    strategicScore: 2,
    totalScore: 83,
    passesThreshold: true,
    diverges: false,
    recommendedGrant: 6500,
    ...overrides,
  } as RankingRow;
}

const reviewers = [
  { id: "r1", name: "Anna Ekspert", email: "anna@example.org" },
  { id: "r2", name: "Jan Ekspert", email: "jan@example.org" },
];

afterEach(cleanup);

describe("RankingTable", () => {
  it("shows the place, the progress and the scores of a row", () => {
    render(
      <RankingTable
        rows={[
          row({}),
          row({
            applicationId: "a2",
            rank: null,
            formal: "Failed",
            meritCardsFinished: 0,
            passesThreshold: false,
            diverges: true,
          }),
        ]}
        reviewers={reviewers}
        assignments={[]}
        onAssign={vi.fn()}
        onUnassign={vi.fn()}
      />,
    );

    const [first, second] = screen.getAllByRole("row").slice(1);
    expect(first.textContent).toContain("Pozytywna");
    expect(first.textContent).toContain("2 z 2");
    expect(first.textContent).toContain("83");
    expect(second.textContent).toContain("Negatywna");
    expect(second.textContent).toContain("poniżej");
    expect(second.textContent).toContain("Rozbieżne oceny ekspertów");
  });

  it("assigns only an expert who is not on the application yet, and takes one off", async () => {
    const onAssign = vi.fn().mockResolvedValue(true);
    const onUnassign = vi.fn().mockResolvedValue(undefined);

    render(
      <RankingTable
        rows={[row({})]}
        reviewers={reviewers}
        assignments={[{ applicationId: "a1", reviewerId: "r1" }]}
        onAssign={onAssign}
        onUnassign={onUnassign}
      />,
    );

    const select = screen.getByLabelText(
      "Ekspert do przypisania do wniosku 1/2026/1",
    );
    expect(
      Array.from(select.querySelectorAll("option")).map(
        (option) => option.textContent,
      ),
    ).toEqual(["wybierz eksperta", "Jan Ekspert"]);

    const assign = within(screen.getAllByRole("row")[1]).getByRole("button", { name: "Przypisz" });
    expect((assign as HTMLButtonElement).disabled).toBe(true);
    fireEvent.change(select, { target: { value: "r2" } });
    fireEvent.click(assign);
    await waitFor(() => expect(onAssign).toHaveBeenCalledWith(["a1"], "r2"));

    fireEvent.click(
      screen.getByRole("button", {
        name: /Cofnij przypisanie eksperta Anna Ekspert/,
      }),
    );
    await waitFor(() => expect(onUnassign).toHaveBeenCalledWith("a1", "r1"));
  });

  it("assigns one expert to the selected applications and skips one who has them already", async () => {
    const onAssign = vi.fn().mockResolvedValue(true);

    render(
      <RankingTable
        rows={[row({}), row({ applicationId: "a2", number: "1/2026/2" }), row({ applicationId: "a3", number: "1/2026/3" })]}
        reviewers={reviewers}
        assignments={[{ applicationId: "a2", reviewerId: "r1" }]}
        onAssign={onAssign}
        onUnassign={vi.fn()}
      />,
    );

    fireEvent.click(screen.getByLabelText("Zaznacz wniosek 1/2026/1"));
    fireEvent.click(screen.getByLabelText("Zaznacz wniosek 1/2026/2"));
    expect(screen.getByText("Zaznaczone wnioski: 2")).toBeDefined();

    fireEvent.change(screen.getByLabelText("Przypisz zaznaczone ekspertowi"), { target: { value: "r1" } });
    fireEvent.click(screen.getAllByRole("button", { name: "Przypisz" })[0]);

    await waitFor(() => expect(onAssign).toHaveBeenCalledWith(["a1"], "r1"));
    await screen.findByText("Zaznaczone wnioski: 0");
  });

  it("selects every application with one box", () => {
    render(
      <RankingTable
        rows={[row({}), row({ applicationId: "a2", number: "1/2026/2" })]}
        reviewers={reviewers}
        assignments={[]}
        onAssign={vi.fn()}
        onUnassign={vi.fn()}
      />,
    );

    fireEvent.click(screen.getByLabelText("Zaznacz wszystkie wnioski"));
    expect(screen.getByText("Zaznaczone wnioski: 2")).toBeDefined();
    fireEvent.click(screen.getByLabelText("Zaznacz wszystkie wnioski"));
    expect(screen.getByText("Zaznaczone wnioski: 0")).toBeDefined();
  });

  it("keeps the selection when the assignment was refused", async () => {
    const onAssign = vi.fn().mockResolvedValue(false);

    render(
      <RankingTable
        rows={[row({}), row({ applicationId: "a2", number: "1/2026/2" })]}
        reviewers={reviewers}
        assignments={[]}
        onAssign={onAssign}
        onUnassign={vi.fn()}
      />,
    );

    fireEvent.click(screen.getByLabelText("Zaznacz wszystkie wnioski"));
    fireEvent.change(screen.getByLabelText("Przypisz zaznaczone ekspertowi"), {
      target: { value: "r1" },
    });
    fireEvent.click(screen.getAllByRole("button", { name: "Przypisz" })[0]);

    await waitFor(() => expect(onAssign).toHaveBeenCalledWith(["a1", "a2"], "r1"));
    expect(screen.getByText("Zaznaczone wnioski: 2")).toBeDefined();
  });
});
