import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, render, screen } from "@testing-library/react";

import { IntakeCountdown } from "./intake-countdown";

const MESSAGE =
  "Nabór trwa. Wnioski można składać do 25.10.2026 o godzinie 11:00 czasu polskiego.";

beforeEach(() => {
  vi.useFakeTimers();
  vi.setSystemTime(new Date("2026-10-22T03:00:00Z"));
});

afterEach(() => {
  cleanup();
  vi.useRealTimers();
});

describe("IntakeCountdown", () => {
  it("shows the remaining time next to the sentence from the rule", () => {
    render(<IntakeCountdown closesAt="2026-10-25T10:00:00Z" message={MESSAGE} />);

    expect(screen.getByText(/3 dni i 7 godzin/)).toBeTruthy();
    // The hour and the zone come from the backend and are printed once, not
    // rebuilt here where they could drift.
    expect(screen.getByText(MESSAGE)).toBeTruthy();
  });

  it("keeps ticking without ever showing seconds", () => {
    render(<IntakeCountdown closesAt="2026-10-25T10:00:00Z" message={MESSAGE} />);

    act(() => {
      vi.advanceTimersByTime(60 * 60 * 1000);
    });

    expect(screen.getByText(/3 dni i 6 godzin/)).toBeTruthy();
    expect(screen.queryByText(/sekund/)).toBeNull();
  });

  it("stops counting the moment the intake closes", () => {
    render(<IntakeCountdown closesAt="2026-10-22T03:30:00Z" message={MESSAGE} />);

    expect(screen.getByText(/30 minut/)).toBeTruthy();

    act(() => {
      vi.advanceTimersByTime(31 * 60 * 1000);
    });

    expect(screen.queryByText(/Do zamknięcia naboru pozostało/)).toBeNull();
    expect(screen.getByText(MESSAGE)).toBeTruthy();
  });

  it("has nothing to count for a continuous intake and says so anyway", () => {
    const continuous = "Nabór ciągły. Wnioski można składać bez terminu końcowego.";

    render(<IntakeCountdown closesAt={null} message={continuous} />);

    expect(screen.queryByText(/Do zamknięcia naboru pozostało/)).toBeNull();
    expect(screen.getByText(continuous)).toBeTruthy();
  });

  it("drops a span measured against a deadline that is no longer there", () => {
    const { rerender } = render(
      <IntakeCountdown closesAt="2026-10-25T10:00:00Z" message={MESSAGE} />,
    );

    expect(screen.getByText(/3 dni i 7 godzin/)).toBeTruthy();

    const continuous = "Nabór ciągły. Wnioski można składać bez terminu końcowego.";
    rerender(<IntakeCountdown closesAt={null} message={continuous} />);

    expect(screen.queryByText(/Do zamknięcia naboru pozostało/)).toBeNull();
  });

  it("stops its timer when it leaves the page", () => {
    const { unmount } = render(
      <IntakeCountdown closesAt="2026-10-25T10:00:00Z" message={MESSAGE} />,
    );

    unmount();

    // A timer left running after unmount keeps a closed page alive in memory
    // and, in a test, sets state on a component that is gone.
    expect(vi.getTimerCount()).toBe(0);
  });
});
