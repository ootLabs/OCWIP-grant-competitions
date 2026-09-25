"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { ContrastSwitch } from "@/components/contrast-switch";
import { accountLabel, type CurrentUser } from "@/lib/session";
import { isCurrentLink } from "../navigation";
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
  const pathname = usePathname();

  return (
    <header className="border-b border-border bg-bg">
      <div className="mx-auto flex w-full max-w-6xl flex-wrap items-center gap-3 px-4 py-3 sm:px-6">
        <Link href={reviewerPanelRoot} className="flex items-center gap-2">
          {/* eslint-disable-next-line @next/next/no-img-element -- vector logo, no optimisation needed */}
          <img src="/ocwip-logo.svg" alt="OCWIP" className="h-9 w-auto" />
        </Link>

        <div className="ml-auto flex items-center gap-3">
          <span className="max-w-[16rem] truncate text-sm" title={accountLabel(user)}>
            Zalogowano jako {accountLabel(user)}
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

      <nav aria-label="Panel recenzenta" className="border-t border-border-muted">
        <ul className="mx-auto flex w-full max-w-6xl flex-wrap gap-1 px-2 sm:px-4">
          {reviewerPanelLinks.map((link) => {
            const current = isCurrentLink(link.href, pathname, reviewerPanelRoot);

            return (
              <li key={link.href}>
                <Link
                  href={link.href}
                  aria-current={current ? "page" : undefined}
                  className={`block border-b-2 px-3 py-3 text-sm no-underline ${
                    current ? "border-active-border font-semibold" : "border-transparent"
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
