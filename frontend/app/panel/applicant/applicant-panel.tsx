"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { usePathname, useRouter } from "next/navigation";
import {
  accountLabel,
  fetchCurrentUser,
  loginPath,
  logout,
  type CurrentUser,
} from "@/lib/session";
import { PanelHeader } from "./panel-header";

/**
 * The frame every applicant screen sits in, and the gate in front of it.
 *
 * The gate is on the client and asks the backend, which is not a shortcut. The
 * session cookie is HttpOnly and belongs to the API origin, so neither this
 * code nor a Next.js middleware can read it, and reading it would answer the
 * wrong question anyway: a logout elsewhere rotates the security stamp
 * (docs/architektura.md), so a cookie that is still in the jar can already be
 * worthless. GET /me is the only thing that knows.
 */

type Gate =
  | { readonly status: "checking" }
  | { readonly status: "allowed"; readonly user: CurrentUser }
  | { readonly status: "refused"; readonly user: CurrentUser }
  | { readonly status: "unavailable" };

export function ApplicantPanel({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const pathname = usePathname();
  const [gate, setGate] = useState<Gate>({ status: "checking" });
  const [loggingOut, setLoggingOut] = useState(false);

  // Held in a ref rather than listed as a dependency. The router object is not
  // promised to keep its identity between renders, and a changed identity here
  // would re-ask GET /me on every render rather than once per screen.
  const routerRef = useRef(router);
  routerRef.current = router;

  useEffect(() => {
    let current = true;

    fetchCurrentUser()
      .then((user) => {
        if (!current) {
          return;
        }

        if (user === null) {
          // Where they wanted to go, so signing in finishes the journey instead
          // of dropping them on a landing page. The backend refuses anything
          // that is not a single local path (Services/LoginLandingPath.cs), so
          // this is a request, not an instruction.
          routerRef.current.replace(
            `${loginPath}?returnUrl=${encodeURIComponent(pathname)}`,
          );
          return;
        }

        setGate(
          user.role === "Applicant"
            ? { status: "allowed", user }
            : { status: "refused", user },
        );
      })
      .catch(() => {
        // A backend that is down is not an expired session. Sending an operator
        // to the login screen because the API blinked would log them out of a
        // session that is perfectly alive.
        if (current) {
          setGate({ status: "unavailable" });
        }
      });

    return () => {
      current = false;
    };
    // Once per screen: a session can end while somebody reads one page, so
    // every navigation inside the panel asks again.
  }, [pathname]);

  const onLogout = useCallback(async () => {
    setLoggingOut(true);
    await logout();
    routerRef.current.replace(loginPath);
  }, []);

  if (gate.status === "checking") {
    return <PanelNotice title="Sprawdzamy sesję..." busy />;
  }

  if (gate.status === "unavailable") {
    return (
      <PanelNotice title="Nie możemy teraz połączyć się z systemem">
        Spróbuj odświeżyć stronę za chwilę. Twoje dane są bezpieczne.
      </PanelNotice>
    );
  }

  if (gate.status === "refused") {
    // Not a redirect to the login screen: this session is valid, it simply is
    // not an applicant's. Sending an operator to log in again would suggest
    // that logging in once more would help, and it would not.
    return (
      <PanelNotice title="Ten panel jest dla wnioskodawców">
        Konto {accountLabel(gate.user)} nie składa wniosków, więc nie ma tu nic
        do zobaczenia.
      </PanelNotice>
    );
  }

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
        user={gate.user}
        onLogout={onLogout}
        loggingOut={loggingOut}
      />

      <main id="tresc" className="mx-auto w-full max-w-6xl flex-1 px-4 py-6 sm:px-6">
        {children}
      </main>
    </div>
  );
}

/**
 * The whole screen replaced by one sentence.
 *
 * Deliberately not the panel frame with a message inside it: the frame names
 * the signed in entity and offers navigation, and neither is true while we do
 * not know who is here or are telling somebody they do not belong here.
 */
function PanelNotice({
  title,
  children,
  busy = false,
}: {
  title: string;
  children?: React.ReactNode;
  busy?: boolean;
}) {
  return (
    <div className="flex min-h-screen items-center justify-center px-4">
      <div
        className="max-w-md text-center"
        // Announced, because the state changes after the page has rendered and
        // a screen reader would otherwise never hear about it.
        aria-live="polite"
        aria-busy={busy || undefined}
      >
        <h1 className="text-2xl">{title}</h1>
        {children ? <p className="mt-3 text-sm">{children}</p> : null}
      </div>
    </div>
  );
}
