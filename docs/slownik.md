# Słownik pojęć

Zamawiający opisuje system językiem Witkaca. Jeśli nasze nazwy w UI i w API będą inne, każde spotkanie zacznie się od tłumaczenia pojęć, a każdy raport od zgadywania, o czym mowa.

**Reguła:** kolumna "W UI" jest obowiązująca w interfejsie i w rozmowie z zamawiającym. Kolumna "W kodzie" jest obowiązująca w identyfikatorach, ścieżkach API i kolumnach bazy.

| W UI (polski) | W kodzie (angielski) | Co to znaczy |
|---|---|---|
| Konkurs | `competition` | Ogłoszony przez OCWIP nabór, w którym można otrzymać dotację. Ma termin, limit kwoty i przypisany formularz. |
| Nabór | `competition` | Używane wymiennie z "konkursem". W kodzie nie tworzymy dwóch pojęć. |
| Oferta | `application` | To, co składa wnioskodawca. Zamawiający używa zarówno "oferta", jak i "wniosek". |
| Wniosek | `application` | Jak wyżej. W UI wybieramy jedno słowo i trzymamy się go konsekwentnie. |
| Wersja robocza | `draft` | Niezłożona oferta. Wniosek ma 5 do 6 stron i nikt nie wypełnia go za jednym posiedzeniem. |
| Złożenie oferty | `submission` | Nieodwracalne wysłanie wniosku przed terminem zamknięcia. |
| Generator | `-` | Tak zamawiający nazywa całą platformę. Nie mylić z kreatorem formularzy. |
| Kreator formularzy | `form builder` | Narzędzie, w którym OCWIP samodzielnie układa formularz wniosku. |
| Definicja formularza | `form definition` | Zapisana w bazie struktura formularza, wersjonowana. |
| Karta oceny | `review sheet` | Formularz, który wypełnia recenzent. Wzoru jeszcze nie mamy. |
| Lista rankingowa | `ranking` | Wnioski ułożone według liczby zebranych punktów. |
| Operator | `operator` | Pracownik OCWIP prowadzący konkurs. Widzi wszystko. |
| Wnioskodawca | `applicant` | Podmiot składający ofertę. Widzi wyłącznie swoje. |
| Recenzent | `reviewer` | Osoba oceniająca. Widzi wyłącznie wnioski jej przypisane. |
| Podmiot | `entity` | Kto składa wniosek: organizacja albo grupa nieformalna. Nie to samo co konto. |
| Grupa nieformalna | `informal group` | Trzy osoby fizyczne, samodzielnie albo pod patronatem organizacji. |
| Dotacja | `grant` | Przyznana kwota. |
| Umowa | `agreement` | Generowana ze wzoru po decyzji, podpisywana poza systemem. |
| Sprawozdanie | `report` | Składane przez podmiot po realizacji projektu. |
| Załącznik | `attachment` | Plik dołączony do oferty. Wymagalność zależy od konfiguracji konkursu. |

## Czego unikamy

- "Grant" w polskim UI. Zamawiający mówi "dotacja".
- "Aplikacja" w znaczeniu wniosku. Po polsku to jest oferta albo wniosek, a "aplikacja" to nasza platforma.
- "Tenant", "workspace" i inne pojęcia z systemów wielotenantowych. Obsługujemy jedną organizację, zobacz [`zakres.md`](zakres.md).
