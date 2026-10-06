import Link from "next/link";

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
    </footer>
  );
}
