import { describe, expect, it } from "vitest";
import { applicantPanelLinks, isCurrentLink } from "./navigation";

describe("applicantPanelLinks", () => {
  it("keeps every path inside the applicant panel", () => {
    expect(
      applicantPanelLinks.every((link) => link.href.startsWith("/panel/applicant")),
    ).toBe(true);
  });
});

describe("isCurrentLink", () => {
  it("marks the root only on the root", () => {
    // A prefix test would light up "Moje wnioski" on every subpage, so the
    // navigation would state the wrong place exactly where it matters most.
    expect(isCurrentLink("/panel/applicant", "/panel/applicant")).toBe(true);
    expect(isCurrentLink("/panel/applicant", "/panel/applicant/profile")).toBe(false);
  });

  it("marks a section on its own subpages", () => {
    expect(
      isCurrentLink("/panel/applicant/competitions", "/panel/applicant/competitions/7"),
    ).toBe(true);
  });

  it("does not match a path that merely starts with the same letters", () => {
    expect(
      isCurrentLink("/panel/applicant/profile", "/panel/applicant/profile-archiwum"),
    ).toBe(false);
  });
});
