# Przejście ręczne testGUI.md: dziennik błędów

Zapis z przejścia scenariusza z [`../testGUI.md`](../testGUI.md) w przeglądarce. Jeden wpis na jedną porażkę, w formacie z karty wyniku: adres strony, co kliknięto, co się stało, co miało się stać.

Przebieg: 2026-09-30, maszyna deweloperska Windows 10, przeglądarka Chrome.

## Stan przejścia

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

## Znaleziska według wagi

**Stan na 2026-10-03: lista zamknięta.** Osiemnaście znalezisk, każde poprawione albo rozstrzygnięte jako nieusterka, każda poprawka z testem. Przegląd kodu po fakcie: 2026-10-03, razem z przejściem przedprodukcyjnym ([`preproduction-bledy.md`](preproduction-bledy.md)), które te same ścieżki przeszło jeszcze raz i potwierdziło poprawki 2 do 9, 11, 12, 15 i 16.

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

## B-GUI-01. Stos lokalny nie wstaje na tej maszynie (rozwiązane)

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

**Odblokowanie:** włączyć wirtualizację w BIOS (VT-x albo AMD-V, przy AMD zwykle "SVM Mode") i włączyć funkcje Windows "Platforma maszyny wirtualnej" oraz "Podsystem Windows dla systemu Linux". Wariant bez Dockera: PostgreSQL 16 natywnie na 5432, backend przez `dotnet run`, frontend przez `npm run dev`. Wirtualizacji nie wymaga, ale komendy z testGUI (`docker compose exec`, `docker compose logs`) trzeba wtedy zastąpić natywnymi odpowiednikami.

## B-GUI-02. Błąd hydratacji Reacta na każdej stronie, przez nonce skryptu kontrastu

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

## B-GUI-03. W trybie wysokiego kontrastu znika logo

**Gdzie:** nagłówek każdej strony, po włączeniu "Wysoki kontrast".

**Co zrobiono:** kliknięcie przełącznika kontrastu, oględziny nagłówka, powiększenie obszaru logo.

**Co się stało:** z logo zostaje pomarańczowy płomyk i kropka. Litery "ocwip" i "pl" znikają.

**Co miało się stać:** logo pozostaje czytelne. Tryb wysokiego kontrastu istnieje dla osób słabowidzących, więc element, który w nim znika, jest odwróceniem celu tej funkcji.

**Przyczyna, sprawdzona w pliku:** `frontend/public/ocwip-logo.svg` ma `fill="#EB6209"` tylko na dwóch ścieżkach (płomyk i kropka). Pozostałe ścieżki, czyli cały napis, nie mają atrybutu `fill`, więc przyjmują domyślne SVG, czyli czerń. Paleta kontrastu ustawia tło strony na czarne (`rgb(0, 0, 0)`, sprawdzone w przeglądarce), a czarny napis na czarnym tle nie istnieje.

**Uwaga do zakresu:** `docs/map/frontend.md` opisuje regułę "paleta kontrastu musi nadpisać każdy token poza pomarańczem logo" i test `app/contrast-tokens.test.ts` jej pilnuje. Reguła mówi o tokenach CSS, a kolor liter logo tokenem nie jest, bo siedzi w pliku SVG. Dlatego test przechodzi, a logo i tak znika. Poprawka to albo wariant logo na ciemne tło, albo wymuszenie koloru liter w trybie kontrastu.

## B-GUI-04. Podwójna kropka w deklaracji dostępności

**Gdzie:** `/deklaracja-dostepnosci`, sekcja "Przygotowanie deklaracji dostępności".

**Co się stało:** "Data sporządzenia deklaracji: 29 września 2026 r.."

**Co miało się stać:** jedna kropka. Do kropki ze skrótu "r." dokładana jest kropka zdania.

**Waga:** kosmetyka, ale to dokument czytany przez urzędnika i podlegający kontroli dostępności, więc literówka na widoku jest tu droższa niż gdzie indziej.

## B-GUI-05. Brak zaznaczonych zgód: komunikat jest widoczny, ale niedostępny dla czytnika ekranu

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

