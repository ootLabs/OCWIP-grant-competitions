/**
 * The applicant panel's navigation, as data.
 *
 * One list, so the header, the tests and any later mobile menu describe the
 * same panel. The shape and the "which one am I on" rule are shared with the
 * operator panel (../navigation.ts).
 *
 * The report asks for two more positions here one day, the organisation card
 * and the reports (docs/runbook/M1-fundament.md, T-15.2). Neither is built:
 * the first waits on decision R-01 and the second is outside the MVP, and an
 * empty route created in advance is a promise this panel cannot keep.
 */

import type { PanelLink } from "../navigation";

export const applicantPanelRoot = "/panel/applicant";

export const applicantPanelLinks: readonly PanelLink[] = [
  { href: applicantPanelRoot, label: "Moje wnioski" },
  { href: `${applicantPanelRoot}/competitions`, label: "Aktualne konkursy" },
  { href: `${applicantPanelRoot}/profile`, label: "Mój profil" },
];
