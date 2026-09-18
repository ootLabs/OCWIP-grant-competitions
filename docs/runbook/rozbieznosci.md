# Rozbieżności i zakres bez karty

Trzy źródła prawdy opisują ten projekt: **tablica Trello** (zakres), **repozytorium** (kod i `docs/`) oraz **raport** `RAPORT-proces-i-pola.docx` (proces i pola, nowszy od kart). W wielu miejscach mówią różne rzeczy. Ten plik trzyma ich listę.

## Jak z tego korzystać

1. **Nic z tej listy nie jest zadaniem, dopóki nie ma karty na Trello.** Kolejka odwzorowuje tablicę, nie nasze pomysły. Zanim weźmiesz pozycję `R-xx` do pracy, załóż kartę i wpisz ją do [`kolejka.md`](kolejka.md).
2. Przy sprzeczności buduj to, co **węższe**, chyba że raport jest nowszy i mówi wprost inaczej. Datę i źródło zapisz przy pozycji.
3. Pozycję rozwiązaną wykreśl stąd i przenieś ustalenie tam, gdzie ma żyć na stałe: `docs/architektura.md`, `docs/model-danych.md` albo karta decyzji na Trello.
4. Nowy rozjazd znaleziony w trakcie pracy dopisujesz tutaj i **idziesz dalej**. Nie naprawiasz przy okazji.

Waga: `krytyczna` znaczy migrację albo przepisanie warstwy, jeśli zdecydujemy późno. `wysoka` znaczy zakres, którego MVP potrzebuje. `średnia` znaczy rzecz do zrobienia, ale nie teraz.

---

## Zmiany modelu danych

### R-01 · Karta organizacji jako osobny rekord z dostępem wielu osób

**Waga: krytyczna.** Źródło: raport, krok 2.2 i decyzja RD7.

Dziś schemat wiąże użytkownika z podmiotem **jeden do jednego** (`users.entity_id` unikalne), a dane podmiotu żyją przy koncie. Raport opisuje kartę organizacji jako osobny rekord, do którego odwołuje się każdy kolejny wniosek, z dostępem przypisanym **do organizacji, nie do osoby**. Konsekwencje:

- tabela pośrednicząca użytkownik do organizacji,
- rozpoznawanie po NIP-ie z komunikatem "ta organizacja jest już zarejestrowana",
- prośba o dostęp zatwierdzana przez założyciela karty,
- eskalacja po siedmiu dniach do administratora OCWIP, z zapisem w historii,
- reguła "kto ma dostęp do karty, widzi wszystkie jej wnioski, także robocze",
- wniosek złożony zachowuje **kopię** danych organizacji z chwili złożenia.

To jest jedno z czterech założeń już wypalonych w schemacie (B-09). Migracja dziś jest bezkosztowa, bo baza jest pusta.

**Dotyka:** T-11.2, T-13.2, T-13.3, T-15.2, T-34, T-36, T-47.
**Co zrobić:** potwierdzić RD7 z zamawiającym przy najbliższej okazji, potem założyć kartę na migrację. Do tego czasu nie rozsypywać `user.EntityId` po serwisach.

### R-02 · Czwarta rola: administrator

**Waga: wysoka.** Źródło: raport, tabela ról.

Repozytorium i Trello znają trzy role (`Applicant`, `Operator`, `Reviewer`). Raport opisuje pięć: gość, wnioskodawca, ekspert, operator, administrator. Gość nie jest rolą w bazie (to brak konta), ekspert to nasz recenzent, ale **administrator jest nowy** i ma trzy uprawnienia, których operator mieć nie powinien, bo są nieodwracalne albo dotykają cudzych danych:

1. dodaje i odbiera dostęp operatorom,
2. zatwierdza awaryjnie dostęp do cudzej karty organizacji,
3. uruchamia usunięcie danych po terminie retencji.

Raport sam zaznacza, że jeśli u zamawiającego to zawsze ta sama osoba co operator, zostają trzy role.

**Dotyka:** T-13.1, T-13.2, T-47.
**Co zrobić:** zapytać. Do czasu odpowiedzi budować polityki tak, żeby dodanie roli było wartością w enumie, a nie przepisaniem handlerów.

### R-17 · Stany konkursu i wniosku

**Waga: wysoka.** Źródło: raport, "Stany konkursu" i "Stany wniosku".

