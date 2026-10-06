import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";

import { PanelSkeleton } from "./panel-skeleton";

afterEach(cleanup);

describe("PanelSkeleton", () => {
  it("tells a screen reader what is happening, because the blocks say nothing", () => {
    render(<PanelSkeleton links={3} />);

    expect(screen.getByRole("status").textContent).toMatch(/Sprawdzamy sesję/);
  });

  it("hides the blocks themselves from assistive technology", () => {
    // Read aloud, a picture of a panel is a list of empty boxes. The sentence
    // above is the whole message.
    const { container } = render(<PanelSkeleton links={3} />);

    const decoration = container.querySelector("[aria-hidden='true']");
    expect(decoration).not.toBeNull();
    expect(decoration?.querySelector("header")).not.toBeNull();
  });

  it("reserves one navigation placeholder per real link", () => {
    // Counted from the panel's navigation module at the call site, so a fifth
    // position added to a panel makes its waiting state taller too, instead of
    // leaving a header that grows by one row the moment the session is known.
    const { container } = render(<PanelSkeleton links={4} />);

    expect(container.querySelectorAll("header > div:last-of-type > div").length).toBe(4);
  });

  it("does not animate for somebody who asked for less motion", () => {
    // A pulsing page is a documented migraine and vestibular trigger, and this
    // is a public body's site where accessibility is a formal requirement.
    const { container } = render(<PanelSkeleton links={3} />);

    const animated = container.querySelector("[class*='animate-pulse']");
    expect(animated).not.toBeNull();
    // Every occurrence carries the motion-safe prefix, so the media query is
    // what switches the animation off rather than a second rule somewhere.
    const classes = (animated as HTMLElement).className.split(/\s+/);
    expect(classes).toContain("motion-safe:animate-pulse");
    expect(classes).not.toContain("animate-pulse");
  });
});
