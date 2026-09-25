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
      className="rounded-sm border border-border-control px-3 py-1.5 text-sm"
    >
      Wysoki kontrast
    </button>
  );
}
