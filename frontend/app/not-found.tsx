import type { Metadata } from "next";
import Link from "next/link";

import { StatusPage, statusActionClassName } from "@/components/status-page";

export const metadata: Metadata = {
  title: "Nie ma takiej strony - Generator konkursów OCWIP",
};

/**
 * 404 for every address in the application (card T-15.4).
 *
 * The way back goes to the start page, not to a panel: this page is reached by
 * people who are not signed in as often as by those who are, most of them from
 * an old link to a call for proposals that has since closed.
 */
export default function NotFound() {
  return (
    <StatusPage
      title="Nie ma takiej strony"
      actions={
        <Link href="/" className={statusActionClassName}>
          Wróć na stronę główną
        </Link>
      }
    >
      Adres jest nieaktualny albo zawiera literówkę. Jeśli trafiłeś tu z linku
      do konkursu, nabór mógł się już zakończyć.
    </StatusPage>
  );
}
