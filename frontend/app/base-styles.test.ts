// @vitest-environment node
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";

const root = process.cwd();

/**
 * The file without what is only written about it.
 *
 * Every rule here is explained in a comment that quotes the rule, so a check
 * reading the file whole passes on the explanation alone and keeps passing
 * after somebody deletes the code under it.
 */
function code(path: string): string {
  return readFileSync(join(root, path), "utf8")
    .replace(/\/\*[\s\S]*?\*\//g, "")
    .replace(/^\s*\/\/.*$/gm, "");
}

const css = code("app/globals.css");
const layout = code("app/layout.tsx");

describe("the base layer", () => {
  // Tailwind 3 gave a button the hand cursor and Tailwind 4 does not. Every
  // control in this product is a <button>, so the upgrade quietly took the
  // pointer off all of them and nothing in the test suite noticed.
  it("gives a button the pointer, which Tailwind 4 stopped doing", () => {
    const rule = css.match(/\bbutton,[^}]*?\{[^}]*?\}/s);

    expect(rule).not.toBeNull();
    expect(rule?.[0]).toContain("cursor: pointer");
  });

  // A modal <dialog> centres itself through the user agent rule
  // dialog:modal { margin: auto }. The preflight sets margin: 0 on every
  // element, which beats it, and every confirmation in the product opened in
  // the top left corner of the window.
  it("puts back the margin a modal dialog centres itself with", () => {
    expect(css).toMatch(/dialog:modal\s*\{[^}]*margin:\s*auto/s);
  });
});

describe("the brand typography", () => {
  // The headings were set in Playfair Display, a serif. One token carries the
  // decision, so this is the one place that can state it.
  it("sets headings in the sans stack, not a serif one", () => {
    const token = css.match(/--font-heading:\s*([^;]+);/);

    expect(token).not.toBeNull();
    expect(token?.[1]).toContain("--font-poppins");
    expect(token?.[1]).not.toMatch(/(?<!-)\bserif\b/);
  });

  it("stops downloading the serif nobody renders any more", () => {
    expect(layout.toLowerCase()).not.toContain("playfair");
  });

  // Headings ask for weight 800, and a weight the font was not loaded with is
  // one the browser fakes by smearing the 400 cut.
  it("loads the weight the heading rule asks for", () => {
    const heading = css.match(/h1,[\s\S]{0,120}?font-weight:\s*(\d+)/);
    const weights = layout.match(/weight:\s*\[([^\]]+)\]/);

    expect(heading).not.toBeNull();
    expect(weights?.[1]).toContain(heading?.[1] ?? "nie znaleziono wagi nagłówka");
  });
});
