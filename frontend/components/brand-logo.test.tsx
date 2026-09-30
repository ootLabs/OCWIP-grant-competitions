import { readFileSync } from "node:fs";
import { join } from "node:path";
import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { BrandLogo } from "./brand-logo";

afterEach(cleanup);

describe("BrandLogo", () => {
  // The bug this guards: the wordmark carried no fill, so it fell back to the
  // SVG default, black, and vanished on the black page of the high contrast
  // palette. currentColor makes it follow --color-text in both palettes.
  it("paints the wordmark with the inherited text colour", () => {
    const { container } = render(<BrandLogo />);
    const wordmark = container.querySelector('g[fill="currentColor"]');

    expect(wordmark).not.toBeNull();
    expect(wordmark!.querySelectorAll("path").length).toBeGreaterThan(1);
    expect(container.querySelector("svg")?.getAttribute("class")).toContain("text-text");
  });

  // The flame and the dot are the one brand colour the contrast palette leaves
  // alone, so they must read the token rather than a hard-coded hex that a
  // later palette change would not reach.
  it("paints the flame and the dot with the logo colour token", () => {
    const { container } = render(<BrandLogo />);
    const branded = [...container.querySelectorAll("[fill]")].filter(
      (node) => node.getAttribute("fill") === "var(--color-brand-logo-orange)",
    );

    expect(branded).toHaveLength(2);
    expect(container.querySelector('[fill="#EB6209"]')).toBeNull();
  });

  // In the panel headers the mark is the only content of the link home.
  it("has an accessible name", () => {
    render(<BrandLogo />);

    expect(screen.getByRole("img", { name: "OCWIP" })).toBeTruthy();
  });

  it("takes the name the caller gives it", () => {
    render(<BrandLogo title="Logo OCWIP" />);

    expect(screen.getByRole("img", { name: "Logo OCWIP" })).toBeTruthy();
  });

  // Nothing may go back to <img src="/ocwip-logo.svg">: a mark loaded that way
  // renders in its own document, where no page token reaches it, which is the
  // whole reason this component exists.
  it("is what every header renders, with no img left behind", () => {
    const pliki = [
      "app/panel/applicant/panel-header.tsx",
      "app/panel/operator/operator-header.tsx",
      "app/panel/reviewer/reviewer-header.tsx",
      "components/public-frame.tsx",
      "app/design-tokens/page.tsx",
    ];

    for (const plik of pliki) {
      const zrodlo = readFileSync(join(process.cwd(), plik), "utf8");
      expect(zrodlo, plik).toContain("<BrandLogo");
      expect(zrodlo, plik).not.toContain("ocwip-logo.svg");
    }
  });
});
