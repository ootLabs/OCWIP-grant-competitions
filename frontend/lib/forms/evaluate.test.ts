import { describe, expect, it } from "vitest";
import {
  computeRowValue,
  computeTopLevelValue,
  isFieldVisible,
  isSectionVisible,
} from "./evaluate";
import type { FormAnswers } from "./answer-types";
import type { FormDocument, FormField } from "./document-types";

function shortText(key: string): FormField {
  return {
    key,
    type: "shortText",
    label: key,
    help: "",
    required: false,
    printed: true,
    maxLength: 100,
  };
}

describe("visibility", () => {
  it("is visible with no condition", () => {
    expect(isFieldVisible(shortText("a"), {})).toBe(true);
  });

  it("reads a singleChoice condition from its stored string value", () => {
    const dependent = {
      ...shortText("forma_prawna_inna"),
      visibleWhen: { field: "forma_prawna", equalsAnyOf: ["inna"] },
    };
    expect(isFieldVisible(dependent, { forma_prawna: "inna" })).toBe(true);
    expect(isFieldVisible(dependent, { forma_prawna: "stowarzyszenie" })).toBe(false);
    expect(isFieldVisible(dependent, {})).toBe(false);
  });

  it("reads a yesNo condition from its stored boolean value, wire form true/false", () => {
    const dependent = {
      ...shortText("adres_papierowy"),
      visibleWhen: { field: "papier", equalsAnyOf: ["true"] },
    };
    expect(isFieldVisible(dependent, { papier: true })).toBe(true);
    expect(isFieldVisible(dependent, { papier: false })).toBe(false);
  });

  it("matches a multipleChoice answer when any selected value is in the list", () => {
    const dependent = {
      ...shortText("dependent"),
      visibleWhen: { field: "formaty", equalsAnyOf: ["pdf"] },
    };
    expect(isFieldVisible(dependent, { formaty: ["jpg", "pdf"] })).toBe(true);
    expect(isFieldVisible(dependent, { formaty: ["jpg"] })).toBe(false);
  });

  it("evaluates a section's own visibleWhen the same way", () => {
    const section: FormDocument["sections"][number] = {
      key: "rozwoj",
      title: "Rozwój instytucjonalny",
      description: "",
      visibleWhen: { field: "forma_prawna", equalsAnyOf: ["stowarzyszenie", "fundacja"] },
      fields: [],
    };
    expect(isSectionVisible(section, { forma_prawna: "fundacja" })).toBe(true);
    expect(isSectionVisible(section, { forma_prawna: "inna" })).toBe(false);
  });
});

describe("computeRowValue", () => {
  const budgetTable: FormField = {
    key: "budzet_a",
    type: "repeatableTable",
    label: "Koszty bezpośrednie",
    help: "",
    required: true,
    printed: true,
    table: {
      columns: [
        {
          key: "liczba",
          type: "number",
          label: "Liczba jednostek",
          help: "",
          required: true,
          printed: true,
        },
        {
          key: "cena",
          type: "amount",
          label: "Cena jednostkowa",
          help: "",
          required: true,
          printed: true,
        },
        {
          key: "wartosc",
          type: "calculated",
          label: "Wartość",
          help: "",
          required: false,
          printed: true,
          calculation: { kind: "product", operands: ["liczba", "cena"] },
        },
      ],
    },
  };

  it("multiplies the row's numeric columns", () => {
    expect(computeRowValue(budgetTable, { liczba: 3, cena: 150 }, "wartosc")).toBe(450);
  });

  it("treats a missing or blank answer as zero, not NaN", () => {
    expect(computeRowValue(budgetTable, { liczba: 3 }, "wartosc")).toBe(0);
    expect(computeRowValue(budgetTable, { liczba: 3, cena: "" }, "wartosc")).toBe(0);
  });

  it("reads a plain column straight from the row", () => {
    expect(computeRowValue(budgetTable, { liczba: 5 }, "liczba")).toBe(5);
  });
});