| Co | W kodzie i na kartach | W raporcie |
|---|---|---|
| Konkurs | cztery: szkic, opublikowany, zamknięty, nieaktywny | siedem: roboczy, opublikowany, trwa nabór, nabór zamknięty, trwa ocena, rozstrzygnięty, archiwalny |
| Wniosek | dwa: `Draft`, `Submitted` | jedenaście, z pętlą zwrotu do poprawy i czterema stanami po rozstrzygnięciu |

Dwa stany wniosku są świadome: dalsze należą do encji oceny, której jeszcze nie budujemy. Siedem stanów konkursu to natomiast realny brak, bo od nich zależy, czy przycisk "Wypełnij wniosek" działa.

**Dotyka:** T-20, T-33, T-42.
**Co zrobić:** w T-20 zbudować przejścia jako tabelę dozwolonych par w jednym miejscu, żeby dołożenie stanów było dopisaniem wierszy.

**Stan: zamknięte po stronie konkursu (T-20, 2026-09-18).** Enum ma siedem stanów, tabela par siedzi w `Models/CompetitionStatusTransitions.cs`, a stan efektywny liczy `Models/CompetitionLifecycle.cs`. Jedenaście stanów wniosku zostaje otwarte i czeka na encję oceny.

---

## Zakres bez karty na Trello

### R-03 · Zwrot wniosku do poprawy

**Waga: wysoka.** Źródło: raport, krok 4.2 i decyzja RD10.

Mechanizm z obecnego narzędzia, który raport uznaje za bardzo dobry i powtarza w całości. Operator wskazuje, które sekcje wnioskodawca może edytować, co ma poprawić i do kiedy. Wniosek wraca do stanu roboczego z odblokowanymi tylko wskazanymi sekcjami. Dostępne na dwóch etapach: w naborze i po ocenie formalnej. Poprawka wymaga ponownego złożenia i tworzy nową wersję z nową sumą kontrolną.

Karta T-33 pisze, że nie ustalono, czy wniosek można poprawić po złożeniu. Raport to rozstrzyga.

**Siada na:** T-33 (historia statusów), T-35 (ekran operatora), T-34 (widok wnioskodawcy).

### R-04 · Ustawienia oceny w konkursie

**Waga: wysoka.** Źródło: raport, krok 5.0. Osiem parametrów wypisanych w [`pola.md`](pola.md).

Odpowiadają na trzy z czterech pytań otwartych w T-37 i pozwalają ruszyć część M5 mimo B-02. Jest to ustawienie konkursu, więc należy do `T-20` albo do osobnej karty, nie do `T-37`.

### R-05 · Oświadczenie o konflikcie interesów

**Waga: wysoka.** Źródło: raport, krok 5.1 i decyzja RD11.

Dopóki ekspert nie zaakceptuje oświadczenia, **nie widzi treści żadnego wniosku**. Odmowa jest dopuszczalna, wymaga powodu i wyklucza go z oceny. Losowanie przypisań pomija osoby bez podpisanego oświadczenia. Obecne narzędzie ma to gotowe, razem ze znacznikiem wstawiającym tabelę do dokumentu.

**Siada na:** T-37 (brama do przypisania) i na mechanizm wzorów dokumentów.

### R-06 · Mechanizm wzorów dokumentów, nie generator umów

**Waga: wysoka.** Źródło: raport, kroki 5.6 i 6.2.

Trello ma jedną kartę na generowanie umowy (`T-45`, zablokowaną przez B-03). Raport opisuje **jeden mechanizm wzorów ze znacznikami**, z którego powstaje umowa, protokół komisji, lista obecności, lista kontaktowa ekspertów, wyniki oceny, oświadczenia o konflikcie interesów i wykaz błędów formalnych. Nie budujemy pięciu generatorów.

To zmienia priorytet: mechanizm jest potrzebny w M5, a nie dopiero w M6, i **nie jest zablokowany przez B-03**, bo spis znaczników mamy.

### R-07 · Dokumenty komisji i wykaz błędów formalnych

**Waga: średnia.** Źródło: raport, krok 5.6. Konsumenci mechanizmu z `R-06`. Wykaz błędów formalnych dla całego konkursu jako jedna tabela zastępuje przy dwudziestu wnioskach dwadzieścia ręcznie pisanych e-maili.

### R-08 · Zmiana adresu e-mail konta

