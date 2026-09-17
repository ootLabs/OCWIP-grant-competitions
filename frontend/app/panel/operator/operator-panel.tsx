"use client";

import { PanelGate, type PanelSession } from "../panel-gate";
import { OperatorHeader } from "./operator-header";

/**
 * The frame every operator screen sits in.
 *
 * Who is let in is the shared gate's job (../panel-gate.tsx). What is left
 * here is this panel's own shape, and it differs from the applicant's in two
 * ways that both come from the same fact: this panel is where lists of over a
 * hundred rows are read (card T-15.3).
 */
export function OperatorPanel({ children }: { children: React.ReactNode }) {
  return (
    <PanelGate
      allow="Operator"
      refusal={() => ({
        // The card asks for a 403, and this is where a person can see one. The
        // real 403 is the backend's: every route is refused unless a rule lets
        // it through (T-13.2), so nothing here is what keeps an applicant out
        // of the operator's data. This screen only explains the refusal, and
        // must never become the thing anybody relies on.
        title: "403. Nie masz dostępu do panelu operatora",
        body: (
          <>
            Ten panel jest dla pracowników OCWIP. Twoje konto ma inne
            uprawnienia, więc ponowne zalogowanie tego nie zmieni.
          </>
        ),
      })}
    >
      {(session) => <OperatorFrame session={session}>{children}</OperatorFrame>}
    </PanelGate>
  );
}

function OperatorFrame({
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
        className="sr-only focus:not-sr-only focus:fixed focus:top-0 focus:left-0 focus:z-20 focus:m-2 focus:rounded-sm focus:bg-bg focus:px-3 focus:py-2"
      >
        Przejdź do treści
      </a>

      <OperatorHeader
        user={session.user}
        onLogout={session.onLogout}
        loggingOut={session.loggingOut}
      />

      {/*
        No max-w-6xl cap, unlike the applicant panel: a competition brings in
        around 120 offers and the screen here is a table, not a form, so the
        width available is the width used.

        overflow-x-auto is what keeps that table from breaking the page. Without
        it a table wider than the viewport widens the document, and the sticky
        header, mode marking included, is sticky against the viewport rather
        than the document, so it slides off to the left exactly when somebody is
        scrolled into a hundred rows of other people's data. With it the table
        scrolls inside its own region and the frame stays put.
      */}
      <main id="tresc" className="w-full flex-1 overflow-x-auto px-4 py-6 sm:px-6">
        {children}
      </main>
    </div>
  );
}
