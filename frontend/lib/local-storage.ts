/**
 * The one place that reads and writes `localStorage`, shared by every
 * feature that keeps a draft in the browser (T-26's form creator, T-22's
 * competition wizard).
 *
 * Every function degrades to "nothing survives a reload" instead of
 * throwing: `localStorage` throws in a private window with blocked site
 * data and is simply absent during server side rendering, and neither of
 * those is a crash a draft feature should surface to the operator.
 */

function storageAvailable(): boolean {
  return typeof window !== "undefined" && typeof window.localStorage !== "undefined";
}

export function readJson<T>(key: string): T | null {
  if (!storageAvailable()) {
    return null;
  }

  try {
    const raw = window.localStorage.getItem(key);
    return raw === null ? null : (JSON.parse(raw) as T);
  } catch {
    return null;
  }
}

export function writeJson(key: string, value: unknown): void {
  if (!storageAvailable()) {
    return;
  }

  try {
    window.localStorage.setItem(key, JSON.stringify(value));
  } catch {
    // A full or blocked store loses autosave, not the current screen.
  }
}

export function removeItem(key: string): void {
  if (!storageAvailable()) {
    return;
  }

  try {
    window.localStorage.removeItem(key);
  } catch {
    // Nothing to recover from here either.
  }
}
