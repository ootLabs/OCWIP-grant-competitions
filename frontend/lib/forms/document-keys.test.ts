import { describe, expect, it } from "vitest";
import { allFieldKeys, isValidKey, slugifyKey, uniqueKey } from "./document-keys";
import type { FormDocument } from "./document-types";

describe("slugifyKey", () => {
  it("lowercases and replaces spaces with underscores", () => {
    expect(slugifyKey("Tytuł projektu")).toBe("tytul_projektu");
  });

  it("folds Polish diacritics instead of dropping them", () => {
    expect(slugifyKey("Wkład własny")).toBe("wklad_wlasny");
    // Dropping diacritics instead of folding them would make this collide.
    expect(slugifyKey("Wklad wlasny")).toBe("wklad_wlasny");
  });

  it("prefixes a label that starts with a digit", () => {
    expect(slugifyKey("100% dotacji")).toBe("pole_100_dotacji");
  });

  it("prefixes a label made only of digits", () => {
    expect(slugifyKey("123")).toBe("pole_123");
  });

  it("falls back to a generic key for a label with nothing usable", () => {
    expect(slugifyKey("!@#")).toBe("pole");
  });

  it("truncates to 64 characters", () => {
    const long = "a".repeat(100);
    expect(slugifyKey(long).length).toBeLessThanOrEqual(64);
  });

  it("always produces a key the backend accepts", () => {
    for (const label of ["Opis pomysłu", "Koszty pośrednie (C)", "  spacje  ", "Ó"]) {
      expect(isValidKey(slugifyKey(label))).toBe(true);
    }
  });
});

describe("uniqueKey", () => {
  it("returns the plain slug when it is free", () => {
    expect(uniqueKey("Tytuł", new Set())).toBe("tytul");
  });

  it("appends a counter when the slug is taken", () => {
    const taken = new Set(["tytul", "tytul_2"]);
    expect(uniqueKey("Tytuł", taken)).toBe("tytul_3");
  });
});

describe("allFieldKeys", () => {
  it("collects section field keys and table column keys", () => {
    const document: FormDocument = {
      schemaVersion: 1,
      sections: [
        {
          key: "s1",
          title: "Sekcja",
          description: "",
          fields: [
            {
              key: "budzet_a",
              type: "repeatableTable",
              label: "Budżet",
              help: "",
              required: true,
              printed: true,
              table: {
                columns: [
                  {
                    key: "liczba",
                    type: "number",
                    label: "Liczba",
                    help: "",
                    required: true,
                    printed: true,
                  },
                ],
              },
            },
          ],
        },
      ],
    };

    expect(allFieldKeys(document)).toEqual(new Set(["budzet_a", "liczba"]));
  });
});
