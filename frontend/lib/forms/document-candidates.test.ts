import { describe, expect, it } from "vitest";
import {
  calculationOperandCandidates,
  conditionValueOptions,
  limitBasisCandidates,
  visibilityCandidates,
} from "./document-candidates";
import type { FormDocument, FormField } from "./document-types";

function shortText(key: string): FormField {
  return {
    key,
    type: "shortText",
    label: key,
    help: "",
    required: true,
    printed: true,
    maxLength: 100,
  };
}

const document: FormDocument = {
  schemaVersion: 1,
  sections: [
    {
      key: "s1",
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
          options: [
            { value: "stowarzyszenie", label: "Stowarzyszenie" },
            { value: "inna", label: "Inna" },
          ],
        },
        shortText("forma_prawna_inna"),
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
                key: "opis",
                type: "shortText",
                label: "Opis pozycji",
                help: "",
                required: true,
                printed: true,
                maxLength: 200,
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
          calculation: { kind: "sum", operands: [] },
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
      ],
    },
  ],
};

describe("visibilityCandidates", () => {
  it("offers a choice field for a field standing below it in the same section", () => {
    const candidates = visibilityCandidates(document, "s1", "forma_prawna_inna");
    expect(candidates).toEqual([{ key: "forma_prawna", label: "Forma prawna" }]);
  });

  it("offers nothing to the very first field of the document", () => {
    expect(visibilityCandidates(document, "s1", "forma_prawna")).toEqual([]);
  });

  it("offers every earlier section's fields to a later section", () => {
    const candidates = visibilityCandidates(document, "budzet");
    expect(candidates.map((c) => c.key)).toEqual(["forma_prawna"]);
  });

  it("does not offer table columns as a condition source", () => {
    const candidates = visibilityCandidates(document, "budzet", "suma_c");
    expect(candidates.some((c) => c.key === "liczba")).toBe(false);
  });

  it("does not offer the first section's own field to its own condition", () => {
    // Regression: a section whose own first field is a choice field used to
    // leak into that same section's visibility picker as a candidate for
    // itself, which is nonsensical (nothing in the section has been shown,
    // let alone answered, before the section itself is).
    expect(visibilityCandidates(document, "s1")).toEqual([]);
  });
});

describe("conditionValueOptions", () => {
  it("offers Tak/Nie for a yesNo field", () => {
    const field: FormField = {
      key: "papier",
      type: "yesNo",
      label: "Wersja papierowa",
      help: "",
      required: true,
      printed: true,
    };
    expect(conditionValueOptions(field)).toEqual([
      { key: "true", label: "Tak" },
      { key: "false", label: "Nie" },
    ]);
  });

  it("offers the field's own options for a choice field", () => {
    const field = document.sections[0].fields[0];
    expect(conditionValueOptions(field)).toEqual([
      { key: "stowarzyszenie", label: "Stowarzyszenie" },
      { key: "inna", label: "Inna" },
    ]);
  });
});

describe("calculationOperandCandidates", () => {
  it("offers numeric sibling columns for a column's own calculation", () => {
    const candidates = calculationOperandCandidates(
      document,
      "budzet",
      "budzet_a",
      "cena",
      "product",
    );
    expect(candidates).toEqual([{ key: "liczba", label: "Liczba jednostek" }]);
  });

  it("excludes the text column, which is not numeric", () => {
    const candidates = calculationOperandCandidates(
      document,
      "budzet",
      "budzet_a",
      "liczba",
      "product",
    );
    expect(candidates.some((c) => c.key === "opis")).toBe(false);
  });

  it("offers qualified table columns for a section level sum", () => {
    const candidates = calculationOperandCandidates(
      document,
      "budzet",
      "suma_a",
      undefined,
      "sum",
    );
    expect(candidates).toEqual(
      expect.arrayContaining([
        { key: "budzet_a.liczba", label: "Koszty bezpośrednie / Liczba jednostek" },
        { key: "budzet_a.cena", label: "Koszty bezpośrednie / Cena jednostkowa" },
      ]),
    );
    expect(candidates.some((c) => c.key === "budzet_a.opis")).toBe(false);
  });

  it("offers the totals of other tables to a section level sum, but not itself", () => {
    // The grant is the total of three cost tables (T-31).
    const candidates = calculationOperandCandidates(
      document,
      "budzet",
      "suma_a",
      undefined,
      "sum",
    );
    expect(candidates).toContainEqual({ key: "suma_c", label: "Suma C" });
    expect(candidates.some((c) => c.key === "suma_a")).toBe(false);
  });

  it("offers other section level numeric fields for a difference, excluding itself", () => {
    const candidates = calculationOperandCandidates(
      document,
      "budzet",
      "suma_a",
      undefined,
      "difference",
    );
    expect(candidates).toEqual([{ key: "suma_c", label: "Suma C" }]);
  });
});

describe("limitBasisCandidates", () => {
  it("offers competition settings and other numeric fields of the section", () => {
    const candidates = limitBasisCandidates(document, "budzet", "suma_a");
    expect(candidates.some((c) => c.key === "competition.maxGrantAmount")).toBe(true);
    expect(candidates.some((c) => c.key === "suma_c")).toBe(true);
    expect(candidates.some((c) => c.key === "suma_a")).toBe(false);
  });
});
