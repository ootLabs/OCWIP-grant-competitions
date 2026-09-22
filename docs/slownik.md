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
| Szkic kreatora | `draft` (`lib/forms/draft-storage.ts`) | Praca operatora nad definicją formularza, niezapisana do bazy. Inne pojęcie niż "wersja robocza": ta dotyczy oferty wnioskodawcy, szkic kreatora dotyczy operatora i formularza, i żyje w przeglądarce, nie w bazie. |
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
| Numer konkursu | `competition number` | Numer, którym OCWIP posługuje się poza systemem, format typu `1/2026`. Niepowtarzalny wśród konkursów aktywnych. |
| Nabór ciągły | `continuous intake` | Konkurs bez terminu zakończenia. Zamyka go operator, nigdy zegar. |
| Stan konkursu | `competition status` | Jeden z siedmiu: roboczy, opublikowany, trwa nabór, nabór zamknięty, trwa ocena, rozstrzygnięty, archiwalny. |
| Stan efektywny | `effective status` | Stan, w którym konkurs jest **teraz**: zapisany stan przesunięty o przejścia wynikające z dat. Odpowiedź API zawsze podaje ten. |
| Kategoria kosztów | `cost category` | Koszty bezpośrednie, rozwój instytucjonalny albo koszty pośrednie. **Ustawienie konkursu, nie stała systemu:** wyłączona kategoria znika z budżetu razem ze swoją sekcją opisową. |
| Podstawa liczenia procentu | `percentage basis` | Od czego liczą się limity procentowe: od kwoty dotacji albo od całkowitej wartości projektu. |
| Wymagany załącznik | `competition attachment` | Dokument, którego konkurs żąda od wnioskodawcy. Wymagalność ma trzy warianty: wymagany, niewymagalny, wymagany warunkowo poza KRS. Nie mylić z plikiem, który wnioskodawca wgrywa (`attachment`). |
| Osoba kontaktowa | `competition contact` | Pracownik OCWIP odpowiadający na pytania o konkurs. Widoczny publicznie, także dla gościa bez konta. |
| Przejście | `transition` | Ruch między dwoma stanami konkursu. Dozwolone pary są wypisane w jednym miejscu w kodzie. |

## Czego unikamy

- "Grant" w polskim UI. Zamawiający mówi "dotacja".
- "Aplikacja" w znaczeniu wniosku. Po polsku to jest oferta albo wniosek, a "aplikacja" to nasza platforma.
- "Tenant", "workspace" i inne pojęcia z systemów wielotenantowych. Obsługujemy jedną organizację, zobacz [`zakres.md`](zakres.md).
