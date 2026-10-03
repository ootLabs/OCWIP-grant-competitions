import { describe, expect, it } from "vitest";

import { columnComputedUnit, topLevelComputedUnit } from "./computed-unit";
import { formatComputedNumber } from "./format-computed";
import type { FormDocument, FormField } from "./document-types";

function field(overrides: Partial<FormField> & Pick<FormField, "key" | "type">): FormField {
  return { label: "", help: "", required: false, printed: true, ...overrides } as FormField;
}

/** An evaluation card: four scores and their sum. */
const card: FormDocument = {
  schemaVersion: 1,
  sections: [
    {
      key: "kryteria",
      title: "Kryteria",
      description: "",
      fields: [
        field({ key: "pomysl", type: "number" }),
        field({ key: "rezultaty", type: "number" }),
        field({
          key: "suma",
          type: "calculated",
          calculation: { kind: "sum", operands: ["pomysl", "rezultaty"] },
        }),
      ],
    },
  ],
};

/** An application: a budget table and the totals under it. */
const application: FormDocument = {
  schemaVersion: 1,
  sections: [
    {
      key: "budzet",
      title: "Budżet",
      description: "",
      fields: [
        field({
          key: "koszty",
          type: "repeatableTable",
          table: {
            columns: [
              field({ key: "liczba", type: "number" }),
              field({ key: "cena", type: "amount" }),
              field({
                key: "wartosc",
                type: "calculated",
                calculation: { kind: "product", operands: ["liczba", "cena"] },
              }),
            ],
          },
        }),
        field({
          key: "razem",
          type: "calculated",
          calculation: { kind: "sum", operands: ["koszty.wartosc"] },
        }),
        field({
          key: "udzial",
          type: "calculated",
          calculation: { kind: "ratio", operands: ["razem", "razem"] },
        }),
      ],
    },
  ],
};

describe("computed unit", () => {
  // The bug this guards (B-GUI-15 of the manual walkthrough): the renderer
  // decided from the calculation kind alone, so everything that was not a
  // ratio was money, and an expert's "Suma (0-50 punktów)" read "44,00 zł".
  it("counts points where the operands are plain numbers", () => {
    const suma = card.sections[0].fields[2];

    expect(topLevelComputedUnit(card, suma)).toBe("number");
    expect(formatComputedNumber(44, "number")).toBe("44");
  });

  it("counts money where an operand is an amount, through a table column", () => {
    const razem = application.sections[0].fields[1];

    expect(topLevelComputedUnit(application, razem)).toBe("amount");
    expect(formatComputedNumber(5800, "amount")).toContain("zł");
  });

  it("keeps a ratio a percentage", () => {
    const udzial = application.sections[0].fields[2];

    expect(topLevelComputedUnit(application, udzial)).toBe("percent");
  });

  it("reads a calculated cell from the columns beside it", () => {
    const table = application.sections[0].fields[0];
    const wartosc = table.table!.columns[2];

    expect(columnComputedUnit(table, wartosc)).toBe("amount");
  });

  it("prints a fractional score without pretending it is money", () => {
    expect(formatComputedNumber(12.5, "number")).toBe("12,50");
  });
});
