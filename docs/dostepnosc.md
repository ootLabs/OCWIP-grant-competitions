# Dostępność: audyt WCAG 2.1 AA

Wynik audytu z karty T-46, zapisany jako lista ustaleń, nie jako wrażenie. Stan na 2026-09-25. Decyzje, które z niego wynikły, są w [`architektura.md`](architektura.md) (sekcja o T-46), a to, jak testy pilnują tego na bieżąco, w [`testy.md`](testy.md).

Poziom: **WCAG 2.1 AA**. Przy zadaniu finansowanym ze środków publicznych wraca to zwykle jako wymóg w umowie z grantodawcą, a wśród wnioskodawców są organizacje osób z niepełnosprawnościami.

---

## Jak audytowano

Cztery narzędzia, bo żadne nie widzi wszystkiego:

| Narzędzie | Co sprawdza | Czego nie widzi |
|---|---|---|
| `axe-core` 4.13 w Chrome 152 (headless), ekrany bez sesji, obie palety | reguły WCAG 2.1 A i AA łącznie z kontrastem na prawdziwie wyrenderowanej stronie; do tego przejście klawiszem Tab z odczytem obwódki fokusu i szerokość strony przy 320 px (reflow) | ekranów paneli, bo wymagają sesji |
| `axe-core` po każdym teście frontu (`frontend/vitest.setup.ts`) | te same reguły plus kolejność nagłówków, na każdym stanie każdego ekranu, który ustawiają testy (panele wnioskodawcy i operatora też) | kontrastu i fokusu, bo jsdom nic nie maluje |
| `app/contrast-tokens.test.ts` z `lib/contrast.ts` | każdą parę tokenów, którą składają komponenty, w obu paletach | tego, która para trafia na który ekran (to widzi axe w przeglądarce) |
| przegląd źródeł, teraz jako test (`app/accessibility-source.test.ts`) | podkreślenie linków, krawędź pól, `outline-none`, dodatni `tabIndex`; ręcznie: podpowiedzi zamiast etykiet, przeciąganie plików, błędy, pola wymagane | zachowania w przeglądarce |

**Ograniczenie, jawnie:** panele wnioskodawcy i operatora nie były przechodzone klawiaturą w prawdziwej przeglądarce. Lokalne konta z `scripts/seed.py` nie mają hasła, a założenie konta z rolą operatora na potrzeby audytu wymagało odczytu danych logowania z bazy, czego nie robiliśmy. Kolejność fokusu w panelach wynika z kolejności w DOM (bez dodatniego `tabIndex` i bez pozycjonowania zmieniającego kolejność, co pilnuje test źródeł), a obwódka fokusu z reguły globalnej w `globals.css`, której żadna klasa nie może zdjąć. Przejście paneli klawiaturą na żywo warto powtórzyć przy pierwszym wdrożeniu na środowisko z kontami testowymi (T-48).

---

## Ustalenia

| ID | Ekrany | Kryterium | Ustalenie | Stan |
|---|---|---|---|---|
| A-01 | wszystkie | 1.4.3, 1.4.11 | Paleta wysokiego kontrastu nadpisywała tylko 6 tokenów. Linki `#413D39` na czarnym 1,95:1, tekst akcentu `#9F3A0C` 3,08:1, szare panele `#F8F9FA` pod białym tekstem 1,05:1, fokus `#663399` 2,1:1. | Poprawione: nadpisany każdy token poza pomarańczem logo, żółty fokus. Test tokenów, który na starej palecie daje 10 czerwonych par. |
| A-02 | wszystkie poza `/design-tokens` | 1.4.3, zobowiązanie z karty | Tryb wysokiego kontrastu dało się włączyć tylko na stronie tokenów, i to na jednym `div`. | Poprawione: przełącznik "Wysoki kontrast" w nagłówku ramy publicznej i obu paneli, wybór zapamiętany w przeglądarce, przywracany przed pierwszym malowaniem. |
| A-03 | logowanie, rejestracja, potwierdzenie adresu | 1.4.1 | Linki w tekście bez podkreślenia, od tekstu różniły się tylko kolorem (1,51:1). Ten sam wzorzec, jeszcze niezgłoszony przez axe, miały reset hasła i przypomnienie hasła. | Poprawione na wszystkich pięciu ekranach konta: podkreślenie. Test źródeł wymaga od każdego linku wyglądu innego niż sam kolor. |
| A-04 | formularz wniosku, kreator konkursu, kreator formularza, lista wniosków operatora, logowanie i ekrany konta | 1.4.11 | Krawędź pól tekstowych, list i obszarów tekstu `#DEE2E6`, 1,3:1 na białym. Pole nie odcinało się od tła. | Poprawione: nowy token `--color-border-control` (`#6C757D`, 4,69:1) na każdym polu tekstowym, liście i obszarze tekstu (43 znaczniki i 4 wspólne klasy pól). Test źródeł. |
| A-05 | formularz wniosku, załączniki | 2.4.7 | Pole pliku ukryte `sr-only` dostawało fokus, ale obwódka rysowała się wokół niewidocznego piksela. Dotyczyło obszaru dodawania i przycisku "Zastąp". | Poprawione: obwódka na etykiecie, która owija pole; "Zastąp" przeniesione do środka etykiety i nazwane plikiem, który podmienia. |
| A-06 | formularz wniosku | 1.3.1, 3.3.2 | Pole wymagane oznaczone tylko gwiazdką ukrytą przed czytnikiem (`aria-hidden`), bez słowa i bez objaśnienia gwiazdki. | Poprawione: czytnik słyszy "(wymagane)", na górze sekcji z polami wymaganymi zdanie "Pola oznaczone gwiazdką (*) są wymagane." |
| A-07 | kreator formularza, limity pola | 4.1.2, 3.3.2 | Dwie listy wyboru w wierszu limitu bez nazwy dostępnej, dwie kolejne nazwane tylko `aria-label`. | Poprawione: `fieldset` z legendą "Limity" i widoczna etykieta przy każdym polu wiersza. |
| A-08 | kreator konkursu, podsumowanie | 1.3.1 | Sekcje podglądu jako `h3` bezpośrednio pod `h1` strony. | Poprawione: `h2`. Kolejność nagłówków sprawdza teraz axe po każdym teście. |
| A-09 | 404, 500, odmowy i awaria w panelach | 1.3.1, 2.4.1 | Strona stanu bez landmarku `main`. | Poprawione: `StatusPage` renderuje `<main>`. |
| A-10 | `/design-tokens` | 1.4.3, 1.3.1 | Znaczek FAIL biały na pomarańczu logo (3,34:1) i próbki typografii jako prawdziwe `h1`/`h2`/`h3` w środku strony. | Poprawione: znaczek obrysowany ciemnym akcentem, próbki jako tekst w kroju nagłówka, próbka "Aa" zdjęta z pary, która nie przechodzi AA. |
| A-11 | tylko środowisko deweloperskie | - | Nakładka deweloperska Next.js (`nextjs-portal`) łapie fokus bez obwódki. | Świadomie zostawione: nie istnieje w buildzie produkcyjnym. |

