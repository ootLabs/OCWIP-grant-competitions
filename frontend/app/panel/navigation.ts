/**
 * What both panels' navigations are made of.
 *
 * Paths are English (docs/konwencje.md: code is English, the interface is
 * Polish) and their roots match what POST /login sends back as redirectPath,
 * see the backend's Services/LoginLandingPath.cs. The link lists themselves
 * stay with their panels, because they are that panel's scope; only the shape
 * and the "which one am I on" rule are shared.
 */

import type { Role } from "@/lib/session";
import { applicantPanelRoot } from "./applicant/navigation";
import { operatorPanelRoot } from "./operator/navigation";
import { reviewerPanelRoot } from "./reviewer/navigation";

export interface PanelLink {
  readonly href: string;
  readonly label: string;
}

/**
 * Whether a link is the one being viewed.
 *
 * The root entry matches only itself: a prefix test would light up the first
 * position on every subpage, so the navigation would state the wrong place on
 * exactly the screens where a person is deepest in and needs it most.
 */
export function isCurrentLink(
  href: string,
  pathname: string,
  root: string,
): boolean {
  return href === root
    ? pathname === href
    : pathname === href || pathname.startsWith(`${href}/`);
}

/**
 * Where a signed in account belongs, when that panel is built.
 *
 * The front's mirror of the backend's Services/LoginLandingPath.cs, and it
 * exists for one screen: the refusal a live session sees when it opens the
 * wrong panel. Without it that screen is a dead end, because the session is
 * valid, so there is nothing to redirect to and nothing to log out of.
 *
 * Deliberately partial. The reviewer panel is T-40 and is blocked, and the
 * login screen has no card at all (R-25), so a role with no entry here gets no
 * link rather than a link to a 404. A promise that lands on a missing page is
 * worse than no promise.
 */
export function panelRootForRole(role: Role): string | null {
  switch (role) {
    case "Applicant":
      return applicantPanelRoot;
    case "Operator":
      return operatorPanelRoot;
    case "Reviewer":
      return reviewerPanelRoot;
    default:
      return null;
  }
}
