"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";

import { BrandLogo } from "@/components/brand-logo";
import { ContrastSwitch } from "@/components/contrast-switch";
import { compactActionClassName } from "@/components/ui/styles";

import { isCurrentLink, type PanelLink } from "./navigation";

/**
 * What the three panel headers are built from: the mark with the panel's name,
 * the account box and the row of tabs. The headers keep what makes each of them
 * different (the operator's mode band, which name the account box shows), and
 * share the look, so the three panels read as one product.
 */

/** The logo, leading to the panel's start page, with the panel named next to it. */
export function PanelBrand({ href, name }: { href: string; name: string }) {
  return (
    <Link href={href} className="flex items-center gap-3 no-underline">
      {/* Same inline mark as everywhere: it follows the contrast palette. */}
      <BrandLogo className="h-9 w-auto" />
      <span className="hidden border-l border-border pl-3 text-sm text-text-muted sm:block">
        {name}
      </span>
    </Link>
  );
}

/** Who is signed in, the contrast switch and the way out. */
export function PanelAccount({
  label,
  name,
  onLogout,
  loggingOut,
}: {
  /** What is printed, for example "Zalogowano jako ...". */
  label: string;
  /** The account's own name, kept whole in the tooltip when the label is cut. */
  name: string;
  onLogout: () => void;
  loggingOut: boolean;
}) {
  return (
    <div className="ml-auto flex flex-wrap items-center gap-3">
      <span className="flex items-center gap-2 text-sm">
        {/* The first letters of the name: decoration that helps the eye find
            the account, nothing a screen reader needs twice. */}
        <span
          aria-hidden="true"
          className="grid size-8 place-items-center rounded-full bg-surface-warm text-xs font-semibold text-brand-accent-text"
        >
          {initials(name)}
        </span>
        <span className="max-w-[16rem] truncate" title={name}>
          {label}
        </span>
      </span>
      <ContrastSwitch />
      <button type="button" onClick={onLogout} disabled={loggingOut} className={compactActionClassName}>
        {loggingOut ? "Wylogowywanie..." : "Wyloguj"}
      </button>
    </div>
  );
}

/** The panel's tabs. The current one is announced, not only underlined. */
export function PanelNav({
  label,
  links,
  root,
  rowClassName = "",
}: {
  label: string;
  links: readonly PanelLink[];
  root: string;
  /** How the panel constrains a row, the same as its header row. */
  rowClassName?: string;
}) {
  const pathname = usePathname();

  return (
    <nav aria-label={label}>
      <ul className={`flex w-full flex-wrap gap-x-6 px-4 sm:px-6 ${rowClassName}`}>
        {links.map((link) => {
          const current = isCurrentLink(link.href, pathname, root);

          return (
            <li key={link.href}>
              <Link
                href={link.href}
                // Colour and a border say nothing to a screen reader, and the
                // high contrast palette repaints both of them anyway.
                aria-current={current ? "page" : undefined}
                className={`-mb-px block border-b-[3px] py-3 text-sm font-semibold no-underline ${
                  current
                    ? "border-active-border text-text"
                    : "border-transparent text-text-muted hover:border-border hover:text-text"
                }`}
              >
                {link.label}
              </Link>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}

/** Up to two first letters of the name, "SŁ" for "Stowarzyszenie Łąka". */
export function initials(name: string): string {
  return name
    .split(/\s+/)
    // A quote or a bracket before a word is not its first letter: "Nasz" in
    // Fundacja "Nasz Dom" still gives an N. A word with no letter at all goes.
    .map((word) => word.replace(/^[^\p{L}]+/u, ""))
    .filter((word) => word.length > 0)
    .slice(0, 2)
    .map((word) => word.charAt(0).toUpperCase())
    .join("");
}
