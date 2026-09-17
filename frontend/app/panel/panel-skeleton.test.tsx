import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";

import { PanelSkeleton } from "./panel-skeleton";

afterEach(cleanup);

describe("PanelSkeleton", () => {
  it("tells a screen reader what is happening, because the blocks say nothing", () => {
    render(<PanelSkeleton />);

    expect(screen.getByRole("status").textContent).toMatch(/Sprawdzamy sesję/);
  });

  it("hides the blocks themselves from assistive technology", () => {
    // Read aloud, a picture of a panel is a list of empty boxes. The sentence
    // above is the whole message.
    const { container } = render(<PanelSkeleton />);

    const decoration = container.querySelector("[aria-hidden='true']");
    expect(decoration).not.toBeNull();
    expect(decoration?.querySelector("header")).not.toBeNull();
  });

  it("reserves the mode band only for the panel that has one", () => {
    // The operator header starts with the mode band, so the operator's waiting
    // state has to be that much taller. Reserved blank, never labelled: nobody
    // has answered yet who is signed in.
    const { container: applicant } = render(<PanelSkeleton />);
    const { container: operator } = render(<PanelSkeleton modeBar />);

    const bands = (root: HTMLElement) =>
      root.querySelectorAll("header > div.h-8").length;

    expect(bands(applicant)).toBe(0);
    expect(bands(operator)).toBe(1);
    expect(operator.textContent).not.toMatch(/Tryb operatora/);
  });

  it("does not animate for somebody who asked for less motion", () => {
    // A pulsing page is a documented migraine and vestibular trigger, and this
    // is a public body's site where accessibility is a formal requirement.
    const { container } = render(<PanelSkeleton />);

    const animated = container.querySelector("[class*='animate-pulse']");
    expect(animated).not.toBeNull();
    // Every occurrence carries the motion-safe prefix, so the media query is
    // what switches the animation off rather than a second rule somewhere.
    const classes = (animated as HTMLElement).className.split(/\s+/);
    expect(classes).toContain("motion-safe:animate-pulse");
    expect(classes).not.toContain("animate-pulse");
  });
});
