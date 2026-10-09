import { describe, expect, it } from "vitest";
import { evaluateLimit } from "./limits";
import type { FormDocument } from "./document-types";

const document: FormDocument = {
  schemaVersion: 1,
  sections: [
    {
      key: "budzet",
      title: "Budżet",
      description: "",
      fields: [
        {
          key: "dotacja",
          type: "calculated",
          label: "Kwota dotacji",
          help: "",
          required: false,
          printed: true,
          calculation: { kind: "sum", operands: [] },
        },
      ],
    },
  ],
};

describe("evaluateLimit", () => {
  it("is invertible: says how much room is left, not just pass or fail (D12)", () => {
    const evaluation = evaluateLimit(
      document,
      {},
      { kind: "maxAmount", basis: "competition.maxGrantAmount" },
      8100,
      { maxGrantAmount: 9000 },
    );

    expect(evaluation).toEqual({ allowedAmount: 9000, remaining: 900, exceeded: false });
  });

  it("flags a limit exceeded with a negative remaining, not a clamp to zero", () => {
    const evaluation = evaluateLimit(
      document,
      {},
      { kind: "maxAmount", basis: "competition.maxGrantAmount" },
      9500,
      { maxGrantAmount: 9000 },
    );

    expect(evaluation).toEqual({ allowedAmount: 9000, remaining: -500, exceeded: true });
  });

  it("computes a maxPercentOf against a competition setting", () => {
    // Maximum 10% of the grant amount, as every 2026 template caps indirect
    // costs (docs/runbook/pola.md).
    const evaluation = evaluateLimit(
      document,
      {},
      { kind: "maxPercentOf", percent: 10, basis: "competition.maxGrantAmount" },
      1000,
      { maxGrantAmount: 9000 },
    );

    expect(evaluation).toEqual({ allowedAmount: 900, remaining: -100, exceeded: true });
  });

  it("computes a maxPercentOf against another field's computed value", () => {
    const evaluation = evaluateLimit(
      document,
      { dotacja: 0 },
      { kind: "maxPercentOf", percent: 50, basis: "dotacja" },
      100,
      {},
    );
    // dotacja is a calculated field: computeTopLevelValue reads its
    // calculation (sum of nothing), never the raw answer under that key.
    expect(evaluation).toEqual({ allowedAmount: 0, remaining: -100, exceeded: true });
  });

  it("returns null when the competition setting is not configured", () => {
    const evaluation = evaluateLimit(
      document,
      {},
      { kind: "maxAmount", basis: "competition.maxGrantAmount" },
      100,
      {},
    );
    expect(evaluation).toBeNull();
  });

  it("returns null when the basis names a field that does not exist", () => {
    const evaluation = evaluateLimit(
      document,
      {},
      { kind: "maxAmount", basis: "brak_pola" },
      100,
      {},
    );
    expect(evaluation).toBeNull();
  });
  it("takes a percentage from a competition setting (T-31)", () => {
    const evaluation = evaluateLimit(
      document,
      {},
      {
        kind: "maxPercentOf",
        percentFrom: "competition.maxIndirectCostPercent",
        basis: "competition.maxGrantAmount",
      },
      1000,
      { maxGrantAmount: 9000, maxIndirectCostPercent: 10 },
    );

    expect(evaluation).toEqual({ allowedAmount: 900, remaining: -100, exceeded: true });
  });

  it("checks nothing when that setting is empty, instead of a ceiling of zero", () => {
    const evaluation = evaluateLimit(
      document,
      {},
      {
        kind: "maxPercentOf",
        percentFrom: "competition.maxInstitutionalDevelopmentPercent",
        basis: "competition.maxGrantAmount",
      },
      1000,
      { maxGrantAmount: 9000 },
    );

    expect(evaluation).toBeNull();
  });
});

describe("evaluateLimit on a basis that counts the limited field itself (P4-13)", () => {
  const amount = (key: string) => ({
    key,
    type: "amount" as const,
    label: key,
    help: "",
    required: false,
    printed: true,
  });
  const budget: FormDocument = {
    schemaVersion: 1,
    sections: [
      {
        key: "budzet",
        title: "Budżet",
        description: "",
        fields: [
          amount("razem_a"),
          amount("razem_b"),
          amount("razem_c"),
          {
            key: "dotacja",
            type: "calculated",
            label: "Kwota dotacji",
            help: "",
            required: false,
            printed: true,
            calculation: { kind: "sum", operands: ["razem_a", "razem_b", "razem_c"] },
          },
        ],
      },
    ],
  };
  const limit = { kind: "maxPercentOf" as const, percent: 10, basis: "dotacja" };

  it("names the amount that clears the limit, not ten percent of a grant that shrinks with it", () => {
    const answers = { razem_a: 5000, razem_b: 200, razem_c: 800 };

    const evaluation = evaluateLimit(budget, answers, limit, 800, {}, "razem_c");

    // 0.1 x (5200 + C) >= C gives C <= 577.77..., rounded down to the grosz.
    expect(evaluation?.allowedAmount).toBe(577.77);
    expect(evaluation?.exceeded).toBe(true);
  });

  it("stops flagging once the named amount is entered", () => {
    const answers = { razem_a: 5000, razem_b: 200, razem_c: 577.77 };

    expect(evaluateLimit(budget, answers, limit, 577.77, {}, "razem_c")?.exceeded).toBe(false);
  });

  it("keeps the plain percentage when the limited field is not part of the basis", () => {
    const answers = { razem_a: 5000, razem_b: 200, razem_c: 800 };

    expect(evaluateLimit(budget, answers, limit, 800, {}, "razem_x")?.allowedAmount).toBe(600);
  });
});
