import { describe, expect, it } from "vitest";

import { localWarsawToUtcIso } from "./local-time";

describe("localWarsawToUtcIso", () => {
  it("converts a winter reading (UTC+1)", () => {
    expect(localWarsawToUtcIso("2026-01-15T12:00")).toBe(
      "2026-01-15T11:00:00.000Z",
    );
  });

  it("converts a summer reading (UTC+2)", () => {
    expect(localWarsawToUtcIso("2026-07-15T12:00")).toBe(
      "2026-07-15T10:00:00.000Z",
    );
  });

  it("defaults the time of day to midnight when none is given", () => {
    expect(localWarsawToUtcIso("2026-01-15")).toBe(
      "2026-01-14T23:00:00.000Z",
    );
  });

  it("stays on summer time the day before the October switch", () => {
    // 2026-10-25 is the last Sunday of October, when clocks move back from
    // CEST (UTC+2) to CET (UTC+1).
    expect(localWarsawToUtcIso("2026-10-24T12:00")).toBe(
      "2026-10-24T10:00:00.000Z",
    );
  });

  it("is on winter time the day after the October switch", () => {
    expect(localWarsawToUtcIso("2026-10-26T12:00")).toBe(
      "2026-10-26T11:00:00.000Z",
    );
  });

  it("stays on winter time the day before the March switch", () => {
    // 2026-03-29 is the last Sunday of March, when clocks move forward from
    // CET (UTC+1) to CEST (UTC+2).
    expect(localWarsawToUtcIso("2026-03-28T12:00")).toBe(
      "2026-03-28T11:00:00.000Z",
    );
  });

  it("is on summer time the day after the March switch", () => {
    expect(localWarsawToUtcIso("2026-03-30T12:00")).toBe(
      "2026-03-30T10:00:00.000Z",
    );
  });
});
