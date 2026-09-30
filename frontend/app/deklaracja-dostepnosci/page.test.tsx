import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

vi.mock("next/headers", () => ({ headers: async () => new Headers({ host: "konkursy.example.pl" }) }));

const { default: AccessibilityDeclarationPage } = await import("./page");

afterEach(cleanup);

/** The ids the validator of "Warunki techniczne..." 2.0 requires. */
const required = [
  "a11y-wstep", "a11y-podmiot", "a11y-zakres", "a11y-url", "a11y-data-publikacja", "a11y-data-aktualizacja",
  "a11y-status", "a11y-kontakt", "a11y-email", "a11y-telefon", "a11y-procedura", "a11y-data-sporzadzenie",
  "a11y-architektura", "a11y-komunikacja",
];

describe("Deklaracja dostępności", () => {
  it("carries every id the validator requires", async () => {
    const { container } = render(await AccessibilityDeclarationPage());

    for (const id of required) {
      expect(container.querySelector(`#${id}`), id).not.toBeNull();
    }
    expect(container.querySelector("#a11y-url")?.getAttribute("href")).toBe("https://konkursy.example.pl");
  });

  it("has the headings of the template in their order", async () => {
    render(await AccessibilityDeclarationPage());

    expect(screen.getByRole("heading", { level: 1 }).textContent).toBe("Deklaracja dostępności");
    expect(screen.getAllByRole("heading", { level: 2 }).map((heading) => heading.textContent)).toEqual([
      "Stan dostępności cyfrowej",
      "Niedostępne treści",
      "Przygotowanie deklaracji dostępności",
      "Udogodnienia, ograniczenia i inne informacje",
      "Skróty klawiszowe",
      "Informacje zwrotne i dane kontaktowe",
      "Obsługa wniosków i skarg związanych z dostępnością",
      "Pozostałe informacje",
    ]);
    expect(screen.getAllByRole("heading", { level: 3 }).map((heading) => heading.textContent)).toEqual([
      "Aplikacje mobilne",
      "Dostępność architektoniczna",
      "Dostępność komunikacyjno-informacyjna",
    ]);
  });

  it("writes the dates as the validator reads them and the status as the template words it", async () => {
    const { container } = render(await AccessibilityDeclarationPage());

    for (const id of ["a11y-data-publikacja", "a11y-data-aktualizacja", "a11y-data-sporzadzenie"]) {
      const time = container.querySelector(`#${id}`);
      expect(time?.tagName).toBe("TIME");
      expect(time?.getAttribute("datetime")).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    }
    expect(container.querySelector("#a11y-status")?.textContent).toContain(
      "jest częściowo zgodna z ustawą z dnia 4 kwietnia 2019 r. o dostępności cyfrowej stron internetowych i aplikacji mobilnych podmiotów publicznych",
    );
  });

  // The dates render as "29 września 2026 r.", so a full stop written after
  // one printed "2026 r..". A typo on a document a public body is audited on.
  it("never doubles the full stop after a date", async () => {
    const { container } = render(await AccessibilityDeclarationPage());

    expect(container.textContent).not.toContain("..");
  });
});
