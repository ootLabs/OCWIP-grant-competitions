# Przejścia ręczne GUI: dziennik błędów

Jeden dziennik na wszystkie przejścia ręczne produktu w przeglądarce. Scenariusz, który się klika, jest w [`przejscie-gui.md`](przejscie-gui.md); tutaj są wyniki, przebieg po przebiegu, bo to samo znalezisko bywało wcześniej opisane w dwóch plikach pod dwoma numerami.

| Przebieg | Kiedy | Zakres | Numeracja | Stan |
|---|---|---|---|---|
| 1 | 2026-09-30, Windows 10, Chrome | cały cykl konkursu, ścieżki 0 do N | `B-GUI-01` do `B-GUI-18` | zamknięty 2026-10-03 |
| 2 | 2026-10-02, stos lokalny, front `localhost:3000` | ten sam cykl na świeżej bazie, dodatkowo próby terminów i próby dostępu | znaleziska 1 do 12 i siedem drobnych obserwacji | zamknięty 2026-10-03 |

**Stan na 2026-10-03: oba przebiegi zamknięte.** Każde znalezisko jest poprawione albo rozstrzygnięte jako nieusterka, każda poprawka ma test. Świadomie bez zmiany zostały dwie obserwacje przebiegu 2 (kolor alarmu i powtórne przypomnienie po przesunięciu terminu), a trzy rzeczy okazały się błędem scenariusza, nie produktu, więc poprawiony został scenariusz.

Kilka znalezisk to ten sam defekt widziany dwa razy: `B-GUI-14` i znalezisko 7 (kolumna oceny formalnej), `B-GUI-13` i znalezisko 10 (połknięty komunikat serwera), `B-GUI-10` i obserwacja 7 (data retencji w klauzuli), `B-GUI-07` i obserwacja 1 (powtórzenie nowego hasła). Opis stanu stoi przy wpisie, który był pierwszy.

Numerów nie przenumerowujemy: `B-GUI-xx` i "znalezisko N" są cytowane w komentarzach w kodzie, w [`log.md`](log.md) i w [`architektura.md`](architektura.md).

**Następne przejście** dopisuje na dole własny rozdział ("Przebieg 3"), z własną numeracją i własną kartą wyniku. Zapisów starych przebiegów nie poprawiamy: są dowodem, co produkt robił w danym dniu.

---

## Przebieg 1 · 2026-09-30

Zapis z przejścia scenariusza w przeglądarce. Jeden wpis na jedną porażkę, w formacie z karty wyniku: adres strony, co kliknięto, co się stało, co miało się stać.

Zapis z przejścia scenariusza z [`przejscie-gui.md`](przejscie-gui.md) w przeglądarce. Jeden wpis na jedną porażkę, w formacie z karty wyniku: adres strony, co kliknięto, co się stało, co miało się stać.

Przebieg: 2026-09-30, maszyna deweloperska Windows 10, przeglądarka Chrome.

### Stan przejścia

| Ścieżka | Stan | Uwaga |
|---|---|---|
| 0 przygotowanie stosu | przeszło | stos wstał, `api=200`, `front=200`, pięć kont założonych i potwierdzonych |
| A gość | przeszło z uwagami | B-GUI-02, B-GUI-03, B-GUI-04 |
| B konto | przeszło z uwagami | B1 do B6 przeszły, uwagi: B-GUI-05, B-GUI-06, B-GUI-07 |
| C operator zakłada konkurs | przeszło z uwagami | konkurs 1/2026 opublikowany, uwagi: B-GUI-08, B-GUI-09 |
| D wniosek organizacji | przeszło z uwagami | wniosek 001 złożony, uwagi: B-GUI-10, B-GUI-11, B-GUI-12 |
| E wniosek grupy nieformalnej | przeszło | wniosek 002 złożony, pola warunkowe poprawne |
| F nabór | przeszło z uwagami | lista, eksporty i zwrot do poprawy działają, uwaga: B-GUI-13 |
| G ocena formalna | przeszło z uwagami | oba wnioski pozytywnie, uwaga: B-GUI-14 |
| H ocena merytoryczna | przeszło z uwagami | cztery karty zamknięte, uwaga: B-GUI-15 |
| I rozstrzygnięcie | przeszło | ranking, kwoty, zatwierdzenie, maile bez duplikatów, karty udostępnione |
| J umowa | przeszło z uwagami | umowa sporządzona, ZIP w obu wariantach, podpisanie; uwaga: B-GUI-17 |
| K rezygnacja | przeszło z uwagami | środki przeszły na listę rezerwową; uwaga: B-GUI-16 |
| L sprawozdanie | przeszło z uwagami | złożone, zwrócone, poprawione, przyjęte, rozliczone; uwaga: B-GUI-18 |
| M terminy w tle | przeszło | odcięcie terminu co do minuty, przypomnienie raz na konkurs |
| N izolacja danych | przeszło | wszystkie próby dostępu odbite, log bez haseł i danych |

**Wynik ogólny: cały cykl konkursu przeszedł w jednym przebiegu, na jednej bazie, od pustego systemu do rozliczonej dotacji.** Żadna ścieżka nie została zablokowana. Znaleziska są usterkami wewnątrz działających ścieżek, nie awariami.

### Znaleziska według wagi

**Stan na 2026-10-03: lista zamknięta.** Osiemnaście znalezisk, każde poprawione albo rozstrzygnięte jako nieusterka, każda poprawka z testem. Przegląd kodu po fakcie: 2026-10-03, razem z przejściem przedprodukcyjnym (przebiegu 2 niżej), które te same ścieżki przeszło jeszcze raz i potwierdziło poprawki 2 do 9, 11, 12, 15 i 16.

| Nr | Co | Gdzie | Stan |
|---|---|---|---|
| B-GUI-08 | Po pierwszym zapisie kreator pokazuje pusty formularz i twierdzi, że nie zapisał | kreator konkursu | poprawione: po zapisie prawdziwe przejście na trasę edycji z numerem kroku |
| B-GUI-09 | Tabela ignoruje warunek widoczności, choć walidacja go respektuje | część I wniosku | poprawione: `table-field.tsx` sprawdza `visibleWhen` jak każde inne pole |
| B-GUI-05 | Komunikat o niezaznaczonych zgodach niewidoczny dla czytnika ekranu | rejestracja | poprawione: `aria-invalid` i `aria-describedby` na polach zgód |
| B-GUI-16 | Rezygnacja z dotacji bez okna potwierdzenia | panel oceny | poprawione: `ConfirmDialog` przed zapisem rezygnacji |
| B-GUI-14 | Brak kolumny z wynikiem oceny formalnej na liście wniosków | lista wniosków | poprawione: kolumna na liście i w obu eksportach, opis niżej |
| B-GUI-12 | Mail potwierdzający złożenie podaje godzinę w UTC | mail | poprawione: `ReaderTime` w mailu i w potwierdzeniu PDF, czas polski |
| B-GUI-10 | Klauzula RODO bez daty retencji z ustawień konkursu | część IV wniosku | zamknięte bez zmiany: klauzula odsyła do ogłoszenia, data jest publiczna, opis niżej |
| B-GUI-11 | Odmowa formatu pliku wymienia formaty spoza wymogu | załączniki wniosku | poprawione: odmowa nazywa formaty tego wymogu, także dla formatu spoza katalogu |
| B-GUI-02 | Błąd hydratacji w konsoli na każdej stronie | cała rama publiczna | poprawione: `suppressHydrationWarning` na skrypcie z nonce, test źródeł |
| B-GUI-03 | Logo znika w trybie wysokiego kontrastu | nagłówek | poprawione: `BrandLogo` inline na `currentColor`, pomarańcz tylko na płomieniu |
| B-GUI-15 | Punkty w karcie oceny wyświetlane jako złotówki | karta eksperta | poprawione: jednostka pola wyliczanego z operandów (`computed-unit.ts`) |
| B-GUI-18 | Sprawozdanie pozwala kliknąć złożenie mimo błędu w sekcji | sprawozdanie | poprawione 2026-10-03, opis niżej |
| B-GUI-13 | Pusty zwrot do poprawy odmawia bez podania przyczyny | panel operatora | poprawione: komunikaty pól z serwera na ekranie, opis niżej |
| B-GUI-06 | Surowy markdown w treści zgód | rejestracja | poprawione: `consentBody` zdejmuje nagłówek pliku |
| B-GUI-07 | Reset hasła nie prosi o powtórzenie hasła | reset hasła | poprawione: drugie pole, sprawdzane przy każdej zmianie |
| B-GUI-17 | Etykiety pól umowy bez polskich znaków | umowa | poprawione 2026-10-03, opis niżej |
| B-GUI-04 | Podwójna kropka w deklaracji dostępności | deklaracja dostępności | poprawione: skrót "r." bez drugiej kropki |

### B-GUI-01. Stos lokalny nie wstaje na tej maszynie (rozwiązane)

**Rozwiązane w trakcie przejścia:** po włączeniu wirtualizacji silnik Dockera wstał, `docker compose up -d` podniósł `db`, `backend` i `frontend`, `health/db` i front odpowiadają 200. Wpis zostaje jako zapis warunku wstępnego.


**Gdzie:** krok 0 scenariusza, `docker compose up -d`.

**Co zrobiono:** sprawdzono środowisko, zainstalowano Docker Desktop (29.8.1, katalog użytkownika `%LOCALAPPDATA%\Programs\DockerDesktop`), uruchomiono aplikację.

**Co się stało:** klient Dockera odpowiada, silnik nie. `docker info` zwraca `500 Internal Server Error` z gniazda `dockerDesktopLinuxEngine`, a `wsl -l -v` nie pokazuje żadnej distry. Docker Desktop zgłasza brak wsparcia dla wirtualizacji, więc backend WSL2 nie startuje.

**Co miało się stać:** `docker compose up -d` podnosi `db`, `backend` i `frontend`, `http://localhost:8080/health/db` odpowiada 200, `http://localhost:3000/` odpowiada 200.

**Stan środowiska w chwili próby:**

```
docker client     29.8.1 (poza PATH, katalog użytkownika)
docker engine     nie startuje (brak wirtualizacji)
wsl               brak distry
postgres :5432    nic nie słucha
dotnet SDK        10.0.400 (net10.0 z Ocwip.Api.csproj jest obsłużone)
node              zainstalowany, frontend/node_modules brak
.env              utworzone z .env.example w ramach tego przejścia
```

**To nie jest błąd produktu.** Wpis zostaje, bo blokuje całe przejście i bo warunek wstępny "stos wstaje" jest pierwszą pozycją karty wyniku.

**Odblokowanie:** włączyć wirtualizację w BIOS (VT-x albo AMD-V, przy AMD zwykle "SVM Mode") i włączyć funkcje Windows "Platforma maszyny wirtualnej" oraz "Podsystem Windows dla systemu Linux". Wariant bez Dockera: PostgreSQL 16 natywnie na 5432, backend przez `dotnet run`, frontend przez `npm run dev`. Wirtualizacji nie wymaga, ale komendy ze scenariusza (`docker compose exec`, `docker compose logs`) trzeba wtedy zastąpić natywnymi odpowiednikami.

### B-GUI-02. Błąd hydratacji Reacta na każdej stronie, przez nonce skryptu kontrastu

**Gdzie:** każda strona w ramie publicznej. Potwierdzone na `/`, `/competitions`, `/archive`, `/kontakt`, `/deklaracja-dostepnosci`, `/regulamin`, `/klauzula-informacyjna`.

**Co zrobiono:** wejście na stronę i odczyt konsoli przeglądarki, czego wprost wymaga ścieżka A ("bez ostrzeżeń w konsoli przeglądarki").

**Co się stało:** przy każdym wejściu leci `[ERROR] A tree hydrated but some attributes of the server rendered HTML didn't match the client properties`, a w diagnostyce widać dokładnie jeden rozjeżdżający się atrybut:

```
<script
+   nonce="YmQ3NmY1NDEtN2M2NC00MmUwLTllMGItNTUxNjE4YTJlODYz"
-   nonce=""
    dangerouslySetInnerHTML={{__html:"try{if(JSO..."}}
>
```

