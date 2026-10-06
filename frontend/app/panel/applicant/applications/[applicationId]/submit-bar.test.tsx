import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";

import type { SubmissionGap } from "@/lib/forms/submission-gaps";

import { SubmitBar } from "./submit-bar";

afterEach(cleanup);

function gap(index: number): SubmissionGap {
  return {
    sectionKey: "s1",
    sectionTitle: "Część I",
    fieldKey: `pole${index}`,
    fieldLabel: `Pole ${index}`,
    message: "To pole jest wymagane.",
    anchorId: `pole${index}`,
  } as SubmissionGap;
}

describe("SubmitBar", () => {
  it("lists the first five gaps and folds the rest behind one button", () => {
    render(<SubmitBar gaps={Array.from({ length: 8 }, (_, i) => gap(i + 1))} onJump={vi.fn()} onContinue={vi.fn()} />);

    expect(screen.getByText("Zanim złożysz wniosek, uzupełnij (8):")).toBeDefined();
    expect(screen.getByRole("button", { name: /Pole 5 -/ })).toBeDefined();
    expect(screen.queryByRole("button", { name: /Pole 6 -/ })).toBeNull();

    const more = screen.getByRole("button", { name: "Pokaż wszystkie (8)" });
    expect(more.getAttribute("aria-expanded")).toBe("false");
    fireEvent.click(more);

    expect(screen.getByRole("button", { name: /Pole 8 -/ })).toBeDefined();
    expect(screen.getByRole("button", { name: "Pokaż mniej" }).getAttribute("aria-expanded")).toBe("true");
  });

  it("offers no fold for a short list", () => {
    render(<SubmitBar gaps={[gap(1), gap(2)]} onJump={vi.fn()} onContinue={vi.fn()} />);

    expect(screen.queryByRole("button", { name: /Pokaż wszystkie/ })).toBeNull();
    expect(screen.getByRole("button", { name: "Złóż wniosek" })).toHaveProperty("disabled", true);
  });
});
