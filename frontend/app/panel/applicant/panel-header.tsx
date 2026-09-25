"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { ContrastSwitch } from "@/components/contrast-switch";
import { accountLabel, type CurrentUser } from "@/lib/session";
import { isCurrentLink } from "../navigation";
import { applicantPanelLinks, applicantPanelRoot } from "./navigation";

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
  const pathname = usePathname();

  return (
    <header className="border-b border-border bg-bg">
      <div className="mx-auto flex w-full max-w-6xl flex-wrap items-center gap-3 px-4 py-3 sm:px-6">
        <Link href={applicantPanelRoot} className="flex items-center gap-2">
          {/* Same plain img as the token preview: a vector mark needs no
              optimisation, and one way of doing one thing. */}
          {/* eslint-disable-next-line @next/next/no-img-element -- vector logo, no optimisation needed */}
          <img src="/ocwip-logo.svg" alt="OCWIP" className="h-9 w-auto" />
        </Link>

        <div className="ml-auto flex items-center gap-3">
          {/* The applicant acts as an organisation, so the organisation is the
              name that has to be on screen: several people may share one
              account today, and the question "whose data am I looking at" has
              to have an answer without clicking anything. */}
          <span className="max-w-[16rem] truncate text-sm" title={accountLabel(user)}>
            {accountLabel(user)}
          </span>
          <ContrastSwitch />
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

      <nav aria-label="Panel wnioskodawcy" className="border-t border-border-muted">
        <ul className="mx-auto flex w-full max-w-6xl flex-wrap gap-1 px-2 sm:px-4">
          {applicantPanelLinks.map((link) => {
            const current = isCurrentLink(link.href, pathname, applicantPanelRoot);

            return (
              <li key={link.href}>
                <Link
                  href={link.href}
                  // The current page is announced, not only underlined: colour
                  // and a border say nothing to a screen reader, and the high
                  // contrast palette repaints both of them anyway.
                  aria-current={current ? "page" : undefined}
                  className={`block border-b-2 px-3 py-3 text-sm no-underline ${
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
