import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";

import AccountPage from "./page";

afterEach(cleanup);

describe("Moje konto", () => {
  it("offers the password and the address change (T-106)", () => {
    render(<AccountPage />);

    expect(screen.getByRole("heading", { level: 1, name: "Moje konto" })).toBeDefined();
    expect(screen.getByRole("button", { name: "Zmień hasło" })).toBeDefined();
    expect(screen.getByRole("button", { name: "Zmień adres" })).toBeDefined();
  });
});
