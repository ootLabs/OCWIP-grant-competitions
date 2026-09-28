/**
 * A complete application on the 2026 form (backend/seed/forms/application-2026.json),
 * the same one Application2026FormTests submits for each kind of applicant:
 * a budget of 5 700 zł under the 7 000 zł ceiling, indirect costs under 10%.
 */
export type ApplicantKind = "Organisation" | "InformalGroup";

const cost = (nazwa: string, liczba: number, cena: number) => ({ nazwa, jednostka: "szt.", liczba, cena });
const member = (name: string) => ({ imie_i_nazwisko: name, adres: "ul. Testowa 1, Opole", telefon: "700 100 200", email: null });

export function answers2026(kind: ApplicantKind, title: string): Record<string, unknown> {
  const answers: Record<string, unknown> = {
    rodzaj_wnioskodawcy: kind,
    gmina_realizacji: "Lewin Brzeski",
    charakterystyka_wnioskodawcy: "Działamy od dwóch lat na rzecz sąsiadów.",
    tytul_projektu: title,
    data_zakonczenia: "2026-11-30",
    charakterystyka_projektu: "Zakładamy ogród i uczymy uprawy warzyw.",
    cel_glowny: "Zbliżyć sąsiadów przez wspólną pracę.",
    opis_pomyslu: "o".repeat(1000),
    opis_dzialan: "Przygotowanie terenu, sadzenie, warsztaty, festyn.",
    promocja: "Plakaty w szkole i profil w mediach społecznościowych.",
    rezultaty_opis: "Ogród zostaje pod opieką rady rodziców.",
    liczba_uczestnikow: 40,
    liczba_uczestnikow_monitorowanie: "Listy obecności",
    rezultaty: [{ rezultat: "Promocja", wartosc_docelowa: "10 plakatów", monitorowanie: "Zdjęcia" }],
    dostepnosc_architektoniczna: "Teren bez progów.",
    dostepnosc_cyfrowa: "Ogłoszenia z tekstem alternatywnym.",
    dostepnosc_informacyjna: "Informacja w tekście łatwym do czytania.",
    koszty_bezposrednie: [cost("Sadzonki", 1, 5000)],
    koszty_promocji: [cost("Plakaty", 10, 20)],
    koszty_posrednie: [cost("Księgowość", 1, 500)],
    o_zwiazanie: true,
    o_zgodnosc: true,
    o_niekaralnosc: true,
    o_pozytek: true,
    o_regulamin: true,
    o_rodo: true,
    o_rodo_osoby_trzecie: true,
  };

  if (kind === "Organisation") {
    Object.assign(answers, {
      rodzaj_organizacji: "mloda",
      data_wpisu: "2024-03-01",
      przychod: 18000,
      o_siedziba: true,
      o_podatki: true,
      o_skladki: true,
    });
  } else {
    Object.assign(answers, {
      nazwa_grupy: "Sąsiedzi z Zaodrza",
      czlonkowie_grupy: [member("Anna Testowa"), member("Jan Testowy"), member("Ewa Testowa")],
      o_mieszkancy: true,
      rachunek_lidera: "73 1111 1111 1111 1111 1111 1111",
    });
  }

  return answers;
}
