import type { PanelLink } from "../navigation";

/** Where LoginLandingPath.cs sends an account with the Reviewer role. */
export const reviewerPanelRoot = "/panel/reviewer";

export const reviewerPanelLinks: readonly PanelLink[] = [
  { href: reviewerPanelRoot, label: "Wnioski do oceny" },
];
