"use client";

import { accountLabel, type CurrentUser } from "@/lib/session";
import { PanelAccount, PanelBrand, PanelNav } from "../panel-header-parts";
import { applicantPanelLinks, applicantPanelRoot } from "./navigation";
import { panelRowClassName } from "@/components/ui/styles";

/**
 * Logo, who you are signed in as, the way out, and the navigation.
 *
 * The order of the elements in the DOM is the order a keyboard walks them, so
 * it is written to be walked: identity first, then navigation, then the
 * content the skip link jumps to. Nothing here is positioned into a different
 * order visually, because that would split what the eye sees from what the
 * Tab key does.
 */
export function PanelHeader({
  user,
  onLogout,
  loggingOut,
}: {
  user: CurrentUser;
  onLogout: () => void;
  loggingOut: boolean;
}) {
  return (
    <header className="border-b border-border bg-bg">
      <div className={`${panelRowClassName} flex flex-wrap items-center gap-3 px-4 pt-3 sm:px-6`}>
        <PanelBrand href={applicantPanelRoot} name="Panel wnioskodawcy" />

        {/* The applicant acts as an organisation, so the organisation is the
            name that has to be on screen: several people may share one
            account today, and the question "whose data am I looking at" has
            to have an answer without clicking anything. */}
        <PanelAccount
          label={accountLabel(user)}
          name={accountLabel(user)}
          onLogout={onLogout}
          loggingOut={loggingOut}
        />
      </div>

      <PanelNav
        label="Panel wnioskodawcy"
        links={applicantPanelLinks}
        root={applicantPanelRoot}
        rowClassName={panelRowClassName}
      />
    </header>
  );
}
