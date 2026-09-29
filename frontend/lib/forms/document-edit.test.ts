import { describe, expect, it } from "vitest";
import {
  addColumnToTable,
  addFieldToSection,
  addSection,
  addTableRow,
  findField,
  moveField,
  moveSection,
  moveTableRow,
  removeField,
  removeSection,
  removeTableRow,
  renameTableRow,
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

describe("addSection / removeSection / moveSection", () => {
  function twoSections(): FormDocument {
    return {
      schemaVersion: 1,
      sections: [
        { key: "s1", title: "Pierwsza", description: "", fields: [field("a")] },
        { key: "s2", title: "Druga", description: "", fields: [field("b")] },
      ],
    };
  }

  it("appends a section at the end of the form", () => {
    const result = addSection(twoSections(), {
      key: "s3",
      title: "Trzecia",
      description: "",
      fields: [],
    });

    expect(result.sections.map((s) => s.key)).toEqual(["s1", "s2", "s3"]);
  });

  it("removes a section with everything in it", () => {
    const result = removeSection(twoSections(), "s1");
    expect(result.sections.map((s) => s.key)).toEqual(["s2"]);
  });

  it("swaps a section with its neighbour", () => {
    expect(moveSection(twoSections(), "s2", "up").sections.map((s) => s.key)).toEqual([
      "s2",
      "s1",
    ]);
  });

  it("does nothing when moving past either end", () => {
    expect(moveSection(twoSections(), "s1", "up").sections.map((s) => s.key)).toEqual([
      "s1",
      "s2",
    ]);
    expect(moveSection(twoSections(), "s2", "down").sections.map((s) => s.key)).toEqual([
      "s1",
      "s2",
    ]);
  });

  it("leaves the document it was given untouched", () => {
    const original = twoSections();
    removeSection(original, "s1");
    expect(original.sections.map((s) => s.key)).toEqual(["s1", "s2"]);
  });
});

describe("fixed table rows", () => {
  const path = { sectionKey: "s1", fieldKey: "czlonkowie" };

  function withFixedTable(): FormDocument {
    return {
      schemaVersion: 1,
      sections: [
        {
          key: "s1",
          title: "Sekcja 1",
          description: "",
          fields: [
            field("czlonkowie", {
              type: "fixedTable",
              maxLength: undefined,
              table: {
                columns: [field("imie", { maxLength: 100 })],
                rows: [
                  { key: "pierwszy", label: "Pierwszy" },
                  { key: "drugi", label: "Drugi" },
                ],
              },
            }),
          ],
        },
      ],
    };
  }

  function rowsOf(document: FormDocument) {
    return findField(document, path)?.table?.rows;
  }

  it("appends a row", () => {
    const result = addTableRow(withFixedTable(), path, { key: "trzeci", label: "Trzeci" });
    expect(rowsOf(result)?.map((row) => row.key)).toEqual(["pierwszy", "drugi", "trzeci"]);
  });

  it("removes a row by key", () => {
    expect(rowsOf(removeTableRow(withFixedTable(), path, "pierwszy"))).toEqual([
      { key: "drugi", label: "Drugi" },
    ]);
  });

  it("renames a row without changing its key, so answers keep pointing at it", () => {
    expect(rowsOf(renameTableRow(withFixedTable(), path, "drugi", "Druga osoba"))).toEqual([
      { key: "pierwszy", label: "Pierwszy" },
      { key: "drugi", label: "Druga osoba" },
    ]);
  });

  it("swaps a row with its neighbour", () => {
    expect(
      rowsOf(moveTableRow(withFixedTable(), path, "drugi", "up"))?.map((row) => row.key),
    ).toEqual(["drugi", "pierwszy"]);
  });

  it("leaves the columns alone", () => {
    const result = removeTableRow(withFixedTable(), path, "pierwszy");
    expect(findField(result, path)?.table?.columns.map((c) => c.key)).toEqual(["imie"]);
  });
});
