/**
 * The data of the accessibility statement and of the contact page (T-121),
 * in one place, so the yearly update due by 31 March is a change of data,
 * not of code. The entity, its address and e-mail are OCWIP's public data;
 * the person, the phone, the dates and the building are working values for
 * OCWIP to replace (PK-E, ZR-18 in docs/runbook/zalozenia-robocze.md).
 */
export interface StatementDate {
  /** yyyy-mm-dd, as the validator reads <time datetime>. */
  readonly iso: string;
  /** "29 września 2026 r.", as a person reads it. */
  readonly text: string;
}

export const accessibilityStatement = {
  entity: "Opolskie Centrum Wspierania Inicjatyw Pozarządowych",
  address: "ul. Damrota 4/36, 45-064 Opole",
  siteName: "Generator konkursów OCWIP",
  published: { iso: "2026-09-29", text: "29 września 2026 r." } satisfies StatementDate,
  updated: { iso: "2026-09-29", text: "29 września 2026 r." } satisfies StatementDate,
  prepared: { iso: "2026-09-29", text: "29 września 2026 r." } satisfies StatementDate,
  contactPerson: "Osoba do kontaktu w sprawie dostępności (do uzupełnienia przez OCWIP)",
  email: "biuro@ocwip.pl",
  phone: "do uzupełnienia przez OCWIP",
  /** What the audit (docs/dostepnosc.md, T-46) left open, in words a visitor understands. */
  inaccessible: [
    "Dokumenty PDF tworzone przez system (potwierdzenie złożenia wniosku, wniosek, umowa, lista rankingowa) nie mają struktury znaczników, więc czytnik ekranu odczytuje je jako zwykły tekst, bez nagłówków i tabel. Na prośbę wyślemy treść dokumentu w innej formie.",
  ],
  building:
    "Opis dostępności architektonicznej siedziby OCWIP (wejście, schody i windy, toalety, miejsca parkingowe dla osób z niepełnosprawnością, pies asystujący) do uzupełnienia przez OCWIP.",
  communication:
    "Opis dostępności komunikacyjno-informacyjnej (tłumacz języka migowego, pętla indukcyjna, kontakt przez e-mail) do uzupełnienia przez OCWIP.",
} as const;
