import { readJson, writeJson } from "./local-storage";

/**
 * The high contrast palette for the whole application (T-46), not only the
 * token preview: `data-contrast="true"` on `<html>` reassigns every colour
 * token in app/globals.css, so every screen built from tokens switches at once.
 *
 * The choice lives in the browser, not on the account: the pages that need it
 * most (a competition, the sign in screen) are read before anybody signs in.
 */
export const CONTRAST_STORAGE_KEY = "ocwip.contrast";

export function contrastEnabled(): boolean {
  return document.documentElement.getAttribute("data-contrast") === "true";
}

export function setContrast(enabled: boolean): void {
  if (enabled) {
    document.documentElement.setAttribute("data-contrast", "true");
  } else {
    document.documentElement.removeAttribute("data-contrast");
  }
  writeJson(CONTRAST_STORAGE_KEY, enabled);
}

/** Read once on load, before React, so the palette never flashes white first. */
export function restoreContrast(): void {
  if (readJson<unknown>(CONTRAST_STORAGE_KEY) === true) {
    document.documentElement.setAttribute("data-contrast", "true");
  }
}

/**
 * restoreContrast as an inline script for the root layout: it has to run
 * before the first paint, which no component effect does. Kept next to the
 * function it mirrors, and tested against it, so the two cannot drift.
 */
export const contrastBootScript = `try{if(JSON.parse(localStorage.getItem(${JSON.stringify(
  CONTRAST_STORAGE_KEY,
)}))===true)document.documentElement.setAttribute("data-contrast","true")}catch(e){}`;