**Waga: średnia.** Źródło: raport, krok 2.1. W MVP raportu, brak karty. Wymaga potwierdzenia z nowego adresu, bo adres służy do logowania. Nie wymaga kontaktu z OCWIP.

### R-09 · Przypomnienie trzy dni przed końcem naboru

**Waga: średnia.** Źródło: raport, krok 3.2. Jedno przypomnienie e-mailem, wyłącznie do osób z **rozpoczętym i niezłożonym** wnioskiem. Treść ustawiana przy konkursie. Siada na T-29, bo tam wiadomo, kto ma rozpoczęty wniosek.

### R-10 · Ekran "co przygotować"

**Waga: średnia.** Źródło: raport, krok 3.1. Jeden krótki ekran przed pierwszym polem, **generowany**: załączniki z kroku 1.5, terminy z 1.1, dwa zdania od zamawiającego z ustawień formularza. Zmiana załącznika w konkursie zmienia tę listę sama. Siada na T-34.

### R-11 · Krok 0: kopia konkursu z poprzedniego roku

**Waga: wysoka.** Źródło: raport, krok 0.

Podstawowy sposób pracy zakładany przez raport: bierzesz zeszłoroczny konkurs, klikasz "skopiuj" i zmieniasz to, co się zmieniło. Kopia przenosi ustawienia, formularz wniosku, karty oceny i wzory dokumentów. Bez tego operator przy drugim naborze przepisuje wszystko ręcznie, a raport typuje ten wariant jako ten, który pokryje większość potrzeb. Siada na T-22 i na T-20.

### R-12 · Kategorie kosztów jako ustawienie konkursu

**Waga: wysoka.** Źródło: raport, krok 1.4.

Lista kategorii kosztów i ich limitów jest ustawieniem konkursu, nie stałą w systemie. Domyślnie trzy: bezpośrednie, rozwój instytucjonalny, pośrednie. **Wyłączenie kategorii chowa nie tylko jej tabelę w budżecie, ale i odpowiadającą jej sekcję opisową w części o projekcie** (pozycja 6b). To potrzebne od razu, bo rozwój instytucjonalny jest w obu wzorach dla grup nieformalnych oznaczony do usunięcia. Siada na T-20, T-24, T-31.

### R-13 · Suma kontrolna wniosku

**Waga: średnia.** Źródło: decyzja D15 na Trello, bez karty implementacyjnej.

Suma powstaje już dla wersji roboczej, zmienia się z każdą zapisaną wersją, format `0a55-22c2-b414`. Wydruk niesie tę samą sumę na każdej stronie. Ekran wniosku pokazuje numer, wersję, datę ostatniego zapisu i sumę. Decyzja jest, karty nie ma. Siada na T-29, T-33, T-44.

### R-14 · Publiczne archiwum wyników

**Waga: średnia.** Źródło: raport, sekcja o granicy publiczne kontra zalogowane, decyzje RD1 i RD3.

Lista projektów sfinansowanych w poprzednich latach razem z kwotami, dostępna bez konta. Nazwa organizacji, tytuł projektu, kwota; przy grupach nieformalnych nazwa grupy **bez imion i nazwisk członków**. Jest to zwykle wymóg przejrzystości wydatkowania środków publicznych. Siada na T-23 i T-42.

### R-15 · Retencja karty organizacji

**Waga: średnia.** Źródło: raport, sekcja o danych osobowych. Zależy od `R-01`.

Karta nie należy do żadnego konkursu, więc parametr retencji z kroku 1.4 jej nie obejmuje. Propozycja raportu: karta bez wniosku przez trzy lata trafia na listę do przeglądu u administratora, a nie kasuje się sama; karta powiązana ze złożonym wnioskiem żyje tak długo, jak najdłuższy termin retencji wśród jej wniosków.

### R-16 · Klauzula informacyjna dla osób trzecich

**Waga: wysoka.** Źródło: raport, sekcja o danych osobowych.

Formularz zbiera dane trzech członków grupy nieformalnej oraz osób uprawnionych do reprezentowania: imię, nazwisko, adres, telefon, e-mail. To dane osób, które nie są wnioskodawcą, i muszą dostać informację o przetwarzaniu. Dziś we wzorze jest jedna klauzula, dla wnioskodawcy. Siada na T-47 i na B-05.

### R-18 · Prawdziwa wysyłka maili

**Waga: wysoka.** Źródło: kod, `EmailSenderService.cs`.

