/**
 * What both panels' navigations are made of.
 *
 * Paths are English (docs/konwencje.md: code is English, the interface is
 * Polish) and their roots match what POST /login sends back as redirectPath,
 * see the backend's Services/LoginLandingPath.cs. The link lists themselves
 * stay with their panels, because they are that panel's scope; only the shape
 * and the "which one am I on" rule are shared.
 */

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
