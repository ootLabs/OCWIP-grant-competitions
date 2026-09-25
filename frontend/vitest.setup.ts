import axe from "axe-core";
import { afterEach, expect, vi } from "vitest";

/*
  Every rendered test is also an accessibility test (T-46): after each test,
  axe-core checks what the screen showed against WCAG 2.1 A and AA, plus the
  heading order. Colour contrast is off here, because jsdom paints nothing;
  app/contrast-tokens.test.ts checks colours on the tokens instead.

  Test files call cleanup() in their own afterEach, which runs before this
  root-level hook, so cleanup is wrapped to keep a copy of the page it is
  about to throw away.
*/
const snapshots: Node[] = [];

vi.mock("@testing-library/react", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@testing-library/react")>();
  return {
    ...actual,
    cleanup: () => {
      if (typeof document !== "undefined" && document.body.childElementCount > 0) {
        snapshots.push(document.body.cloneNode(true));
      }
      actual.cleanup();
    },
  };
});

afterEach(async () => {
  if (typeof document === "undefined") return;
  const pending = snapshots.splice(0);
  // axe schedules its own work on timers; a test that left fake ones on
  // would leave it waiting forever.
  if (vi.isFakeTimers()) vi.useRealTimers();

  const contexts: Element[] = document.body.childElementCount > 0 ? [document.body] : [];
  for (const snapshot of pending) {
    const host = document.createElement("div");
    host.append(...Array.from(snapshot.childNodes));
    document.body.append(host);
    contexts.push(host);
  }

  const problems: string[] = [];
  for (const context of contexts) {
    const result = await axe.run(context, {
      runOnly: { type: "tag", values: ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"] },
      rules: {
        "color-contrast": { enabled: false },
        "heading-order": { enabled: true },
        "empty-heading": { enabled: true },
      },
    });
    for (const violation of result.violations) {
      problems.push(`${violation.id}: ${violation.help}`);
      for (const node of violation.nodes.slice(0, 3)) {
        problems.push(`  ${node.target.join(" ")}: ${node.html.slice(0, 160)}`);
      }
    }
    if (context !== document.body) context.remove();
  }

  expect(problems, expect.getState().currentTestName).toEqual([]);
});
