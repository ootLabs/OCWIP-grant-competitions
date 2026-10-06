// @vitest-environment node
import { describe, expect, it } from "vitest";

import * as styles from "./styles";

const actions = Object.entries(styles).filter(([name]) => name.endsWith("ActionClassName"));

describe("action styles", () => {
  // "Wstecz" on the first step and "Złóż wniosek" with gaps left are disabled,
  // and they used to fill with orange under the pointer like any live button.
  it.each(actions)("%s keeps a disabled control unlit on hover", (_, classes) => {
    const hoverBackground = classes.split(" ").filter((name) => name.startsWith("hover:bg-"));

    expect(hoverBackground.length).toBeGreaterThan(0);
    expect(classes.split(" ").some((name) => name.startsWith("disabled:hover:bg-"))).toBe(true);
  });
});
