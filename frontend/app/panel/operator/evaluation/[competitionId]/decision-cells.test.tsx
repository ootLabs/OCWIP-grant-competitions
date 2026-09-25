import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";

import type { RankingRow } from "@/lib/operator-evaluation";

import { DecisionCells, parseAmount } from "./decision-cells";

const row = {
  rank: 1,
  applicationId: "a1",
  number: "1/2026/1",
  awardedGrant: null,
  decisionNote: null,
} as unknown as RankingRow;

function renderCells(props: Partial<Parameters<typeof DecisionCells>[0]> = {}) {
  const onSave = vi.fn().mockResolvedValue(true);
  render(
    <table>
      <tbody>
        <tr>
          <DecisionCells row={row} locked={false} onSave={onSave} {...props} />
        </tr>
      </tbody>
    </table>,
  );
  return onSave;
}

afterEach(cleanup);

describe("parseAmount", () => {
  it("reads a Polish or a dotted amount and refuses zero and fractions of a grosz", () => {
    expect(parseAmount("5 000,50")).toBe(5000.5);
    expect(parseAmount("5000.5")).toBe(5000.5);
    expect(parseAmount("")).toBeNull();
    expect(parseAmount("0")).toBe("invalid");
    expect(parseAmount("12,345")).toBe("invalid");
    expect(parseAmount("abc")).toBe("invalid");
  });
});

describe("DecisionCells", () => {
  it("saves the amount and the note when the field is left", () => {
    const onSave = renderCells();

    fireEvent.change(screen.getByLabelText("Kwota przyznana dla wniosku 1/2026/1"), { target: { value: "6 500" } });
    fireEvent.blur(screen.getByLabelText("Kwota przyznana dla wniosku 1/2026/1"));

    expect(onSave).toHaveBeenCalledWith("a1", 6500, null);
  });

  it("says what is wrong and sends nothing for an amount that is not one", () => {
    const onSave = renderCells();

    const input = screen.getByLabelText("Kwota przyznana dla wniosku 1/2026/1");
    fireEvent.change(input, { target: { value: "0" } });
    fireEvent.blur(input);

    expect(onSave).not.toHaveBeenCalled();
    expect(input.getAttribute("aria-invalid")).toBe("true");
    expect(screen.getByText(/Wpisz kwotę większą od zera/)).toBeDefined();
  });

  it("does not save a field left unchanged", () => {
    const onSave = renderCells();

    fireEvent.blur(screen.getByLabelText("Uwagi do decyzji dla wniosku 1/2026/1"));

    expect(onSave).not.toHaveBeenCalled();
  });

  it("shows the decision as text once the results are approved", () => {
    renderCells({ locked: true, row: { ...row, awardedGrant: 5000, decisionNote: "Obniżona" } as RankingRow });

    expect(screen.queryByRole("textbox")).toBeNull();
    expect(screen.getByText("Obniżona")).toBeDefined();
  });
});
