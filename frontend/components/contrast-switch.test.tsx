import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";

import { ContrastSwitch } from "./contrast-switch";

afterEach(() => {
  cleanup();
  document.documentElement.removeAttribute("data-contrast");
  window.localStorage.clear();
});

describe("ContrastSwitch", () => {
  it("says whether it is on, and switches the whole document", () => {
    render(<ContrastSwitch />);
    const button = screen.getByRole("button", { name: "Wysoki kontrast" });
    expect(button.getAttribute("aria-pressed")).toBe("false");

    fireEvent.click(button);

    expect(button.getAttribute("aria-pressed")).toBe("true");
    expect(document.documentElement.getAttribute("data-contrast")).toBe("true");

    fireEvent.click(button);

    expect(button.getAttribute("aria-pressed")).toBe("false");
    expect(document.documentElement.hasAttribute("data-contrast")).toBe(false);
  });

  it("shows as pressed when the page loaded with the palette already on", () => {
    document.documentElement.setAttribute("data-contrast", "true");

    render(<ContrastSwitch />);

    expect(
      screen.getByRole("button", { name: "Wysoki kontrast" }).getAttribute("aria-pressed"),
    ).toBe("true");
  });
});
