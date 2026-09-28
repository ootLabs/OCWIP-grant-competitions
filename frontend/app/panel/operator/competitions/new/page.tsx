"use client";

import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";

import { newDraft } from "@/lib/competition-wizard/draft-factory";
import { clearWizardDraft, loadWizardDraft } from "@/lib/competition-wizard/draft-storage";

import { CompetitionWizard, editPath } from "./competition-wizard";

/**
 * A competition not saved yet (T-22). Krok 0 (kopia z poprzedniego roku)
 * świadomie nie tu, patrz T-98. Once the first save succeeds the wizard
 * moves to the saved competition's address (T-97), so from then on it opens
 * from the server.
 */
export default function NewCompetitionPage() {
  const router = useRouter();
  const [ready, setReady] = useState(false);

  useEffect(() => {
    // A buffer from before T-97 may name a competition already saved: that
    // one is edited from the server now, not from this slot.
    const stored = loadWizardDraft(null);
    if (stored?.competitionId) {
      clearWizardDraft(null);
      router.replace(editPath(stored.competitionId));
      return;
    }

    setReady(true);
  }, [router]);

  return ready ? <CompetitionWizard initialDraft={newDraft()} initialCompetition={null} /> : null;
}
