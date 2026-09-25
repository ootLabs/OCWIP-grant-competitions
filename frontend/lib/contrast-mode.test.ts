import { afterEach, describe, expect, it } from "vitest";

import {
  CONTRAST_STORAGE_KEY,
  contrastBootScript,
  contrastEnabled,
  restoreContrast,
  setContrast,
} from "./contrast-mode";

afterEach(() => {
  document.documentElement.removeAttribute("data-contrast");
  window.localStorage.clear();
});

describe("setContrast", () => {
  it("switches the palette on the whole document and remembers it", () => {
    setContrast(true);

    expect(document.documentElement.getAttribute("data-contrast")).toBe("true");
    expect(contrastEnabled()).toBe(true);
    expect(window.localStorage.getItem(CONTRAST_STORAGE_KEY)).toBe("true");
  });

  it("switches it off without leaving an attribute behind", () => {
    setContrast(true);
    setContrast(false);

    expect(document.documentElement.hasAttribute("data-contrast")).toBe(false);
    expect(window.localStorage.getItem(CONTRAST_STORAGE_KEY)).toBe("false");
  });
});

// The boot script is restoreContrast written out as a string for the root
// layout. Both run against the same stored values, so neither can drift.
describe.each([
  ["restoreContrast", () => restoreContrast()],
  // eslint-disable-next-line no-new-func -- the exact string the layout inlines
  ["the boot script", () => new Function(contrastBootScript)()],
])("%s", (_, restore) => {
  it("brings back a palette chosen on an earlier visit", () => {
    window.localStorage.setItem(CONTRAST_STORAGE_KEY, "true");

    restore();

    expect(contrastEnabled()).toBe(true);
  });

  it.each([null, "false", "\"true\"", "not json"])("stays on the normal palette for %s", (stored) => {
    if (stored !== null) window.localStorage.setItem(CONTRAST_STORAGE_KEY, stored);

    restore();

    expect(contrastEnabled()).toBe(false);
  });
});
