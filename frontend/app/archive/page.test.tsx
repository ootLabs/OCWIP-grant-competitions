import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";

import ArchivePage from "./page";

afterEach(cleanup);

describe("Archiwum wyników", () => {
  it("says the screen is not ready instead of a dead link", () => {
    render(<ArchivePage />);

    expect(screen.getByRole("heading", { level: 1, name: "Archiwum wyników" })).toBeDefined();
    expect(screen.getByText("To jeszcze nie jest gotowe")).toBeDefined();
  });
});
