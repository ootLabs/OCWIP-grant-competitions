import { describe, expect, it } from "vitest";

import {
  POLISH_TIME_LABEL,
  formatAmount,
  formatDateOnly,
  formatFileSize,
  formatMoment,
  formatPercent,
  timeZoneLabel,
} from "./format";

describe("formatMoment", () => {
  it("places an instant on the Polish wall clock, not on the machine's", () => {
    // Summer time: 10:00 UTC is 12:00 in Warsaw.
    expect(formatMoment("2026-09-12T10:00:00Z")).toBe("12.09.2026, 12:00");
  });

  it("follows the October switch, which lands mid season", () => {
    // Winter time: the same 10:00 UTC is now 11:00. The instant decides the
    // hour, which is the whole reason these are stored in UTC.
    expect(formatMoment("2026-10-25T10:00:00Z")).toBe("25.10.2026, 11:00");
  });

  it("gives the hour in 24 hour form, with no AM or PM", () => {
    expect(formatMoment("2026-09-12T18:30:00Z")).toBe("12.09.2026, 20:30");
  });
});

describe("formatDateOnly", () => {
  it("leaves a calendar date alone instead of turning it into an instant", () => {
    // Through Date this would be midnight UTC and could print as 30.03.
    expect(formatDateOnly("2027-03-31")).toBe("31.03.2027");
  });
});

describe("formatAmount", () => {
  it("writes an amount in the currency the whole system counts in", () => {
    const text = formatAmount(15000);

    expect(text).toMatch(/15/);
    expect(text).toMatch(/000,00/);
    expect(text).toMatch(/zł/);
  });

  it("takes the decimal as the string OpenAPI may send", () => {
    expect(formatAmount("1234.50")).toMatch(/1234,50|1 234,50/);
  });
});

describe("formatPercent", () => {
  it("writes a whole percentage without trailing zeros", () => {
    expect(formatPercent(20)).toBe("20%");
  });

  it("keeps a fraction that is really there", () => {
    expect(formatPercent("12.5")).toBe("12,5%");
  });
});

describe("timeZoneLabel", () => {
  it("names the zone out loud, because a deadline hour without one is a guess", () => {
    expect(timeZoneLabel()).toBe(POLISH_TIME_LABEL);
  });
});

describe("formatFileSize", () => {
  it("counts in the megabytes an upload dialog counts in", () => {
    expect(formatFileSize(10 * 1024 * 1024)).toBe("10 MB");
  });

  it("keeps one digit for a limit that is not whole", () => {
    expect(formatFileSize(1_572_864)).toBe("1,5 MB");
  });
});
