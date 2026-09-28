/**
 * Unsaved changes of the competition wizard, kept in the browser (T-22),
 * and since T-97 only as a buffer: the competition itself lives on the
 * server and the wizard opens it from there, in any browser.
 *
 * Two kinds of slot:
 *
 * - "new": the one competition not saved even once. Before the first save
 *   there is no id to key by, and the backend refuses a competition without
 *   number, title, dates and maximum grant (T-22), so this is the only copy.
 * - one per saved competition, holding changes typed since the last save,
 *   with the `updatedAt` they were made on top of. They are offered back
 *   only when the server still holds that same version: restoring them over
 *   somebody else's later save would quietly undo it.
 */
import { readJson, removeItem, writeJson } from "@/lib/local-storage";

import type { CompetitionDraft } from "./types";

const STORAGE_KEY = "ocwip:competition-wizard-draft";

export interface CompetitionWizardDraft {
  readonly draft: CompetitionDraft;
  /** ISO timestamp, so a stale draft can be shown as stale. */
  readonly savedAt: string;
  /** Null in the "new" slot. */
  readonly competitionId: string | null;
  /** The server's `updatedAt` the changes were typed on top of. */
  readonly baseUpdatedAt: string | null;
}

function keyFor(competitionId: string | null): string {
  return competitionId === null ? STORAGE_KEY : `${STORAGE_KEY}:${competitionId}`;
}

export function loadWizardDraft(competitionId: string | null = null): CompetitionWizardDraft | null {
  return readJson<CompetitionWizardDraft>(keyFor(competitionId));
}

export function saveWizardDraft(
  draft: CompetitionDraft,
  competitionId: string | null,
  baseUpdatedAt: string | null = null,
): void {
  const entry: CompetitionWizardDraft = {
    draft,
    savedAt: new Date().toISOString(),
    competitionId,
    baseUpdatedAt,
  };

  writeJson(keyFor(competitionId), entry);
}

export function clearWizardDraft(competitionId: string | null = null): void {
  removeItem(keyFor(competitionId));
}