Overlay deweloperski Next.js pokazuje to jako stałe "1 Issue" w rogu każdej strony.

**Co miało się stać:** konsola czysta.

**To nie jest rozszerzenie przeglądarki** (komunikat Reacta wymienia taką możliwość, więc sprawdzone): chodzi o własny skrypt aplikacji z `frontend/app/layout.tsx:44`, ten od trybu kontrastu przed pierwszym malowaniem (T-112). Serwer renderuje `nonce` z nagłówka `x-nonce`, a przeglądarka ukrywa wartość `nonce` przed odczytem z DOM (mechanizm CSP), więc React po stronie klienta widzi pusty string i zgłasza rozjazd.

**Dlaczego `suppressHydrationWarning` tego nie łapie:** flaga stoi na `<html>` (`layout.tsx:41`), a dotyczy tylko tego jednego elementu i jego tekstu, nie atrybutów elementów w środku. Ostrzeżenie dotyczy `<script>` w `<head>`.

**Proponowany kierunek poprawki:** `suppressHydrationWarning` wprost na tym `<script>`. Do sprawdzenia przy poprawce: czy ostrzeżenie znika też w buildzie produkcyjnym, bo rozjazd hydratacji nie jest wyłącznie zjawiskiem deweloperskim.

### B-GUI-03. W trybie wysokiego kontrastu znika logo

**Gdzie:** nagłówek każdej strony, po włączeniu "Wysoki kontrast".

**Co zrobiono:** kliknięcie przełącznika kontrastu, oględziny nagłówka, powiększenie obszaru logo.

**Co się stało:** z logo zostaje pomarańczowy płomyk i kropka. Litery "ocwip" i "pl" znikają.

**Co miało się stać:** logo pozostaje czytelne. Tryb wysokiego kontrastu istnieje dla osób słabowidzących, więc element, który w nim znika, jest odwróceniem celu tej funkcji.

**Przyczyna, sprawdzona w pliku:** `frontend/public/ocwip-logo.svg` ma `fill="#EB6209"` tylko na dwóch ścieżkach (płomyk i kropka). Pozostałe ścieżki, czyli cały napis, nie mają atrybutu `fill`, więc przyjmują domyślne SVG, czyli czerń. Paleta kontrastu ustawia tło strony na czarne (`rgb(0, 0, 0)`, sprawdzone w przeglądarce), a czarny napis na czarnym tle nie istnieje.

**Uwaga do zakresu:** `docs/map/frontend.md` opisuje regułę "paleta kontrastu musi nadpisać każdy token poza pomarańczem logo" i test `app/contrast-tokens.test.ts` jej pilnuje. Reguła mówi o tokenach CSS, a kolor liter logo tokenem nie jest, bo siedzi w pliku SVG. Dlatego test przechodzi, a logo i tak znika. Poprawka to albo wariant logo na ciemne tło, albo wymuszenie koloru liter w trybie kontrastu.

### B-GUI-04. Podwójna kropka w deklaracji dostępności

**Gdzie:** `/deklaracja-dostepnosci`, sekcja "Przygotowanie deklaracji dostępności".

**Co się stało:** "Data sporządzenia deklaracji: 29 września 2026 r.."

**Co miało się stać:** jedna kropka. Do kropki ze skrótu "r." dokładana jest kropka zdania.

**Waga:** kosmetyka, ale to dokument czytany przez urzędnika i podlegający kontroli dostępności, więc literówka na widoku jest tu droższa niż gdzie indziej.

### B-GUI-05. Brak zaznaczonych zgód: komunikat jest widoczny, ale niedostępny dla czytnika ekranu

**Gdzie:** `/register`, wysłanie formularza z poprawnymi polami i niezaznaczonymi zgodami.

**Co się stało:** pod formularzem pojawiają się dwa czerwone zdania: "Zaakceptuj: Regulamin serwisu, Klauzula informacyjna o przetwarzaniu danych osobowych." oraz "Popraw zaznaczone pola.". Widzący użytkownik dostaje pełną informację. Ale:

- pierwsze zdanie to zwykły `<p>` bez `role`, bez `aria-live` i bez `id`, więc nic go nie ogłasza i nic go nie wskazuje,
- żaden element go nie referuje przez `aria-describedby` ani `aria-errormessage` (sprawdzone zapytaniem w DOM),
- checkboxy zgód nie dostają `aria-invalid`,
- w regionie `role=alert` siedzi wyłącznie zdanie "Popraw zaznaczone pola.".

**Efekt dla osoby korzystającej z czytnika ekranu:** słyszy "Popraw zaznaczone pola", po czym nie znajduje żadnego zaznaczonego pola, bo zaznaczonego nie ma ani jednego. Rejestracja staje w miejscu bez wskazania przyczyny.

**Co miało się stać:** komunikat o zgodach powiązany z polami (`aria-describedby` albo `aria-errormessage`), checkboxy z `aria-invalid="true"`, a całość ogłoszona w regionie na żywo. Przy porównaniu: pola tekstowe robią to dobrze, bo rozjechany adres i hasło dostają komunikat przy polu ("Adresy e-mail są różne. Sprawdź oba pola.", "Hasła są różne. Wpisz je jeszcze raz.").

**Dlaczego to nie wypadło w testach:** `axe-core` chodzi po każdym renderze (`frontend/vitest.setup.ts`), ale widzi stan spoczynkowy formularza. Ten stan powstaje dopiero po nieudanym wysłaniu.

**Waga:** to WCAG 2.1 AA, punkt 3.3.1 Identyfikacja błędu, a serwis publikuje deklarację dostępności i deklaruje w niej zgodność częściową. Rejestracja jest też najczęstszym miejscem porzucenia, co sam scenariusz podkreśla.

### B-GUI-06. Surowy markdown w treści zgód na ekranie rejestracji

**Gdzie:** `/register`, oba okienka z treścią dokumentów.

**Co się stało:** pierwsza linia każdego dokumentu wyświetla się dosłownie jako `# Regulamin serwisu` i `# Klauzula informacyjna o przetwarzaniu danych osobowych`, razem ze znakiem `#`.

**Co miało się stać:** nagłówek jako nagłówek albo bez znaku `#`. Backend traktuje tę linię jako tytuł dokumentu (`ConsentCatalog` bierze pierwszą linię po `# ` i pokazuje ją w etykiecie "Akceptuję: ..."), więc tytuł jest już podany obok. W okienku zostaje nieprzetworzony znacznik.

**Waga:** kosmetyka, ale to ekran, na którym użytkownik akceptuje dokument prawny, więc wygląd "nieprzetworzonego pliku" obniża zaufanie do treści.

### B-GUI-07. Ekran nowego hasła nie prosi o powtórzenie, ekran rejestracji prosi

**Gdzie:** `/reset-password`.

**Co się stało:** jedno pole "Nowe hasło" i przycisk. Rejestracja i tylko ona wymaga wpisania hasła dwa razy.

**Co miało się stać:** spójność w obrębie produktu. Literówka przy ustawianiu nowego hasła daje konto, do którego użytkownik nie wejdzie, i wymusza kolejny reset, a właśnie z resetu ten człowiek przyszedł.

**Waga:** drobna, ale to niespójność w obrębie jednej ścieżki, a nie kwestia gustu.

### B-GUI-08. Po pierwszym zapisie kreator pokazuje pusty formularz i twierdzi, że nic nie zapisał

**Gdzie:** `/panel/operator/competitions/new`, przycisk "Zapisz" przy zakładaniu konkursu.

**Co zrobiono:** wypełniono krok 1.1 i 1.4, kliknięto "Zapisz".

**Co się stało:** adres zmienia się na `/panel/operator/competitions/<id>/edit`, czyli zapis się udał, ale ekran pokazuje **pusty formularz kroku 1.1** i baner "Konkurs jeszcze nie zapisany.". Dane są w bazie: po ręcznym przeładowaniu tego samego adresu wracają wszystkie wartości, a baner zmienia się na "Edycja konkursu 1/2026, Zapisano jako roboczy 30.09.2026, 16:08:03".

**Co miało się stać:** po zapisie ekran pokazuje zapisany konkurs, jak po przeładowaniu.

**Dlaczego to nie jest kosmetyka.** Operator widzi pusty formularz i komunikat "jeszcze nie zapisany", więc naturalną reakcją jest wypełnić wszystko drugi raz i zapisać. Sprawdzono, co się wtedy dzieje:

- przy **tym samym** numerze konkursu zapis odmawia komunikatem "Inny konkurs ma już ten numer. Numer konkursu musi być niepowtarzalny.". Operator dostaje więc informację o kolizji z konkursem, którego przed chwilą nie zobaczył i o którym nie wie, że istnieje;
- przed powstaniem duplikatu chroni wyłącznie ten warunek niepowtarzalności numeru. Przy innym numerze powstałby drugi konkurs.

**Zakres:** dotyczy tylko **pierwszego** zapisu, czyli utworzenia. Kolejne zapisy na istniejącym konkursie działają poprawnie, baner odświeża godzinę zapisu (sprawdzone przy krokach 1.2, 1.5 i 1.6).

**Uwaga porządkowa:** w bazie po tej próbie został konkurs roboczy `9/2026 - Konkurs probny do testu duplikatu`. Nieopublikowany, więc niewidoczny publicznie. Zostawiony celowo, bo posłużył w C5 do sprawdzenia listy braków przy publikacji.

### B-GUI-09. Tabela w formularzu ignoruje warunek widoczności, choć walidacja go respektuje

**Gdzie:** część I formularza wniosku. Tak samo w podglądzie operatora (`/panel/operator/forms/<id>`) i na prawdziwym wniosku wnioskodawcy (`/panel/applicant/applications/<id>`).

**Co się stało:** pole tabelaryczne "Członkowie grupy nieformalnej", oznaczone gwiazdką jako wymagane, jest widoczne i edytowalne **przy wybranej opcji "A. Młoda albo lokalna organizacja pozarządowa"**, czyli wtedy, gdy żadnej grupy nieformalnej nie ma. Siedzi w środku bloku organizacji, między "Roczny przychód organizacji" a "Gmina, w której będzie realizowany projekt". Widoczne jest też zanim ktokolwiek wybierze rodzaj wnioskodawcy.

**Co miało się stać:** pole znika, tak jak znika sąsiednie "Nazwa grupy nieformalnej realizującej projekt", które ma identyczny warunek.

**Że to nie jest brak w danych, tylko w renderowaniu:** definicja formularza `backend/seed/forms/application-2026.json` ma dla tego pola warunek

```json
{"field": "rodzaj_wnioskodawcy", "equalsAnyOf": ["PatronInformalGroup", "InformalGroup"]}
```

identyczny co do formy z warunkiem pola "Nazwa grupy nieformalnej", które **działa poprawnie**. Działają też warunki w drugą stronę: "Data wpisu do rejestru" i "Roczny przychód organizacji" pojawiają się dopiero po wybraniu organizacji. Różnica między polem, które słucha warunku, a tym, które go ignoruje, to typ pola: tamte są proste, to jest tabelą.

**Widoczność zmierzona, nie wywnioskowana z tekstu:** `display: inline-block`, `visibility: visible`, `tabIndex 0`, wymiary 148x26 piksela, pozycja y=2149, czyli pod zgięciem strony. Zrzut ekranu po przewinięciu pokazuje pełną tabelę z wierszami "Lider grupy", "Członek drugi", "Członek trzeci".

**Walidacja jest po dobrej stronie:** na liście "Zanim złożysz wniosek, uzupełnij" tego pola **nie ma**, mimo że jest oznaczone jako wymagane. Czyli reguła warunkowa jest znana walidatorowi, a nie jest znana warstwie rysującej formularz.

**Skutek dla wnioskodawcy z organizacji:** widzi wymaganą gwiazdką tabelę na trzy osoby z adresami i telefonami, której nie dotyczy i której nie ma po co wypełniać. Albo wypełni ją danymi, które nie powinny powstać (dane osobowe trzech osób zebrane bez podstawy), albo zadzwoni do OCWIP z pytaniem, co tu wpisać.

