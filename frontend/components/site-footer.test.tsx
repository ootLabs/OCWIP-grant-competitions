import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";

import { SiteFooter } from "./site-footer";

afterEach(cleanup);

describe("SiteFooter", () => {
  it("links the statement with the exact text the regulation asks for, and the three information pages", () => {
    render(<SiteFooter />);

    expect(screen.getByRole("link", { name: "Deklaracja dostępności" }).getAttribute("href")).toBe("/deklaracja-dostepnosci");
    expect(screen.getByRole("link", { name: "Regulamin serwisu" }).getAttribute("href")).toBe("/regulamin");
    expect(screen.getByRole("link", { name: "Klauzula informacyjna" }).getAttribute("href")).toBe("/klauzula-informacyjna");
    expect(screen.getByRole("link", { name: "Kontakt" }).getAttribute("href")).toBe("/kontakt");
  });

  it("credits mLab and mobitouch with the marks themselves as links to their sites", () => {
    render(<SiteFooter />);

    // Inline marks with a name, not <img>: the high contrast palette reaches them (B-GUI-03).
    expect(document.querySelector("footer img")).toBeNull();
    expect(document.querySelector("footer")!.textContent).toContain("Aplikacja stworzona przez");

    for (const [name, href] of [
      ["mLab", "https://mkariera.pl"],
      ["mobitouch", "https://mobitouch.net"],
    ]) {
      const link = screen.getByRole("link", { name: new RegExp(`^${name}`) });
      expect(link.getAttribute("href")).toBe(href);
      expect(link.getAttribute("target")).toBe("_blank");
      expect(link.getAttribute("rel")).toContain("noopener");
      expect(link.querySelector("svg")!.getAttribute("aria-label")).toBe(name);
      // Said to a screen reader, not printed beside the mark.
      expect(link.textContent).toContain("otwiera się w nowej karcie");
      expect(link.querySelector(".sr-only")).not.toBeNull();
    }
  });

  it("keeps the credit in the one row of the footer, after the name of OCWIP", () => {
    render(<SiteFooter />);

    const footer = document.querySelector("footer")!;
    expect(footer.children.length).toBe(1);
    const ocwip = footer.textContent!.indexOf("Opolskie Centrum Wspierania Inicjatyw Pozarządowych");
    const credit = footer.textContent!.indexOf("Aplikacja stworzona przez");
    expect(ocwip).toBeGreaterThanOrEqual(0);
    expect(credit).toBeGreaterThan(ocwip);
    expect(footer.textContent).not.toContain("Portfolio");
  });
});
