import { describe, expect, it } from "vitest";
import {
  fieldMoveBlockers,
  conditionViolations,
  sectionMoveBlockers,
  sectionRemovalBlockers,
} from "./section-guards";
import type { FormDocument, FormField, FormSection } from "./document-types";

function field(key: string, overrides: Partial<FormField> = {}): FormField {
  return {
    key,
    type: "shortText",
    label: key,
    help: "",
    required: true,
    printed: true,
    ...overrides,
  };
}

function section(key: string, fields: FormField[], overrides: Partial<FormSection> = {}): FormSection {
  return { key, title: key, description: "", fields, ...overrides };
}

/** s1 asks a yes or no question, s3 is shown only when the answer was yes. */
function document(): FormDocument {
  return {
    schemaVersion: 1,
    sections: [
      section("s1", [field("zgoda", { type: "yesNo", label: "Zgoda" })]),
      section("s2", [field("b", { label: "Pole B" })]),
      section("s3", [
        field("c", {
          label: "Pole C",
          visibleWhen: { field: "zgoda", equalsAnyOf: ["true"] },
        }),
      ]),
    ],
  };
}

describe("conditionViolations", () => {
  it("is empty for a document whose conditions all read upwards", () => {
    expect(conditionViolations(document())).toEqual([]);
  });

  it("names the condition and the answer it reads when the order is wrong", () => {
    const broken = document();
    const violations = conditionViolations({
      ...broken,
      sections: [broken.sections[2], broken.sections[1], broken.sections[0]],
    });

    expect(violations).toEqual([
      { id: "s3:c", dependentLabel: "Pole C", sourceLabel: "Zgoda" },
    ]);
  });

  it("ignores a condition pointing at a field that does not exist", () => {
    const broken = document();
    expect(
      conditionViolations({
        ...broken,
        sections: [
          section("s1", [
            field("a", { visibleWhen: { field: "nie_ma_takiego", equalsAnyOf: ["true"] } }),
          ]),
        ],
      }),
    ).toEqual([]);
  });

  it("measures a section's own condition at its first field", () => {
    const base = document();
    const withSectionCondition: FormDocument = {
      ...base,
      sections: [
        base.sections[0],
        { ...base.sections[1], visibleWhen: { field: "zgoda", equalsAnyOf: ["true"] } },
        base.sections[2],
      ],
    };

    expect(conditionViolations(withSectionCondition)).toEqual([]);

    const moved: FormDocument = {
      ...withSectionCondition,
      sections: [withSectionCondition.sections[1], withSectionCondition.sections[0], withSectionCondition.sections[2]],
    };

    expect(conditionViolations(moved)).toEqual([
      { id: "s2", dependentLabel: 'sekcja "s2"', sourceLabel: "Zgoda" },
    ]);
  });

  it("does not throw on a section without fields, which a draft may hold", () => {
    expect(conditionViolations({ schemaVersion: 1, sections: [section("pusta", [])] })).toEqual([]);
  });
});

/** The answer and the section reading it, with nothing between them. */
function adjacent(): FormDocument {
  return {
    schemaVersion: 1,
    sections: [document().sections[0], document().sections[2]],
  };
}

describe("sectionMoveBlockers", () => {
  it("blocks pulling a dependent section above the answer it reads", () => {
    expect(sectionMoveBlockers(adjacent(), "s3", "up")).toEqual([
      { id: "s3:c", dependentLabel: "Pole C", sourceLabel: "Zgoda" },
    ]);
  });

  it("blocks pushing the answer below the section that reads it", () => {
    expect(sectionMoveBlockers(adjacent(), "s1", "down")).toEqual([
      { id: "s3:c", dependentLabel: "Pole C", sourceLabel: "Zgoda" },
    ]);
  });

  it("allows a move that only swaps two sections neither of which reads the other", () => {
    expect(sectionMoveBlockers(document(), "s3", "up")).toEqual([]);
  });

  it("allows a move that leaves the two sections in the same order", () => {
    expect(sectionMoveBlockers(document(), "s2", "up")).toEqual([]);
    expect(sectionMoveBlockers(document(), "s2", "down")).toEqual([]);
  });

  it("allows a move at the edge, which changes nothing", () => {
    expect(sectionMoveBlockers(document(), "s1", "up")).toEqual([]);
  });

  it("does not blame a move for a violation the document already had", () => {
    const broken = document();
    const alreadyBroken: FormDocument = {
      ...broken,
      sections: [broken.sections[2], broken.sections[0], broken.sections[1]],
    };

    expect(conditionViolations(alreadyBroken)).toHaveLength(1);
    expect(sectionMoveBlockers(alreadyBroken, "s2", "up")).toEqual([]);
  });
});

describe("sectionRemovalBlockers", () => {
  it("names what reads the section from outside it", () => {
    const base = document();
    expect(sectionRemovalBlockers(base, base.sections[0])).toEqual(["Pole C"]);
  });

  it("is empty when nothing outside the section reads it", () => {
    const base = document();
    expect(sectionRemovalBlockers(base, base.sections[1])).toEqual([]);
  });

  it("ignores a reference from inside the section, which leaves with it", () => {
    const internal: FormDocument = {
      schemaVersion: 1,
      sections: [
        section("s1", [
          field("zgoda", { type: "yesNo", label: "Zgoda" }),
          field("c", { label: "Pole C", visibleWhen: { field: "zgoda", equalsAnyOf: ["true"] } }),
        ]),
      ],
    };

    expect(sectionRemovalBlockers(internal, internal.sections[0])).toEqual([]);
  });

  it("sees a reference to a table column of the section", () => {
    const withTable: FormDocument = {
      schemaVersion: 1,
      sections: [
        section("s1", [
          field("budzet", {
            type: "repeatableTable",
            label: "Budżet",
            table: { columns: [field("wartosc", { type: "amount", label: "Wartość" })], minRows: 1 },
          }),
        ]),
        section("s2", [
          field("suma", {
            type: "calculated",
            label: "Suma",
            calculation: { kind: "sum", operands: ["budzet.wartosc"] },
          }),
        ]),
      ],
    };

    expect(sectionRemovalBlockers(withTable, withTable.sections[0])).toEqual(["Suma"]);
  });
});

describe("fieldMoveBlockers (R-42)", () => {
  const within = (): FormDocument => ({
    schemaVersion: 1,
    sections: [
      section("s", [
        field("zgoda", { type: "yesNo", label: "Zgoda" }),
        field("opis", { label: "Opis", visibleWhen: { field: "zgoda", equalsAnyOf: ["true"] } }),
        field("inne", { label: "Inne" }),
      ]),
    ],
  });

  it("blocks pushing the answer below the field that reads it", () => {
    expect(fieldMoveBlockers(within(), "s", "zgoda", "down")).toEqual([
      { id: "s:opis", dependentLabel: "Opis", sourceLabel: "Zgoda" },
    ]);
  });

  it("blocks pulling the reading field above its answer", () => {
    expect(fieldMoveBlockers(within(), "s", "opis", "up")).toHaveLength(1);
  });

  it("allows a move between fields that do not read each other", () => {
    expect(fieldMoveBlockers(within(), "s", "inne", "up")).toEqual([]);
  });
});