**Uwaga o rzetelności tego wpisu:** w trakcie przejścia zapisałem najpierw wersję łagodniejszą, że na prawdziwym wniosku pole znika. Była błędna, bo dopasowałem inny element o tej samej treści. Powyższe opiera się na pomiarze geometrii pola i na zrzucie ekranu.

**Że to nie jest brak w danych, tylko w silniku warunków:** definicja formularza `backend/seed/forms/application-2026.json` ma dla tego pola warunek

```json
{"field": "rodzaj_wnioskodawcy", "equalsAnyOf": ["PatronInformalGroup", "InformalGroup"]}
```

identyczny co do formy z warunkiem pola "Nazwa grupy nieformalnej", które **działa poprawnie**. Poprawnie działają też warunki w drugą stronę: "Data wpisu do rejestru" i "Roczny przychód organizacji" pojawiają się dopiero po wybraniu organizacji. Różnica między polem, które słucha warunku, a tym, które go ignoruje, to typ pola: tamte są proste, to jest tabelą.

**Widoczność potwierdzona twardo**, nie samym tekstem: element ma `offsetParent`, `display: block` i niezerowe wymiary przy zaznaczonej opcji A.

**Waga:** nie blokuje złożenia (potwierdzone w D5), ale dotyczy pierwszej sekcji najważniejszej ścieżki produktu i prowadzi do zbierania danych osobowych bez podstawy.

### B-GUI-10. Klauzula RODO we wniosku nie podstawia daty retencji z ustawień konkursu

**Gdzie:** wniosek, część IV. Oświadczenia.

**Co się stało:** klauzula mówi "do daty wskazanej w ogłoszeniu konkursu".

**Co miało się stać:** konkretna data z kroku 1.4 kreatora, czyli 31.12.2032. Krok 1.4 opisuje to pole jako "data usunięcia danych osobowych", a scenariusz wprost zapowiada "klauzula RODO z datą retencji z kroku 1.4".

**Stan faktyczny:** data jest ustawiona i poprawnie widnieje na publicznej stronie konkursu ("Dane osobowe przetwarzane do 31.12.2032"), ale do klauzuli we wniosku nie wchodzi. Wnioskodawca czyta więc klauzulę, która odsyła go do innego dokumentu zamiast podać termin.

**Waga:** informacja o okresie przetwarzania jest obowiązkowym elementem klauzuli informacyjnej, więc to nie jest wyłącznie kwestia wygody.

**Przy okazji, rozbieżność liczbowa:** scenariusz mówi o trzynastu oświadczeniach, formularz 2026 z `backend/seed/forms/application-2026.json` ma ich dziesięć. Nie wiadomo, czy zgubiono trzy, czy scenariusz liczy inaczej. Do rozstrzygnięcia z zamawiającym, bo wzór oferty jest dokumentem zewnętrznym.

**Stan (2026-10-03):** zamknięte bez zmiany w produkcie. Przejście przedprodukcyjne (przebiegu 2 niżej, obserwacja 7) rozstrzygnęło, że klauzula odsyłająca do ogłoszenia jest spójna z datą na publicznej stronie konkursu, a zapowiedź daty wprost w klauzuli była opisem scenariusza, nie wymaganiem. Liczba oświadczeń: dziesięć dla organizacji, osiem dla grupy nieformalnej, scenariusz poprawiony.

### B-GUI-11. Odmowa złego formatu pliku wymienia formaty, których ten wymóg nie dopuszcza

**Gdzie:** wniosek, sekcja "Załączniki", kafelek "Statut".

**Co zrobiono:** próba wgrania pliku `.txt` na kafelek opisany jako "Wymagany · formaty: PDF".

**Co się stało:** "Niedozwolony format pliku. Dozwolone formaty: PDF, DOC, DOCX, XLS, XLSX, JPG, ODT, ODS."

**Co miało się stać:** komunikat wymienia formaty dopuszczone **przez ten wymóg**, czyli sam PDF. Operator ustawił PDF w kroku 1.5 kreatora i tak to jest pokazane dwie linijki wyżej.

**Skutek:** komunikat zachęca wnioskodawcę do wgrania DOCX albo JPG na wymóg, który przyjmuje tylko PDF. Albo zostanie odrzucony drugi raz, albo, jeśli lista globalna wygrywa też przy sprawdzaniu, do konkursu wejdzie statut w formacie, którego operator nie chciał. Nie sprawdzono, która z tych dwóch rzeczy się dzieje, bo wymagałoby to prawdziwego pliku DOCX, a sprawdzanie idzie po zawartości, nie po rozszerzeniu.

### B-GUI-12. Mail potwierdzający złożenie podaje godzinę w UTC

**Gdzie:** mail "Potwierdzenie złożenia oferty".

**Co się stało:** "Data złożenia: 2026-09-30 14:24 UTC", podczas gdy ekran wniosku pokazuje "Wniosek został złożony 30.09.2026, 16:24".

**Co miało się stać:** czas polski, tak jak wszędzie indziej w produkcie. Strona konkursu pisze wprost "do 07.10.2026 o godzinie 23:59 czasu polskiego", a ekran wniosku podaje czas lokalny.

**Skutek:** wnioskodawca dostaje potwierdzenie dwie godziny wcześniejsze niż to, co widział na ekranie. Przy wniosku złożonym tuż przed północą w dniu zamknięcia naboru różnica dotyczy daty, a to jest dokument, którym człowiek dowodzi terminowości.

### B-GUI-13. Pusty formularz zwrotu do poprawy odmawia bez podania przyczyny

**Gdzie:** panel operatora, wniosek, "Zwróć do poprawy".

**Co się stało:** wysłanie formularza bez wskazanych sekcji, bez powodu i bez terminu daje jedno zdanie: "Nie udało się zwrócić wniosku.". Nie wiadomo, czego brakuje ani czy to błąd operatora, czy awaria.

**Co miało się stać:** komunikat przy polu albo wyliczenie braków, tak jak robi to formularz wniosku po stronie wnioskodawcy ("To pole jest wymagane" przy konkretnej pozycji) i ekran rejestracji.

**Waga:** drobna, ale sformułowanie "Nie udało się" sugeruje awarię systemu, a nie brak danych, więc operator zgłosi to jako błąd zamiast poprawić formularz.

**Stan (2026-10-03):** poprawione. `apiErrorMessage` bierze komunikaty pól z `fieldErrors`, gdy odmowa walidacyjna nie ma `detail`, więc ekran pisze "Wskaż co najmniej jedną sekcję. Podaj termin poprawy." zamiast własnego zdania o nieudanej próbie. Dotyczy każdego ekranu, który łapie błąd tym samym pomocnikiem (przebieg 2, znalezisko 10).

### B-GUI-14. Lista wniosków nie ma kolumny z wynikiem oceny formalnej

**Gdzie:** `/panel/operator/applications/<id konkursu>`.

**Co zrobiono:** zakończono ocenę formalną obu wniosków z wynikiem pozytywnym, wrócono na listę.

**Co się stało:** kolumny to `Lp. | Numer wniosku | Nazwa podmiotu | Rodzaj wnioskodawcy | Tytuł projektu | Całkowity koszt zadania | Wnioskowana kwota | Status | Data złożenia`. Wyniku oceny formalnej nie ma nigdzie, a kolumna "Status" dla obu wniosków nadal pokazuje "Złożony".

**Co miało się stać:** scenariusz wymienia kolumny wprost: "numer, nazwa podmiotu, tytuł projektu, całkowity koszt, wnioskowana kwota, status, wynik oceny formalnej", a krok G kończy się zdaniem "Wynik oceny formalnej ma pojawić się w kolumnie na liście wniosków".

**Skutek:** przy naborze na kilkadziesiąt wniosków operator nie widzi na liście, które przeszły ocenę formalną, a które nie, i które w ogóle są ocenione. Musi wchodzić w każdy wniosek osobno.

**Do rozstrzygnięcia:** czy brakuje kolumny, czy zmiany statusu po ocenie formalnej. Dziś nie ma ani jednego, ani drugiego.

**Stan (2026-10-03):** poprawione kolumną. Lista wniosków i jej eksporty (CSV, PDF) mają "Ocena formalna" z wynikiem tej samej reguły co lista rankingowa (`FormalStandingReader`); status wniosku zostaje bez zmian, bo zmienia go rozstrzygnięcie konkursu, nie etap oceny (przebieg 2, znalezisko 7).

### B-GUI-15. W karcie oceny merytorycznej punkty są wyświetlane jako złotówki

**Gdzie:** panel recenzenta, karta oceny merytorycznej, pola liczone.

**Co się stało:** pole "Suma (0-50 punktów)" pokazuje **"44,00 zł"**. To samo dotyczy sumy kryteriów strategicznych, gdzie jeden punkt wyświetla się jako **"1,00 zł"**.

**Co miało się stać:** "44" albo "44 pkt". To są punkty, nie pieniądze.

**Że to nie jest wyłącznie wartość w atrybucie:** potwierdzone zrzutem ekranu, pole renderuje się z napisem "44,00 zł" pod etykietą "Suma (0-50 punktów)".

**Zakres:** nagłówek karty liczy i opisuje to poprawnie ("Suma punktów: 44, kryteria strategiczne: 1, proponowana kwota: 5700,00 zł"), więc błąd jest w formatowaniu pól liczonych, nie w samym liczeniu. Wygląda na to, że pola wyliczane w karcie oceny dziedziczą formatowanie walutowe z pól budżetu we wniosku.

**Waga:** ograniczona do ekranu, na którym ekspert wypełnia kartę. Sprawdzone po udostępnieniu kart: **wnioskodawca widzi punktację poprawnie** ("Suma punktów: 44, kryteria strategiczne: 1, proponowana kwota: 5700,00 zł", punkty przy kryteriach jako 18 i 14, bez nazwisk ekspertów). Usterka myli więc osobę oceniającą, ale nie wychodzi na zewnątrz.

### B-GUI-16. Rezygnacja z dotacji idzie jednym kliknięciem, bez potwierdzenia

**Gdzie:** panel oceny konkursu, sekcja "Umowy, rezygnacje i lista rezerwowa", przycisk "Potwierdź rezygnację 001".

**Co się stało:** jedno kliknięcie przestawia wniosek w stan `Rezygnacja`, zwalnia kwotę z puli i wysyła do wnioskodawcy maila o rezygnacji. Okna potwierdzenia nie ma.

**Co miało się stać:** drugi krok, jak przy każdej innej nieodwracalnej decyzji w tym produkcie. Sprawdzone w tym samym przejściu: publikacja konkursu, zamknięcie naboru, rozpoczęcie oceny, zakończenie karty oceny, zatwierdzenie wyników, udostępnienie kart, zapisanie podpisania umowy i przyjęcie sprawozdania, każde z nich pyta "Wróć / Tak" i tłumaczy skutek.

**Dlaczego to nie jest czepianie się o nazwę przycisku:** słowo "Potwierdź" w etykiecie odnosi się do potwierdzenia rezygnacji zgłoszonej telefonicznie, a nie do potwierdzenia własnego kliknięcia. Skutkiem pomyłki jest mail do wnioskodawcy z informacją, że zrezygnował z dotacji, i przesunięcie pieniędzy do kolejnego podmiotu. Cofnięcie tego wymaga interwencji poza systemem.

### B-GUI-17. Pola umowy mają etykiety wzięte ze znaczników, bez polskich znaków

**Gdzie:** panel operatora, wniosek dofinansowany, sekcja umowy.

**Co się stało:** etykiety pól brzmią "Numer umowy niw", "Data umowy niw", "Termin wydatkow", "Zrodlo danych osobowych".

**Co miało się stać:** "Numer umowy z NIW", "Data umowy z NIW", "Termin wydatków", "Źródło danych osobowych". Interfejs jest po polsku, a te cztery etykiety są nazwami technicznymi znaczników wzoru, z wyciętą polszczyzną.

**Skąd to się bierze:** wzór umowy używa znaczników w rodzaju `{{numer_rachunku}}`, a ekran buduje etykietę z nazwy znacznika. Pozostałe pola ("Rejestr", "Reprezentant", "Osoba kontaktowa") wyglądają dobrze, bo ich nazwy nie mają polskich liter.

