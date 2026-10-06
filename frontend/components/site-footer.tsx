import Link from "next/link";

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
 * As wide as the header above it, so the footer lines up with the logo: the
 * row of the public pages and the applicant panel, or the whole window in the
 * operator panel (fluid), which uses all of it for its tables.
 */
export function SiteFooter({ fluid = false }: { fluid?: boolean }) {
  return (
    <footer className="border-t border-border bg-surface-muted">
      <div className={`mx-auto flex w-full ${fluid ? "" : "max-w-6xl"} flex-wrap items-center justify-between gap-x-8 gap-y-3 px-4 py-6 text-sm sm:px-6`}>
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
