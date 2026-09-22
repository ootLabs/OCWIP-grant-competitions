/**
 * The creator's work in progress, kept in the browser (T-26).
 *
 * There is no backend endpoint for a draft form definition: T-24 and T-25
 * deliberately shipped publish and read only, nothing that would let a
 * half-built document sit in the database next to real, versioned ones. So
 * "survives closing the browser" (the card's own acceptance criterion) is
 * solved here, in localStorage, one draft per competition, and never sent
 * anywhere until an operator publishes through T-27.
 */
import type { FormDocument } from "./document-types";

const PREFIX = "ocwip:form-draft:";

function storageKey(competitionId: string): string {
  return `${PREFIX}${competitionId}`;
}

export interface FormDraft {
  readonly document: FormDocument;
  /** ISO timestamp, shown to the operator so a stale draft reads as stale. */
  readonly savedAt: string;
  /** The competition a copy started from, for the "skopiowano z" note. */
  readonly copiedFromCompetitionId: string | null;
}

/**
 * localStorage throws in a private window with blocked site data, and is
 * simply absent during server side rendering. Both are normal, not errors:
 * the draft feature degrades to "nothing survives a reload", which is worse
 * than the criterion but not a crash.
 */
function storageAvailable(): boolean {
  return typeof window !== "undefined" && typeof window.localStorage !== "undefined";
}

export function loadDraft(competitionId: string): FormDraft | null {
  if (!storageAvailable()) {
    return null;
  }

  try {
    const raw = window.localStorage.getItem(storageKey(competitionId));
    return raw === null ? null : (JSON.parse(raw) as FormDraft);
  } catch {
    return null;
  }
}

export function saveDraft(
  competitionId: string,
  document: FormDocument,
  copiedFromCompetitionId: string | null,
): void {
  if (!storageAvailable()) {
    return;
  }

  const draft: FormDraft = {
    document,
    savedAt: new Date().toISOString(),
    copiedFromCompetitionId,
  };

  try {
    window.localStorage.setItem(storageKey(competitionId), JSON.stringify(draft));
  } catch {
    // A full or blocked store loses autosave, not the current screen: the
    // operator keeps editing, they just cannot rely on it surviving a reload.
  }
}

export function clearDraft(competitionId: string): void {
  if (!storageAvailable()) {
    return;
  }

  try {
    window.localStorage.removeItem(storageKey(competitionId));
  } catch {
    // Nothing to recover from here either.
  }
}
