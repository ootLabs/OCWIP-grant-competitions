"use client";

import { StatusPage, statusActionClassName } from "@/components/status-page";

import "./globals.css";

/**
 * The same 500, for the one case error.tsx cannot catch: a failure in the root
 * layout itself.
 *
 * It replaces that layout, so it brings its own html and body, its own
 * stylesheet and its own lang. Without this file that case falls through to the
 * framework's default page, which is in English and looks like nothing else in
 * the product.
 *
 * What it cannot bring are the brand fonts: next/font is loaded by the root
 * layout, which is the thing that just failed, and it cannot be called from a
 * client component. So font-sans here, explicitly, rather than the token that
 * would resolve to an undefined variable and drop the page onto whatever the
 * browser considers a default serif.
 *
 * The way out is a plain reload rather than a Link: the router lives in the
 * layout that just failed.
 */
export default function GlobalError({ reset }: { error: Error; reset: () => void }) {
  return (
    <html lang="pl">
      <body className="min-h-screen font-sans antialiased">
        <StatusPage
          title="Coś poszło nie tak"
          actions={
            <button type="button" onClick={reset} className={statusActionClassName}>
              Spróbuj ponownie
            </button>
          }
        >
          Nie udało się wyświetlić tej strony. Odśwież ją za chwilę. Jeśli
          wypełniałeś wniosek, zapisane dane są bezpieczne.
        </StatusPage>
      </body>
    </html>
  );
}
