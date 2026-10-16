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

  it("credits the programme and its organiser, as named images and one link that opens elsewhere safely", () => {
    render(<SiteFooter />);

    // Inline marks with a name, not <img>: the high contrast palette reaches them (B-GUI-03).
    expect(screen.getByRole("img", { name: "mLab" })).toBeTruthy();
    expect(screen.getByRole("img", { name: "mobitouch" })).toBeTruthy();
    expect(document.querySelector("footer img")).toBeNull();

    const portfolio = screen.getByRole("link", { name: /mkariera\.pl/ });
    expect(portfolio.getAttribute("href")).toBe("https://mkariera.pl");
    expect(portfolio.getAttribute("target")).toBe("_blank");
    expect(portfolio.getAttribute("rel")).toContain("noopener");
    expect(portfolio.textContent).toContain("otwiera się w nowej karcie");
  });

  it("puts the organiser of the competition first and the credit after it", () => {
    render(<SiteFooter />);

    const footer = document.querySelector("footer")!;
    const ocwip = footer.textContent!.indexOf("Opolskie Centrum Wspierania Inicjatyw Pozarządowych");
    const credit = footer.textContent!.indexOf("Aplikacja stworzona przez praktykantów programu");
    expect(ocwip).toBeGreaterThanOrEqual(0);
    expect(credit).toBeGreaterThan(ocwip);
  });
});
