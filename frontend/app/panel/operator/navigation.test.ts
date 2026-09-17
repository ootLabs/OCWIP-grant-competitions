import { describe, expect, it } from "vitest";
import { operatorPanelLinks } from "./navigation";

describe("operatorPanelLinks", () => {
  it("keeps every path inside the operator panel", () => {
    // A position leading out of the panel would drop the operator into the
    // applicant's frame, where nothing says whose data is on screen.
    expect(
      operatorPanelLinks.every((link) => link.href.startsWith("/panel/operator")),
    ).toBe(true);
  });
});
