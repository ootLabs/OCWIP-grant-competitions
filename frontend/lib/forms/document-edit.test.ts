import { describe, expect, it } from "vitest";
import {
  addColumnToTable,
  addFieldToSection,
  findField,
  moveField,
  removeField,
  updateField,
  updateSection,
} from "./document-edit";
import type { FormDocument, FormField } from "./document-types";

function field(key: string, overrides: Partial<FormField> = {}): FormField {
  return {
    key,
    type: "shortText",
    label: key,
    help: "",
    required: true,
    printed: true,
    maxLength: 100,
    ...overrides,
  };
}

function document(): FormDocument {
  return {
    schemaVersion: 1,
    sections: [
      {
        key: "s1",
        title: "Sekcja 1",
        description: "",
        fields: [
          field("a"),
          field("b"),
          field("budzet", {
            type: "repeatableTable",
            maxLength: undefined,
            table: {
              columns: [field("liczba", { maxLength: undefined, type: "number" })],
              minRows: 1,
            },
          }),
        ],
      },
    ],
  };
}

describe("updateSection", () => {
  it("replaces only the matching section", () => {
    const result = updateSection(document(), "s1", (section) => ({
      ...section,
      title: "Nowy tytuł",
    }));

    expect(result.sections[0].title).toBe("Nowy tytuł");
  });
});

describe("updateField", () => {
  it("edits a top level field", () => {
    const result = updateField(
      document(),
      { sectionKey: "s1", fieldKey: "a" },
      (f) => ({ ...f, label: "Zmieniona etykieta" }),
    );

    expect(findField(result, { sectionKey: "s1", fieldKey: "a" })?.label).toBe(
      "Zmieniona etykieta",
    );
  });

  it("edits a table column without touching sibling columns", () => {
    const result = updateField(
      document(),
      { sectionKey: "s1", fieldKey: "budzet", columnKey: "liczba" },
      (f) => ({ ...f, label: "Liczba jednostek" }),
    );

    expect(
      findField(result, { sectionKey: "s1", fieldKey: "budzet", columnKey: "liczba" })
        ?.label,
    ).toBe("Liczba jednostek");
  });
});

describe("addFieldToSection / addColumnToTable", () => {
  it("appends a field at the end of the section", () => {
    const result = addFieldToSection(document(), "s1", field("c"));
    expect(result.sections[0].fields.map((f) => f.key)).toEqual([
      "a",
      "b",
      "budzet",
      "c",
    ]);
  });

  it("appends a column at the end of the table", () => {
    const result = addColumnToTable(
      document(),
      { sectionKey: "s1", fieldKey: "budzet" },
      field("cena", { type: "amount", maxLength: undefined }),
    );

    expect(
      findField(result, { sectionKey: "s1", fieldKey: "budzet" })?.table?.columns.map(
        (c) => c.key,
      ),
    ).toEqual(["liczba", "cena"]);
  });
});

describe("removeField", () => {
  it("removes a top level field", () => {
    const result = removeField(document(), { sectionKey: "s1", fieldKey: "a" });
    expect(result.sections[0].fields.map((f) => f.key)).toEqual(["b", "budzet"]);
  });

  it("removes a table column, leaving the table itself in place", () => {
    const result = removeField(document(), {
      sectionKey: "s1",
      fieldKey: "budzet",
      columnKey: "liczba",
    });

    expect(
      findField(result, { sectionKey: "s1", fieldKey: "budzet" })?.table?.columns,
    ).toEqual([]);
  });
});

describe("moveField", () => {
  it("swaps a field with its previous sibling", () => {
    const result = moveField(document(), { sectionKey: "s1", fieldKey: "b" }, "up");
    expect(result.sections[0].fields.map((f) => f.key)).toEqual(["b", "a", "budzet"]);
  });

  it("does nothing when moving the first field up", () => {
    const result = moveField(document(), { sectionKey: "s1", fieldKey: "a" }, "up");
    expect(result.sections[0].fields.map((f) => f.key)).toEqual(["a", "b", "budzet"]);
  });

  it("does nothing when moving the last field down", () => {
    const result = moveField(document(), { sectionKey: "s1", fieldKey: "budzet" }, "down");
    expect(result.sections[0].fields.map((f) => f.key)).toEqual(["a", "b", "budzet"]);
  });
});

describe("findField", () => {
  it("returns null for a key that does not exist", () => {
    expect(findField(document(), { sectionKey: "s1", fieldKey: "brak" })).toBeNull();
  });
});