## B-GUI-06. Surowy markdown w treści zgód na ekranie rejestracji

**Gdzie:** `/register`, oba okienka z treścią dokumentów.

**Co się stało:** pierwsza linia każdego dokumentu wyświetla się dosłownie jako `# Regulamin serwisu` i `# Klauzula informacyjna o przetwarzaniu danych osobowych`, razem ze znakiem `#`.

**Co miało się stać:** nagłówek jako nagłówek albo bez znaku `#`. Backend traktuje tę linię jako tytuł dokumentu (`ConsentCatalog` bierze pierwszą linię po `# ` i pokazuje ją w etykiecie "Akceptuję: ..."), więc tytuł jest już podany obok. W okienku zostaje nieprzetworzony znacznik.

**Waga:** kosmetyka, ale to ekran, na którym użytkownik akceptuje dokument prawny, więc wygląd "nieprzetworzonego pliku" obniża zaufanie do treści.

## B-GUI-07. Ekran nowego hasła nie prosi o powtórzenie, ekran rejestracji prosi

**Gdzie:** `/reset-password`.

**Co się stało:** jedno pole "Nowe hasło" i przycisk. Rejestracja i tylko ona wymaga wpisania hasła dwa razy.

**Co miało się stać:** spójność w obrębie produktu. Literówka przy ustawianiu nowego hasła daje konto, do którego użytkownik nie wejdzie, i wymusza kolejny reset, a właśnie z resetu ten człowiek przyszedł.

**Waga:** drobna, ale to niespójność w obrębie jednej ścieżki, a nie kwestia gustu.

## B-GUI-08. Po pierwszym zapisie kreator pokazuje pusty formularz i twierdzi, że nic nie zapisał

**Gdzie:** `/panel/operator/competitions/new`, przycisk "Zapisz" przy zakładaniu konkursu.

**Co zrobiono:** wypełniono krok 1.1 i 1.4, kliknięto "Zapisz".

**Co się stało:** adres zmienia się na `/panel/operator/competitions/<id>/edit`, czyli zapis się udał, ale ekran pokazuje **pusty formularz kroku 1.1** i baner "Konkurs jeszcze nie zapisany.". Dane są w bazie: po ręcznym przeładowaniu tego samego adresu wracają wszystkie wartości, a baner zmienia się na "Edycja konkursu 1/2026, Zapisano jako roboczy 30.09.2026, 16:08:03".

**Co miało się stać:** po zapisie ekran pokazuje zapisany konkurs, jak po przeładowaniu.

**Dlaczego to nie jest kosmetyka.** Operator widzi pusty formularz i komunikat "jeszcze nie zapisany", więc naturalną reakcją jest wypełnić wszystko drugi raz i zapisać. Sprawdzono, co się wtedy dzieje:

- przy **tym samym** numerze konkursu zapis odmawia komunikatem "Inny konkurs ma już ten numer. Numer konkursu musi być niepowtarzalny.". Operator dostaje więc informację o kolizji z konkursem, którego przed chwilą nie zobaczył i o którym nie wie, że istnieje;
- przed powstaniem duplikatu chroni wyłącznie ten warunek niepowtarzalności numeru. Przy innym numerze powstałby drugi konkurs.

**Zakres:** dotyczy tylko **pierwszego** zapisu, czyli utworzenia. Kolejne zapisy na istniejącym konkursie działają poprawnie, baner odświeża godzinę zapisu (sprawdzone przy krokach 1.2, 1.5 i 1.6).

**Uwaga porządkowa:** w bazie po tej próbie został konkurs roboczy `9/2026 - Konkurs probny do testu duplikatu`. Nieopublikowany, więc niewidoczny publicznie. Zostawiony celowo, bo posłużył w C5 do sprawdzenia listy braków przy publikacji.

## B-GUI-09. Tabela w formularzu ignoruje warunek widoczności, choć walidacja go respektuje

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

## B-GUI-10. Klauzula RODO we wniosku nie podstawia daty retencji z ustawień konkursu

**Gdzie:** wniosek, część IV. Oświadczenia.

