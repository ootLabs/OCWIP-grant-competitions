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
 */
export function SiteFooter({ wide = false }: { wide?: boolean }) {
  return (
    <footer className="border-t border-border">
      <div
        className={`mx-auto flex w-full ${wide ? "max-w-6xl" : "max-w-3xl"} flex-wrap items-center gap-x-4 gap-y-2 px-4 py-4 text-sm`}
      >
        <p>Opolskie Centrum Wspierania Inicjatyw Pozarządowych</p>
        <nav aria-label="Informacje o serwisie">
          <ul className="flex flex-wrap gap-x-4 gap-y-2">
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
