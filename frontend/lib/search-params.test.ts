import { describe, expect, it } from "vitest";

import { firstParam } from "./search-params";

describe("firstParam", () => {
  it("takes the value as it came", () => {
    expect(firstParam("/competitions/abc")).toBe("/competitions/abc");
  });

  it("takes the first of a parameter written twice", () => {
    expect(firstParam(["/a", "/b"])).toBe("/a");
  });

  it("answers null for a missing or empty one", () => {
    expect(firstParam(undefined)).toBeNull();
    expect(firstParam("")).toBeNull();
    expect(firstParam([])).toBeNull();
  });
});
