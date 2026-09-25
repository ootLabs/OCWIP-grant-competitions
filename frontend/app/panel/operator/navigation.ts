/**
 * The operator panel's navigation, as data.
 *
 * Four positions from the system the operator uses today (card T-15.3), and
 * "Ocena" added in T-41 for the evaluation and the ranking list. The shape and
 * the "which one am I on" rule are shared with the applicant panel
 * (../navigation.ts).
 *
 * Inside a single competition the report asks for eight tabs of its own
 * (docs/runbook/proces.md, step 4.1). They are not here and not built: they
 * need a competition detail route, which does not exist yet, and the text on a
 * greyed out tab depends on the competition states that are still an open
 * discrepancy. That level belongs to T-22, see docs/runbook/rozbieznosci.md.
 */

import type { PanelLink } from "../navigation";

export const operatorPanelRoot = "/panel/operator";

export const operatorPanelLinks: readonly PanelLink[] = [
  { href: operatorPanelRoot, label: "Konkursy" },
  { href: `${operatorPanelRoot}/applications`, label: "Wnioski" },
  { href: `${operatorPanelRoot}/evaluation`, label: "Ocena" },
  { href: `${operatorPanelRoot}/forms`, label: "Formularze" },
  { href: `${operatorPanelRoot}/reviewers`, label: "Recenzenci" },
];
