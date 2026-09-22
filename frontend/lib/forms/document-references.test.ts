import { describe, expect, it } from "vitest";
import { findReferencesTo } from "./document-references";
import type { FormDocument } from "./document-types";

const budgetDocument: FormDocument = {
  schemaVersion: 1,
  sections: [
    {
      key: "wnioskodawca",
      title: "Wnioskodawca",
      description: "",
      fields: [
        {
          key: "forma_prawna",
          type: "singleChoice",
          label: "Forma prawna",
          help: "",
          required: true,
          printed: true,
          options: [{ value: "inna", label: "Inna" }],
        },
        {
          key: "forma_prawna_inna",
          type: "shortText",
          label: "Jaka forma prawna",
          help: "",
          required: true,
          printed: true,
          maxLength: 200,
          visibleWhen: { field: "forma_prawna", equalsAnyOf: ["inna"] },
        },
      ],
    },
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
        },
        {
          key: "suma_a",
          type: "calculated",
          label: "Suma tabeli A",
          help: "",
          required: false,
          printed: true,
          calculation: { kind: "sum", operands: ["budzet_a.wartosc"] },
          limits: [{ kind: "maxAmount", basis: "competition.maxGrantAmount" }],
        },
      ],
    },
  ],
};

describe("findReferencesTo", () => {
  it("finds a field referenced by a visibleWhen condition", () => {
    const references = findReferencesTo(budgetDocument, "forma_prawna");
    expect(references).toHaveLength(1);
    expect(references[0]).toMatchObject({
      fieldKey: "forma_prawna_inna",
      via: "visibleWhen",
    });
  });

  it("finds a table column referenced by a sibling column's calculation", () => {
    const references = findReferencesTo(budgetDocument, "liczba");
    expect(references).toHaveLength(1);
    expect(references[0]).toMatchObject({ fieldKey: "wartosc", via: "calculation" });
  });

  it("finds a table column referenced from outside the table by its qualified key", () => {
    const references = findReferencesTo(budgetDocument, "wartosc");
    expect(references).toHaveLength(1);
    expect(references[0]).toMatchObject({ fieldKey: "suma_a", via: "calculation" });
  });

  it("returns nothing for a field nobody reads", () => {
    expect(findReferencesTo(budgetDocument, "suma_a")).toHaveLength(0);
  });

  it("finds a column referenced by a product calculation on its sibling", () => {
    const references = findReferencesTo(budgetDocument, "cena");
    expect(references).toHaveLength(1);
    expect(references[0]).toMatchObject({ fieldKey: "wartosc", via: "calculation" });
  });
});
