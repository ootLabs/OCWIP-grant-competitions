/**
 * The operator's work in progress on a new competition, kept in the browser
 * (T-22), the same way the form creator keeps its own draft (T-26).
 *
 * One slot, not one draft per competition: unlike a form, which is always
 * attached to an existing competition, this draft exists BEFORE a
 * competition does. `competitionId` starts null and is filled in the moment
 * the first save succeeds (see lib/operator-competitions.ts), after which
 * further saves are edits of that row rather than a second create.
 */
import type { CompetitionDraft } from "./types";

const STORAGE_KEY = "ocwip:competition-wizard-draft";

export interface CompetitionWizardDraft {
  readonly draft: CompetitionDraft;
  /** ISO timestamp, so a stale draft can be shown as stale. */
  readonly savedAt: string;
  /** Set once the draft has been written to the backend at least once. */
  readonly competitionId: string | null;
}

/**
 * localStorage throws in a private window with blocked site data, and is
 * absent during server side rendering. Both are normal: the draft feature
 * degrades to "nothing survives a reload", not a crash.
 */
function storageAvailable(): boolean {
  return typeof window !== "undefined" && typeof window.localStorage !== "undefined";
}

export function loadWizardDraft(): CompetitionWizardDraft | null {
  if (!storageAvailable()) {
    return null;
  }

  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    return raw === null ? null : (JSON.parse(raw) as CompetitionWizardDraft);
  } catch {
    return null;
  }
}

export function saveWizardDraft(
  draft: CompetitionDraft,
  competitionId: string | null,
): void {
  if (!storageAvailable()) {
    return;
  }

  const entry: CompetitionWizardDraft = {
    draft,
    savedAt: new Date().toISOString(),
    competitionId,
  };

  try {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(entry));
  } catch {
    // A full or blocked store loses autosave, not the current screen.
  }
}

export function clearWizardDraft(): void {
  if (!storageAvailable()) {
    return;
  }

  try {
    window.localStorage.removeItem(STORAGE_KEY);
  } catch {
    // Nothing to recover from here either.
  }
}
