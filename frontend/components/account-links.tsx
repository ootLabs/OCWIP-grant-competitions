"use client";

import Link from "next/link";
import { useEffect, useState } from "react";

import { panelRootForRole } from "@/app/panel/navigation";
import { compactActionClassName, compactPrimaryActionClassName } from "@/components/ui/styles";
import { registerPath } from "@/lib/login";
import { fetchCurrentUser, loginPath, type CurrentUser } from "@/lib/session";

/**
 * The way in from every public page (T-99): "Zaloguj" and "Załóż konto" for a
 * visitor, "Mój panel" once signed in. Asked of GET /me here, in the browser,
 * because the session cookie is only there; the public pages themselves are
 * rendered on the server without one (D6).
 *
 * The visitor's links are shown until the answer arrives, not a blank space:
 * most readers of a public page have no session, and a link that turns into
 * "Mój panel" a moment later costs less than a header that jumps.
 */
export function AccountLinks() {
  const [user, setUser] = useState<CurrentUser | null>(null);

  useEffect(() => {
    let current = true;
    fetchCurrentUser()
      .then((found) => current && setUser(found))
      // A backend blip reads as nobody signed in: the sign in link still works.
      .catch(() => undefined);
    return () => {
      current = false;
    };
  }, []);

  const panel = user ? panelRootForRole(user.role) : null;

  if (panel) {
    return (
      <Link className={compactActionClassName} href={panel}>
        Mój panel
      </Link>
    );
  }

  return (
    <nav aria-label="Konto" className="flex items-center gap-2 text-sm">
      <Link className={compactActionClassName} href={loginPath}>
        Zaloguj
      </Link>
      <Link className={compactPrimaryActionClassName} href={registerPath}>
        Załóż konto
      </Link>
    </nav>
  );
}
