import { describe, expect, it } from "vitest";
import { contrastRatio, meetsAA } from "./contrast";

// Reference values from the OCWIP branding research (card T-07, Notion "Branding OCWIP",
// section 5). Pins the calculator against numbers already checked by hand, and locks in
// which token pairs are actually allowed to carry normal text.
describe("contrastRatio", () => {
  it("matches the researched ratio for body text on the page background", () => {
    expect(contrastRatio("#FFFFFF", "#231F20")).toBeCloseTo(16.3, 1);
  });

  it("matches the researched ratio for link text at rest", () => {
    expect(contrastRatio("#FFFFFF", "#413D39")).toBeCloseTo(10.76, 1);
  });

  it("matches the researched ratio for the brand accent on white", () => {
    expect(contrastRatio("#FFFFFF", "#CF4B0F")).toBeCloseTo(4.5, 1);
  });

  it("matches the researched ratio for the darker accent on white", () => {
    expect(contrastRatio("#FFFFFF", "#9F3A0C")).toBeCloseTo(6.81, 1);
  });

  it("matches the researched ratio for the logo orange on white", () => {
    expect(contrastRatio("#FFFFFF", "#EB6209")).toBeCloseTo(3.34, 1);
  });

  it("matches the researched ratios for high contrast mode", () => {
    expect(contrastRatio("#000000", "#FFFFFF")).toBeCloseTo(21, 0);
    expect(contrastRatio("#000000", "#FFE800")).toBeCloseTo(16.79, 1);
    expect(contrastRatio("#000000", "#CF4B0F")).toBeCloseTo(4.67, 1);
  });
});

describe("meetsAA", () => {
  it("passes body text, link text and the darker accent for normal text", () => {
    expect(meetsAA("#FFFFFF", "#231F20")).toBe(true);
    expect(meetsAA("#FFFFFF", "#413D39")).toBe(true);
    expect(meetsAA("#FFFFFF", "#9F3A0C")).toBe(true);
  });

  it("passes the main brand accent for normal text, but right at the AA floor", () => {
    expect(meetsAA("#FFFFFF", "#CF4B0F")).toBe(true);
    expect(contrastRatio("#FFFFFF", "#CF4B0F")).toBeLessThan(4.6);
  });

  it("fails the logo orange for normal text, so it must stay out of text tokens", () => {
    expect(meetsAA("#FFFFFF", "#EB6209")).toBe(false);
  });

  it("passes the logo orange as large text / UI, where the threshold is 3:1", () => {
    expect(meetsAA("#FFFFFF", "#EB6209", true)).toBe(true);
  });
});
