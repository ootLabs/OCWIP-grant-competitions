"use client";

import { accountLabel, type CurrentUser } from "@/lib/session";
import { PanelAccount, PanelBrand, PanelNav } from "../panel-header-parts";
import { reviewerPanelLinks, reviewerPanelRoot } from "./navigation";

/** Logo, who is signed in, the contrast switch, the way out, the navigation. */
export function ReviewerHeader({
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
      <div className="mx-auto flex w-full max-w-6xl flex-wrap items-center gap-3 px-4 pt-3 sm:px-6">
        <PanelBrand href={reviewerPanelRoot} name="Panel recenzenta" />
        <PanelAccount
          label={`Zalogowano jako ${accountLabel(user)}`}
          name={accountLabel(user)}
          onLogout={onLogout}
          loggingOut={loggingOut}
        />
      </div>

      <PanelNav
        label="Panel recenzenta"
        links={reviewerPanelLinks}
        root={reviewerPanelRoot}
        rowClassName="mx-auto max-w-6xl"
      />
    </header>
  );
}
