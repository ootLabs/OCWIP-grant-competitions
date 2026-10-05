import { describe, expect, it, vi } from "vitest";

vi.mock("next/navigation", () => ({ usePathname: () => "/panel/applicant" }));

import { initials } from "./panel-header-parts";

describe("initials", () => {
  it("takes the first letters of the first two words, Polish ones included", () => {
    expect(initials("Stowarzyszenie Łąka")).toBe("SŁ");
    expect(initials("Anna Operator")).toBe("AO");
  });

  it("skips what does not start with a letter and stops at two", () => {
    expect(initials("Fundacja \"Nasz Dom\" 2026")).toBe("FN");
    expect(initials("ekspert@example.org")).toBe("E");
    expect(initials("")).toBe("");
  });
});
