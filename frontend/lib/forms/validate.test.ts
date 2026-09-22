import { describe, expect, it } from "vitest";
import { sectionStatus, validateCell, validateField, validateRowCount } from "./validate";
import type { FormAnswers } from "./answer-types";
import type { FormDocument, FormField, FormSection } from "./document-types";

function section(fields: FormField[], overrides: Partial<FormSection> = {}): FormSection {
  return { key: "s1", title: "Sekcja", description: "", fields, ...overrides };
}

function document(fields: FormField[]): FormDocument {
  return { schemaVersion: 1, sections: [section(fields)] };
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

describe("validateField", () => {
  it("requires a value when the field is required", () => {
    expect(validateField(document([title]), {}, title, {})).toBe("To pole jest wymagane.");
  });

  it("accepts an empty optional field", () => {
    const optional = { ...title, required: false };
    expect(validateField(document([optional]), {}, optional, {})).toBeNull();
  });

  it("rejects text past its character limit", () => {
    const answers: FormAnswers = { tytul: "0123456789X" };
    expect(validateField(document([title]), answers, title, {})).toMatch(/Przekroczono limit 10/);
  });

  it("accepts text within its limit", () => {
    const answers: FormAnswers = { tytul: "krótko" };
    expect(validateField(document([title]), answers, title, {})).toBeNull();
  });

  it("rejects a number below its minimum", () => {
    const count: FormField = {
      key: "liczba",
      type: "number",
      label: "Liczba",
      help: "",
      required: true,
      printed: true,
      minValue: 1,
    };
    expect(validateField(document([count]), { liczba: 0 }, count, {})).toMatch(/nie może być mniejsza/);
  });

  it("flags an amount over its limit with the concrete ceiling, not the raw rule", () => {
    const amount: FormField = {
      key: "dotacja",
      type: "amount",
      label: "Kwota dotacji",
      help: "",
      required: true,
      printed: true,
      limits: [{ kind: "maxAmount", basis: "competition.maxGrantAmount" }],
    };
    const error = validateField(
      document([amount]),
      { dotacja: 9500 },
      amount,
      { maxGrantAmount: 9000 },
    );
    expect(error).toMatch(/9.?000,00.zł/);
    expect(error).toMatch(/500,00.zł/);
  });

  it("flags a ratio-kind calculated field's limit in percent, not zł", () => {
    const ratio: FormField = {
      key: "udzial",
      type: "calculated",
      label: "Udział kosztów pośrednich",
      help: "",
      required: false,
      printed: true,
      calculation: { kind: "ratio", operands: ["kosztyC", "dotacja"] },
      limits: [{ kind: "maxAmount", basis: "competition.maxIndirectCostPercent" }],
    };
    const kosztyC: FormField = { ...title, key: "kosztyC", type: "amount", required: false };
    const dotacja: FormField = { ...title, key: "dotacja", type: "amount", required: false };
    const error = validateField(
      document([kosztyC, dotacja, ratio]),
      { kosztyC: 20, dotacja: 100 },
      ratio,
      { maxIndirectCostPercent: 10 },
    );
    expect(error).toMatch(/%/);
    expect(error).not.toMatch(/zł/);
  });

  it("never flags a calculated field as missing, only as over a limit", () => {
    const calculated: FormField = {
      key: "suma",
      type: "calculated",
      label: "Suma",
      help: "",
      required: false,
      printed: true,
      calculation: { kind: "sum", operands: [] },
    };
    expect(validateField(document([calculated]), {}, calculated, {})).toBeNull();
  });

  it("requires a statement to be explicitly checked, not merely non-empty", () => {
    const statement: FormField = {
      key: "oswiadczenie",
      type: "statement",
      label: "Oświadczenie",
      help: "",
      required: true,
      printed: false,
      statementText: "Oświadczam, że dane są zgodne z prawdą.",
    };
    // Pristine (never touched): required.
    expect(validateField(document([statement]), {}, statement, {})).toBe("To pole jest wymagane.");
    // Explicitly unchecked: still required, not accepted as "answered false"
    // the way a yesNo question would be.
    expect(validateField(document([statement]), { oswiadczenie: false }, statement, {})).toBe(
      "To pole jest wymagane.",
    );
    // Checked: satisfied.
    expect(validateField(document([statement]), { oswiadczenie: true }, statement, {})).toBeNull();
  });
});

describe("validateCell", () => {
  const column: FormField = {
    key: "cena",
    type: "amount",
    label: "Cena",
    help: "",
    required: true,
    printed: true,
  };

  it("requires a value", () => {
    expect(validateCell(column, undefined)).toBe("Wymagane.");
  });

  it("accepts a filled cell", () => {
    expect(validateCell(column, 100)).toBeNull();
  });
});

describe("validateRowCount", () => {
  const table: FormField = {
    key: "rezultaty",
    type: "repeatableTable",
    label: "Rezultaty",
    help: "",
    required: true,
    printed: true,
    table: { columns: [], minRows: 1 },
  };

  it("requires the minimum number of rows", () => {
    expect(validateRowCount(table, 0)).toMatch(/co najmniej 1/);
  });

  it("accepts enough rows", () => {
    expect(validateRowCount(table, 1)).toBeNull();
  });

  it("says nothing about a fixed table, which has no widths to check", () => {
    const fixed: FormField = { ...table, type: "fixedTable", table: { columns: [], rows: [] } };
    expect(validateRowCount(fixed, 0)).toBeNull();
  });
});

describe("sectionStatus", () => {
  it("is inProgress for an empty required field, not hasErrors", () => {
    const doc = document([title]);
    expect(sectionStatus(doc, {}, doc.sections[0], {})).toBe("inProgress");
  });

  it("is ready once every required field is filled and valid", () => {
    const doc = document([title]);
    expect(sectionStatus(doc, { tytul: "OK" }, doc.sections[0], {})).toBe("ready");
  });

  it("is hasErrors when a filled field breaks its own shape", () => {
    const doc = document([title]);
    expect(sectionStatus(doc, { tytul: "0123456789X" }, doc.sections[0], {})).toBe("hasErrors");
  });

  it("does not count a hidden required field as incomplete", () => {
    const hidden: FormField = {
      ...title,
      key: "forma_prawna_inna",
      visibleWhen: { field: "forma_prawna", equalsAnyOf: ["inna"] },
    };
    const doc = document([hidden]);
    // forma_prawna is never answered "inna" here, so the field never shows,
    // and a section made only of it must read as complete, not stuck open.
    expect(sectionStatus(doc, {}, doc.sections[0], {})).toBe("ready");
  });

  function resultsTable(overrides: Partial<FormField> = {}): FormField {
    return {
      key: "rezultaty",
      type: "repeatableTable",
      label: "Rezultaty",
      help: "",
      required: true,
      printed: true,
      table: {
        columns: [
          { key: "wartosc", type: "shortText", label: "Wartość", help: "", required: true, printed: true, maxLength: 50 },
        ],
      },
      ...overrides,
    };
  }

  it("is inProgress, not hasErrors, when a table row's required cell is simply empty", () => {
    const doc = document([resultsTable()]);
    const answers: FormAnswers = { rezultaty: [{ wartosc: "" }] };
    // Nothing invalid has been typed, it just has not been filled in yet:
    // the same distinction sectionStatus already makes for a plain field.
    expect(sectionStatus(doc, answers, doc.sections[0], {})).toBe("inProgress");
  });

  it("is hasErrors when a table row's cell genuinely breaks its own shape", () => {
    const doc = document([resultsTable()]);
    const answers: FormAnswers = { rezultaty: [{ wartosc: "x".repeat(60) }] };
    expect(sectionStatus(doc, answers, doc.sections[0], {})).toBe("hasErrors");
  });

  it("counts an untouched fixedTable's required cells as incomplete, not ready", () => {
    // A fixedTable's rows are structural (table.rows), not stored under the
    // field's key until a cell is edited: reading answers directly would
    // undercount it to zero rows and report the section as ready.
    const fixed: FormField = {
      key: "czlonkowie",
      type: "fixedTable",
      label: "Członkowie grupy",
      help: "",
      required: true,
      printed: true,
      table: {
        rows: [
          { key: "lider", label: "Lider" },
          { key: "czlonek_2", label: "Członek 2" },
        ],
        columns: [
          { key: "imie", type: "shortText", label: "Imię i nazwisko", help: "", required: true, printed: true, maxLength: 100 },
        ],
      },
    };
    const doc = document([fixed]);
    expect(sectionStatus(doc, {}, doc.sections[0], {})).toBe("inProgress");
  });

  it("is hasErrors, not inProgress, for a required calculated field over its limit", () => {
    const calculated: FormField = {
      key: "dotacja",
      type: "calculated",
      label: "Kwota dotacji",
      help: "",
      required: true,
      printed: true,
      calculation: { kind: "sum", operands: [] },
      limits: [{ kind: "maxAmount", basis: "competition.maxGrantAmount" }],
    };
    const doc = document([calculated]);
    // The calculated value (0, an empty sum) never exceeds a positive
    // ceiling, so force the exceeded branch with a ceiling below zero.
    expect(
      sectionStatus(doc, {}, doc.sections[0], { maxGrantAmount: -1 }),
    ).toBe("hasErrors");
  });
});
