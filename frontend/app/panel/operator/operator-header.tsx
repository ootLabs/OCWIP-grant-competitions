"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { accountLabel, type CurrentUser } from "@/lib/session";
import { isCurrentLink } from "../navigation";
import { operatorPanelLinks, operatorPanelRoot } from "./navigation";

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
  const pathname = usePathname();

  return (
    // Sticky, because the operator scrolls lists of over a hundred rows and
    // the marking has to still be there at the bottom of one.
    <header className="sticky top-0 z-10 border-b border-border bg-bg">
      <p className="bg-active-bg px-4 py-1.5 text-center text-sm font-semibold text-active-text sm:px-6">
        Tryb operatora. Widzisz dane wszystkich podmiotów, nie własne.
      </p>

      <div className="flex w-full flex-wrap items-center gap-3 px-4 py-3 sm:px-6">
        <Link href={operatorPanelRoot} className="flex items-center gap-2">
          {/* Same plain img as the token preview: a vector mark needs no
              optimisation, and one way of doing one thing. */}
          {/* eslint-disable-next-line @next/next/no-img-element -- vector logo, no optimisation needed */}
          <img src="/ocwip-logo.svg" alt="OCWIP" className="h-9 w-auto" />
        </Link>

        <div className="ml-auto flex items-center gap-3">
          {/* The person, not an entity: an operator account belongs to nobody's
              organisation, and naming one here would be the exact ambiguity the
              band above exists to remove. */}
          <span className="max-w-[16rem] truncate text-sm" title={accountLabel(user)}>
            Zalogowano jako {accountLabel(user)}
          </span>
          <button
            type="button"
            onClick={onLogout}
            disabled={loggingOut}
            className="rounded-sm border border-brand-accent px-3 py-1.5 text-sm text-brand-accent-text hover:bg-brand-accent hover:text-bg disabled:opacity-60"
          >
            {loggingOut ? "Wylogowywanie..." : "Wyloguj"}
          </button>
        </div>
      </div>

      <nav aria-label="Panel operatora" className="border-t border-border-muted">
        <ul className="flex w-full flex-wrap gap-1 px-2 sm:px-4">
          {operatorPanelLinks.map((link) => {
            const current = isCurrentLink(link.href, pathname, operatorPanelRoot);

            return (
              <li key={link.href}>
                <Link
                  href={link.href}
                  // The current page is announced, not only underlined: colour
                  // and a border say nothing to a screen reader, and the high
                  // contrast palette repaints both of them anyway.
                  aria-current={current ? "page" : undefined}
                  className={`block border-b-2 px-3 py-3 text-sm ${
                    current
                      ? "border-active-border font-semibold"
                      : "border-transparent"
                  }`}
                >
                  {link.label}
                </Link>
              </li>
            );
          })}
        </ul>
      </nav>
    </header>
  );
}
