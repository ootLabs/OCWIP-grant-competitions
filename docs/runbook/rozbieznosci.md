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

**Waga: wysoka, podniesiona przy T-23.** Źródło: stan repozytorium, znalezione przy T-15.2.

Backend ma `POST /login` od T-12.3, a front nie ma ekranu, który by go wołał. Żadna karta tego nie obejmuje: T-12.3 jest backendowa, a T-15.2 i T-15.3 budują ramy paneli i mają ekrany logowania poza zakresem. Strażnik sesji panelu wnioskodawcy przekierowuje dziś na `/login?returnUrl=...`, czyli na trasę, której nie ma.

**Od `T-23` to już nie jest wyłącznie sprawa strażnika panelu.** Przycisk "Wypełnij wniosek" na publicznej stronie konkursu prowadzi na `/login?returnUrl=/competitions/<id>`, czyli dziś na 404. Jest to jedyna droga z ogłoszenia do wniosku, więc brakujący ekran przestał być niedogodnością dla zalogowanych i stał się przerwaną ścieżką dla każdego, kto trafi na konkurs z odnośnika. Karta na ten ekran jest teraz potrzebna przed `T-34`, nie razem z nim.

**Zamknięte 2026-09-23 kartą `T-12.7` (Trello `h5Dz9yj2`).** Pozostałe ekrany konta (rejestracja, potwierdzenie adresu, reset hasła), na które prowadzą linki z logowania i z maili, dostały kartę `T-12.8` (`xkyhzw7r`).

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

### R-28 · Moment ręcznego zamknięcia naboru nie jest nigdzie zapisany

**Waga: niska.** Źródło: repozytorium, znalezione przy T-21.

Tabela przejść pozwala operatorowi zamknąć nabór przed jego datą, a schemat nie ma kolumny na to, kiedy to zrobił. Reguła odcięcia (`CompetitionIntake`) oddaje więc w takim przypadku **brak** momentu zamknięcia zamiast daty z kolumny: data, która jeszcze nie nadeszła, opisywałaby zamknięcie, którego nie ma, i dałaby licznikowi z T-23 coś do odliczania na konkursie już zamkniętym. Cena jest taka, że komunikat mówi wtedy "Nabór został zamknięty" bez godziny, choć D12 woli podać wartość graniczną.

**Dotyka:** T-21, T-23, T-33.
**Co zrobić:** gdy okaże się, że wnioskodawcy pytają "o której to się zamknęło", dołożyć kolumnę `intake_closed_at` stemplowaną przy ręcznym przejściu i podawać ją w komunikacie. Do tego czasu brak daty jest uczciwszy niż data zmyślona.

---

### R-29 · Kryterium T-21 o ścieżkach zapisu wyprzedza swoje endpointy

**Waga: niska.** Źródło: Trello kontra kolejność kolejki, znalezione przy T-21.

Checklista karty `jU68qLLX` wymaga, żeby złożenie, autozapis i upload załącznika pytały regułę odcięcia. Żadna z tych ścieżek jeszcze nie istnieje: to `T-29`, `T-32` i `T-33`, a ich sekcje ZALEŻNOŚCI wymieniają `T-21` jako blokera, więc kolejność jest właśnie taka. Strażnik napisany dzisiaj byłby kodem bez wywołania i bez testu.

**Dotyka:** T-21, T-29, T-32, T-33.
**Co zrobić:** ten jeden punkt checklisty zostaje nieodhaczony i przechodzi na trzy karty, które go realnie domkną. Każda z nich woła `CompetitionIntake` zamiast porównywać daty u siebie.

**Stan: zamknięte po stronie autozapisu i załączników (T-29, T-32).** `ApplicationService` i `AttachmentService` wołają `CompetitionIntake.For` przed każdym zapisem. Zostaje `T-33` (złożenie oferty).

---

### R-30 · Wzory załączników do pobrania czekają na przechowywanie plików

**Waga: średnia.** Źródło: kryteria akceptacji T-23 kontra schemat, znalezione przy T-23.

Kryterium "wzory załączników do pobrania bez logowania" nie ma na czym stanąć: `competition_attachments` niesie tytuł, opis, wymagalność i dozwolone formaty, ale **nie ma kolumny na sam plik wzoru**, bo przechowywanie plików to `T-32`. Granica została tak postawiona świadomie przy `T-20a` i jest opisana w [`M2-konkurs.md`](M2-konkurs.md). Strona konkursu pokazuje więc, co przygotować, i nie oferuje pobrania wzoru.