**Waga:** kosmetyka, ale widoczna przy każdej umowie i sprzeczna z regułą "UI po polsku" z `AGENTS.md`. Poprawka to słownik etykiet obok listy znaczników.

**Stan (2026-10-03):** poprawione dokładnie tak. `TemplatePlaceholders.BlankLabels` podaje polską pisownię dla tych nazw, których etykieta generowana z nazwy jest zła ("Numer umowy z NIW", "Data umowy z NIW", "Termin wydatków", "Źródło danych osobowych", "E-mail kontaktowy", "Adres lidera grupy"). Nazwa, której w słowniku nie ma, nadal dostaje etykietę z nazwy, więc nowa luka we wzorze OCWIP nie wymaga kodu (D16). Etykiety idą też do `braki.txt` w paczce umów.

### B-GUI-18. Ekran sprawozdania pozwala kliknąć złożenie, choć sekcja ma błąd

**Gdzie:** `/panel/applicant/reports/<id>`.

**Co się stało:** przy sekcji z niespełnionym minimum długości pasek sekcji pokazuje "(są błędy)", ale przycisk "Złóż sprawozdanie" jest **aktywny**, a nad nim nie ma żadnej listy braków. Po kliknięciu pojawia się jeszcze okno potwierdzenia, a dopiero po nim komunikat "Sprawozdania nie da się jeszcze złożyć: Wymagane co najmniej 1000 znaków (jest 208).".

**Co miało się stać:** to samo, co na wniosku, gdzie przycisk jest widoczny i **wyłączony**, a pod nim stoi lista braków, w której każda pozycja jest odnośnikiem do pola.

**Waga:** drobna, bo odmowa jest wyjaśniona i trafia do regionu `aria-live`. Kosztuje jednak dwa zbędne kliknięcia i psuje wrażenie, że produkt zachowuje się tak samo w obu miejscach. Wniosek ustawia tu poprzeczkę, sprawozdanie jej nie sięga.

**Stan (2026-10-03):** poprawione do parzystości z wnioskiem. `ReportWorkspace` liczy braki tym samym `submissionGaps` co wniosek, przycisk "Złóż sprawozdanie" jest wyłączony, dopóki któryś zostaje, a lista pod nim prowadzi kursorem do pola (wspólny `useFieldFocus`). Odmowa serwera zostaje jako druga warstwa: te same reguły, policzone wcześniej, nie zamiast.

### Sprawdzone i bez zastrzeżeń

- `/` strona główna: pusty stan po polsku ("Nie ma teraz otwartego naboru"), zejście do konkursów, archiwum, rejestracji i logowania.
- `/competitions` i `/archive`: pusty stan nazwany po polsku, bez białej strony i bez błędu.
- `/regulamin` i `/klauzula-informacyjna`: renderują się, oznaczone jako wersje robocze do zatwierdzenia, co jest uczciwym stanem przed wdrożeniem, a nie brakiem.
- `/kontakt` i `/deklaracja-dostepnosci`: pełna treść, miejsca do uzupełnienia jawnie opisane jako "do uzupełnienia przez OCWIP", bez nieprawdy.
- Przełącznik kontrastu: działa bez przeładowania strony (znacznik w `window` przeżywa kliknięcie), przestawia `data-contrast` i `aria-pressed`, zapisuje stan w `localStorage` pod `ocwip.contrast` i przeżywa przejście na inną stronę.

Ścieżka B, konto (konto `test.reczny@example.org`, potem po zmianie adresu `test.reczny.nowy@example.org`):

- B1: komunikaty polowe przy rozjechanym adresie i haśle są konkretne. Po wysłaniu "Sprawdź skrzynkę pocztową. Jeśli konto dla tego adresu można założyć...", a powtórzona rejestracja tym samym adresem daje **dosłownie tę samą** odpowiedź. Drugiego maila nie wysłano.
- B2: zepsuty token daje czytelną odmowę po polsku z drogą wyjścia ("Wyślij nowy link"), bez 500 i bez białej strony. Prawidłowy link: "Adres e-mail jest potwierdzony.". Przed potwierdzeniem logowanie odmawia.
- B3: logowanie ląduje w `/panel/applicant`, wylogowanie wraca na `/login`, a wejście na `/panel/applicant` wprost z adresu odsyła na `/login?returnUrl=%2Fpanel%2Fapplicant`.
- B4: `/forgot-password` odpowiada identycznie dla adresu istniejącego i nieistniejącego, a dla nieistniejącego **nie wysyła nic** (w logu zero wystąpień tego adresu). Po resecie nowe hasło wpuszcza, stare odmawia.
- B5: piąta nieudana próba blokuje konto, szósta z poprawnym hasłem też odmawia, komunikatem "Konto zostało tymczasowo zablokowane po kilku nieudanych próbach logowania. Spróbuj ponownie za około 15 min.".
- B6: zmiana hasła wymaga starego, sesja po niej działa dalej. Zmiana adresu wysyła link potwierdzający na **nowy** adres, a na stary idzie osobne powiadomienie "Prośba o zmianę adresu e-mail" z ostrzeżeniem. Po potwierdzeniu logowanie nowym adresem działa.

Ścieżka C, operator (konkurs `1/2026 - Kierunek NOWE FIO 2026 (próba)`, id `88808c46-98c1-4518-b565-f019d9843d5e`):

- C1: przejście do dalszego kroku z niedokończonym poprzednim jest dozwolone, sekcje dostają stan, podsumowanie 1.7 pokazuje komplet z kwotami w złotówkach i datami po polsku.
- C2: `import-content` opublikował formularz i obie karty oceny oraz wzór sprawozdania i umowy jako wersję 1. Powtórzony przebieg odpowiedział "already version 1. Nothing changed." dla każdego pliku, czyli nie zdublował wersji.
- C3: kreator formularza pokazuje zaimportowany formularz 2026 z czterema częściami i zna zależności między polami ("Tego pola używają: ...", z blokadą usunięcia). Podgląd działa.
- C4: wzór załącznika wgrany, widoczny na publicznej stronie konkursu jako "Pobierz wzór: statut-wzor.pdf", z opcjami "Wycofaj wzór" i "Podmień wzór".
- C5: publikacja odmówiona na konkursie bez treści, dokładnie trzema pozycjami ("Brak opublikowanego formularza wniosku.", "Brak karty oceny formalnej.", "Brak karty oceny merytorycznej."). Na konkursie kompletnym publikacja przeszła, konkurs od razu wszedł w stan "Trwa nabór", bo początek naboru był wczoraj, i pojawił się na `/competitions` oraz pod własnym adresem z pełnym ogłoszeniem, limitami, wymaganym załącznikiem i osobą kontaktową.

Ścieżka D, wniosek organizacji (wniosek nr `001`):

- D1: przycisk "Wypełnij wniosek" u niezalogowanego prowadzi na logowanie z `returnUrl`, a po zalogowaniu wraca na tę samą stronę konkursu. Ekran "Co przygotować" zgadza się z ustawieniami konkursu (załącznik Statut jako wymagany PDF, limity rozmiaru, termin, kwota).
- D2: karta podmiotu przyjęła komplet danych organizacji i od razu utworzyła wniosek roboczy.
- D3 autozapis: po wpisaniu treści pojawia się "Zapisano o 16:19", a wyjście na inną stronę i powrót odtwarza wszystkie wartości.
- D3 liczby: sumy, wartość całkowita, wnioskowana kwota i oba procenty są polami tylko do odczytu, liczonymi na bieżąco. Nie ma pola, w które wpisuje się sumę.
- D3 limity: kwota ponad próg daje "Przekroczono dopuszczalną wartość o 2000,00 zł. Maksymalnie 7000,00 zł.", a koszty pośrednie 16,67 procent przy limicie 10 dają "Razem C - Przekroczono dopuszczalną wartość o 400,00 zł. Maksymalnie 600,00 zł.". Po poprawieniu budżetu oba błędy znikają same.
- D3 liczniki znaków: działają ("48 z 200", "204 z 500"), razem z progiem dolnym ("Wymagane co najmniej 1000 znaków (jest 255)").
- D4: zły format odrzucony (uwaga B-GUI-11 dotyczy treści komunikatu, nie samego odrzucenia), PDF przyjęty, kafelek dostaje "Zastąp".
- D5: przycisk "Złóż wniosek" jest widoczny i wyłączony, dopóki jest brak, a każda pozycja listy braków jest przyciskiem prowadzącym do pola. Po uzupełnieniu włącza się. Potwierdzenie jest **jedno**, z ostrzeżeniem "Po złożeniu wniosku nie będzie można go już edytować.". Po złożeniu wniosek ma numer `001`, sumę kontrolną, zamrożoną treść i dwa pliki PDF do pobrania, a w logu backendu jest mail "Potwierdzenie złożenia oferty" z numerem i z tekstem, który operator wpisał w kroku 1.6.

Ścieżka E, grupa nieformalna (wniosek nr `002`):

- Karta podmiotu przy wyborze "Grupa nieformalna" zwija się do jednego pola "Nazwa grupy". Znikają rejestr, NIP, REGON, rachunek organizacji i reprezentanci.
- W części I wniosku znikają pola organizacji (data wpisu do rejestru, roczny przychód), a pojawiają się nazwa grupy, tabela trzech członków i rachunek lidera.
- Tabela członków przyjmuje trzy wiersze z danymi kontaktowymi.
- Oświadczeń jest osiem zamiast dziesięciu, co znaczy, że warunkowe są też oświadczenia.
- Wniosek dał się złożyć bez żadnej danej wymaganej wyłącznie od organizacji.

Ścieżka F, nabór:

- F1: lista pokazuje wyłącznie wnioski złożone, oba, w kolejności numerów, z kompletem kolumn i podsumowaniem ("Suma wnioskowanych kwot 10 200,00 zł", "Pozostało z puli konkursu 9800,00 zł z 20 000,00 zł"). Są filtry po statusie i rodzaju wnioskodawcy.
- F1 eksport CSV: odpowiedź 200, znacznik BOM (czyli Excel otworzy polskie znaki poprawnie), średnik jako separator, polskie znaki w porządku, daty w czasie lokalnym.
- F1 eksport PDF: poprawny plik, obejrzany w przeglądarce. Polskie znaki renderują się prawidłowo ("Sąsiedzka świetlica międzypokol...", "Sąsiedzi z Nysy Łużyckiej", "Podwórko otwarte: zielony kąt"). PDF sam deklaruje "Daty według czasu polskiego". Długie tytuły są ucinane wielokropkiem, co przy stałej szerokości kolumn jest zrozumiałe, ale warto wiedzieć.
- F2 zwrot do poprawy: operator wskazuje sekcje, wpisuje powód i termin. Wnioskodawca widzi stan "Wniosek zwrócony do poprawy", powód, zdanie "Zmienić możesz tylko sekcje: Część III. Budżet. Pozostałe części wniosku są zablokowane.", termin i licznik dni. Zablokowane sekcje nie mają **ani jednego** pola do edycji (sprawdzone zapytaniem w DOM: zero pól formularza w części I).
- F2 mail: "Wniosek 001 zwrócony do poprawy" z powodem, listą sekcji i terminem podanym jako "do 05.10.2026 o godzinie 15:00 czasu polskiego". Ten mail podaje czas polski, w odróżnieniu od maila potwierdzającego złożenie (B-GUI-12).
- F2 po poprawce: numer wniosku bez zmian (`001`), nowa suma kontrolna (`cfde-7904-fc69` wobec pierwotnej `4fce-d154-8e4c`), a u operatora widać wersję 1 z jej sumą i datą zwrotu oraz pełną historię statusów z trzema wpisami.

Ścieżki G i H, ocena:

- G: karta formalna jest warunkowa, organizacja dostaje osiem pytań, grupa nieformalna sześć (odpadają przychód i wpis do rejestru). Przy każdym pytaniu jest uzasadnienie, zakończenie oceny pyta "Po zakończeniu oceny nie będzie można już zmienić karty" i blokuje kartę.
- H1: ustawienia oceny zapisują się ("Zapisano ustawienia oceny."), przyjmują dwóch ekspertów, sumę punktów i próg 50.
- H2: przypisanie zaznaczonych wniosków ekspertowi działa zbiorczo i pojedynczo, tabela ekspertów pokazuje liczbę przypisań i stan deklaracji.
- H3: **przed deklaracją bezstronności ekspert nie widzi treści wniosków**, widzi tylko ich liczbę. Wejście wprost z adresu na konkretny wniosek daje "Nie masz dostępu do tego wniosku.". Po deklaracji widzi listę przypisanych wniosków z sumami. Karta liczy sumę punktów sama, zakończenie jest nieodwracalne.
- H3 izolacja: z konta eksperta `/panel/operator` i `/panel/operator/competitions/<id>` dają 403 z wyjaśnieniem "Ten panel jest dla pracowników OCWIP. Twoje konto ma inne uprawnienia, więc ponowne zalogowanie tego nie zmieni.".

Ścieżka I, rozstrzygnięcie:

- I1: zamknięcie naboru pyta o potwierdzenie i odcina składanie. Publiczna strona konkursu przestaje pokazywać przycisk "Wypełnij wniosek" i pisze "Wniosku nie da się teraz rozpocząć.". Po "Rozpocznij ocenę" konkurs jest w stanie "Trwa ocena".
- I2: lista rankingowa ma miejsce, punkty, kryteria strategiczne, sumę, próg, kwotę wnioskowaną, rekomendowaną i **edytowalną wprost na liście kwotę przyznaną**. Stan puli przelicza się na bieżąco ("Przyznano 5700,00 zł z 20 000,00 zł, zostało 14 300,00 zł"), a po przekroczeniu robi się czerwony i nazywa nadwyżkę ("pula przekroczona o 5000,00 zł"). Eksporty PDF, XLSX i CSV odpowiadają poprawnymi plikami.
- I3: zatwierdzenie wyników ostrzega, co zrobi, i zapisuje "Wyniki zatwierdzono 30.09.2026, 16:45. Kwot nie można już zmieniać.". Wysyłka dała dwa maile: dofinansowany z kwotą i lista rezerwowa z numerem. Ponowne "Wyślij brakujące wiadomości" **nie wysłało nic** (21 maili w logu przed i po).
- I4: udostępnienie kart ostrzega, że decyzji nie można cofnąć. U wnioskodawcy pojawiają się karta formalna i dwie karty merytoryczne, z punktacją i bez nazwisk ekspertów.
- I5: `/competitions/<id>/results` pokazuje dwie tabele, dofinansowane z kwotą i rezerwowe bez kwoty.

Ścieżki J i K, umowa i rezygnacja:

- J1: wzór umowy w wersji 1 przyszedł z importu, ekran rozdziela znaczniki systemowe od tych do wpisania przy każdej umowie.
- J2: umowa sporządzona, PDF ma dziesięć stron, polskie znaki są poprawne, a znaczniki wypełnione zarówno danymi systemowymi (nazwa podmiotu, NIP, kwota, tytuł), jak i wpisanymi przez operatora. Kwota jest też podana słownie ("cztery tysiące czterysta złotych"). Pusta data podpisania drukuje się jako kropki. U wnioskodawcy: "Umowa jest przygotowana do podpisu." i odnośnik do PDF.
- J3: paczka ZIP sprawdzona w obu wariantach. Z jednym pustym polem zawiera **wyłącznie** `braki.txt` o treści "001 Stowarzyszenie Razem dla Opola: Nazwa banku". Po uzupełnieniu pola zawiera `umowa-001.pdf` i nic więcej.
- J4: data z przyszłości jest odrzucona komunikatem "Data podpisania nie może być z przyszłości.". Data dzisiejsza przestawia wniosek w stan "umowa podpisana", a wnioskodawca widzi "Umowa podpisana 30.09.2026.".
- K: panel rezygnacji pokazuje termin podpisania umów (14 dni od ogłoszenia wyników), wolne środki i kolejny wniosek z listy rezerwowej wraz z proponowaną kwotą. Po rezygnacji poszedł mail do rezygnującego, a po przyznaniu środków z rezerwy mail "Dofinansowanie z listy rezerwowej: wniosek 002" z kwotą i terminem. Pula przeliczyła się na 15 600,00 zł wolnych środków.

Ścieżka L, sprawozdanie:

- Formularz sprawozdania działa według zasady "było i jest": wartości z wniosku są pokazane jako tekst, obok wpisuje się wykonanie. Budżet w sprawozdaniu przenosi pozycje z wniosku i pyta o numer dokumentu księgowego.
- Ocena kosztów po stronie operatora przelicza rozliczenie na bieżąco: 100 zł nieuznane dało "Koszty uznane 4300,00 zł" i "Kwota do zwrotu 100,00 zł", z powodem widocznym dla wnioskodawcy.
- Zwrot do poprawy działa razem z powodem, wnioskodawca poprawia i składa ponownie, przyjęcie ostrzega o skutkach i przestawia wniosek w stan "Rozliczony".

Ścieżka M, terminy i zadania w tle (na osobnych konkursach próbnych `2/2026` i `3/2026`, oba zrobione przez "Skopiuj konkurs", co zalicza też C6):

- M1, twarde odcięcie terminu: wniosek kompletny i gotowy do złożenia o 17:05, koniec naboru o 17:12. O 17:13, **bez przeładowania strony**, czyli z ekranem, który nadal twierdził "Nabór trwa", kliknięcie złożenia zostało odrzucone komunikatem "Nabór został zamknięty 30.09.2026 o godzinie 17:12 czasu polskiego. Wniosku nie można już złożyć.". Odcięcie działa co do minuty i po stronie serwera, a nie tylko w interfejsie. Wersja robocza została nietknięta.
- M1 licznik: przed terminem ekran pokazywał "Do zamknięcia naboru pozostało mniej niż minuta".
- M2, przypomnienie: konkurs z końcem naboru za niecałe trzy dni i rozpoczętym, niezłożonym wnioskiem. Zadanie w tle wysłało w ciągu minuty mail "Przypomnienie: nabór ... kończy się 03.10.2026 o godzinie 17:04 czasu polskiego" z odnośnikiem do wniosku i zdaniem "Wersja robocza nie jest wnioskiem".
- M2 bez duplikatów: po ośmiu przebiegach zadania w logu są dokładnie dwa maile przypominające, po jednym na **każdy z dwóch** konkursów próbnych, oba do jedynej osoby z rozpoczętym i niezłożonym wnioskiem. Żaden konkurs nie przypomniał dwa razy.

Ścieżka N, izolacja danych. Wszystkie próby zakończone odmową, żadna nie pokazała cudzej treści:

| Próba | Wynik |
|---|---|
| konto `grupa@example.org` otwiera wniosek organizacji | "Nie ma takiego wniosku. Ten adres nie prowadzi do żadnego z Twoich wniosków." |
| wnioskodawca otwiera `/panel/operator` | 403 z wyjaśnieniem, że konto ma inne uprawnienia |
| wnioskodawca otwiera `/panel/reviewer` | 403 z wyjaśnieniem |
| ekspert otwiera `/panel/operator` i konkretny konkurs operatora | 403 w obu przypadkach |
| ekspert otwiera wniosek, którego nie ma przypisanego | "Nie masz dostępu do tego wniosku." |
| ekspert przed złożeniem deklaracji otwiera przypisany wniosek wprost z adresu | "Nie masz dostępu do tego wniosku." |
| wylogowany otwiera adres w `/panel/...` | przekierowanie na `/login?returnUrl=...` |
| wylogowany otwiera nieopublikowany konkurs | "Nie ma takiej strony" |
| pobranie cudzego załącznika wprost z adresu API | 403, "Nie masz dostępu do tego załącznika." |

Log backendu sprawdzony osobno: nie ma w nim żadnego hasła ani treści wniosku, a parametry zapytań SQL są zamaskowane jako `?`. Słowa `pesel` i `password_hash` występują wyłącznie jako **nazwy kolumn** w treści zapytań, bez wartości.

**Sprawdzone osobno, bo wyglądało na wyciek, i okazało się poprawne:** konto z niepotwierdzonym adresem dostaje przy logowaniu inny komunikat ("Potwierdź swój adres e-mail, zanim się zalogujesz") niż adres nieznany ("Nieprawidłowy e-mail lub hasło"). Ten komunikat pada jednak **tylko przy poprawnym haśle**, przy złym wraca odpowiedź generyczna. Kto zna hasło, ten i tak wie, że konto istnieje, więc reguła "nie ujawniamy, czy konto istnieje" nie jest tu naruszona.

### Narzędzia tego przejścia

- `create_test_users.py` w katalogu głównym (lokalny, w `.gitignore`): zakłada pięć kont ze scenariusza przez prawdziwe ścieżki produktu, czyli `POST /register` z obiema zgodami, odczyt linku z maila w logu backendu, `POST /verify-email`, na koniec `grant-role` dla operatora i dwóch recenzentów. Kolejne wywołania są rozłożone w czasie, bo trasy konta mają limit 10 żądań na minutę.
- `.env` skopiowane z `.env.example`, bez zmian.

### Czego tym przejściem nie sprawdziłem

Żeby nikt nie liczył, że to zostało odhaczone:

- **Kolumna "wyszarzone zakładki" w stanie "trwa nabór"** (F1, ostatni akapit): zakładki konkursu bez treści miały być wyszarzone z wyjaśnieniem, a nie ukryte. Nie sprawdzone osobno.
- **Edycja formularza w kreatorze** (C3, druga część): dodanie sekcji testowej, przesunięcie i usunięcie. Świadomie pominięte, bo scenariusz ostrzega, że to psuje dalsze kroki, a przejście robiłem jednym ciągiem na jednej bazie.
- **Kolumna "Uwagi do decyzji"** na liście rankingowej: widoczna, ale nie wypełniana.
- **Podmiana załącznika przeciągnięciem myszą** (D4): podmiana sprawdzona przez pole wyboru pliku, samo przeciąganie nie.
- **Prawdziwa wysyłka maili, HTTPS, kopie zapasowe, retencja, aneksy, oferty wspólne**: poza zakresem tego przejścia, tak jak mówi sam scenariusz.

### Stan bazy po przejściu

Przejście zostawiło dane, które warto znać przed kolejnym uruchomieniem:

| Co | Stan |
|---|---|
| Konkurs `1/2026` | Archiwalny, rozstrzygnięty, wniosek 001 w stanie Rezygnacja, wniosek 002 rozliczony |
| Konkurs `2/2026` | Trwa nabór zakończony 30.09.2026 17:12, jeden wniosek roboczy niezłożony (próba terminu) |
| Konkurs `3/2026` | Trwa nabór do 03.10.2026, jeden wniosek roboczy niezłożony (próba przypomnienia) |
| Konkurs `9/2026` | Roboczy, bez treści, powstał przy próbie duplikatu z B-GUI-08 |
| Konta | pięć ze skryptu plus `test.reczny.nowy@example.org` z przejścia ścieżki B (hasło zmienione dwa razy, konto po blokadzie) |

Kasowanie tego nie jest potrzebne do dalszej pracy, ale przy powtórce całego scenariusza czystsza jest świeża baza (`docker compose down -v`, co kasuje też wolumeny).

### Jak powtórzyć to przejście

```powershell
# Docker Desktop siedzi w katalogu użytkownika, nie na PATH
$env:Path += ";$env:LOCALAPPDATA\Programs\DockerDesktop\resources\bin"
docker compose up -d
curl.exe -s -o NUL -w "api=%{http_code}`n" http://localhost:8080/health/db   # 200 po minucie albo dwóch
curl.exe -s -o NUL -w "front=%{http_code}`n" http://localhost:3000/          # 200

python create_test_users.py
```

Import treści startowej w kroku C2 wymaga na Windows wyłączenia konwersji ścieżek w Git Bashu, inaczej `/src/seed/...` zamienia się w `C:/Program Files/Git/src/seed/...` i komenda kończy się pięcioma błędami "could not be read":

```bash
MSYS_NO_PATHCONV=1 docker compose exec -T backend dotnet run --project src/Ocwip.Api/Ocwip.Api.csproj \
  --no-launch-profile -- import-content --competition <ID> --application /src/seed/forms/application-2026.json ...