Znalezione przy okazji, poza dostępnością: `POST /verify-email` z identyfikatorem, który nie jest GUID-em, daje 500. Zapisane jako `R-35` w [`runbook/rozbieznosci.md`](runbook/rozbieznosci.md).

---

## Ekran po ekranie

"Bez uwag" znaczy: axe bez naruszeń w obu paletach (ekrany bez sesji) albo na każdym stanie z testów (panele), fokus widoczny, nagłówki w porządku.

| Ekran | Wynik |
|---|---|
| `/` strona startowa | A-01 (szary panel w trybie kontrastu); poza tym bez uwag |
| `/competitions` lista konkursów | A-01 (tytuł konkursu jako link w trybie kontrastu); poza tym bez uwag, reflow 320 px bez przewijania w poziomie |
| `/competitions/[id]` strona konkursu | A-01 (tekst na szarym panelu); poza tym bez uwag |
| `/login` | A-01, A-03, A-04 |
| `/register` | A-01, A-03, A-04 |
| `/verify-email` | A-01, A-03, A-04 |
| `/forgot-password` | A-01, A-04 |
| `/reset-password` | A-01, A-04 |
| 404 i 500 | A-09 |
| `/design-tokens` | A-10 |
| Panel wnioskodawcy: start, konkursy, profil | A-02; poza tym bez uwag |
| Wniosek: wypełnianie, podsumowanie, złożenie | A-04, A-05, A-06 |
| Wniosek złożony, tylko do odczytu | bez uwag |
| Panel operatora: start, recenzenci | A-02; poza tym bez uwag |
| Kreator ogłoszenia konkursu | A-04, A-08 |
| Kreator formularza wniosku | A-04, A-07 |
| Lista wniosków, podgląd oferty | A-04 (filtry) |

Przeciąganie plików nie jest jedyną drogą dodania załącznika: obszar dodawania jest etykietą pola pliku, więc kliknięcie i klawiatura otwierają wybór pliku bez skryptu (to było już od T-34, audyt potwierdził).

---

## Jak powtórzyć

Na bieżąco nic nie trzeba uruchamiać osobno: `npm test` we froncie oblewa się przy naruszeniu, a CI chodzi przy każdym PR-ze.

Audyt w przeglądarce, z kontrastem, dla ekranów bez sesji: postaw stos, w katalogu roboczym poza repozytorium zainstaluj `puppeteer-core` i `axe-core`, a potem dla każdego adresu z tabeli wyżej, w obu paletach (w `localStorage` klucz `ocwip.contrast` na `true` albo `false` i przeładowanie), wstrzyknij `axe.min.js` i uruchom `axe.run(document, { runOnly: { type: "tag", values: ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"] } })`. Przejście klawiaturą: kolejne naciśnięcia Tab i odczyt `getComputedStyle(document.activeElement).outlineStyle` przy każdym zatrzymaniu. Reflow: okno 320 px i porównanie `scrollWidth` z `innerWidth`.