**Co się stało:** klauzula mówi "do daty wskazanej w ogłoszeniu konkursu".

**Co miało się stać:** konkretna data z kroku 1.4 kreatora, czyli 31.12.2032. Krok 1.4 opisuje to pole jako "data usunięcia danych osobowych", a scenariusz wprost zapowiada "klauzula RODO z datą retencji z kroku 1.4".

**Stan faktyczny:** data jest ustawiona i poprawnie widnieje na publicznej stronie konkursu ("Dane osobowe przetwarzane do 31.12.2032"), ale do klauzuli we wniosku nie wchodzi. Wnioskodawca czyta więc klauzulę, która odsyła go do innego dokumentu zamiast podać termin.

**Waga:** informacja o okresie przetwarzania jest obowiązkowym elementem klauzuli informacyjnej, więc to nie jest wyłącznie kwestia wygody.

**Przy okazji, rozbieżność liczbowa:** scenariusz mówi o trzynastu oświadczeniach, formularz 2026 z `backend/seed/forms/application-2026.json` ma ich dziesięć. Nie wiadomo, czy zgubiono trzy, czy scenariusz liczy inaczej. Do rozstrzygnięcia z zamawiającym, bo wzór oferty jest dokumentem zewnętrznym.

**Stan (2026-10-03):** zamknięte bez zmiany w produkcie. Przejście przedprodukcyjne ([`preproduction-bledy.md`](preproduction-bledy.md), obserwacja 7) rozstrzygnęło, że klauzula odsyłająca do ogłoszenia jest spójna z datą na publicznej stronie konkursu, a zapowiedź daty wprost w klauzuli była opisem scenariusza, nie wymaganiem. Liczba oświadczeń: dziesięć dla organizacji, osiem dla grupy nieformalnej, scenariusz poprawiony.

## B-GUI-11. Odmowa złego formatu pliku wymienia formaty, których ten wymóg nie dopuszcza

**Gdzie:** wniosek, sekcja "Załączniki", kafelek "Statut".

**Co zrobiono:** próba wgrania pliku `.txt` na kafelek opisany jako "Wymagany · formaty: PDF".

**Co się stało:** "Niedozwolony format pliku. Dozwolone formaty: PDF, DOC, DOCX, XLS, XLSX, JPG, ODT, ODS."

**Co miało się stać:** komunikat wymienia formaty dopuszczone **przez ten wymóg**, czyli sam PDF. Operator ustawił PDF w kroku 1.5 kreatora i tak to jest pokazane dwie linijki wyżej.

**Skutek:** komunikat zachęca wnioskodawcę do wgrania DOCX albo JPG na wymóg, który przyjmuje tylko PDF. Albo zostanie odrzucony drugi raz, albo, jeśli lista globalna wygrywa też przy sprawdzaniu, do konkursu wejdzie statut w formacie, którego operator nie chciał. Nie sprawdzono, która z tych dwóch rzeczy się dzieje, bo wymagałoby to prawdziwego pliku DOCX, a sprawdzanie idzie po zawartości, nie po rozszerzeniu.

## B-GUI-12. Mail potwierdzający złożenie podaje godzinę w UTC

**Gdzie:** mail "Potwierdzenie złożenia oferty".

**Co się stało:** "Data złożenia: 2026-09-30 14:24 UTC", podczas gdy ekran wniosku pokazuje "Wniosek został złożony 30.09.2026, 16:24".

**Co miało się stać:** czas polski, tak jak wszędzie indziej w produkcie. Strona konkursu pisze wprost "do 07.10.2026 o godzinie 23:59 czasu polskiego", a ekran wniosku podaje czas lokalny.

**Skutek:** wnioskodawca dostaje potwierdzenie dwie godziny wcześniejsze niż to, co widział na ekranie. Przy wniosku złożonym tuż przed północą w dniu zamknięcia naboru różnica dotyczy daty, a to jest dokument, którym człowiek dowodzi terminowości.

## B-GUI-13. Pusty formularz zwrotu do poprawy odmawia bez podania przyczyny

**Gdzie:** panel operatora, wniosek, "Zwróć do poprawy".

