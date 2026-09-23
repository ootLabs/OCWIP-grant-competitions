import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { readJson, removeItem, writeJson } from "./local-storage";

beforeEach(() => {
  window.localStorage.clear();
});

describe("readJson / writeJson / removeItem", () => {
  it("round trips a value", () => {
    writeJson("k", { a: 1 });
    expect(readJson<{ a: number }>("k")).toEqual({ a: 1 });
  });

  it("reads a missing key as null", () => {
    expect(readJson("missing")).toBeNull();
  });

  it("survives a corrupted entry instead of throwing", () => {
    window.localStorage.setItem("k", "{not json");
    expect(readJson("k")).toBeNull();
  });

  it("removes a key", () => {
    writeJson("k", 1);
    removeItem("k");
    expect(readJson("k")).toBeNull();
  });

  it("degrades to null when localStorage is unavailable", () => {
    const original = window.localStorage;
    // @ts-expect-error simulating an environment without localStorage
    delete window.localStorage;

    expect(readJson("k")).toBeNull();
    expect(() => writeJson("k", 1)).not.toThrow();
    expect(() => removeItem("k")).not.toThrow();

    Object.defineProperty(window, "localStorage", {
      value: original,
      configurable: true,
    });
  });

  it("swallows a write failure instead of throwing", () => {
    const setItem = vi
      .spyOn(window.localStorage, "setItem")
      .mockImplementation(() => {
        throw new Error("QuotaExceededError");
      });

    expect(() => writeJson("k", 1)).not.toThrow();

    setItem.mockRestore();
  });
});
