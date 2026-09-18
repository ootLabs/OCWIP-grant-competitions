import { describe, expect, it } from "vitest";
import { isCurrentLink } from "./navigation";

describe("isCurrentLink", () => {
  it("marks the root only on the root", () => {
    // A prefix test would light up the first position on every subpage, so the
    // navigation would state the wrong place exactly where it matters most.
    const root = "/panel/applicant";

    expect(isCurrentLink(root, root, root)).toBe(true);
    expect(isCurrentLink(root, "/panel/applicant/profile", root)).toBe(false);
  });

  it("marks a section on its own subpages", () => {
    expect(
      isCurrentLink(
        "/panel/applicant/competitions",
        "/panel/applicant/competitions/7",
        "/panel/applicant",
      ),
    ).toBe(true);
  });

  it("does not match a path that merely starts with the same letters", () => {
    expect(
      isCurrentLink(
        "/panel/applicant/profile",
        "/panel/applicant/profile-archiwum",
        "/panel/applicant",
      ),
    ).toBe(false);
  });

  it("takes the root it is given, so each panel judges its own first position", () => {
    // The rule is not "anything called /panel/applicant". Handing the operator
    // panel's root in has to make the operator's first position behave the same
    // way, otherwise the shared helper only really serves one panel.
    const root = "/panel/operator";

    expect(isCurrentLink(root, root, root)).toBe(true);
    expect(isCurrentLink(root, "/panel/operator/applications", root)).toBe(false);
  });
});