**Co się stało:** wysłanie formularza bez wskazanych sekcji, bez powodu i bez terminu daje jedno zdanie: "Nie udało się zwrócić wniosku.". Nie wiadomo, czego brakuje ani czy to błąd operatora, czy awaria.

**Co miało się stać:** komunikat przy polu albo wyliczenie braków, tak jak robi to formularz wniosku po stronie wnioskodawcy ("To pole jest wymagane" przy konkretnej pozycji) i ekran rejestracji.

**Waga:** drobna, ale sformułowanie "Nie udało się" sugeruje awarię systemu, a nie brak danych, więc operator zgłosi to jako błąd zamiast poprawić formularz.

**Stan (2026-10-03):** poprawione. `apiErrorMessage` bierze komunikaty pól z `fieldErrors`, gdy odmowa walidacyjna nie ma `detail`, więc ekran pisze "Wskaż co najmniej jedną sekcję. Podaj termin poprawy." zamiast własnego zdania o nieudanej próbie. Dotyczy każdego ekranu, który łapie błąd tym samym pomocnikiem (`preproduction-bledy.md`, znalezisko 10).

## B-GUI-14. Lista wniosków nie ma kolumny z wynikiem oceny formalnej

**Gdzie:** `/panel/operator/applications/<id konkursu>`.

**Co zrobiono:** zakończono ocenę formalną obu wniosków z wynikiem pozytywnym, wrócono na listę.

**Co się stało:** kolumny to `Lp. | Numer wniosku | Nazwa podmiotu | Rodzaj wnioskodawcy | Tytuł projektu | Całkowity koszt zadania | Wnioskowana kwota | Status | Data złożenia`. Wyniku oceny formalnej nie ma nigdzie, a kolumna "Status" dla obu wniosków nadal pokazuje "Złożony".

**Co miało się stać:** scenariusz wymienia kolumny wprost: "numer, nazwa podmiotu, tytuł projektu, całkowity koszt, wnioskowana kwota, status, wynik oceny formalnej", a krok G kończy się zdaniem "Wynik oceny formalnej ma pojawić się w kolumnie na liście wniosków".

**Skutek:** przy naborze na kilkadziesiąt wniosków operator nie widzi na liście, które przeszły ocenę formalną, a które nie, i które w ogóle są ocenione. Musi wchodzić w każdy wniosek osobno.

**Do rozstrzygnięcia:** czy brakuje kolumny, czy zmiany statusu po ocenie formalnej. Dziś nie ma ani jednego, ani drugiego.

**Stan (2026-10-03):** poprawione kolumną. Lista wniosków i jej eksporty (CSV, PDF) mają "Ocena formalna" z wynikiem tej samej reguły co lista rankingowa (`FormalStandingReader`); status wniosku zostaje bez zmian, bo zmienia go rozstrzygnięcie konkursu, nie etap oceny (`preproduction-bledy.md`, znalezisko 7).

## B-GUI-15. W karcie oceny merytorycznej punkty są wyświetlane jako złotówki

**Gdzie:** panel recenzenta, karta oceny merytorycznej, pola liczone.

**Co się stało:** pole "Suma (0-50 punktów)" pokazuje **"44,00 zł"**. To samo dotyczy sumy kryteriów strategicznych, gdzie jeden punkt wyświetla się jako **"1,00 zł"**.

**Co miało się stać:** "44" albo "44 pkt". To są punkty, nie pieniądze.

**Że to nie jest wyłącznie wartość w atrybucie:** potwierdzone zrzutem ekranu, pole renderuje się z napisem "44,00 zł" pod etykietą "Suma (0-50 punktów)".

**Zakres:** nagłówek karty liczy i opisuje to poprawnie ("Suma punktów: 44, kryteria strategiczne: 1, proponowana kwota: 5700,00 zł"), więc błąd jest w formatowaniu pól liczonych, nie w samym liczeniu. Wygląda na to, że pola wyliczane w karcie oceny dziedziczą formatowanie walutowe z pól budżetu we wniosku.

