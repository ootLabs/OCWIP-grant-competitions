// @vitest-environment node
import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative } from "node:path";
import { describe, expect, it } from "vitest";

// S-45: without JavaScript, or before it has loaded, a form with no method is
// sent as a GET and the password travels in the address, into the browser
// history and the proxy's access log. Every form on a screen that handles a
// password therefore says POST, so the worst case is a refused request.
const root = process.cwd();

function sources(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) return sources(path);
    return path.endsWith(".tsx") && !path.endsWith(".test.tsx") ? [path] : [];
  });
}

describe("forms that handle a password", () => {
  it("are sent with POST, never in the address", () => {
    const loose: string[] = [];
    let checked = 0;
    for (const dir of ["app", "components"]) {
      for (const path of sources(join(root, dir))) {
        const source = readFileSync(path, "utf8");
        if (!/type=["']password["']|autoComplete=["'](new|current)-password["']|PasswordField/.test(source)) continue;
        for (const match of source.matchAll(/<form\b[^>]*>/g)) {
          checked++;
          if (!/\bmethod=["']post["']/i.test(match[0])) {
            loose.push(`${relative(root, path)}:${source.slice(0, match.index).split("\n").length}`);
          }
        }
      }
    }
    expect(checked).toBeGreaterThanOrEqual(5);
    expect(loose).toEqual([]);
  });
});
