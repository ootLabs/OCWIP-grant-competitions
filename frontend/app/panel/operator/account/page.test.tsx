import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";

import OperatorAccountPage from "./page";

afterEach(cleanup);

describe("Moje konto (operator)", () => {
  it("says the screen is not ready instead of a dead link", () => {
    render(<OperatorAccountPage />);

    expect(screen.getByRole("heading", { level: 1, name: "Moje konto" })).toBeDefined();
    expect(screen.getByText("To jeszcze nie jest gotowe")).toBeDefined();
  });
});
