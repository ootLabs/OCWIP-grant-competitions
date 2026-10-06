"use client";

import { useEffect, useState } from "react";

import { contrastEnabled, setContrast } from "@/lib/contrast-mode";

/**
 * The high contrast switch in every header (T-46). A toggle button with
 * aria-pressed, not a checkbox: it acts at once, with nothing to submit, and
 * a screen reader announces it as "Wysoki kontrast, przycisk przełączający,
 * wciśnięty".
 */
export function ContrastSwitch() {
  const [enabled, setEnabled] = useState(false);

  // The layout's boot script may already have switched the palette on before
  // React ran, so the button reads the page instead of assuming it is off.
  useEffect(() => setEnabled(contrastEnabled()), []);

  return (
    <button
      type="button"
      aria-pressed={enabled}
      onClick={() => {
        setContrast(!enabled);
        setEnabled(!enabled);
      }}
      className="inline-flex min-h-10 items-center gap-2 rounded-sm border border-border-control px-3 py-1.5 text-sm aria-pressed:border-active-border aria-pressed:bg-active-bg aria-pressed:text-active-text"
    >
      {/* A half filled circle, the usual sign for contrast. Hidden from a
          screen reader, which has the words. */}
      <svg aria-hidden="true" className="size-4" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24">
        <circle cx="12" cy="12" r="9" />
        <path d="M12 3a9 9 0 0 1 0 18z" fill="currentColor" />
      </svg>
      Wysoki kontrast
    </button>
  );
}
