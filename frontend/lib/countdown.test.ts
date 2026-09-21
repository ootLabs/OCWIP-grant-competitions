import { describe, expect, it } from "vitest";

import { formatRemaining, remainingUntil } from "./countdown";

const closesAt = "2026-10-25T10:00:00Z";

function at(instant: string) {
  return remainingUntil(closesAt, new Date(instant));
}

describe("remainingUntil", () => {
  it("counts down to the closing moment", () => {
    expect(at("2026-10-22T03:00:00Z")).toEqual({
      days: 3,
      hours: 7,
      minutes: 0,
    });
  });

  it("truncates instead of rounding up", () => {
    // 2 days, 23 hours and 59 minutes. Rounding this to "3 dni" would tell
    // somebody they have a day they do not have.
    expect(at("2026-10-22T10:01:00Z")).toEqual({
      days: 2,
      hours: 23,
      minutes: 59,
    });
  });

  it("stops at the closing minute rather than counting zeros", () => {
    expect(at("2026-10-25T10:00:00Z")).toBeNull();
  });

  it("stays null after the deadline", () => {
    expect(at("2026-10-25T10:01:00Z")).toBeNull();
  });

  it("answers null for an unusable instant instead of printing NaN", () => {
    expect(remainingUntil("nie data", new Date("2026-10-01T00:00:00Z"))).toBeNull();
  });
});

describe("formatRemaining", () => {
  it.each([
    [{ days: 1, hours: 0, minutes: 0 }, "1 dzień"],
    [{ days: 2, hours: 0, minutes: 0 }, "2 dni"],
    [{ days: 5, hours: 0, minutes: 0 }, "5 dni"],
    // The trap in Polish plurals: 22 takes the "few" form, 12 does not.
    [{ days: 22, hours: 0, minutes: 0 }, "22 dni"],
    [{ days: 12, hours: 0, minutes: 0 }, "12 dni"],
    [{ days: 3, hours: 7, minutes: 30 }, "3 dni i 7 godzin"],
    [{ days: 1, hours: 1, minutes: 0 }, "1 dzień i 1 godzina"],
    [{ days: 0, hours: 22, minutes: 2 }, "22 godziny i 2 minuty"],
    [{ days: 0, hours: 0, minutes: 5 }, "5 minut"],
    [{ days: 0, hours: 0, minutes: 1 }, "1 minuta"],
    [{ days: 0, hours: 0, minutes: 0 }, "mniej niż minuta"],
  ])("writes %o as %s", (remaining, expected) => {
    expect(formatRemaining(remaining)).toBe(expected);
  });

  it("never mentions seconds", () => {
    const text = formatRemaining({ days: 3, hours: 7, minutes: 30 });

    expect(text).not.toMatch(/sekund/);
  });
});
