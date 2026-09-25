import { describe, expect, it } from "vitest";
import type { FormDocument, FormField } from "./document-types";
import { roleChoices, roleFitsField } from "./field-roles";

function field(overrides: Partial<FormField> & Pick<FormField, "key" | "type">): FormField {
  return { label: overrides.key, help: "", required: false, printed: true, ...overrides };
}

function documentOf(...fields: FormField[]): FormDocument {
  return {
    schemaVersion: 1,
    sections: [{ key: "sekcja", title: "Sekcja", description: "", fields }],
  };
}

describe("roleFitsField", () => {
  it("keeps the title to a short text", () => {
    expect(roleFitsField("projectTitle", field({ key: "a", type: "shortText" }))).toBe(true);
    expect(roleFitsField("projectTitle", field({ key: "a", type: "longText" }))).toBe(false);
  });

  it("keeps amounts to an amount or a calculated field that is not a percentage", () => {
    expect(roleFitsField("totalCost", field({ key: "a", type: "amount" }))).toBe(true);
    expect(roleFitsField("totalCost", field({ key: "a", type: "number" }))).toBe(false);
    expect(
      roleFitsField(
        "requestedGrant",
        field({ key: "a", type: "calculated", calculation: { kind: "difference", operands: [] } }),
      ),
    ).toBe(true);
    expect(
      roleFitsField(
        "requestedGrant",
        field({ key: "a", type: "calculated", calculation: { kind: "ratio", operands: [] } }),
      ),
    ).toBe(false);
  });
});

describe("roleChoices", () => {
  it("names the field that already holds a role, but not the field itself", () => {
    const cost = field({ key: "koszt", type: "amount", role: "totalCost" });
    const grant = field({ key: "dotacja", type: "amount", role: "requestedGrant" });
    const document = documentOf(cost, grant);

    expect(roleChoices(document, grant)).toEqual([
      { role: "totalCost", takenBy: cost },
      { role: "requestedGrant", takenBy: null },
    ]);
  });
});
