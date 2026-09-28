import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";

import AccessibilityDeclarationPage from "./page";

afterEach(cleanup);

describe("Deklaracja dostępności", () => {
  it("says the screen is not ready instead of a dead link", () => {
    render(<AccessibilityDeclarationPage />);

    expect(screen.getByRole("heading", { level: 1, name: "Deklaracja dostępności" })).toBeDefined();
    expect(screen.getByText("To jeszcze nie jest gotowe")).toBeDefined();
  });
});
