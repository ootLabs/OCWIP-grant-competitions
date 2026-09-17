import { describe, expect, it } from "vitest";
import { applicantPanelLinks } from "./navigation";

describe("applicantPanelLinks", () => {
  it("keeps every path inside the applicant panel", () => {
    expect(
      applicantPanelLinks.every((link) => link.href.startsWith("/panel/applicant")),
    ).toBe(true);
  });
});
