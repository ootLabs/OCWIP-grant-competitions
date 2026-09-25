import { describe, expect, it } from "vitest";
import { submissionGaps } from "./submission-gaps";
import type { FormAnswers } from "./answer-types";
import type { FormDocument, FormField, FormSection } from "./document-types";

function section(fields: FormField[], overrides: Partial<FormSection> = {}): FormSection {
  return { key: "s1", title: "Sekcja", description: "", fields, ...overrides };
}

function document(sections: FormSection[]): FormDocument {
  return { schemaVersion: 1, sections };
}

const title: FormField = {
  key: "tytul",
  type: "shortText",
  label: "Tytuł",
  help: "",
  required: true,
  printed: true,
  maxLength: 10,
};

describe("submissionGaps", () => {
  it("is empty for a document with no required fields left empty", () => {
    const doc = document([section([{ ...title, required: false }])]);
    expect(submissionGaps(doc, {}, {})).toEqual([]);
  });

  it("names a required field that has never been touched", () => {
    const doc = document([section([title])]);
    const gaps = submissionGaps(doc, {}, {});

    expect(gaps).toHaveLength(1);
    expect(gaps[0]).toMatchObject({
      sectionKey: "s1",
      fieldKey: "tytul",
      fieldLabel: "Tytuł",
      message: "To pole jest wymagane.",
      anchorId: "field-tytul",
    });
  });

  it("skips a field hidden by a condition, even when it is required and empty", () => {
    const hidden: FormField = {
      ...title,
      key: "ukryte",
      visibleWhen: { field: "brama", equalsAnyOf: ["tak"] },
    };
    const doc = document([section([hidden])]);
    expect(submissionGaps(doc, {}, {})).toEqual([]);
  });

  it("skips every field of a section whose own visibleWhen is not met", () => {
    const hiddenSection = section([title], {
      key: "s2",
      visibleWhen: { field: "brama", equalsAnyOf: ["tak"] },
    });
    const doc = document([hiddenSection]);
    expect(submissionGaps(doc, {}, {})).toEqual([]);
  });

  it("reports one gap per field, across sections, in document order", () => {
    const first = section([title], { key: "s1", title: "Pierwsza" });
    const secondField: FormField = { ...title, key: "opis", label: "Opis" };
    const second = section([secondField], { key: "s2", title: "Druga" });
    const doc = document([first, second]);

    const gaps = submissionGaps(doc, {}, {});

    expect(gaps.map((gap) => gap.fieldKey)).toEqual(["tytul", "opis"]);
    expect(gaps.map((gap) => gap.sectionTitle)).toEqual(["Pierwsza", "Druga"]);
  });

  it("flags a required table cell nobody filled in yet, naming the row and column", () => {
    const column: FormField = {
      key: "kwota",
      type: "amount",
      label: "Kwota",
      help: "",
      required: true,
      printed: true,
    };
    const table: FormField = {
      key: "budzet",
      type: "repeatableTable",
      label: "Budżet",
      help: "",
      required: false,
      printed: true,
      table: { columns: [column] },
    };
    const doc = document([section([table])]);
    const answers: FormAnswers = { budzet: [{}] };

    const gaps = submissionGaps(doc, answers, {});

    expect(gaps).toHaveLength(1);
    expect(gaps[0].fieldKey).toBe("budzet");
    expect(gaps[0].anchorId).toBe("field-budzet");
    expect(gaps[0].message).toContain('Wiersz 1, kolumna "Kwota"');
  });

  it("flags a repeatableTable under its own minimum row count", () => {
    const column: FormField = {
      key: "opis",
      type: "shortText",
      label: "Opis",
      help: "",
      required: false,
      printed: true,
    };
    const table: FormField = {
      key: "rezultaty",
      type: "repeatableTable",
      label: "Rezultaty",
      help: "",
      required: false,
      printed: true,
      table: { columns: [column], minRows: 1 },
    };
    const doc = document([section([table])]);

    const gaps = submissionGaps(doc, {}, {});

    expect(gaps.map((gap) => gap.fieldKey)).toEqual(["rezultaty"]);
    expect(gaps[0].message).toMatch(/co najmniej 1 wiersz/);
  });

  it("flags a limit genuinely exceeded, not only an empty required field", () => {
    const amount: FormField = {
      key: "dotacja",
      type: "amount",
      label: "Wnioskowana kwota",
      help: "",
      required: true,
      printed: true,
      limits: [{ kind: "maxAmount", basis: "competition.maxGrantAmount" }],
    };
    const doc = document([section([amount])]);
    const gaps = submissionGaps(doc, { dotacja: 5000 }, { maxGrantAmount: 1000 });

    expect(gaps).toHaveLength(1);
    expect(gaps[0].message).toMatch(/Przekroczono dopuszczalną wartość/);
  });
});
