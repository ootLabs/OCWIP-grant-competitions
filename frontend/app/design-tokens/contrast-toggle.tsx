"use client";

import { useState } from "react";

// Demo scoped to this wrapper only: [data-contrast="true"] in globals.css reassigns
// --color-bg/--color-text/--color-active-* on whatever element carries the attribute,
// not just <html>, so flipping it here previews OCWIP's high contrast mode through the
// same token classes the rest of the app uses, without touching the rest of the page.
export function ContrastToggle() {
  const [contrast, setContrast] = useState(false);

  return (
    <div>
      <label className="flex w-fit items-center gap-2 text-sm">
        <input
          type="checkbox"
          checked={contrast}
          onChange={(event) => setContrast(event.target.checked)}
        />
        Podgląd trybu wysokiego kontrastu
      </label>

      <div
        data-contrast={contrast ? "true" : undefined}
        className="mt-3 flex flex-wrap items-center gap-4 rounded-lg border border-border bg-bg p-6 text-text"
      >
        <p>Przykładowy tekst w trybie kontrastu.</p>
        <button
          type="button"
          className="rounded-[var(--radius-sm)] border-2 border-active-border bg-active-bg px-4 py-2 font-semibold text-active-text"
        >
          Przykładowy przycisk
        </button>
        <a href="#" className="text-text underline">
          Przykładowy link z widocznym fokusem (Tab)
        </a>
      </div>
    </div>
  );
}
