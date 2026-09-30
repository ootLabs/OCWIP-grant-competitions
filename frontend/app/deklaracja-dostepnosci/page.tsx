import type { Metadata } from "next";
import { headers } from "next/headers";

import { PublicFrame } from "@/components/public-frame";
import { accessibilityStatement as statement, type StatementDate } from "@/lib/accessibility-statement";

export const metadata: Metadata = {
  title: "Deklaracja dostępności | Generator konkursów OCWIP",
};

/**
 * The accessibility statement (T-121), in the structure of "Warunki
 * techniczne publikacji oraz struktury dokumentu elektronicznego deklaracji
 * dostępności" 2.0 (Ministerstwo Cyfryzacji, 31.07.2024): the headings in
 * their order and the ids the validator reads. The status sentence is the
 * template's own, word for word; only the list of what is not accessible
 * changes, from the audit in docs/dostepnosc.md. The data lives in
 * lib/accessibility-statement.ts.
 */
function Time({ id, date }: { id: string; date: StatementDate }) {
  return (
    <time id={id} dateTime={date.iso}>
      {date.text}
    </time>
  );
}

export default async function AccessibilityDeclarationPage() {
  // The address the statement is about, the one this page is served from.
  const host = (await headers()).get("host") ?? "";
  const url = host ? `https://${host}` : "/";

  return (
    <PublicFrame>
      <article className="flex flex-col gap-4">
        <h1 className="text-3xl">Deklaracja dostępności</h1>

        <p id="a11y-wstep">
          <span id="a11y-podmiot">{statement.entity}</span> zobowiązuje się zapewnić dostępność swojej strony
          internetowej zgodnie z ustawą z dnia 4 kwietnia 2019 r. o dostępności cyfrowej stron internetowych i
          aplikacji mobilnych podmiotów publicznych. Deklaracja dostępności dotyczy strony internetowej{" "}
          <a id="a11y-url" className="underline" href={url}>
            <span id="a11y-zakres">{statement.siteName}</span>
          </a>
          .
        </p>
        <ul className="list-disc pl-6">
          <li>
            Data publikacji strony internetowej: <Time id="a11y-data-publikacja" date={statement.published} />
          </li>
          <li>
            Data ostatniej istotnej aktualizacji: <Time id="a11y-data-aktualizacja" date={statement.updated} />
          </li>
        </ul>

        <h2 className="text-xl">Stan dostępności cyfrowej</h2>
        <p id="a11y-status">
          Strona internetowa jest częściowo zgodna z ustawą z dnia 4 kwietnia 2019 r. o dostępności cyfrowej stron
          internetowych i aplikacji mobilnych podmiotów publicznych z powodu niezgodności lub wyłączeń wymienionych
          poniżej.
        </p>

        <h2 className="text-xl">Niedostępne treści</h2>
        <ul className="list-disc pl-6">
          {statement.inaccessible.map((item) => (
            <li key={item}>{item}</li>
          ))}
        </ul>

        <h2 className="text-xl">Przygotowanie deklaracji dostępności</h2>
        <p>
          {/* No full stop after the date: the rendered text already ends in
              the "r." abbreviation, and the two together printed "2026 r..".
              The two dates above are written without one for the same reason. */}
          Data sporządzenia deklaracji: <Time id="a11y-data-sporzadzenie" date={statement.prepared} />
        </p>
        <p id="a11y-ocena">
          Deklarację sporządzono na podstawie samooceny przeprowadzonej przez zespół wykonawcy serwisu: przegląd
          ekran po ekranie według WCAG 2.1 na poziomie AA oraz automatyczne sprawdzenie narzędziem axe po każdym teście
          interfejsu.
        </p>

        <h2 className="text-xl">Udogodnienia, ograniczenia i inne informacje</h2>
        <p>
          Serwis ma tryb wysokiego kontrastu, włączany przyciskiem &bdquo;Wysoki kontrast&rdquo; w nagłówku każdej
          strony. Tekst można powiększać narzędziami przeglądarki bez utraty treści.
        </p>

        <h2 className="text-xl">Skróty klawiszowe</h2>
        <p>
          Serwis nie ma własnych skrótów klawiszowych. Działają standardowe skróty przeglądarki, a pierwszy klawisz
          Tab przenosi do łącza &bdquo;Przejdź do treści&rdquo;.
        </p>

        <h2 className="text-xl">Informacje zwrotne i dane kontaktowe</h2>
        <p>
          Wszystkie problemy z dostępnością cyfrową tej strony internetowej zgłoś do:{" "}
          <span id="a11y-kontakt">{statement.contactPerson}</span>, e-mail:{" "}
          <a id="a11y-email" className="underline" href={`mailto:${statement.email}`}>
            {statement.email}
          </a>
          , telefon: <span id="a11y-telefon">{statement.phone}</span>.
        </p>

        <h2 className="text-xl">Obsługa wniosków i skarg związanych z dostępnością</h2>
        <p id="a11y-procedura">
          Każdy ma prawo wystąpić z żądaniem zapewnienia dostępności cyfrowej strony internetowej lub jej elementów.
          Można także zażądać udostępnienia informacji w alternatywnym sposobie dostępu. Żądanie powinno zawierać
          dane osoby zgłaszającej, wskazanie strony lub elementu oraz sposób kontaktu, a przy prośbie o dostęp
          alternatywny także preferowany sposób przedstawienia informacji. Żądanie realizujemy niezwłocznie, najpóźniej
          w ciągu 7 dni. Jeżeli dotrzymanie tego terminu nie jest możliwe, informujemy o nowym terminie, nie dłuższym
          niż 2 miesiące. Jeżeli zapewnienie dostępności nie jest możliwe, zaproponujemy alternatywny sposób dostępu.
          Po odmowie można złożyć skargę na zapewnienie dostępności cyfrowej, a po jej wyczerpaniu wniosek do
          Rzecznika Praw Obywatelskich.
        </p>

        <h2 className="text-xl">Pozostałe informacje</h2>
        <h3 className="text-lg">Aplikacje mobilne</h3>
        <p id="a11y-aplikacje">Serwis nie ma aplikacji mobilnej.</p>
        <h3 className="text-lg">Dostępność architektoniczna</h3>
        <p id="a11y-architektura">
          {statement.entity}, {statement.address}. {statement.building}
        </p>
        <h3 className="text-lg">Dostępność komunikacyjno-informacyjna</h3>
        <p id="a11y-komunikacja">{statement.communication}</p>
      </article>
    </PublicFrame>
  );
}