`IEmailSender` na produkcji **wyłącznie loguje treść maila**, nie wysyła nic naprawdę. To była świadoma decyzja pierwszej wersji weryfikacji adresu, ale bez prawdziwego dostawcy SMTP nie działa ani weryfikacja konta, ani reset hasła, ani powiadomienie o wyniku konkursu, czyli najważniejsza wiadomość, jaką ten system wysyła. Brak karty na Trello.

### R-19 · Pola rejestracji

**Waga: niska.** Źródło: raport, krok 2.1 kontra `RegisterRequest.cs`.

Dzisiejszy kontrakt ma cztery pola: adres, hasło, imię, nazwisko. Raport wymienia dziewięć: dochodzi powtórzenie adresu, powtórzenie hasła, telefon kontaktowy i dwie zgody (regulamin, przetwarzanie danych). Powtórzenia można obsłużyć na froncie, ale telefon i zgody to dane, których dziś nie zbieramy, a zgody mają skutek prawny.

### R-20 · Nabór ciągły

**Waga: średnia.** Źródło: raport, krok 1.1.

Konkurs bez daty zakończenia. Reguła odcięcia terminu (`T-21`) musi mieć jawną gałąź dla takiego konkursu, a nie traktować pustą datę jak przeszłość. Schemat wymaga dziś `start_date < end_date`, więc **nabór ciągły nie da się dziś zapisać**: to check constraint do przemyślenia razem z T-20.

### R-21 · Eksport listy wniosków i publikacja listy rankingowej

**Waga: średnia.** Źródło: raport, kroki 4.1 i 6.1.

Eksport listy wniosków do arkusza i PDF (T-35 to ma w kryteriach, karta o tym nie mówi) oraz eksport listy rankingowej do PDF, XLSX i CSV z możliwością opublikowania jej na stronie bez przepisywania (T-42, T-44).

### R-22 · Sekcja "Dane do dokumentów" w ustawieniach konkursu

**Waga: średnia.** Źródło: raport, krok 6.2.

Dane urzędowe potrzebne wzorom dokumentów, a niepochodzące ani z wniosku, ani z limitów: organ wydający zarządzenia, numery i daty zarządzeń, uchwała o programie współpracy, paragraf i klasyfikacja budżetowa. Osobna sekcja w ustawieniach konkursu. Jeśli są zbędne, zostają puste.

### R-23 · Wersja papierowa jako przełącznik konkursu

**Waga: średnia.** Źródło: raport, krok 1.3 i decyzja RD4.

Trzy pola w ustawieniach konkursu plus wariant wymagalności załącznika "wymagany papierowo" plus kolumna z datą wpływu na liście wniosków. **Nie budujemy pod papier osobnego obiegu.**

### R-24 · Transze wypłat i aneksy

**Waga: niska, ale z pułapką.** Źródło: raport, krok 6.4.

Oba są poza MVP i to jest zgodne z Trello. Pułapka jest w modelu: **model umowy ma dopuszczać wiele wypłat od początku**, nawet jeśli interfejs pokazuje jedną. Dorobienie tabeli transz później jest tanie; rozbicie pojedynczej kwoty na wiele wypłat po wdrożeniu, gdy w bazie leżą podpisane umowy, nie jest.

### R-25 · Ekran logowania po stronie frontu

**Waga: wysoka.** Źródło: stan repozytorium, znalezione przy T-15.2.

Backend ma `POST /login` od T-12.3, a front nie ma ekranu, który by go wołał. Żadna karta tego nie obejmuje: T-12.3 jest backendowa, a T-15.2 i T-15.3 budują ramy paneli i mają ekrany logowania poza zakresem. Strażnik sesji panelu wnioskodawcy przekierowuje dziś na `/login?returnUrl=...`, czyli na trasę, której nie ma.

Do zrobienia razem z ekranem: obsługa `returnUrl` (backend już go waliduje, `Services/LoginLandingPath.cs`), jeden komunikat na wszystkie błędy poświadczeń, czytelny komunikat 429 po blokadzie konta z T-12.5 i wejście w reset hasła z T-12.4.

### R-26 · Konkurs raz dezaktywowany nie wraca

**Waga: niska.** Źródło: stan repozytorium, znalezione przy T-20.

