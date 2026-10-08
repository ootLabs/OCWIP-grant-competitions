import { describe, expect, it } from "vitest";

import { changesLabel, documentChanges } from "./document-changes";
import type { FormDocument, FormField } from "./document-types";

const field = (key: string, label: string, extra: Partial<FormField> = {}): FormField => ({
  key,
  type: "longText",
  label,
  help: "",
  required: true,
  printed: true,
  ...extra,
});

const doc = (...fields: FormField[]): FormDocument => ({
  schemaVersion: 1,
  sections: [{ key: "s1", title: "Część II", description: "", fields }],
});

describe("documentChanges", () => {
  it("says nothing without a document to compare with", () => {
    expect(documentChanges(null, doc(field("cel", "Cel")))).toEqual([]);
  });

  it("names a renamed field with its old name and a changed limit", () => {
    const before = doc(field("cel", "Cel główny projektu", { maxLength: 2000 }));
    const after = doc(field("cel", "Rezultat główny projektu", { maxLength: 1500 }));

    expect(documentChanges(before, after)).toEqual([
      {
        fieldKey: "cel",
        sectionKey: "s1",
        sectionTitle: "Część II",
        label: "Rezultat główny projektu",
        kind: "changed",
        what: ["nazwa", "limit znaków"],
        wasLabel: "Cel główny projektu",
      },
    ]);
  });

  it("lists new fields in place and removed ones last", () => {
    const before = doc(field("a", "Stare"), field("b", "Zostaje"));
    const after = doc(field("c", "Nowe"), field("b", "Zostaje"));

    expect(documentChanges(before, after).map((change) => [change.fieldKey, change.kind])).toEqual([
      ["c", "new"],
      ["a", "removed"],
    ]);
  });

  it("ignores a field that only moved", () => {
    const a = field("a", "A");
    const b = field("b", "B");
    expect(documentChanges(doc(a, b), doc(b, a))).toEqual([]);
  });
});

describe("changesLabel", () => {
  it.each([
    [1, "1 zmiana"],
    [3, "3 zmiany"],
    [5, "5 zmian"],
    [12, "12 zmian"],
    [22, "22 zmiany"],
  ])("%i", (count, label) => {
    expect(changesLabel(count)).toBe(label);
  });
});
