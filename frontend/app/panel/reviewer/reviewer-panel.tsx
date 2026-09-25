"use client";

import { PanelGate, type PanelSession } from "../panel-gate";
import { PanelSkeleton } from "../panel-skeleton";
import { reviewerPanelLinks } from "./navigation";
import { ReviewerHeader } from "./reviewer-header";

/**
 * The third panel (T-40): an expert sees the applications assigned to them
 * and nothing else. The gate only checks the role; what the list and each
 * application show is scoped by the assignment on the server (T-37).
 */
export function ReviewerPanel({ children }: { children: React.ReactNode }) {
  return (
    <PanelGate
      allow="Reviewer"
      skeleton={<PanelSkeleton links={reviewerPanelLinks.length} />}
      refusal={() => ({
        title: "403. Nie masz dostępu do panelu recenzenta",
        body: (
          <>
            Ten panel jest dla ekspertów oceniających wnioski. Twoje konto ma inne
            uprawnienia, więc ponowne zalogowanie tego nie zmieni.
          </>
        ),
      })}
    >
      {(session) => <ReviewerFrame session={session}>{children}</ReviewerFrame>}
    </PanelGate>
  );
}

function ReviewerFrame({ session, children }: { session: PanelSession; children: React.ReactNode }) {
  return (
    <div className="flex min-h-screen flex-col">
      <a
        href="#tresc"
        className="sr-only focus:not-sr-only focus:fixed focus:top-0 focus:left-0 focus:z-20 focus:m-2 focus:rounded-sm focus:bg-bg focus:px-3 focus:py-2"
      >
        Przejdź do treści
      </a>

      <ReviewerHeader user={session.user} onLogout={session.onLogout} loggingOut={session.loggingOut} />

      <main id="tresc" className="mx-auto w-full max-w-6xl flex-1 px-4 py-6 sm:px-6">
        {children}
      </main>
    </div>
  );
}
