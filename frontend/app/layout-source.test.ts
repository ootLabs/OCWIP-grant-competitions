// @vitest-environment node
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";

const layout = readFileSync(join(process.cwd(), "app", "layout.tsx"), "utf8");

describe("root layout", () => {
  // The contrast boot script carries the CSP nonce. A browser hides a nonce
  // value from DOM reads once the policy applies, so React hydrates "" against
  // the real value in the server HTML and logs a mismatch on every page load.
  // suppressHydrationWarning on <html> does not reach it: the flag covers one
  // element and its text, never the attributes of elements inside it.
  it("keeps the nonced boot script out of hydration diffing", () => {
    const script = layout.slice(layout.indexOf("<script"), layout.indexOf("</head>"));

    expect(script).toContain("nonce={nonce}");
    expect(script).toContain("suppressHydrationWarning");
  });
});