```

To nie jest usterka produktu, tylko właściwość powłoki, ale kosztuje kwadrans, jeśli się o tym nie wie. Warto dopisać tę uwagę do [`przejscie-gui.md`](przejscie-gui.md).

---

## Przebieg 2 · 2026-10-02

Stos lokalny, front `http://localhost:3000`, backend `http://localhost:8080`. Stan startowy: `health/db` 200, front 200, w bazie zero konkursów i zero wniosków, sześć kont (pięć z `create_test_users.py`).

Przejście objęło ścieżki od A do N na jednej bazie, w jednym przebiegu: dwa wnioski (organizacja i grupa nieformalna), zwrot do poprawy, ocena formalna, dwie karty merytoryczne na wniosek, rozstrzygnięcie, umowa, rezygnacja z wejściem z listy rezerwowej, sprawozdanie z rozliczeniem, próby terminów i próby dostępu.

Format wpisu: adres, co zrobione, co się stało, co miało się stać, a od poprawek także **Stan**.

---

### Znaleziska

#### 1. Hydration mismatch na stronach publicznych (ścieżka A)

- **Adres:** każda strona publiczna, sprawdzone na `/competitions`, `/kontakt`, `/login`.
- **Co zrobione:** wejście na stronę bez konta, konsola przeglądarki.
- **Co się stało:** React zgłasza błąd `A tree hydrated but some attributes of the server rendered HTML didn't match the client properties`. Różni się wartość `nonce` na skrypcie wstrzykiwanym w `<head>` przez `RootLayout`: serwer renderuje `nonce="OWM0Yzk5YWQ..."`, klient widzi `nonce=""`. To skrypt trybu wysokiego kontrastu (`try{if(JSO...`).
- **Co miało się stać:** scenariusz wymaga stron publicznych "bez ostrzeżeń w konsoli przeglądarki".
- **Zasięg:** sam przełącznik kontrastu działa poprawnie, więc to nie psuje funkcji, ale zaśmieca konsolę na każdej stronie i w produkcji oznacza ponowne renderowanie drzewa po stronie klienta.
- **Stan:** Poprawione: skrypt trybu kontrastu dostaje nonce po stronie klienta tak samo jak na serwerze, więc drzewo się nie rozjeżdża.

#### 2. Komunikat "Hasła są różne" nie znika po poprawieniu pola (ścieżka B1)

- **Adres:** `/register`.
- **Co zrobione:** wysłanie formularza z różnym powtórzeniem adresu i różnym powtórzeniem hasła, potem poprawienie obu pól (zaznaczenie zawartości, wpisanie poprawnej wartości, wyjście z pola klawiszem Tab).
- **Co się stało:** komunikat przy adresie e-mail zniknął po poprawieniu, a komunikat `Hasła są różne. Wpisz je jeszcze raz.` został pod polem mimo zgodnych wartości (sprawdzone w DOM: oba pola identyczne). Zniknął dopiero po wysłaniu formularza.
- **Co miało się stać:** błąd znika, gdy pole jest poprawione. Scenariusz stawia to wprost jako obietnicę produktu: "błąd pojawia się tylko wtedy, gdy naprawdę jest, i znika, gdy poprawisz".
- **Waga:** drobne, ale myli akurat przy haśle, bo użytkownik nie widzi wpisanej treści i nie ma jak sprawdzić, czy komunikat mówi prawdę.
- **Stan:** Poprawione: oba powtórzenia (rejestracja, nowe hasło z linku) są przeliczane przy każdej zmianie pola od pierwszego wysłania, więc komunikat znika po poprawieniu któregokolwiek z dwóch pól i wraca, gdy znów się rozjadą. Testy: `register-form.test.tsx`, `reset-password-form.test.tsx`.

#### 3. Po zapisie kreator pokazuje pusty formularz i komunikat "Konkurs jeszcze nie zapisany" (ścieżka C1)

- **Adres:** `/panel/operator/competitions/new`, po zapisie `/panel/operator/competitions/<id>/edit`.
- **Co zrobione:** wypełnienie kroków od 1.1 do 1.6 kreatora, potem przejście na krok 1.7 Podsumowanie.
- **Co się stało:** adres zmienił się na `/panel/operator/competitions/45c605da-711e-4958-9cbe-25c37201f3b3/edit`, czyli konkurs został zapisany, ale ekran wrócił do kroku 1.1 z **pustymi polami** i nagłówkiem `Konkurs jeszcze nie zapisany.` Żadna z wpisanych danych nie była widoczna.
- **Stan faktyczny:** dane są w bazie. Wiersz w `competitions`: `number = 1/2026`, `title = Kierunek NOWE FIO 2026 (próba)`, `start_date = 2026-10-01 06:00+00`, `end_date = 2026-10-09 21:59+00`, `status = Draft`. Po ręcznym przeładowaniu strony kreator pokazuje komplet danych i banner `Zapisano jako roboczy 2.10.2026, 17:56:52.`
- **Co miało się stać:** po zapisie kreator zostaje na tym samym kroku z wpisanymi danymi i mówi, że zapisał. Scenariusz stawia to wprost: "zapis w trakcie i powrót nie gubi wpisanych danych".
- **Waga:** wysoka. To nie jest utrata danych, tylko jej pozór, a to gorsze: operator widzi pusty formularz i komunikat "nie zapisany", więc naturalną reakcją jest wpisanie wszystkiego jeszcze raz, co kończy się drugim konkursem albo kolizją numeru.
- **Stan:** Poprawione: po pierwszym zapisie kreator zostaje na swoim kroku z wpisanymi danymi i mówi, że zapisał.

#### 4. Tabela "Członkowie grupy nieformalnej" nie znika przy wnioskodawcy będącym organizacją (ścieżki D3 i E)

- **Adres:** `/panel/applicant/applications/<id>`, Część I. Dane wnioskodawcy.
- **Co zrobione:** w polu "Wniosek składa" wybrana opcja `A. Młoda albo lokalna organizacja pozarządowa`.
- **Co się stało:** pojawiły się poprawnie pola dla organizacji (rodzaj organizacji, data wpisu do rejestru, roczny przychód), ale **tabela `Członkowie grupy nieformalnej *` została na ekranie**, razem z gwiazdką, dopiskiem `(wymagane)` i trzema wierszami na lidera i dwóch członków.
- **Co miało się stać:** pole ma zniknąć, tak jak znikają pozostałe pola grupy. Sprawdzone w tym samym przebiegu: `Nazwa grupy nieformalnej` oraz `Numer rachunku bankowego lidera grupy` **znikają poprawnie**.
- **Przyczyna, z definicji formularza:** w `backend/seed/forms/application-2026.json` pola `nazwa_grupy` (typ `shortText`) i `czlonkowie_grupy` (typ `fixedTable`) mają **identyczny** warunek `visibleWhen: {field: rodzaj_wnioskodawcy, equalsAnyOf: [PatronInformalGroup, InformalGroup]}`. Znika tylko pierwsze z nich, więc renderer nie stosuje `visibleWhen` do pól typu `fixedTable`.
- **Zasięg:** walidacja jest poprawna, pole nie trafia na listę braków i nie blokuje złożenia. Problem jest w tym, co widzi człowiek: organizacja dostaje do wypełnienia wymaganą tabelę danych osobowych trzech osób, która jej nie dotyczy.
- **Stan:** Poprawione: renderer stosuje `visibleWhen` także do tabel o stałej liczbie wierszy, więc tabela członków znika razem z pozostałymi polami grupy.

#### 5. Odmowa złego formatu załącznika podaje formaty, których ten załącznik nie przyjmuje (ścieżka D4)

- **Adres:** `/panel/applicant/applications/<id>`, sekcja Załączniki, wymóg `Statut` ustawiony przez operatora na sam PDF.
- **Co zrobione:** próba wgrania pliku `notatka.txt`, czyli formatu spoza katalogu w ogóle.
- **Co się stało:** komunikat `Niedozwolony format pliku. Dozwolone formaty: PDF, DOC, DOCX, XLS, XLSX, JPG, ODT, ODS.`
- **Co miało się stać:** komunikat ma podać formaty dopuszczone **dla tego wymogu**, czyli sam PDF, tak jak kafelek obok ("Wymagany, formaty: PDF").
- **Dowód, że to niespójność, a nie tylko niezręczne zdanie:** próba wgrania `skan-statutu.jpg`, czyli formatu z tej wypisanej listy, kończy się odmową z poprawnym komunikatem `Załącznik "Statut" przyjmuje tylko: PDF.` System zna właściwe ograniczenie, ale pierwszy komunikat go nie używa.
- **Waga:** średnia. Wnioskodawca, który trafi na pierwszy komunikat, pójdzie zrobić DOCX i wróci po drugiej odmowie. To dokładnie ten telefon do OCWIP, którego scenariusz każe unikać.
- **Stan:** Poprawione: odmowa wymienia formaty dopuszczone dla tego wymogu, a nie cały katalog.

#### 6. Potwierdzenie złożenia podaje godzinę w UTC, reszta produktu w czasie polskim (ścieżka D5)

- **Adres:** PDF z `http://localhost:8080/applications/<id>/confirmation` oraz mail "Potwierdzenie złożenia oferty" w logu backendu.
- **Co się stało:** wniosek złożony o 18:16 czasu polskiego. Ekran wnioskodawcy mówi `Wniosek został złożony 02.10.2026, 18:16.`, pełny wniosek w PDF mówi `Data złożenia: 2026-10-02 18:16 (czasu polskiego)`, ale **potwierdzenie w PDF** mówi `Data złożenia (UTC): 2026-10-02 16:16`, a mail do wnioskodawcy `Data złożenia: 2026-10-02 16:16 UTC`.
- **Co miało się stać:** jedna godzina w całym produkcie, w czasie polskim, bo to ten czas rozstrzyga o dotrzymaniu terminu naboru (`Nabór trwa do 09.10.2026 o godzinie 23:59 czasu polskiego`).
- **Waga:** niska technicznie, wyższa prawnie. Potwierdzenie złożenia to dokument, którym wnioskodawca dowodzi, że złożył wniosek w terminie, a ten dokument podaje inną godzinę niż ogłoszenie konkursu. Przy wniosku złożonym tuż przed północą różnica dwóch godzin przenosi datę na dzień wcześniejszy.
- **Uwaga:** oznaczenie `(UTC)` jest obecne, więc to niespójność, nie błąd rachunkowy.
- **Stan:** Poprawione: potwierdzenie w PDF i mail podają godzinę w czasie polskim, tak jak ekran i pełny wniosek.

#### 7. Lista wniosków nie ma kolumny z wynikiem oceny formalnej (ścieżki F1 i G)

- **Adres:** `/panel/operator/applications/<id konkursu>`.
- **Co zrobione:** obie oceny formalne zakończone wynikiem "spełnia wymogi formalne", potem powrót na listę wniosków.
- **Co się stało:** nagłówki tabeli to `Lp. | Numer wniosku | Nazwa podmiotu | Rodzaj wnioskodawcy | Tytuł projektu | Całkowity koszt zadania | Wnioskowana kwota | Status | Data złożenia`. Kolumny z wynikiem oceny formalnej **nie ma**, a kolumna `Status` pokazuje dalej `Złożony` dla obu wniosków.
- **Co miało się stać:** scenariusz wymienia wynik oceny formalnej wśród kolumn listy wniosków (F1) i mówi wprost: "Wynik oceny formalnej ma pojawić się w kolumnie na liście wniosków" (G).
- **Gdzie wynik jednak jest:** na liście rankingowej w `/panel/operator/evaluation/<id konkursu>`, w kolumnie `Ocena formalna`.
- **Waga:** niska do średniej. Dane są w systemie, brakuje ich w tym jednym widoku, więc operator pracujący z listy wniosków musi przeskakiwać do listy rankingowej.
- **Stan:** Poprawione: `ApplicationListItem` niesie wynik oceny formalnej (`FormalStanding`, ta sama reguła co na liście rankingowej, `FormalStandingReader`), lista ma kolumnę "Ocena formalna" z sortowaniem od nierozpoczętych, a CSV i PDF tę samą kolumnę. Testy: `ApplicationListEndpointsTests`, `ApplicationListExportTests`, `operator/applications/[competitionId]/page.test.tsx`.

