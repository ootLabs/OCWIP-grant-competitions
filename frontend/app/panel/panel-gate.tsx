"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { usePathname, useRouter } from "next/navigation";
import {
  fetchCurrentUser,
  loginPath,
  logout,
  type CurrentUser,
  type Role,
} from "@/lib/session";
import { PanelNotice } from "./panel-notice";

/**
 * Who is allowed into a panel, asked before that panel renders anything.
 *
 * Shared by both panels rather than copied into each, because the subtle parts
 * of this are the same for every panel and were not obvious the first time: the
 * difference between "no session" and "backend down", resetting to "checking"
 * before every ask, and reading the return address off the browser rather than
 * off usePathname. A second copy would be a second place to get them wrong.
 *
 * The gate is on the client and asks the backend, which is not a shortcut. The
 * session cookie is HttpOnly and belongs to the API origin, so neither this
 * code nor a Next.js middleware can read it, and reading it would answer the
 * wrong question anyway: a logout elsewhere rotates the security stamp
 * (docs/architektura.md), so a cookie that is still in the jar can already be
 * worthless. GET /me is the only thing that knows.
 *
 * This is not an access control. Access control is the backend's default deny
 * policy (T-13.2), which answers 401 and 403 no matter what this component
 * decides. What happens here is the panel declining to draw a frame its caller
 * has no use for.
 */

/** What a panel frame gets once the gate has let somebody through. */
export interface PanelSession {
  readonly user: CurrentUser;
  readonly onLogout: () => void;
  readonly loggingOut: boolean;
}

/** The refusal shown to a live session that belongs to another role. */
export interface PanelRefusal {
  readonly title: string;
  readonly body: React.ReactNode;
}

type Gate =
  | { readonly status: "checking" }
  | { readonly status: "allowed"; readonly user: CurrentUser }
  | { readonly status: "refused"; readonly user: CurrentUser }
  | { readonly status: "unavailable" };

export function PanelGate({
  allow,
  refusal,
  children,
}: {
  allow: Role;
  refusal: (user: CurrentUser) => PanelRefusal;
  children: (session: PanelSession) => React.ReactNode;
}) {
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

    // Back to "checking" before every ask, not only on the first one. A session
    // that ends while somebody is reading would otherwise leave the previous
    // answer, and therefore the frame naming their account, on screen for the
    // whole time the redirect takes.
    setGate({ status: "checking" });

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
            `${loginPath}?returnUrl=${encodeURIComponent(currentUrl())}`,
          );
          return;
        }

        // One comparison against the allowed role, not a list of refused ones.
        // A role added to the enum later has to land on a refusal by default,
        // the same way the backend's authorization handler only ever succeeds.
        setGate(
          user.role === allow
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
  }, [pathname, allow]);

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
    // not the right role's. Sending somebody to log in again would suggest that
    // logging in once more would help, and it would not.
    const refused = refusal(gate.user);

    return <PanelNotice title={refused.title}>{refused.body}</PanelNotice>;
  }

  return children({ user: gate.user, onLogout, loggingOut });
}

/**
 * The address to come back to after signing in, query string included.
 *
 * Read off the browser rather than from usePathname, which drops everything
 * after the "?": a competition opened from a link in an e-mail carries its
 * parameters there, and losing them turns "back where you were" into "back to
 * roughly where you were". The backend refuses anything that is not a single
 * local path, so the worst this can produce is the role's own panel
 * (Services/LoginLandingPath.cs).
 */
function currentUrl(): string {
  return `${window.location.pathname}${window.location.search}`;
}