**Waga:** ograniczona do ekranu, na którym ekspert wypełnia kartę. Sprawdzone po udostępnieniu kart: **wnioskodawca widzi punktację poprawnie** ("Suma punktów: 44, kryteria strategiczne: 1, proponowana kwota: 5700,00 zł", punkty przy kryteriach jako 18 i 14, bez nazwisk ekspertów). Usterka myli więc osobę oceniającą, ale nie wychodzi na zewnątrz.

## B-GUI-16. Rezygnacja z dotacji idzie jednym kliknięciem, bez potwierdzenia

**Gdzie:** panel oceny konkursu, sekcja "Umowy, rezygnacje i lista rezerwowa", przycisk "Potwierdź rezygnację 001".

**Co się stało:** jedno kliknięcie przestawia wniosek w stan `Rezygnacja`, zwalnia kwotę z puli i wysyła do wnioskodawcy maila o rezygnacji. Okna potwierdzenia nie ma.

**Co miało się stać:** drugi krok, jak przy każdej innej nieodwracalnej decyzji w tym produkcie. Sprawdzone w tym samym przejściu: publikacja konkursu, zamknięcie naboru, rozpoczęcie oceny, zakończenie karty oceny, zatwierdzenie wyników, udostępnienie kart, zapisanie podpisania umowy i przyjęcie sprawozdania, każde z nich pyta "Wróć / Tak" i tłumaczy skutek.

**Dlaczego to nie jest czepianie się o nazwę przycisku:** słowo "Potwierdź" w etykiecie odnosi się do potwierdzenia rezygnacji zgłoszonej telefonicznie, a nie do potwierdzenia własnego kliknięcia. Skutkiem pomyłki jest mail do wnioskodawcy z informacją, że zrezygnował z dotacji, i przesunięcie pieniędzy do kolejnego podmiotu. Cofnięcie tego wymaga interwencji poza systemem.

## B-GUI-17. Pola umowy mają etykiety wzięte ze znaczników, bez polskich znaków

**Gdzie:** panel operatora, wniosek dofinansowany, sekcja umowy.

**Co się stało:** etykiety pól brzmią "Numer umowy niw", "Data umowy niw", "Termin wydatkow", "Zrodlo danych osobowych".

**Co miało się stać:** "Numer umowy z NIW", "Data umowy z NIW", "Termin wydatków", "Źródło danych osobowych". Interfejs jest po polsku, a te cztery etykiety są nazwami technicznymi znaczników wzoru, z wyciętą polszczyzną.

**Skąd to się bierze:** wzór umowy używa znaczników w rodzaju `{{numer_rachunku}}`, a ekran buduje etykietę z nazwy znacznika. Pozostałe pola ("Rejestr", "Reprezentant", "Osoba kontaktowa") wyglądają dobrze, bo ich nazwy nie mają polskich liter.

**Waga:** kosmetyka, ale widoczna przy każdej umowie i sprzeczna z regułą "UI po polsku" z `AGENTS.md`. Poprawka to słownik etykiet obok listy znaczników.

**Stan (2026-10-03):** poprawione dokładnie tak. `TemplatePlaceholders.BlankLabels` podaje polską pisownię dla tych nazw, których etykieta generowana z nazwy jest zła ("Numer umowy z NIW", "Data umowy z NIW", "Termin wydatków", "Źródło danych osobowych", "E-mail kontaktowy", "Adres lidera grupy"). Nazwa, której w słowniku nie ma, nadal dostaje etykietę z nazwy, więc nowa luka we wzorze OCWIP nie wymaga kodu (D16). Etykiety idą też do `braki.txt` w paczce umów.

## B-GUI-18. Ekran sprawozdania pozwala kliknąć złożenie, choć sekcja ma błąd

**Gdzie:** `/panel/applicant/reports/<id>`.

**Co się stało:** przy sekcji z niespełnionym minimum długości pasek sekcji pokazuje "(są błędy)", ale przycisk "Złóż sprawozdanie" jest **aktywny**, a nad nim nie ma żadnej listy braków. Po kliknięciu pojawia się jeszcze okno potwierdzenia, a dopiero po nim komunikat "Sprawozdania nie da się jeszcze złożyć: Wymagane co najmniej 1000 znaków (jest 208).".