#### 8. Suma punktów na karcie merytorycznej jest wyświetlana jako kwota w złotych (ścieżka H3)

- **Adres:** `/panel/reviewer/applications/<id>`, karta oceny merytorycznej, Część I.
- **Co zrobione:** wpisanie punktacji 18 + 14 + 9 + 3.
- **Co się stało:** pole podsumowania z etykietą `Suma (0-50 punktów)` pokazuje **`44,00 zł`**. Nagłówek karty liczy poprawnie i pisze `Suma punktów: 44, kryteria strategiczne: 0.`, więc arytmetyka jest dobra, złe jest formatowanie pola wyliczanego.
- **Co miało się stać:** `44` albo `44 z 50 punktów`. Punkty nie są kwotą.
- **To samo w drugim miejscu tej samej karty:** pole `Punkty za kryteria strategiczne` w Części II pokazuje `1,00 zł` zamiast `1`.
- **Przyczyna prawdopodobna:** pole wyliczane w karcie dziedziczy formatowanie walutowe po polach wyliczanych w budżecie wniosku, gdzie `zł` jest poprawne.
- **Waga:** niska funkcjonalnie, wyższa wizerunkowo: po udostępnieniu kart tę samą kartę widzi wnioskodawca, a na niej jego ocena merytoryczna stoi w złotówkach obok prawdziwej kwoty dotacji. Sprawdzone, że wnioskodawca faktycznie widzi `44,00 zł`.
- **Stan:** Poprawione: pole wyliczane w karcie oceny drukuje punkty jako punkty, nie jako kwotę.

#### 9. Potwierdzenie rezygnacji działa od razu, bez pytania o potwierdzenie (ścieżka K)

- **Adres:** `/panel/operator/evaluation/<id konkursu>`, panel "Umowy, rezygnacje i lista rezerwowa".
- **Co zrobione:** jedno kliknięcie w `Potwierdź rezygnację 001`.
- **Co się stało:** wniosek od razu przeszedł w stan `Resigned`, środki wróciły do puli (z 14 300 zł na 20 000 zł), a do wnioskodawcy poszedł mail "Rezygnacja z dotacji: wniosek 001". **Żadnego okna potwierdzenia nie było.**
- **Co miało się stać:** decyzja tej wagi zachowuje się jak pozostałe decyzje nieodwracalne w tym produkcie. Dla porównania, w tym samym przebiegu pytanie o potwierdzenie pojawiło się przy: publikacji konkursu, zamknięciu naboru, rozpoczęciu oceny, zatwierdzeniu wyników, udostępnieniu kart, archiwizacji, złożeniu wniosku, złożeniu sprawozdania, zapisaniu podpisania umowy i zakończeniu każdej karty oceny. Rezygnacja jest jedyną akcją tej wagi bez potwierdzenia.
- **Skutek pomyłki:** cofnięcia w interfejsie nie ma. Wnioskodawca dostaje mail o rezygnacji, której nie zgłaszał, a przyznana kwota wraca do puli.
- **Drobiazg przy okazji:** po rezygnacji, gdy nie ma już żadnego dofinansowanego wniosku, panel pisze `Wszystkie dofinansowane wnioski mają podpisaną umowę.`, co jest logicznie prawdziwe i mylące w praktyce.
- **Stan:** Poprawione: potwierdzenie rezygnacji przechodzi przez okno potwierdzenia, jak pozostałe decyzje nieodwracalne.

#### 10. Przyznanie z listy rezerwowej gubi komunikat błędu z serwera (ścieżka K)

- **Adres:** `/panel/operator/evaluation/<id konkursu>`, przycisk `Przyznaj dofinansowanie 002`.
- **Co zrobione:** próba przyznania kwoty większej niż wolne środki w puli.
- **Co się stało:** interfejs napisał tylko `Nie udało się przyznać dofinansowania.` Bez przyczyny i bez podpowiedzi, co zrobić.
- **Co backend faktycznie odpowiada:** `400` z treścią `{"errors":{"awardedGrant":["W puli zostało 20000,00 zł. Kwota nie może być większa."]}}`, czyli gotowy, polski, konkretny komunikat. Interfejs go wyrzuca.
- **Co miało się stać:** komunikat z serwera pod polem kwoty, tak jak działa to w budżecie wniosku u wnioskodawcy ("Przekroczono dopuszczalną wartość o 1000,00 zł. Maksymalnie 7000,00 zł.").
- **Uwaga:** ten sam wzorzec, czyli połknięty komunikat serwera, nie powtórzył się przy składaniu wniosku po terminie. Tam odmowa `409` jest pokazana w całości.
- **Stan:** Poprawione: `apiErrorMessage` bierze komunikaty pól z `fieldErrors`, gdy odmowa walidacyjna nie ma `detail`, więc operator czyta zdanie backendu o pozostałej puli. Testy: `api-client.test.ts`, `resignation-panel.test.tsx`.

#### 11. Umowa grupy nieformalnej wymaga rejestru i numeru w rejestrze (ścieżki J i K)

- **Adres:** `/panel/operator/evaluation/<id konkursu>/<id wniosku>`, sekcja Umowa, wniosek 002 złożony przez **grupę nieformalną bez osobowości prawnej**.
- **Co zrobione:** przygotowanie umowy i wypełnienie wszystkich pól, które dla grupy mają sens (reprezentant, funkcja, dane NIW, kontakt, terminy, rachunek lidera, bank, źródło danych).
- **Co się stało:** umowa nie weszła do paczki ZIP, a `braki.txt` wymienił: `002 Grupa Sąsiedzka Zaodrze: Rejestr, Numer w rejestrze`. Formularz umowy pokazuje grupie ten sam komplet czternastu pól co organizacji, w tym `Rejestr`, `Numer w rejestrze` i `Funkcja reprezentanta`.
- **Co miało się stać:** lista pól do wpisania zależy od rodzaju wnioskodawcy, tak jak zależy od niego formularz wniosku (znikają pola organizacji) i karta oceny formalnej (sześć kryteriów zamiast ośmiu). Grupa nieformalna nie ma rejestru ani numeru w rejestrze.
- **Obejście użyte, żeby przejść dalej:** wpisanie `nie dotyczy` w oba pola. To znaczy, że operator wpisuje nieprawdę do umowy, żeby system pozwolił ją wydać.
- **Przy okazji:** pole `Adres wnioskodawcy` w umowie grupy jest puste i drukuje się jako kropki, bo karta podmiotu grupy nieformalnej ma tylko nazwę. Adres lidera jest we wniosku, ale do umowy nie trafia.
- **Waga:** średnia. Blokuje wydanie umowy grupie nieformalnej, a grupy są jedną z dwóch grup docelowych konkursu.
- **Stan:** Poprawione w części blokującej: wzór umowy zna fragment dla wybranych rodzajów wnioskodawcy (`{{#Organisation,PatronInformalGroup}} ... {{/}}`), więc grupa nieformalna nie jest już pytana o rejestr, numer w rejestrze, NIP ani funkcję reprezentanta i da się jej umowę wydać bez wpisywania "nie dotyczy". Adresu grupy system nadal nie zna (karta podmiotu grupy ma tylko nazwę, a kolumn tabeli członków nic nie oznacza rolą), więc wzór 2026 pyta o `{{adres_lidera}}` jako pole do wpisania: **pytanie do zamawiającego**, czy adres lidera ma być polem wniosku zaciąganym do umowy, zapisane jako P21 w [`runbook/decyzje.md`](runbook/decyzje.md) i jako założenie ZR-19 w [`runbook/zalozenia-robocze.md`](runbook/zalozenia-robocze.md). Zmiana wzoru dotyczy pliku startowego, więc konkurs z zaimportowanym wzorem potrzebuje nowej wersji wzoru (ekran wzoru umowy albo `import-content --contract`). Testy: `TemplatePlaceholdersTests`, `Contract2026TemplateTests`, `ContractEndpointsTests`.

#### 12. Zwrot i przyjęcie sprawozdania nie wysyłają maila do wnioskodawcy (ścieżka L2)

- **Adres:** `/panel/operator/evaluation/<id konkursu>/reports/<id sprawozdania>`.
- **Co się stało przy zwrocie:** sprawozdanie przeszło w stan `Zwrócone do poprawy`, powód jest zapisany i widoczny u operatora oraz u wnioskodawcy w panelu, ale **w logu backendu nie pojawił się żaden mail**.
- **To samo przy przyjęciu:** przyjęcie sprawozdania (stan `Przyjęte`, wniosek przechodzi w `Settled`, kwota do zwrotu 100,00 zł) również nie wysyła maila. Wnioskodawca nie dowiaduje się ani o przyjęciu rozliczenia, ani o tym, że ma coś zwrócić.
- **Co miało się stać:** tak samo jak przy zwrocie **wniosku** do poprawy, gdzie system wysyła `Wniosek 001 zwrócony do poprawy` z powodem i terminem. Zwrot sprawozdania to ta sama sytuacja: piłka jest po stronie wnioskodawcy i on musi się o tym dowiedzieć.
- **Skutek:** wnioskodawca dowie się o zwrocie tylko wtedy, gdy sam zajrzy do panelu. Przy sprawozdaniu, które składa się raz na kilka miesięcy, to znaczy, że nie dowie się wcale.
- **Stan:** Poprawione: zwrot sprawozdania wysyła mail z powodem, a przyjęcie mail z kwotą do zwrotu, oba na adres konta, które sprawozdanie złożyło (`ReportService.Mail.cs`). Test: `ReportEndpointsTests`.

---

### Drobne obserwacje

Rzeczy, które nie blokują niczego i nie są błędami w ścisłym sensie, ale ktoś powinien o nich wiedzieć przed wystawieniem na serwer.

1. **Reset hasła bez powtórzenia.** `/reset-password` ma jedno pole `Nowe hasło`, bez `Powtórz hasło`, choć rejestracja i zmiana adresu takiego powtórzenia wymagają. Literówka w nowym haśle kończy się kolejnym resetem.
   **Stan:** poprawione, ekran pyta o nowe hasło dwa razy.
2. **Adres e-mail w parametrze adresu URL.** Link potwierdzający zmianę adresu ma postać `/confirm-email-change?userId=...&email=test.reczny3%40example.org&token=...`. Adres trafia do historii przeglądarki i do logów każdego pośrednika, a `userId` z tokenem by wystarczyły.
   **Stan:** poprawione, adres czeka na koncie (`users.pending_email`), link niesie samo `userId` i `token`, a druga prośba o zmianę unieważnia starszy link. Test: `AccountSettingsTests`.
3. **Przekroczenie puli na liście rankingowej jest w kolorze akcentu**, `rgb(159, 58, 12)`, a nie w czerwieni, której oczekuje scenariusz. Komunikat jest czytelny, ale nie czyta się jako alarm.
   **Stan:** świadomie bez zmiany. Produkt ma jeden kolor błędu z palety OCWIP, sprawdzony na kontrast w obu motywach (`contrast-tokens.test.ts`), a osobna czerwień wymagałaby drugiej pary kolorów i odpowiednika w trybie wysokiego kontrastu, gdzie czerwień na czarnym nie przechodzi AA. Informacja nie stoi na kolorze: komunikat nazywa kwotę przekroczenia i jest pogrubiony (`dostepnosc.md`, 1.4.1). Zmiana palety to decyzja dla zamawiającego, nie poprawka.
4. **Dwa formaty kwot.** Interfejs pisze `20 000,00 zł`, komunikat walidacyjny z backendu `20000,00 zł`.
   **Stan:** poprawione w zdaniach, które czyta człowiek (odmowa kwoty ponad pulę, mail o wyniku, mail o przyznaniu z listy rezerwowej, mail o przyjęciu sprawozdania): idą przez `PolishNumbers.Amount`, czyli tak jak ekrany. CSV i XLSX zostają bez separatora tysięcy świadomie, żeby arkusz czytał je jako liczbę.
