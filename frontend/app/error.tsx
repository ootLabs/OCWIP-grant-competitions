"use client";

import Link from "next/link";

import { StatusPage, statusActionClassName } from "@/components/status-page";

/**
 * Anything that breaks below the root layout: the 500 a person actually sees.
 *
 * Nothing from the error reaches the screen, neither the message nor the
 * digest. The people on both sides of this system are grant applicants and one
 * operator who says outright that they do not know the technical side, so a
 * stack trace helps nobody here, and it describes the inside of an application
 * that handles personal data to anybody who can make it fail (card T-15.4,
 * docs/konwencje.md).
 *
 * Two ways out, because the causes differ: a failed request is usually over by
 * the next attempt, and reset() retries this screen without losing whatever
 * else is open, while a broken screen needs the way home.
 */
export default function ErrorPage({ reset }: { error: Error; reset: () => void }) {
  return (
    <StatusPage
      title="Coś poszło nie tak"
      actions={
        <>
          <button type="button" onClick={reset} className={statusActionClassName}>
            Spróbuj ponownie
          </button>
          <Link href="/" className={statusActionClassName}>
            Wróć na stronę główną
          </Link>
        </>
      }
    >
      Nie udało się wyświetlić tej strony. Spróbuj jeszcze raz za chwilę. Jeśli
      wypełniałeś wniosek, zapisane dane są bezpieczne.
    </StatusPage>
  );
}
