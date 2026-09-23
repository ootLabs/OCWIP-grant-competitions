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
import { readJson, removeItem, writeJson } from "@/lib/local-storage";

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

export function loadDraft(competitionId: string): FormDraft | null {
  return readJson<FormDraft>(storageKey(competitionId));
}

export function saveDraft(
  competitionId: string,
  document: FormDocument,
  copiedFromCompetitionId: string | null,
): void {
  const draft: FormDraft = {
    document,
    savedAt: new Date().toISOString(),
    copiedFromCompetitionId,
  };

  writeJson(storageKey(competitionId), draft);
}

export function clearDraft(competitionId: string): void {
  removeItem(storageKey(competitionId));
}
