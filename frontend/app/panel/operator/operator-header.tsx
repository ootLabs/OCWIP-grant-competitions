"use client";

import { accountLabel, type CurrentUser } from "@/lib/session";
import { PanelAccount, PanelBrand, PanelNav } from "../panel-header-parts";
import { operatorPanelLinks, operatorPanelRoot } from "./navigation";
import { operatorRowClassName } from "@/components/ui/styles";

/**
 * The mode band, who is signed in, the way out, and the navigation.
 *
 * The band is the first thing in the header and the first thing in the DOM
 * after the skip link, so it is also the first thing read aloud. The operator
 * looks at other people's personal data all day, often with somebody from
 * outside the organisation watching the same screen, so "whose view is this"
 * must never need a click to answer (card T-15.3).
 *
 * It is painted with --color-active-bg and --color-active-text rather than the
 * brand accent on purpose: those are the two tokens the high contrast palette
 * reassigns (app/globals.css), so the marking survives that mode instead of
 * quietly turning into an orange strip on black.
 */
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
    // the marking has to still be there at the bottom of one.
    <header className="sticky top-0 z-10 border-b border-border bg-bg">
      {/* The band itself spans the window, the way a banner does; its sentence
          is centred inside, so only the row below it needs the cap. */}
      <p className="bg-active-bg px-4 py-1.5 text-center text-sm font-semibold text-active-text sm:px-6">
        Tryb operatora. Widzisz dane wszystkich podmiotów, nie własne.
      </p>

      <div className={`${operatorRowClassName} flex flex-wrap items-center gap-3 px-4 pt-3 sm:px-6`}>
        <PanelBrand href={operatorPanelRoot} name="Panel operatora" />

        {/* The person, not an entity: an operator account belongs to nobody's
            organisation, and naming one here would be the exact ambiguity the
            band above exists to remove. */}
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