`DELETE /competitions/{id}` oznacza konkurs jako nieaktywny, i nie ma trasy, która by to cofnęła. Karta T-20 prosi o dezaktywację i nic nie mówi o przywracaniu, więc endpointu nie dorobiono, ale drzwi są w tej chwili jednostronne: operator, który dezaktywuje niewłaściwy wiersz, potrzebuje dostępu do bazy.

Numer jest oddawany (indeks unikalny jest filtrowany po `is_active`), więc konkurs da się odtworzyć od nowa pod tym samym numerem. To wystarcza na dziś i jest powodem, dla którego waga jest niska. Konsekwencja uboczna, warta zapamiętania przy umowach i sprawozdawczości: **numer konkursu nie jest stabilnym identyfikatorem historycznym** przez cykl dezaktywacji i odtworzenia, stabilne jest `id`.

**Dotyka:** T-22.
**Co zrobić:** dorobić przywracanie razem z ekranem operatora, który w ogóle pokazuje konkursy nieaktywne.

### R-27 · Zaplanowana data publikacji konkursu

**Waga: średnia.** Źródło: raport, krok 1.1, znalezione przy T-20.

Raport wymienia w kroku 1.1 pole "data publikacji konkursu", czyli moment, od którego konkurs staje się widoczny publicznie. T-20 zbudował publikację jako **świadomy akt operatora** (`POST /competitions/{id}/status`), a `published_at` jest stemplem tego, co się zdarzyło, nie ustawieniem na przyszłość. Dwa mechanizmy na jedną rzecz nie mogą stać obok siebie bez rozstrzygnięcia, który wygrywa.

Do rozstrzygnięcia jest też, co ma się dziać, gdy zaplanowana data minie, a operator nigdy nie kliknął publikacji. Publikacja z zegara wymagałaby trzeciego wiersza terminowego w tabeli przejść, a T-22 wprost prosi o potwierdzenie przed publikacją, więc te dwa wymagania trzeba pogodzić, a nie wybrać po cichu.

**Dotyka:** T-20a, T-22.
**Co zrobić:** zapytać zamawiającego, czy publikacja ma być planowana z datą, czy klikana. Do czasu odpowiedzi zostaje klikana.

---

## Pytania otwarte, na które nikt jeszcze nie odpowiedział

Nie są rozbieżnościami, tylko dziurami. Każda warta jest jednego zdania w najbliższym mailu do zamawiającego.

| Pytanie | Od czego zależy odpowiedź | Co bez niej robimy |
|---|---|---|
| Czy budżet potrzebuje czwartej tabeli ze źródłami finansowania | wzór nie zbiera wkładu własnego, więc udział dotacji zawsze wychodzi 100%; decyzja D11 zakłada, że wkłady istnieją | budujemy trzy tabele, D11 działa na wkładach pustych |
| Czy REGON jest potrzebny umowie | wzór wniosku go nie zbiera, my dopisaliśmy | pole zostaje nieobowiązkowe |
| Jak rozstrzygamy remis w rankingu | regulamin | nie budujemy rankingu |
| Co przy rezygnacji po przyznaniu dotacji, czy jest lista rezerwowa | regulamin | nie budujemy stanu rezygnacji |
| Czy istnieje ścieżka odwoławcza od oceny | regulamin | nie projektujemy tego |
| Który z trzech wariantów edycji formularza jest naturalny dla OCWIP | odpowiedź zamawiającego na cztery pytania z raportu | budujemy kopię z poprawkami plus minimum edytora |
| Czy któryś konkurs jest prowadzony w trybie art. 13 ustawy | gdyby tak, obowiązuje ustawowy wzór oferty i cały kreator traci sens dla tego konkursu | zakładamy regranting |

---

## Bieżący stan drzewa roboczego

Rzeczy, które nie są rozbieżnością projektu, tylko stanem tego klonu repozytorium. Skasuj wiersz, gdy przestanie być prawdziwy.

| Co | Stan |
|---|---|
| `RAPORT-proces-i-pola.docx` w katalogu głównym | **nie commitujemy go.** To materiał od zamawiającego, a te idą do Notion, nie do repozytorium. Jego treść jest przepisana do [`pola.md`](pola.md) i [`proces.md`](proces.md), bo tam jest już specyfikacją, a nie materiałem |
| Gałąź `main` | 43 commity za `dev`. Release do `main` nie był robiony od czasu szkieletu i jest decyzją człowieka, nie agenta |