**Dotyka:** T-23, T-32.
**Co zrobić:** ten jeden punkt checklisty `T-23` zostaje nieodhaczony i domyka go `T-32` razem ze wzorem pliku do 10 MB. Wtedy do `CompetitionAttachments` dochodzi odnośnik do pobrania, anonimowy jak reszta tej strony.

**Stan: przechowywanie już istnieje (T-32), wzór pliku nadal czeka.** `IAttachmentStorage`/`AttachmentStorageService` przechowują dziś załączniki WNIOSKODAWCY (tabela `attachments`), i tej samej mechaniki da się użyć dla wzoru operatora. Nie zrobiono tego przy okazji, bo `CompetitionAttachmentRequest.cs` wprost mówi, że lista `competition_attachments` **wjeżdża w całości i zastępuje zapisaną** przy każdej edycji: dzisiejsze wiersze nie mają stabilnego identyfikatora między edycjami, więc plik wzoru dowiązany do wiersza dzisiejszym mechanizmem osierocałby się przy pierwszej zmianie listy w kreatorze. Zanim wzór pliku wejdzie, ten kontrakt edycji musi umieć dopasować wiersze (na przykład przez identyfikator w żądaniu), a to jest decyzja o kształcie API operatora, nie o przechowywaniu. Karta na ten kawałek jeszcze nie istnieje.

---

### R-31 · Publiczne archiwum wyników czeka na rozstrzygnięcia

**Waga: średnia.** Źródło: `R-14` zderzone ze stanem produktu przy T-23.

`R-14` sadza archiwum wyników na `T-23`, ale nie ma jeszcze czego archiwizować: nie istnieje wniosek, ocena ani rozstrzygnięcie, więc lista "nazwa organizacji, tytuł projektu, kwota" nie ma źródła. Strony publiczne z `T-23` są miejscem, w którym to archiwum stanie, i nic w nich tego nie blokuje.

**Dotyka:** T-23, T-42, M5.
**Co zrobić:** archiwum wchodzi razem z `T-42`, na gotowe strony publiczne. Przy grupach nieformalnych publikujemy nazwę grupy, bez imion i nazwisk członków.

### R-32 · Generowanie klienta TypeScript wymaga restartu backendu i `npx`

**Waga: niska.** Źródło: praca nad T-25.

`npm run api:generate` w kontenerze frontu kończy się `openapi-typescript: not found`, bo pakiet jest w `devDependencies`, a obraz instaluje zależności produkcyjne; przechodzi przez `npx openapi-typescript@7.13.0`. Drugie: dokument OpenAPI oddaje **poprzedni** build, dopóki kontenera backendu nie zrestartujesz, więc typy wygenerowane zaraz po `dotnet build` nie mają nowych tras i przy okazji cofają część opisów.

**Dotyka:** każdą kartę zmieniającą kontrakt API.
**Co zrobić:** przed generowaniem `docker compose restart backend` i odczekanie na `/openapi/v1.json`. Trwałą poprawką jest instalacja `openapi-typescript` w obrazie frontu albo skrypt robiący oba kroki, i to jest karta na `chore/`, nie robota przy okazji.

---

### R-33 · Załącznik wnioskodawcy nie wie, który wymóg konkursu spełnia

**Waga: średnia.** Źródło: kryteria akceptacji T-34 kontra schemat, znalezione przy T-34.

Kryterium T-34 "załączniki jako kafelki: nazwa, opis, wzór do pobrania" zakłada, że każdy przesłany plik odpowiada jednemu wierszowi `competition_attachments`. Schemat tego nie niesie: `attachments` ma `application_id` i `entity_id`, ale żadnej kolumny wskazującej, który wymóg spełnia. T-33 już to odnotowała we własnym wpisie w `docs/log.md` ("Kompletność wymaganych załączników NIE jest sprawdzana przy złożeniu"), więc to nie jest nowa luka, tylko ta sama, teraz dotykająca frontu wnioskodawcy, nie tylko walidacji przy złożeniu.

Ekran T-34 pokazuje więc dwie osobne listy: czego wymaga konkurs (z `competition_attachments`, tytuł, opis, wymagalność, formaty) i co już przesłano (z `attachments`, nazwa pliku, rozmiar), bez łączenia jednej pozycji z drugą. Lista braków przed złożeniem nie sprawdza kompletności załączników z tego samego powodu, którym kierowało się T-33: backend też tego nie sprawdza, więc front udający, że sprawdza, kłamałby dokładniej niż milczenie.

