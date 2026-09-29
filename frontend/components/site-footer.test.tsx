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
});
