// @vitest-environment node
import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative } from "node:path";
import { describe, expect, it } from "vitest";

// What axe cannot see in jsdom, because it needs real colours or real focus:
// checked in the source instead, so a new screen cannot quietly undo T-46.
const root = process.cwd();

function sources(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) return sources(path);
    return path.endsWith(".tsx") && !path.endsWith(".test.tsx") ? [path] : [];
  });
}

type Tag = { file: string; line: number; text: string; after: string };

function tags(pattern: RegExp): Tag[] {
  return ["app", "components"].flatMap((dir) =>
    sources(join(root, dir)).flatMap((path) => {
      const source = readFileSync(path, "utf8");
      return [...source.matchAll(pattern)].map((match) => {
        let end = match.index + match[0].length;
        for (let depth = 0; end < source.length; end++) {
          const char = source[end];
          if (char === "{") depth++;
          else if (char === "}") depth--;
          else if (char === ">" && depth === 0) break;
        }
        return {
          file: relative(root, path),
          line: source.slice(0, match.index).split("\n").length,
          text: source.slice(match.index, end + 1),
          after: source.slice(end + 1, end + 400),
        };
      });
    }),
  );
}

const where = (tag: Tag) => `${tag.file}:${tag.line}`;

describe("the source of every screen", () => {
  // Link text is #413d39 on #231f20 body text, 1.51:1: without an underline a
  // link inside a sentence is found only by colour (WCAG 1.4.1). Every link
  // says how it looks: underlined, explicitly not (navigation), a button, the
  // skip link, or a logo.
  it("gives every link a look that is not colour alone", () => {
    const unmarked = tags(/<(Link|a)\b/g).filter(
      (tag) =>
        !/underline|statusActionClassName|bg-brand-accent|sr-only/.test(tag.text) &&
        !/^\s*(\{\/\*[\s\S]*?\*\/\}\s*)*<img\b/.test(tag.after),
    );
    expect(unmarked.map(where)).toEqual([]);
  });

  // The edge of a form control needs 3:1 (WCAG 1.4.11); --color-border is
  // 1.3:1 and stays for cards and table rules.
  it("draws every text control with the control border", () => {
    const faint = tags(/<(input|select|textarea)\b/g).filter(
      (tag) =>
        !/type="(checkbox|radio|file)"/.test(tag.text) &&
        /(^|[^\w-])border-border([^\w-]|$)/.test(tag.text),
    );
    expect(faint.map(where)).toEqual([]);
  });

  it("never takes the focus ring away or reorders the tab sequence", () => {
    const offenders = tags(/<[A-Za-z][\w.]*\b/g).filter((tag) =>
      /outline-none|outline-0|tabIndex=\{[1-9]/.test(tag.text),
    );
    expect(offenders.map(where)).toEqual([]);
  });
});