**Dotyka:** T-33, T-34, R-30 (ten sam obszar, inny kawałek: R-30 to brak wzoru do pobrania, ten wpis to brak powiązania przesłanego pliku z wymogiem).
**Co zrobić:** decyzja o kształcie powiązania (nowa kolumna `competition_attachment_id` na `attachments`, nullable, bo T-32 wciąż przyjmuje pliki, których konkurs nie wymienił z nazwy) nie mieści się w żadnej z dwóch kart. Potrzebuje własnej karty, obejmującej migrację, `AttachmentService.UploadAsync` (przyjęcie identyfikatora wymogu), `ApplicationSubmissionService` (sprawdzenie kompletności przy złożeniu, dziś świadomie pominięte) i front (kafelek na wymóg zamiast wspólnej listy).

### R-34 · Front czyta kontrakt formularza z uwzględnieniem wielkości liter, backend nie

**Waga: średnia.** Źródło: review poprawek do T-35, znalezione przy sprawdzaniu `FormFieldRoles`.

Backend czyta nazwy z definicji formularza przez `FormJsonReader.TryParseName`, czyli **bez względu na wielkość liter**: `"Ratio"` jest tym samym rodzajem wyliczenia co `"ratio"`, i tak samo jest z rodzajami pól, rodzajami limitów i formatami plików. Front ma własne odwzorowanie kontraktu, w którym te same nazwy są literałami małymi literami: `CalculationKind` w `frontend/lib/forms/document-types.ts`, `switch` w `evaluate.ts` z `default: return 0` i porównanie w `format-computed.ts`.

Definicja z `"kind": "Ratio"` przechodzi więc bramkę schematu, zapisuje się dosłownie i wraca do przeglądarki bez zmian, a wtedy backend liczy udział procentowy, podczas gdy ekran wnioskodawcy pokazuje `0,00 zł`. Dziś nikt takiej definicji nie napisał (kreator wpisuje małe litery), więc to jest rozjazd uśpiony, nie awaria.

**Dotyka:** T-24 (kontrakt), T-26 (kreator), T-30 (walidacja), każdą kartę czytającą definicję po stronie frontu.
**Co zrobić:** jedna z dwóch stron musi ustąpić i obie zmiany leżą poza kartą, w której to znaleziono. Albo front normalizuje nazwę przed porównaniem (trzy pliki), albo `TryParseName` zaczyna rozróżniać wielkość liter, co zmienia zachowanie całego kontraktu i wymaga sprawdzenia zasianych definicji. Węższe jest pierwsze, ale wybór to karta, nie robota przy okazji.

---

### R-35 · `POST /verify-email` z identyfikatorem, który nie jest GUID-em, kończy się 500

**Waga: niska.** Źródło: audyt dostępności T-46, przejście po `/verify-email?token=x&userId=y` w przeglądarce.

`VerifyEmailRequest.UserId` jest napisem, a endpoint zamienia go na `Guid` bez sprawdzenia, więc zniekształcony link z maila (ucięty przez klienta poczty, przepisany ręcznie) daje w logu `System.FormatException: Unrecognized Guid format` i odpowiedź 500. Ekran potwierdzenia pokazuje wtedy ogólny komunikat, zamiast "link jest nieważny, wyślij nowy", który dostaje ktoś ze zwykłym przeterminowanym tokenem.

**Dotyka:** T-12.2 (weryfikacja adresu), T-12.8 (ekran potwierdzenia).
**Co zrobić:** identyfikator, którego nie da się przeczytać jako `Guid`, traktować tak samo jak nieznany: ta sama odpowiedź co dla złego tokenu, bez ujawniania, czy konto istnieje (reguła 3 z `AGENTS.md`), plus test. Poprawka jest mała, ale należy do backendu kont, nie do audytu frontu, więc to karta `fix/`, nie robota przy okazji.

---

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
| Czy wniosek oznaczony jako nieaktywny znika własnemu podmiotowi, czy tylko z listy | soft delete z retencji pięcioletniej mówi, co się zachowuje, ale nie komu się pokazuje; operator odpowiada za taki wniosek przez cały ten okres | sonda z T-13.3 nie filtruje po `IsActive`, a decyzja należy do T-29, T-32 i T-33 |

---

## Bieżący stan drzewa roboczego

Rzeczy, które nie są rozbieżnością projektu, tylko stanem tego klonu repozytorium. Skasuj wiersz, gdy przestanie być prawdziwy.

| Co | Stan |
|---|---|
| `RAPORT-proces-i-pola.docx` w katalogu głównym | **nie commitujemy go.** To materiał od zamawiającego, a te idą do Notion, nie do repozytorium. Jego treść jest przepisana do [`pola.md`](pola.md) i [`proces.md`](proces.md), bo tam jest już specyfikacją, a nie materiałem |
| Gałąź `main` | 43 commity za `dev`. Release do `main` nie był robiony od czasu szkieletu i jest decyzją człowieka, nie agenta |
