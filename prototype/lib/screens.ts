/**
 * The nine screens of direction C, in the order an applicant walks through them.
 *
 * One list feeds both the side navigation and the overview page, so adding a
 * screen means touching this file and the route, never a third place that
 * quietly falls out of sync.
 */
export type Screen = {
  slug: string;
  step: number;
  title: string;
  summary: string;
  /** True when the screen carries the little state the visualisation needs. */
  interactive?: boolean;
};

export const SCREENS: Screen[] = [
  {
    slug: "competition",
    step: 1,
    title: "Strona konkursu",
    summary:
      "Wejście do systemu. Warunki naboru, termin, limit kwoty i zapowiedź sześciu kroków wniosku.",
  },
  {
    slug: "registration",
    step: 2,
    title: "Rejestracja podmiotu",
    summary:
      "Wybór typu podmiotu decyduje wyłącznie o tym, o co pytamy dalej. Grupa nieformalna nie podaje NIP-u.",
  },
  {
    slug: "applications",
    step: 3,
    title: "Moje wnioski i statusy",
    summary:
      "Wnioskodawca widzi wyłącznie swoje oferty. W jednym konkursie może złożyć więcej niż jedną.",
  },
  {
    slug: "wizard",
    step: 4,
    title: "Kreator wniosku",
    summary:
      "Sześć kroków, jedno pytanie naraz. Przełączanie kroków, licznik znaków i wskaźnik autozapisu działają.",
    interactive: true,
  },
  {
    slug: "mobile",
    step: 5,
    title: "Ten sam krok na telefonie",
    summary:
      "Szerokość 390 px. Dla części grup nieformalnych telefon będzie jedynym urządzeniem.",
  },
  {
    slug: "budget",
    step: 6,
    title: "Budżet i limit kwoty",
    summary:
      "Zmiana dowolnej kwoty przelicza sumę i limit. Komunikat wskazuje konkretną pozycję do zmniejszenia.",
    interactive: true,
  },
  {
    slug: "attachments",
    step: 7,
    title: "Załączniki",
    summary:
      "Wymagane i nieobowiązkowe pliki, stan każdego z nich oraz repozytorium z retencją pięcioletnią.",
  },
  {
    slug: "submission",
    step: 8,
    title: "Sprawdzenie i złożenie",
    summary:
      "Podsumowanie sześciu kroków, twarde odcięcie terminu i nieodwracalność złożenia oferty.",
  },
  {
    slug: "confirmation",
    step: 9,
    title: "Potwierdzenie złożenia",
    summary:
      "Numer oferty, godzina złożenia co do minuty, eksport PDF i to, co dzieje się dalej.",
  },
];

export function screenBySlug(slug: string): Screen | undefined {
  return SCREENS.find((screen) => screen.slug === slug);
}
