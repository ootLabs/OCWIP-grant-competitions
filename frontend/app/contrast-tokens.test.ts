// @vitest-environment node
import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

import { contrastRatio, WCAG_AA_LARGE_TEXT, WCAG_AA_TEXT } from "@/lib/contrast";

// Both palettes read from globals.css itself, not copied here, so a token
// changed there is checked here on the next run (T-46: contrast checked with a
// tool, not by eye). The pairs are the ones the components actually put
// together: foreground utility on background utility.
const css = readFileSync(new URL("./globals.css", import.meta.url), "utf8");

function block(selector: string): string {
  const start = css.indexOf(`${selector} {`);
  if (start < 0) throw new Error(`No ${selector} block in globals.css`);
  return css.slice(start, css.indexOf("\n}", start));
}

function declarations(source: string): Map<string, string> {
  const tokens = new Map<string, string>();
  for (const match of source.matchAll(/--color-([a-z-]+):\s*([^;]+);/g)) {
    tokens.set(match[1], match[2].trim());
  }
  return tokens;
}

function resolve(tokens: Map<string, string>, name: string): string {
  const value = tokens.get(name);
  if (value === undefined) throw new Error(`Unknown token --color-${name}`);
  const reference = /^var\(--color-([a-z-]+)\)$/.exec(value);
  return reference ? resolve(tokens, reference[1]) : value;
}

const light = declarations(block("@theme"));
const contrast = new Map([...light, ...declarations(block('[data-contrast="true"]'))]);

type Pair = { foreground: string; background: string; minimum: number; use: string };

const text = WCAG_AA_TEXT;
// Non-text contrast (WCAG 1.4.11) has the same 3:1 floor as large text.
const ui = WCAG_AA_LARGE_TEXT;

const pairs: Pair[] = [
  { foreground: "text", background: "bg", minimum: text, use: "body text" },
  { foreground: "text", background: "surface-muted", minimum: text, use: "text on a grey panel" },
  { foreground: "text-link", background: "bg", minimum: text, use: "link" },
  { foreground: "brand-accent-text", background: "bg", minimum: text, use: "accent text, errors" },
  { foreground: "brand-accent-text", background: "surface-muted", minimum: text, use: "accent text on a grey panel" },
  { foreground: "brand-accent", background: "bg", minimum: text, use: "accent text" },
  { foreground: "brand-accent-hover", background: "bg", minimum: text, use: "accent text, hovered" },
  { foreground: "bg", background: "brand-accent", minimum: text, use: "button label" },
  { foreground: "bg", background: "brand-accent-hover", minimum: text, use: "button label, hovered" },
  { foreground: "active-text", background: "active-bg", minimum: text, use: "operator band, current item" },
  { foreground: "border-control", background: "bg", minimum: ui, use: "edge of a form control" },
  { foreground: "border-control", background: "surface-muted", minimum: ui, use: "form control on a grey panel" },
  { foreground: "brand-accent", background: "bg", minimum: ui, use: "button edge" },
  { foreground: "active-border", background: "bg", minimum: ui, use: "current navigation item" },
  { foreground: "focus", background: "bg", minimum: ui, use: "focus ring" },
  { foreground: "focus", background: "surface-muted", minimum: ui, use: "focus ring on a grey panel" },
];

describe.each([
  ["the normal palette", light],
  ["the high contrast palette", contrast],
])("%s", (_, tokens) => {
  it.each(pairs)("$foreground on $background ($use)", ({ foreground, background, minimum }) => {
    const ratio = contrastRatio(resolve(tokens, foreground), resolve(tokens, background));
    expect(ratio).toBeGreaterThanOrEqual(minimum);
  });
});

describe("the high contrast palette", () => {
  // A token the high contrast block forgets keeps its light value on a black
  // page. Every colour a component can use for text, a background or an edge
  // has to be reassigned; only the logo orange, which is never text, is not.
  it("reassigns every colour token but the logo mark", () => {
    const overridden = new Set(declarations(block('[data-contrast="true"]')).keys());
    const missing = [...light.keys()].filter(
      (name) => !overridden.has(name) && !name.startsWith("brand-logo"),
    );
    expect(missing).toEqual([]);
  });
});
