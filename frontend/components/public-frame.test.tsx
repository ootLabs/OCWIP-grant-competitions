import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";

import { PublicFrame } from "./public-frame";

afterEach(cleanup);

describe("PublicFrame", () => {
  it("offers the high contrast switch before anybody signs in", () => {
    render(
      <PublicFrame>
        <h1>Konkursy</h1>
      </PublicFrame>,
    );

    // The pages with the most outside traffic are these, so the switch has
    // to be here and not only in the panels (T-46).
    expect(screen.getByRole("button", { name: "Wysoki kontrast" })).toBeDefined();
    expect(screen.getByRole("main").id).toBe("tresc");
    expect(screen.getByRole("link", { name: "Przejdź do treści" }).getAttribute("href")).toBe("#tresc");
  });
});
