/**
 * The applicant panel's navigation, as data.
 *
 * One list, so the header, the tests and any later mobile menu describe the
 * same panel. Paths are English (docs/konwencje.md: code is English, the
 * interface is Polish) and match what POST /login sends back as redirectPath,
 * see backend Services/LoginLandingPath.cs.
 *
 * The report asks for two more positions here one day, the organisation card
 * and the reports (docs/runbook/M1-fundament.md, T-15.2). Neither is built:
 * the first waits on decision R-01 and the second is outside the MVP, and an
 * empty route created in advance is a promise this panel cannot keep.
 */

export interface PanelLink {
  readonly href: string;
  readonly label: string;
}

export const applicantPanelRoot = "/panel/applicant";

export const applicantPanelLinks: readonly PanelLink[] = [
  { href: applicantPanelRoot, label: "Moje wnioski" },
  { href: `${applicantPanelRoot}/competitions`, label: "Aktualne konkursy" },
  { href: `${applicantPanelRoot}/profile`, label: "Mój profil" },
];

/**
 * Whether a link is the one being viewed.
 *
 * The root entry matches only itself: a prefix test would light up "Moje
 * wnioski" on every subpage, so the navigation would state the wrong place on
 * exactly the screens where a person is deepest in and needs it most.
 */
export function isCurrentLink(href: string, pathname: string): boolean {
  return href === applicantPanelRoot
    ? pathname === href
    : pathname === href || pathname.startsWith(`${href}/`);
}
