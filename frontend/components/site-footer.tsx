import Link from "next/link";

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
 * A credit link is the mark itself, so it carries no underline (the link look
 * test knows a link made of a logo). The accessible name is the one of the mark;
 * that the link opens a new tab is said to a screen reader only, so nothing is
 * printed beside the logo.
 */
const creditLinkClassName = "inline-flex items-center rounded-sm hover:opacity-75";

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
 * that built the application and of its organiser, each mark linking to its
 * own site. It lives in this footer, not in a dialog or a page of
 * its own, because this is the one block every screen already has. OCWIP is
 * first: it is the organiser of the competition, the credit is a note under it.
 */
export function SiteFooter({ rowClassName = panelRowClassName }: { rowClassName?: string }) {
  return (
    <footer className="border-t border-border bg-surface-muted">
      <div className={`${rowClassName} flex flex-col gap-6 px-4 py-6 text-sm sm:flex-row sm:items-start sm:justify-between sm:gap-x-12 sm:px-6`}>
        <div className="flex flex-col gap-4 text-text-muted">
          <p>Opolskie Centrum Wspierania Inicjatyw Pozarządowych</p>
          {/* Two aligned lines, label then mark: the same on a phone and on a desktop, so
              nothing wraps in the middle of a sentence. */}
          <div className="grid grid-cols-[auto_auto] items-center justify-start gap-x-3 gap-y-2">
            <span>Aplikacja stworzona przez</span>
            <a className={creditLinkClassName} href="https://mkariera.pl" rel="noopener noreferrer" target="_blank">
              <MlabLogo className="h-4 w-auto" />
              <span className="sr-only">(otwiera się w nowej karcie)</span>
            </a>
            <span>organizowany przez</span>
            <a className={creditLinkClassName} href="https://mobitouch.net" rel="noopener noreferrer" target="_blank">
              <MobitouchLogo className="h-3.5 w-auto" />
              <span className="sr-only">(otwiera się w nowej karcie)</span>
            </a>
          </div>
        </div>
        <nav aria-label="Informacje o serwisie">
          <ul className="flex flex-col gap-2 sm:items-end">
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
