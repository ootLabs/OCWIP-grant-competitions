import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";

import { NotReadyView } from "./not-ready-view";

afterEach(cleanup);

describe("NotReadyView", () => {
  it("names the screen and says exactly that it is not ready yet", () => {
    render(<NotReadyView title="Moje konto" />);

    expect(screen.getByRole("heading", { level: 1, name: "Moje konto" })).toBeDefined();
    expect(screen.getByRole("heading", { level: 2, name: "To jeszcze nie jest gotowe" })).toBeDefined();
    expect(screen.getByText(/kolejnych aktualizacji/)).toBeDefined();
  });
});