describe("computeTopLevelValue", () => {
  function document(): FormDocument {
    return {
      schemaVersion: 1,
      sections: [
        {
          key: "budzet",
          title: "Budżet",
          description: "",
          fields: [
            {
              key: "budzet_a",
              type: "repeatableTable",
              label: "Koszty bezpośrednie",
              help: "",
              required: true,
              printed: true,
              table: {
                columns: [
                  { key: "liczba", type: "number", label: "Liczba", help: "", required: true, printed: true },
                  { key: "cena", type: "amount", label: "Cena", help: "", required: true, printed: true },
                  {
                    key: "wartosc",
                    type: "calculated",
                    label: "Wartość",
                    help: "",
                    required: false,
                    printed: true,
                    calculation: { kind: "product", operands: ["liczba", "cena"] },
                  },
                ],
              },
            },
            {
              key: "suma_a",
              type: "calculated",
              label: "Suma A",
              help: "",
              required: false,
              printed: true,
              calculation: { kind: "sum", operands: ["budzet_a.wartosc"] },
            },
            {
              key: "suma_c",
              type: "calculated",
              label: "Suma C",
              help: "",
              required: false,
              printed: true,
              calculation: { kind: "sum", operands: [] },
            },
            {
              key: "dotacja",
              type: "calculated",
              label: "Kwota dotacji",
              help: "",
              required: false,
              printed: true,
              calculation: { kind: "difference", operands: ["suma_a", "suma_c"] },
            },
          ],
        },
      ],
    };
  }

  it("sums a table column across every row", () => {
    const answers: FormAnswers = {
      budzet_a: [
        { liczba: 2, cena: 100 },
        { liczba: 1, cena: 50 },
      ],
    };
    const field = document().sections[0].fields[1];
    expect(computeTopLevelValue(document(), answers, field)).toBe(250);
  });

  it("resolves a calculated field that depends on another calculated field (D11)", () => {
    const answers: FormAnswers = {
      budzet_a: [{ liczba: 3, cena: 100 }],
      suma_c: 30,
    };
    const dotacja = document().sections[0].fields[3];
    // suma_a = 300 (3 * 100), suma_c has no operands (0 by sum of nothing,
    // deliberately probing that an empty operand list is 0, not the raw
    // answer, since D11's engine must never read a typed value for this).
    expect(computeTopLevelValue(document(), answers, dotacja)).toBe(300);
  });

  it("reads a non-calculated field's raw numeric answer", () => {
    const number: FormField = {
      key: "liczba_uczestnikow",
      type: "number",
      label: "Liczba uczestników",
      help: "",
      required: true,
      printed: true,
    };
    expect(computeTopLevelValue(document(), { liczba_uczestnikow: 42 }, number)).toBe(42);
  });
});

describe("evaluation cards (T-38, T-40)", () => {
  function yesNo(key: string, extra: Partial<FormField> = {}): FormField {
    return { key, type: "yesNo", label: key, help: "", required: true, printed: true, ...extra };
  }

  it("hides a criterion from an applicant it is not asked of, and only then", () => {
    const income = yesNo("przychod", { appliesTo: ["Organisation"] });

    expect(isFieldVisible(income, {}, "Organisation")).toBe(true);
    expect(isFieldVisible(income, {}, "InformalGroup")).toBe(false);
    // An application form never names an applicant, so nothing is hidden.
    expect(isFieldVisible(income, {})).toBe(true);
  });

  it("counts a scored yes in a sum and nothing for a no or no answer", () => {
    const document: FormDocument = {
      schemaVersion: 1,
      sections: [
        {
          key: "s",
          title: "S",
          description: "",
          fields: [
            yesNo("plamy", { points: 1 }),
            yesNo("patron", { points: 1 }),
            {
              key: "suma",
              type: "calculated",
              label: "Suma",
              help: "",
              required: false,
              printed: true,
              calculation: { kind: "sum", operands: ["plamy", "patron"] },
            },
          ],
        },
      ],
    };
    const sum = document.sections[0].fields[2];

    expect(computeTopLevelValue(document, { plamy: true, patron: false }, sum)).toBe(1);
    expect(computeTopLevelValue(document, { plamy: true, patron: true }, sum)).toBe(2);
    expect(computeTopLevelValue(document, {}, sum)).toBe(0);
  });
});
