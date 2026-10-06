"use client";

import { accountLabel, type CurrentUser } from "@/lib/session";
import { PanelAccount, PanelBrand, PanelNav } from "../panel-header-parts";
import { operatorPanelLinks, operatorPanelRoot } from "./navigation";
import { operatorRowClassName } from "@/components/ui/styles";

/** Who is signed in, the way out, and the navigation (card T-15.3). */
export function OperatorHeader({
  user,
  onLogout,
  loggingOut,
}: {
  user: CurrentUser;
  onLogout: () => void;
  loggingOut: boolean;
}) {
  return (
    // Sticky, because the operator scrolls lists of over a hundred rows and
    // the navigation has to still be there at the bottom of one.
    <header className="sticky top-0 z-10 border-b border-border bg-bg">
      <div className={`${operatorRowClassName} flex flex-wrap items-center gap-3 px-4 pt-3 sm:px-6`}>
        <PanelBrand href={operatorPanelRoot} name="Panel operatora" />

        {/* The person, not an entity: an operator account belongs to nobody's
            organisation. */}
        <PanelAccount
          label={`Zalogowano jako ${accountLabel(user)}`}
          name={accountLabel(user)}
          onLogout={onLogout}
          loggingOut={loggingOut}
        />
      </div>

      <PanelNav
        label="Panel operatora"
        links={operatorPanelLinks}
        root={operatorPanelRoot}
        rowClassName={operatorRowClassName}
      />
    </header>
  );
}
