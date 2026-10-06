// @vitest-environment node
import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative } from "node:path";
import { describe, expect, it } from "vitest";

const root = process.cwd();

/**
 * The file without its comments, which quote the very names being checked:
 * read whole, the frame below passes on its own explanation.
 */
function code(path: string): string {
  return readFileSync(join(root, path), "utf8")
    .replace(/\{?\/\*[\s\S]*?\*\/\}?/g, "")
    .replace(/^\s*\/\/.*$/gm, "");
}

function sources(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) return sources(path);
    return path.endsWith(".tsx") && !path.endsWith(".test.tsx") ? [path] : [];
  });
}

/**
 * A capped block that is allowed to sit where it is put, with the reason.
 *
 * Everything else capped narrower than the row around it has to centre itself,
 * because the row is centred and a narrow block inside it otherwise hugs the
 * left edge with the rest of the screen empty. Measured before this was fixed:
 * "Mój profil" sat 336 px off centre, the operator's competition page 976 px.
 */
function allowed(classes: string, path: string): boolean {
  // A <dialog>: centred by the user agent, see app/base-styles.test.ts.
  if (classes.includes("backdrop:")) return true;
  // A measure for reading, set under its own heading, not a block on a page.
  if (/max-w-prose\b/.test(classes)) return true;
  // Truncation of a name that has to fit beside other things in a row.
  if (/max-w-\[/.test(classes)) return true;
  // Its own parent is a centring flex box (components/status-page.tsx).
  if (path.endsWith("status-page.tsx")) return true;
  return false;
}

describe("every capped block in a panel", () => {
  it("centres itself inside the row, instead of hugging its left edge", () => {
    const adrift = ["app/panel", "components"].flatMap((dir) =>
      sources(join(root, dir)).flatMap((path) =>
        [...readFileSync(path, "utf8").matchAll(/className=(?:"|\{`)([^"`]*\bmax-w-[^"`]*)/g)]
          .filter((match) => !/\bmx-auto\b/.test(match[1]))
          .filter((match) => !allowed(match[1], path))
          .map((match) => `${relative(root, path)}: ${match[1]}`),
      ),
    );

    expect(adrift).toEqual([]);
  });
});

describe("a panel frame and its header", () => {
  // A header capped at one width over content capped at another reads as a
  // page that cannot decide where its left edge is. They share a constant so
  // that they cannot be changed apart.
  const frames = [
    ["app/panel/applicant/applicant-panel.tsx", "app/panel/applicant/panel-header.tsx", "panelRowClassName"],
    ["app/panel/reviewer/reviewer-panel.tsx", "app/panel/reviewer/reviewer-header.tsx", "panelRowClassName"],
    ["app/panel/operator/operator-panel.tsx", "app/panel/operator/operator-header.tsx", "operatorRowClassName"],
  ] as const;

  it.each(frames)("agree on one row width (%s)", (frame, header, constant) => {
    expect(code(frame)).toContain(constant);
    expect(code(header)).toContain(constant);
  });

  // Full bleed was the operator's old answer and the one being replaced.
  it("caps the operator too, who used to take the whole window", () => {
    const panel = code("app/panel/operator/operator-panel.tsx");

    expect(panel).toContain("operatorRowClassName");
    // The wide table still scrolls in main rather than widening the document.
    expect(panel).toMatch(/id="tresc"[^>]*overflow-x-auto/);
  });
});
