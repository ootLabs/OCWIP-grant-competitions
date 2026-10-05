// @vitest-environment node
import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative } from "node:path";
import { describe, expect, it } from "vitest";

const root = process.cwd();

function sources(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) return sources(path);
    return path.endsWith(".tsx") && !path.endsWith(".test.tsx") ? [path] : [];
  });
}

describe("every horizontally scrolled box", () => {
  // A wide table scrolls inside its own box. Its cells carry sr-only labels,
  // which are absolutely positioned; without a positioned box around them they
  // are placed against the page instead, past the right edge of the table, and
  // the whole document grows sideways by the width of the table. That is how
  // the evaluation page came out 1970 px wide in a 1440 px window.
  it("is a positioned box, so absolutely placed content stays inside it", () => {
    const loose = ["app", "components"].flatMap((dir) =>
      sources(join(root, dir)).flatMap((path) =>
        [...readFileSync(path, "utf8").matchAll(/className=(?:"|\{`)([^"`]*\boverflow-x-auto\b[^"`]*)/g)]
          .filter((match) => !/\b(relative|absolute|fixed|sticky)\b/.test(match[1]))
          .map((match) => `${relative(root, path)}: ${match[1]}`),
      ),
    );

    expect(loose).toEqual([]);
  });
});
