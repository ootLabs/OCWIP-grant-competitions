import Link from "next/link";
import type { ReactNode } from "react";

import { MlabLogo, MobitouchLogo } from "@/components/partner-logos";
import { panelRowClassName } from "@/components/ui/styles";

/** The pages every screen links to (T-121): the accessibility statement, the legal texts and the contact. */
export const footerLinks: readonly { readonly href: string; readonly label: string }[] = [
  { href: "/deklaracja-dostepnosci", label: "Deklaracja dostępności" },
  { href: "/regulamin", label: "Regulamin serwisu" },
  { href: "/klauzula-informacyjna", label: "Klauzula informacyjna" },
  { href: "/kontakt", label: "Kontakt" },
];

/**
 * The footer of every page, public and panel alike (T-121). The link text
 * "Deklaracja dostępności" is exactly the one the regulation asks for.
 *
 * As wide as the header above it, so the footer lines up with the logo. The
 * default is the row every public page and the applicant panel uses; a panel
 * with a row of its own (the operator's, which is wider for its tables) passes
 * that row in, so the two cannot be capped differently.
 *
 * Under the name of OCWIP, on the same side, stands the credit of the programme
 * that built the application and of its organiser, each mark with the link to
 * its own site beneath. It lives in this footer, not in a dialog or a page of
 * its own, because this is the one block every screen already has. OCWIP is
 * first: it is the organiser of the competition, the credit is a note under it.
 */
export function SiteFooter({ rowClassName = panelRowClassName }: { rowClassName?: string }) {
  return (
    <footer className="border-t border-border bg-surface-muted">
      <div className={`${rowClassName} flex flex-wrap items-center justify-between gap-x-8 gap-y-3 px-4 py-6 text-sm sm:px-6`}>
        <div className="flex max-w-full flex-col gap-3 text-text-muted">
          <p>Opolskie Centrum Wspierania Inicjatyw Pozarządowych</p>
          <div className="flex flex-wrap items-center gap-x-2 gap-y-2">
            <span>Aplikacja stworzona przez</span>
            <CreditMark href="https://mkariera.pl" label="mkariera.pl" suffix=",">
              <MlabLogo className="h-4 w-auto" />
            </CreditMark>
            <span>organizowany przez</span>
            <CreditMark href="https://mobitouch.net" label="mobitouch.net">
              <MobitouchLogo className="h-3.5 w-auto" />
            </CreditMark>
          </div>
        </div>
        <nav aria-label="Informacje o serwisie" className="min-w-[14rem] flex-1">
          <ul className="flex flex-wrap gap-x-5 gap-y-2 sm:justify-end">
            {footerLinks.map((link) => (
              <li key={link.href}>
                <Link className="underline" href={link.href}>
                  {link.label}
                </Link>
              </li>
            ))}
          </ul>
        </nav>
      </div>
    </footer>
  );
}

/** A mark with the link to its own site beneath; the link says it opens elsewhere for a screen reader. */
function CreditMark({ href, label, suffix, children }: { href: string; label: string; suffix?: string; children: ReactNode }) {
  return (
    <span className="inline-flex flex-col items-start gap-1">
      <span className="flex items-end">
        {children}
        {suffix}
      </span>
      <a className="underline" href={href} rel="noopener noreferrer" target="_blank">
        {label}
        <span className="sr-only"> (otwiera się w nowej karcie)</span>
      </a>
    </span>
  );
}