5. **Przesunięcie terminu naboru wysyła przypomnienie jeszcze raz.** Po zmianie końca naboru z 05.10 na 02.10 zadanie w tle wysłało drugie przypomnienie, z nowym terminem. Wygląda to na zamierzone (klucz deduplikacji zawiera termin), ale warto wiedzieć, że każda zmiana daty to kolejny mail do wszystkich z rozpoczętym wnioskiem.
   **Stan:** zachowanie zamierzone, bez zmiany: przypomnienie o terminie, który się przesunął, byłoby nieprawdziwe, a klucz deduplikacji z terminem jest tym, co trzyma "najwyżej raz na termin" (T-105).
6. **Liczba oświadczeń.** Scenariusz mówi o trzynastu oświadczeniach w części IV, formularz 2026 ma ich dziesięć dla organizacji i osiem dla grupy nieformalnej. To rozbieżność scenariusza ze wzorem, nie błąd.
   **Stan:** poprawiony scenariusz ([`przejscie-gui.md`](przejscie-gui.md)), produkt bez zmiany.
7. **Data retencji w klauzuli RODO.** Scenariusz zapowiada datę z kroku 1.4 wprost w klauzuli we wniosku. Klauzula odsyła do "daty wskazanej w ogłoszeniu konkursu", a sama data (31.12.2032) jest na publicznej stronie konkursu. Spójne, tylko inaczej niż opisuje scenariusz.
   **Stan:** poprawiony scenariusz ([`przejscie-gui.md`](przejscie-gui.md)), produkt bez zmiany.

---

### Co wyszło dobrze i warto o tym wiedzieć

- **Reguła nieujawniania kont trzyma się wszędzie, gdzie trzeba.** Rejestracja na zajęty adres, reset hasła na nieistniejący adres i otwarcie cudzego wniosku dają odpowiedzi nie do odróżnienia od przypadku pozytywnego, a do adresów, których nie ma, nie poszedł żaden mail.
- **Log jest czysty.** Zero haseł, zero treści wniosku, zero numerów rachunku i NIP. Parametry zapytań SQL logują się jako znaki zapytania.
- **Izolacja danych działa w każdej sprawdzonej próbie:** cudzy wniosek, cudzy załącznik, cudzy PDF, cudze panele, konkurs roboczy bez publicznego adresu.
- **Polskie znaki są poprawne we wszystkich wygenerowanych plikach:** PDF wniosku, potwierdzenia, listy wniosków, listy rankingowej i umowy, a CSV wychodzi jako UTF-8 z BOM i średnikiem, czyli otworzy się w Excelu bez zabawy z importem.
- **Przycisk "Wyślij brakujące wiadomości" sam się wyłącza**, gdy komplet poszedł. Scenariusz zakładał kliknięcie i sprawdzenie, że nikt nie dostał drugiego maila; produkt nie dopuszcza nawet do kliknięcia.
- **Lista braków przy przycisku "Złóż wniosek" prowadzi kursorem prosto do brakującego pola.** Sprawdzone na polu "Pomysł na projekt": kliknięcie pozycji przewija ekran i ustawia kursor w tym polu.

---

### Karta wyniku przejścia

Kolumna "Wynik": OK znaczy zgodnie ze scenariuszem.

| Punkt | Wynik | Uwagi |
|---|---|---|
| 0 | OK | health 200, front 200, konta istnieją z rolami |
| A | OK z uwagą | puste stany po polsku, strony informacyjne kompletne, kontrast działa i przeżywa nawigację; znalezisko 1 |
| B1 | OK z uwagą | niezgodne pola nazwane po imieniu, obie zgody wymagane, ta sama odpowiedź dla zajętego adresu; znalezisko 2 |
| B2 | OK | zepsuty token: czytelna odmowa po polsku; dobry link potwierdza; przed potwierdzeniem logowanie odmawia i proponuje nowy link |
| B3 | OK | wylogowanie działa, `/panel/applicant` bez sesji odsyła na `/login?returnUrl=...` |
| B4 | OK | ta sama odpowiedź dla adresu istniejącego i nieistniejącego, zero maili do nieistniejącego, nowe hasło działa, stare odmawia |
| B5 | OK | piąta nieudana próba blokuje, szósta z dobrym hasłem odmawia z informacją o 15 minutach |
| B6 | OK | zmiana hasła nie zrywa sesji, link do zmiany adresu idzie na nowy adres, logowanie nowym adresem działa |
| C1 | BŁĄD | siedem kroków wypełnione, dane zapisane poprawnie w bazie, ale po zapisie ekran wraca do pustego kroku 1.1 z komunikatem "nie zapisany"; znalezisko 3 |
| C2 | OK | import wypisał pięć wersji 1, powtórzenie: "Nothing changed" pięć razy, bez dublowania |
| C3 | OK | formularz 2026 w kreatorze, cztery sekcje, zależności pól opisane, podgląd działa |
| C4 | OK | wzór PDF wgrany do wymogu Statut, publiczny odnośnik odpowiada 200 bez logowania |
| C5 | OK | publikacja przeszła, stan "Trwa nabór", konkurs widoczny publicznie z limitami i osobą kontaktową |
| C6 | OK | kopia 1/2026 na 2/2026 przeniosła ustawienia, formularz, karty oceny i wzór sprawozdania; kopię dało się opublikować bez importu |
| D1 | OK | wejście z publicznej strony, odesłanie na logowanie i powrót na ten sam konkurs; ekran "co przygotować" zgodny z ustawieniami |
| D2 | OK | karta podmiotu zapisana; przy drugim wniosku wypełniona, z przyciskami "Dane są aktualne" i "Popraw" |
| D3 | OK z błędem | autozapis przeżył przeładowanie, sumy i procenty liczą się same, limit 7000 zł i 10 procent kosztów pośrednich blokują z dokładną kwotą, licznik znaków działa, sekcje dostają "(gotowa)" albo "(są błędy)"; znalezisko 4 |
| D4 | OK z błędem | PDF przyjęty, podmiana działa, zły format odrzucony; znalezisko 5. Przeciąganie pliku myszką niesprawdzone |
| D5 | OK z uwagą | podsumowanie z "Popraw" przy każdej części, jedno potwierdzenie z ostrzeżeniem, numer 001, treść zamrożona, PDF z polskimi znakami, mail z numerem; znalezisko 6 |
| E | OK | grupa nieformalna: karta podmiotu zwężona do nazwy, w części I znikają pola organizacji, pojawiają się nazwa grupy, tabela członków i rachunek lidera, w części IV osiem oświadczeń zamiast dziesięciu; wniosek 002 złożony |
| F1 | OK | lista pokazuje tylko złożone, kolumny i sumy się zgadzają, sortowanie i filtr działają, CSV UTF-8 z BOM, PDF z polskimi znakami. Wyszarzonych zakładek nie dało się zobaczyć: każda miała już treść |
| F2 | OK | zwrot z wyborem sekcji, mail z powodem i terminem, u wnioskodawcy odblokowana tylko Część III, po ponownym złożeniu ten sam numer, dwie sumy kontrolne i pełna historia statusów |
| G | OK z uwagą | obie karty formalne zakończone, karta grupy ma sześć kryteriów zamiast ośmiu, po zakończeniu karta zablokowana; znalezisko 7 |
| H1 | OK | dwóch ekspertów, suma punktów, próg 50, strategiczne poza progiem, "Zapisano ustawienia oceny" |
| H2 | OK | obaj recenzenci przypisani do obu wniosków przypisaniem hurtem |
| H3 | OK z błędem | przed deklaracją widać tylko liczbę wniosków, po deklaracji listę; cztery karty wypełnione i zakończone (001: 44 i 44, 002: 30 i 30, kwoty 5700 zł); ekspert nie wchodzi na cudze panele; znalezisko 8 |
| I1 | OK | zamknięcie naboru i start oceny, publiczna strona mówi "Wniosku nie da się teraz rozpocząć" |
| I2 | OK z uwagą | ranking po punktach, kwota przyznana edytowalna wprost na liście, stan puli przelicza się po opuszczeniu pola, przekroczenie puli daje komunikat z kwotą, eksporty PDF, XLSX i CSV z polskimi znakami |
| I3 | OK | zatwierdzenie z ostrzeżeniem, dwa maile (dofinansowany z kwotą, lista rezerwowa), wysyłka brakujących wyłączona po komplecie |
| I4 | OK | udostępnienie kart nieodwracalne i potwierdzone, u wnioskodawcy karta formalna i dwie merytoryczne bez nazwisk ekspertów |
| I5 | OK | publiczne wyniki w dwóch tabelach, po archiwizacji konkurs znika z `/competitions`, jest na `/archive`, a jego strona działa pod tym samym adresem |
| J1 | OK | wzór umowy w wersji 1, podział znaczników na systemowe i do wpisania |
| J2 | OK | wartości zapisane, PDF umowy z polskimi znakami i bez nieuzupełnionych znaczników, u wnioskodawcy "Umowa jest przygotowana do podpisu" |
| J3 | OK | ZIP przed uzupełnieniem pól: sam `braki.txt`; po uzupełnieniu: `umowa-001.pdf` i żadnego `braki.txt` |
| J4 | OK | data z przyszłości odrzucona, data dzisiejsza zapisana po potwierdzeniu, stan "Podpisana 02.10.2026", znacznik daty wypełnił się słownie |
| K | OK z błędami | rezygnacja odnotowana z mailem, środki wróciły do puli, system zaproponował następny wniosek z listy rezerwowej, przyznanie 5700 zł i mail; znaleziska 9, 10, 11 |
| L | OK z błędem | sprawozdanie złożone, zwrócone z powodem, poprawione, przyjęte; rozliczenie policzyło 100 zł kosztów nieuznanych i 100 zł do zwrotu, wniosek w stanie Settled; znalezisko 12 |
| M1 | OK | po terminie odmowa 409 z komunikatem "Nabór został zamknięty 02.10.2026 o godzinie 19:13 czasu polskiego. Wniosku nie można już złożyć.", wersja robocza zostaje |
| M2 | OK | przypomnienie poszło tylko do konta z rozpoczętym i niezłożonym wnioskiem, z terminem i odnośnikiem; po przesunięciu terminu drugie, z nowym terminem (obserwacja 5) |
| N | OK | wszystkie próby dostępu zakończone odmową, log bez haseł i danych wrażliwych |

#### Czego nie sprawdzono

- Wyszarzone zakładki konkursu w stanie "trwa nabór": na tym etapie każda zakładka miała już treść, więc przypadek nie wystąpił.
- Przeciąganie pliku myszką do kafelka załącznika: użyto wyboru pliku z okna systemowego.
- Prawdziwa wysyłka SMTP: świadomie poza zakresem przejścia lokalnego, maile czytane z logu backendu.

#### Dane zostawione w bazie po przejściu

| Co | Identyfikator |
|---|---|
| Konkurs 1/2026 "Kierunek NOWE FIO 2026 (próba)", archiwalny | `45c605da-711e-4958-9cbe-25c37201f3b3` |
| Konkurs 2/2026 "(próba terminów)", nabór zamknięty | `b96454bd-2e07-4891-a8f8-538305c71c07` |
| Konkurs 3/2026 "Konkurs roboczy do próby dostępu", roboczy | `b68a09ab-fe58-42a9-b9ea-3516daad5cde` |
| Wniosek 001 organizacji, stan Resigned | `be0a8f0e-5ca5-4c09-8799-9c5c304e1341` |
| Wniosek 002 grupy, stan Settled | `859fbcaf-2e06-4b37-970e-dfab91064007` |
| Wniosek roboczy w konkursie 2/2026, stan Draft | `d7463a10-adc8-47c6-a3d2-9247d34dfc4e` |

Konto `test.reczny2@example.org` założone w ścieżce B zostało zmienione na `test.reczny3@example.org` i jest zablokowane po próbie B5 (blokada wygasa 15 minut od 2026-10-02 18:01). Hasła kont testowych z `create_test_users.py` są bez zmian.
