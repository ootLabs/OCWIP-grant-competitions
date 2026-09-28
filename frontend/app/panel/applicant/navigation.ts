/**
 * The applicant panel's navigation, as data.
 *
 * One list, so the header, the tests and any later mobile menu describe the
 * same panel. The shape and the "which one am I on" rule are shared with the
 * operator panel (../navigation.ts).
 *
 * The organisation card the report asks for lives under "Mój profil"
 * (T-93). The reports have no position of their own: each is reached from
 * its application.
 */

import type { PanelLink } from "../navigation";

export const applicantPanelRoot = "/panel/applicant";

export const applicantPanelLinks: readonly PanelLink[] = [
  { href: applicantPanelRoot, label: "Moje wnioski" },
  { href: `${applicantPanelRoot}/competitions`, label: "Aktualne konkursy" },
  { href: `${applicantPanelRoot}/profile`, label: "Mój profil" },
  // Placeholder screen, T-122x: real change of password/e-mail is T-106.
  { href: `${applicantPanelRoot}/account`, label: "Moje konto" },
];
