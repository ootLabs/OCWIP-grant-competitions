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
 * The footer of every page, public and panel alike (T-121). The link text
 * "Deklaracja dostępności" is exactly the one the regulation asks for.
 *
 * As wide as the header above it, so the footer lines up with the logo. The
 * default is the row every public page and the applicant panel uses; a panel
 * with a row of its own (the operator's, which is wider for its tables) passes
 * that row in, so the two cannot be capped differently.
 *
 * The second row credits the programme that built the application and its
 * organiser. It lives here, not in a dialog or a page of its own, because this
 * is the one block every screen already has, so the credit is on all of them
 * and the contract with the client (OCWIP is the organiser of the competition,
 * not of the programme) stays visible in the order of the rows: OCWIP first.
 */
export function SiteFooter({ rowClassName = panelRowClassName }: { rowClassName?: string }) {
  return (
    <footer className="border-t border-border bg-surface-muted">
      <div className={`${rowClassName} flex flex-wrap items-center justify-between gap-x-8 gap-y-3 px-4 py-6 text-sm sm:px-6`}>
        <p className="text-text-muted">Opolskie Centrum Wspierania Inicjatyw Pozarządowych</p>
        <nav aria-label="Informacje o serwisie">
          <ul className="flex flex-wrap gap-x-5 gap-y-2">
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
      <div className="border-t border-border">
        <p className={`${rowClassName} px-4 py-4 text-sm leading-7 text-text-muted sm:px-6`}>
          Aplikacja stworzona przez praktykantów programu <MlabLogo className="inline-block h-4 w-auto align-middle" />, organizowanego przez{" "}
          <MobitouchLogo className="inline-block h-3.5 w-auto align-middle" />.
          <a className="block w-fit underline" href="https://mkariera.pl" rel="noopener noreferrer" target="_blank">
            Portfolio programu na mkariera.pl (otwiera się w nowej karcie)
          </a>
        </p>
      </div>
    </footer>
  );
}