**Co miało się stać:** to samo, co na wniosku, gdzie przycisk jest widoczny i **wyłączony**, a pod nim stoi lista braków, w której każda pozycja jest odnośnikiem do pola.

**Waga:** drobna, bo odmowa jest wyjaśniona i trafia do regionu `aria-live`. Kosztuje jednak dwa zbędne kliknięcia i psuje wrażenie, że produkt zachowuje się tak samo w obu miejscach. Wniosek ustawia tu poprzeczkę, sprawozdanie jej nie sięga.

**Stan (2026-10-03):** poprawione do parzystości z wnioskiem. `ReportWorkspace` liczy braki tym samym `submissionGaps` co wniosek, przycisk "Złóż sprawozdanie" jest wyłączony, dopóki któryś zostaje, a lista pod nim prowadzi kursorem do pola (wspólny `useFieldFocus`). Odmowa serwera zostaje jako druga warstwa: te same reguły, policzone wcześniej, nie zamiast.

## Sprawdzone i bez zastrzeżeń

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

## Narzędzia tego przejścia

- `create_test_users.py` w katalogu głównym (lokalny, w `.gitignore`): zakłada pięć kont z testGUI przez prawdziwe ścieżki produktu, czyli `POST /register` z obiema zgodami, odczyt linku z maila w logu backendu, `POST /verify-email`, na koniec `grant-role` dla operatora i dwóch recenzentów. Kolejne wywołania są rozłożone w czasie, bo trasy konta mają limit 10 żądań na minutę.
- `.env` skopiowane z `.env.example`, bez zmian.

## Czego tym przejściem nie sprawdziłem

Żeby nikt nie liczył, że to zostało odhaczone:

- **Kolumna "wyszarzone zakładki" w stanie "trwa nabór"** (F1, ostatni akapit): zakładki konkursu bez treści miały być wyszarzone z wyjaśnieniem, a nie ukryte. Nie sprawdzone osobno.
- **Edycja formularza w kreatorze** (C3, druga część): dodanie sekcji testowej, przesunięcie i usunięcie. Świadomie pominięte, bo scenariusz ostrzega, że to psuje dalsze kroki, a przejście robiłem jednym ciągiem na jednej bazie.
- **Kolumna "Uwagi do decyzji"** na liście rankingowej: widoczna, ale nie wypełniana.
- **Podmiana załącznika przeciągnięciem myszą** (D4): podmiana sprawdzona przez pole wyboru pliku, samo przeciąganie nie.
- **Prawdziwa wysyłka maili, HTTPS, kopie zapasowe, retencja, aneksy, oferty wspólne**: poza zakresem tego przejścia, tak jak mówi sam scenariusz.

## Stan bazy po przejściu

Przejście zostawiło dane, które warto znać przed kolejnym uruchomieniem:

| Co | Stan |
|---|---|
| Konkurs `1/2026` | Archiwalny, rozstrzygnięty, wniosek 001 w stanie Rezygnacja, wniosek 002 rozliczony |
| Konkurs `2/2026` | Trwa nabór zakończony 30.09.2026 17:12, jeden wniosek roboczy niezłożony (próba terminu) |
| Konkurs `3/2026` | Trwa nabór do 03.10.2026, jeden wniosek roboczy niezłożony (próba przypomnienia) |
| Konkurs `9/2026` | Roboczy, bez treści, powstał przy próbie duplikatu z B-GUI-08 |
| Konta | pięć ze skryptu plus `test.reczny.nowy@example.org` z przejścia ścieżki B (hasło zmienione dwa razy, konto po blokadzie) |

Kasowanie tego nie jest potrzebne do dalszej pracy, ale przy powtórce całego scenariusza czystsza jest świeża baza (`docker compose down -v`, co kasuje też wolumeny).

## Jak powtórzyć to przejście

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

To nie jest usterka produktu, tylko właściwość powłoki, ale kosztuje kwadrans, jeśli się o tym nie wie. Warto dopisać tę uwagę do [`../testGUI.md`](../testGUI.md).
