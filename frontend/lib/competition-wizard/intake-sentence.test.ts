import { describe, expect, it } from "vitest";

import { describeDeadline } from "./intake-sentence";

describe("describeDeadline", () => {
  it("says nothing yet when no date has been typed", () => {
    expect(describeDeadline("", false)).toBeNull();
  });

  it("names the exact moment typed, in words", () => {
    const sentence = describeDeadline("2026-09-12T12:00", false);

    expect(sentence).toContain("12 września");
    expect(sentence).toContain("12:00");
    expect(sentence).toContain("Wnioski złożone później nie wejdą");
  });

  it("explains a continuous intake instead of naming a date", () => {
    const sentence = describeDeadline("2026-09-12T12:00", true);

    expect(sentence).toContain("nie ma terminu zamknięcia");
  });
});
