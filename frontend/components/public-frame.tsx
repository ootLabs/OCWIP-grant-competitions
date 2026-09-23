import Link from "next/link";

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
 * One frame for both, because the sign in screen is where "Wypełnij wniosek"
 * lands: somebody who just left a competition page should not feel they have
 * left the site.
 */
export function PublicFrame({ children }: { children: React.ReactNode }) {
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

      <header className="border-b border-border">
        <div className="mx-auto flex w-full max-w-3xl items-center gap-3 px-4 py-3">
          <Link className="flex items-center gap-2" href="/competitions">
            {/* Same plain img as everywhere else in this product: a vector
                mark needs no optimisation, and one way of doing one thing. */}
            {/* eslint-disable-next-line @next/next/no-img-element -- vector logo, no optimisation needed */}
            <img alt="OCWIP" className="h-9 w-auto" src="/ocwip-logo.svg" />
            <span className="sr-only">Konkursy OCWIP</span>
          </Link>
        </div>
      </header>

      <main className="mx-auto w-full max-w-3xl flex-1 px-4 py-8" id="tresc">
        {children}
      </main>

      <footer className="border-t border-border">
        <p className="mx-auto w-full max-w-3xl px-4 py-4 text-sm">
          Opolskie Centrum Wspierania Inicjatyw Pozarządowych
        </p>
      </footer>
    </div>
  );
}
