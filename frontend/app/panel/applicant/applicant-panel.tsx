"use client";

import { accountLabel } from "@/lib/session";
import { PanelGate, type PanelSession } from "../panel-gate";
import { PanelSkeleton } from "../panel-skeleton";
import { applicantPanelLinks } from "./navigation";
import { PanelHeader } from "./panel-header";

/**
 * The frame every applicant screen sits in.
 *
 * Who is let in and what happens to everybody else is the shared gate's job
 * (../panel-gate.tsx). What is left here is this panel's own shape: the width
 * of a form rather than of a table, and a header that names the entity.
 */
export function ApplicantPanel({ children }: { children: React.ReactNode }) {
  return (
    <PanelGate
      allow="Applicant"
      // The same row width and the same number of links as the frame below,
      // taken from the same places the frame takes them.
      skeleton={
        <PanelSkeleton
          links={applicantPanelLinks.length}
          rowClassName="mx-auto max-w-6xl"
        />
      }
      refusal={(user) => ({
        title: "Ten panel jest dla wnioskodawców",
        body: (
          <>
            Konto {accountLabel(user)} nie składa wniosków, więc nie ma tu nic
            do zobaczenia.
          </>
        ),
      })}
    >
      {(session) => <ApplicantFrame session={session}>{children}</ApplicantFrame>}
    </PanelGate>
  );
}

function ApplicantFrame({
  session,
  children,
}: {
  session: PanelSession;
  children: React.ReactNode;
}) {
  return (
    <div className="flex min-h-screen flex-col">
      {/* First thing the keyboard reaches on every screen. Without it every
          subpage begins with tabbing through the whole navigation again. */}
      <a
        href="#tresc"
        className="sr-only focus:not-sr-only focus:absolute focus:z-10 focus:m-2 focus:rounded-sm focus:bg-bg focus:px-3 focus:py-2"
      >
        Przejdź do treści
      </a>

      <PanelHeader
        user={session.user}
        onLogout={session.onLogout}
        loggingOut={session.loggingOut}
      />

      <main id="tresc" className="mx-auto w-full max-w-6xl flex-1 px-4 py-6 sm:px-6">
        {children}
      </main>
    </div>
  );
}
