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
import { readJson, removeItem, writeJson } from "@/lib/local-storage";

import type { CompetitionDraft } from "./types";

const STORAGE_KEY = "ocwip:competition-wizard-draft";

export interface CompetitionWizardDraft {
  readonly draft: CompetitionDraft;
  /** ISO timestamp, so a stale draft can be shown as stale. */
  readonly savedAt: string;
  /** Set once the draft has been written to the backend at least once. */
  readonly competitionId: string | null;
}

export function loadWizardDraft(): CompetitionWizardDraft | null {
  return readJson<CompetitionWizardDraft>(STORAGE_KEY);
}

export function saveWizardDraft(
  draft: CompetitionDraft,
  competitionId: string | null,
): void {
  const entry: CompetitionWizardDraft = {
    draft,
    savedAt: new Date().toISOString(),
    competitionId,
  };

  writeJson(STORAGE_KEY, entry);
}

export function clearWizardDraft(): void {
  removeItem(STORAGE_KEY);
}
