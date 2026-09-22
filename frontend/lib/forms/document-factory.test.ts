import { describe, expect, it } from "vitest";
import { newField, newSection } from "./document-factory";
import { ALL_FIELD_TYPES } from "./document-types";

describe("newSection", () => {
  it("derives a key from the title and starts empty", () => {
    const section = newSection("Budżet projektu", new Set());
    expect(section.key).toBe("budzet_projektu");
    expect(section.fields).toHaveLength(0);
    expect(section.visibleWhen).toBeNull();
  });
});

describe("newField", () => {
  it("produces a valid default for every one of the fifteen kinds", () => {
    for (const type of ALL_FIELD_TYPES) {
      const field = newField(type, "Pole testowe", new Set());
      expect(field.type).toBe(type);
      expect(field.key.length).toBeGreaterThan(0);
    }
  });

  it("gives a required maxLength to textual fields", () => {
    expect(newField("shortText", "Tytuł", new Set()).maxLength).toBe(500);
    expect(newField("longText", "Opis", new Set()).maxLength).toBe(500);
  });

  it("starts choice fields with an empty option list, not undefined", () => {
    expect(newField("singleChoice", "Forma prawna", new Set()).options).toEqual([]);
  });

  it("gives a repeatable table row widths but no fixed rows", () => {
    const field = newField("repeatableTable", "Rezultaty", new Set());
    expect(field.table?.minRows).toBe(1);
    expect(field.table?.rows).toBeUndefined();
  });

  it("gives a fixed table an empty row list and no widths", () => {
    const field = newField("fixedTable", "Członkowie grupy", new Set());
    expect(field.table?.rows).toEqual([]);
    expect(field.table?.minRows).toBeUndefined();
  });

  it("starts a calculated field with an empty sum, not required", () => {
    const field = newField("calculated", "Suma", new Set());
    expect(field.calculation).toEqual({ kind: "sum", operands: [] });
    expect(field.required).toBe(false);
  });

  it("hides a statement from the print flag by default", () => {
    // Statements carry their own text and their own RODO clause elsewhere;
    // printed defaults to false until an operator decides otherwise.
    expect(newField("statement", "Oświadczenie 1", new Set()).printed).toBe(false);
  });

  it("avoids a key collision with an already used one", () => {
    const taken = new Set(["tytul"]);
    expect(newField("shortText", "Tytuł", taken).key).toBe("tytul_2");
  });
});
