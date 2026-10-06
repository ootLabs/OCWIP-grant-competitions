import Link from "next/link";

import { BrandLogo } from "@/components/brand-logo";
import { SiteFooter } from "@/components/site-footer";
import { archivePath } from "@/lib/competitions";

import { AccountLinks } from "./account-links";
import { ContrastSwitch } from "./contrast-switch";

/**
 * The frame around every page a visitor sees without a session: the public
 * competition pages (T-23) and the sign in screen (T-12.7).
 *
 * Deliberately not the panel frame: there is no session here, nobody to name
 * and nothing to log out of. What it does share with the panels is the shape
 * of the accessibility work, because these are the pages with the most outside
 * traffic in the product: a skip link ahead of the header, one landmark for
 * navigation and one for content, and a layout that survives a phone held by
 * somebody from an informal group with no work laptop.
 *
 * The header carries the way in (T-99): sign in and register for a visitor,
 * the panel once signed in, and the logo leads to the home page.
 *
 * One frame for both, because the sign in screen is where "Wypełnij wniosek"
 * lands: somebody who just left a competition page should not feel they have
 * left the site.
 *
 * The header and the footer are always the full row; the content is narrow by
 * default, a measure a legal text or a sign in form reads well at, and wide
 * where a page lays cards side by side (the home page, the competitions).
 */
export function PublicFrame({
  children,
  width = "narrow",
}: {
  children: React.ReactNode;
  width?: "narrow" | "wide";
}) {
  return (
    <div className="flex min-h-screen flex-col">
      {/* focus:fixed rather than focus:absolute: further down a long list of
          competitions an absolutely positioned link is focused off screen
          (the same bug T-15.4 fixed in the applicant panel). */}
      <a
        className="sr-only focus:not-sr-only focus:fixed focus:left-4 focus:top-4 focus:z-10 focus:rounded-sm focus:bg-bg focus:px-4 focus:py-2 focus:underline"
        href="#tresc"
      >
        Przejdź do treści
      </a>

      <header className="border-b border-border bg-bg">
        <div className="mx-auto flex w-full max-w-6xl flex-wrap items-center gap-x-8 gap-y-3 px-4 py-3 sm:px-6">
          <Link className="flex items-center gap-3 no-underline" href="/">
            <BrandLogo className="h-10 w-auto" />
            <span aria-hidden="true" className="hidden border-l border-border pl-3 text-xs leading-tight text-text-muted sm:block">
              Generator
              <br />
              konkursów
            </span>
            <span className="sr-only">Konkursy OCWIP, strona główna</span>
          </Link>

          {/* Short labels: the home page itself carries the long ones
              ("Archiwum wyników"), and two links with one name on one page
              are two links a screen reader user cannot tell apart. */}
          <nav aria-label="Serwis" className="flex flex-1 flex-wrap gap-x-6 gap-y-1">
            <Link className="py-2 text-sm font-semibold text-text no-underline hover:underline" href="/competitions">
              Konkursy
            </Link>
            <Link className="py-2 text-sm font-semibold text-text no-underline hover:underline" href={archivePath}>
              Archiwum
            </Link>
          </nav>

          <div className="flex flex-wrap items-center gap-3">
            <ContrastSwitch />
            <AccountLinks />
          </div>
        </div>
      </header>

      <main
        className={`mx-auto w-full flex-1 px-4 py-8 sm:px-6 sm:py-10 ${width === "wide" ? "max-w-6xl" : "max-w-3xl"}`}
        id="tresc"
      >
        {children}
      </main>

      <SiteFooter />
    </div>
  );
}
