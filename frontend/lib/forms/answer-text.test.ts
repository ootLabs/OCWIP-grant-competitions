import { describe, expect, it } from "vitest";
import { answerText } from "./answer-text";
import type { FormField } from "./document-types";

function field(overrides: Partial<FormField> & Pick<FormField, "type">): FormField {
  return { key: "pole", label: "Pole", help: "", required: false, printed: true, ...overrides };
}

describe("answerText", () => {
  it("says nothing was answered rather than printing an empty string", () => {
    expect(answerText(field({ type: "shortText" }), "")).toBeNull();
    expect(answerText(field({ type: "multipleChoice" }), [])).toBeNull();
    expect(answerText(field({ type: "amount" }), undefined)).toBeNull();
  });

  it("reads choices by their labels, not their stored values", () => {
    const choice = field({
      type: "multipleChoice",
      options: [
        { value: "a", label: "Dzieci" },
        { value: "b", label: "Seniorzy" },
      ],
    });

    expect(answerText(choice, ["b", "a"])).toBe("Seniorzy, Dzieci");
  });

  it("keeps a no as an answer", () => {
    expect(answerText(field({ type: "yesNo" }), false)).toBe("Nie");
  });

  it("prints a date without moving it a day", () => {
    expect(answerText(field({ type: "date" }), "2027-03-31")).toBe("31.03.2027");
  });
});
