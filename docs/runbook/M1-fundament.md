# M1 · Fundament

Uwierzytelnianie, autoryzacja i ramy obu paneli. Kamień prawie zamknięty: baza, encje, rejestracja, weryfikacja adresu, role i kontrakt API są na `dev`. Zostało wejście do systemu i to, co z niego wynika.

Kolejność bierzesz z [`kolejka.md`](kolejka.md), nie z tego pliku.

## Już zrobione, nie ruszaj bez powodu

| Karta | Co z tego wyszło |
|---|---|
| T-11.1 | `UseOcwipPostgres` jako jedyne miejsce konfiguracji EF, migracja bazowa, `Database:MigrateOnStartup` |
| T-11.2 do T-11.4 | Sześć tabel domenowych: `users`, `entities`, `competitions`, `form_definitions`, `applications`, `attachments`, osiem check constraintów, zero kaskad |
| T-11.5 | ERD w [`../model-danych.md`](../model-danych.md) plus `scripts/seed.py` |
| T-12.0 | Konta na ASP.NET Core Identity, ale na naszym schemacie: tabela nadal `users`, unikalność na `normalized_email`, `IdentityUserContext` bez `AspNetRoles` |
| T-12.1 | `POST /register`, odpowiedź 202 nierozróżnialna dla adresu wolnego i zajętego |
| T-12.2 | `POST /verify-email` i `/resend-verification`, rosnący throttling odsyłania |
| T-13.1 | Rola jako kolumna z check constraintem, nadawana komendą `grant-role`, nie ekranem |
| T-15.1 | Tokeny brandingowe OCWIP w `app/globals.css`, tryb wysokiego kontrastu, kalkulator WCAG |
| T-17 | Dokument OpenAPI z kodu, typy generowane do `frontend/lib/api-schema.ts`, `apiFetch` z `fieldErrors` |

Dwie rzeczy z tego bloku zostały otwarte i ugryzą dalej: **reaktywacja konta** (dezaktywowane konto blokuje swój adres i podmiot na zawsze) oraz **przydział numeru wniosku** (nic go nie generuje, a para konkurs plus numer jest unikalna). Opis obu w [`../model-danych.md`](../model-danych.md), sekcja "Otwarte punkty implementacyjne".

---

## T-12.3 [P0 / Backend] Logowanie, sesja, wylogowanie

Karta: <https://trello.com/c/7MkNtmIH> · Stan: **w toku**

**Kontekst.** Wejście do systemu dla wszystkich trzech ról. Po zalogowaniu operator trafia do panelu operatora, a wnioskodawca do swojego. To pierwszy moment, w którym role zaczynają cokolwiek znaczyć w interfejsie.

**Zakres.** Logowanie, zarządzanie sesją, wylogowanie, przekierowanie zależne od roli.

**Stack.** .NET plus Next.js. Sposób trzymania sesji jest ustalony w T-17 i tutaj go tylko wdrażasz: ciasteczko HttpOnly z SameSite, nie token w nagłówku. Rola trafia do claimów przy logowaniu, bo `IdentityUserContext` nie ma `AspNetRoles` i kolumna `role` jest jedynym źródłem prawdy.

**Dlaczego jeden komunikat na wszystkie błędy.** Rozróżnienie "nie ma takiego konta" i "złe hasło" pozwala sprawdzić z zewnątrz, kto ma u nas konto. Jeden komunikat na obie sytuacje.

**Dlaczego wylogowanie musi działać po stronie serwera.** Usunięcie ciasteczka w przeglądarce nie unieważnia sesji. Wnioskodawcy będą wchodzić z komputerów w bibliotekach, w siedzibach organizacji i ze sprzętu współdzielonego. `SecurityStamp` z Identity jest do tego mechanizmem, który już jest w schemacie.

**Czego nie robimy tutaj.** Ograniczania liczby prób logowania, to T-12.5. Progi blokady konta i wymóg potwierdzonego adresu przy logowaniu zostały w `IdentityConfiguration.cs` celowo na domyślnych, bo należą do tej karty.

**Zależności.** Blokuje nas: T-12.1, T-12.2, T-17. Blokujemy: T-12.5, T-13.2, T-15.2, T-15.3.

**Kryteria akceptacji (checklista z karty):**

- [ ] Logowanie e-mailem i hasłem; konto niezweryfikowane odrzucone z czytelnym komunikatem
- [ ] Błędne dane, więc JEDEN wspólny komunikat ("nieprawidłowy e-mail lub hasło"), bez podpowiadania, co było nie tak
- [ ] Sesja albo token z określonym czasem życia; ciasteczka httpOnly i secure
- [ ] Wylogowanie faktycznie unieważnia sesję PO STRONIE SERWERA
- [ ] Po zalogowaniu przekierowanie zależne od roli (operator do panelu operatora, wnioskodawca do swojego)
- [ ] Testy: poprawne logowanie, złe hasło, konto niezweryfikowane, wygasła sesja

**Uzupełnienie z raportu.** Po zalogowaniu wnioskodawca wraca **na stronę tego konkursu, z której przyszedł** (krok 3.1), a nie na ogólny pulpit. Kliknięcie "Wypełnij wniosek" przez osobę bez konta prowadzi do rejestracji, potem z powrotem na ten sam konkurs. Adres e-mail służy do logowania, więc jego zmiana wymaga potwierdzenia z nowego adresu: to jest w MVP raportu, ale **nie ma karty na Trello**, patrz `R-08` w [`rozbieznosci.md`](rozbieznosci.md).

**Gdzie to siada.** `backend/src/Ocwip.Api/Endpoints/AccountEndpoints.cs`, `Services/AccountService.cs`, `Configuration/IdentityConfiguration.cs`, testy w `backend/tests/Ocwip.Api.Tests/Endpoints/`.

---

## T-12.4 [P0 / Backend] Reset hasła

Karta: <https://trello.com/c/mRVXGg2U>

**Kontekst.** Wnioskodawcy wracają do systemu rzadko, przy kolejnym naborze. Organizacja ma konto założone dwa lata wcześniej i wraca dopiero teraz. Przy takim rytmie zapomniane hasło to norma, a nie wyjątek. Każde takie zgłoszenie bez działającego resetu trafia jako telefon do OCWIP, czyli do jednej osoby, która i tak prowadzi cały konkurs.

**Zakres.** Ścieżka odzyskiwania hasła oparta o token jednorazowy.

**Dlaczego zmiana hasła unieważnia wszystkie sesje.** Jeśli ktoś resetuje hasło dlatego, że podejrzewa obcy dostęp do konta, to pozostawienie aktywnych sesji czyni cały reset bezużytecznym. Ta sama zasada dotyczy sytuacji, w której konto organizacji przechodzi między pracownikami.

**Dlaczego formularz zawsze odpowiada tak samo.** Tak samo jak przy rejestracji: różnica w odpowiedzi dla istniejącego i nieistniejącego adresu pozwala z zewnątrz sprawdzić, kto ma u nas konto.

**Zależności.** Blokuje nas: T-12.1 i T-12.2, bo korzystamy z tej samej infrastruktury mailowej i tego samego mechanizmu tokenów. Blokujemy: nic, można prowadzić równolegle z T-12.5.

**Kryteria akceptacji (checklista z karty):**

- [ ] Formularz "zapomniałem hasła" przyjmuje e-mail i ZAWSZE odpowiada tak samo (brak enumeracji kont)
- [ ] Token jednorazowy, ważność krótka (proponowane 1 h), unieważniany po użyciu i po zmianie hasła
- [ ] Zmiana hasła unieważnia WSZYSTKIE aktywne sesje użytkownika
- [ ] Nowe hasło przechodzi te same reguły co przy rejestracji
- [ ] Testy: pełna ścieżka, token wygasły, token użyty dwa razy

**Uzupełnienie z raportu.** Reset jest w MVP i **nie wymaga kontaktu z OCWIP** (krok 2.1). Treść wiadomości resetującej jest tekstem systemowym, nieedytowalnym przez OCWIP, w odróżnieniu od wiadomości konkursowych (tabela wiadomości w [`proces.md`](proces.md)).

---

## T-12.5 [P0 / Backend] Ochrona przed brute force

Karta: <https://trello.com/c/MZoxJ6zk>

**Kontekst.** Publiczny formularz logowania, a za nim baza z danymi osobowymi wnioskodawców, docelowo także z PESEL-ami przy umowach. Ograniczenie liczby prób to minimum przy takich danych, a nie nadgorliwość.

**Zakres.** Ograniczenie liczby prób na endpointach logowania, rejestracji, resetu hasła i wysyłki maili weryfikacyjnych.

**Dlaczego limit musi być liczony dwojako.** Limit liczony wyłącznie po adresie IP nie chroni konta, bo atak może iść z wielu adresów naraz. Limit liczony wyłącznie po koncie pozwala złośliwie zablokować cudze konto, wystarczy wpisywać błędne hasło do skutku. Potrzebne są oba mechanizmy naraz.

**Dlaczego obejmuje to także wysyłkę maili.** Endpoint wysyłający maile bez limitu to darmowe narzędzie do zasypywania cudzej skrzynki. Przy okazji pali reputację naszej domeny nadawczej i kolejne maile zaczynają lądować w spamie. W systemie, który mailem informuje o przyznaniu dotacji, to jest realna szkoda.

**Czego nie logujemy.** Nieudane próby logowania zapisujemy do logu, ale nigdy razem z wpisanym hasłem.

**Zależności.** Blokuje nas: T-12.3. Blokujemy: nic formalnie, ale ta karta musi być zamknięta, zanim aplikacja pojawi się pod jakimkolwiek publicznym adresem.

**Kryteria akceptacji (checklista z karty):**

- [ ] Rate limiting na logowanie, rejestrację, reset hasła i wysyłkę maili weryfikacyjnych
- [ ] Limit liczony po adresie IP ORAZ po koncie
- [ ] Po serii nieudanych prób konto tymczasowo blokowane, z komunikatem i czasem odblokowania
- [ ] Nieudane próby logowania trafiają do logu, BEZ zapisywania wpisanego hasła
- [ ] Test symulujący serię prób potwierdza zadziałanie limitu

**Uwaga do istniejącego kodu.** Throttling odsyłania maila weryfikacyjnego już istnieje (`ResendBackoffCalculator`, `IMemoryCache`), ale jest **per konto i tylko w pamięci procesu**. Ta karta dokłada wymiar po adresie IP i decyduje, czy magazyn zostaje w pamięci. Przy skalowaniu do wielu instancji potrzebny jest wspólny magazyn, i to jest moment na tę decyzję.

---

## T-13.2 [P0 / Backend] Warstwa autoryzacji

Karta: <https://trello.com/c/RDyoUhkh>

**Kontekst.** To jest miejsce, w którym system albo chroni dane osobowe, albo je wycieka. Zamawiający postawił sprawę jednoznacznie: wnioskodawca widzi wyłącznie swój kawałek, operator widzi wszystko.

**Zakres.** Reguły dostępu zebrane w jednym miejscu i wpięte we wszystkie chronione ścieżki.

**Stack.** Polityki autoryzacji oparte o wymagania i handlery, zamiast warunków rozsypanych po kontrolerach. Powód jest konkretny: dostęp do wniosku zależy nie tylko od roli, ale od tego, CZYJ jest ten wniosek. Same atrybuty sprawdzające rolę tego nie załatwią, bo dwóch wnioskodawców ma tę samą rolę i różne uprawnienia do tych samych zasobów.

**Zasada nadrzędna.** Brak reguły oznacza brak dostępu. Domyślną odpowiedzią jest odmowa. Endpoint, o którym ktoś zapomniał, ma być niedostępny, a nie otwarty dla wszystkich. Przy tempie, w jakim będzie rosła ta aplikacja, ktoś na pewno kiedyś zapomni.

**Dlaczego 403, a nie 500 ani pusta strona.** Odmowa dostępu to normalny stan aplikacji, nie awaria. Jeśli wraca 500, to znaczy że leci gdzieś nieobsłużony wyjątek, a logi zapełnią się szumem, w którym utopimy prawdziwe błędy. Pusta strona z kolei wygląda dla użytkownika jak zepsuta aplikacja i generuje telefon do OCWIP.

**Zależności.** Blokuje nas: T-13.1, T-12.3. Blokujemy: T-13.3, T-15.2, T-15.3. Żaden panel nie powstanie, dopóki nie wiadomo, kto ma do niego wchodzić.

**Kryteria akceptacji (checklista z karty):**

- [ ] Reguły dostępu w JEDNYM miejscu, nie rozsypane po kontrolerach
- [ ] Operator: dostęp do wszystkich konkursów, wniosków i podmiotów
- [ ] Wnioskodawca: dostęp WYŁĄCZNIE do własnych wniosków i własnego podmiotu
- [ ] Recenzent: dostęp wyłącznie do wniosków mu przypisanych (mechanizm przypisania powstaje później, na razie sam gate)
- [ ] Domyślna odpowiedź to ODMOWA, brak reguły oznacza brak dostępu, nie dostęp
- [ ] Brak dostępu zwraca 403, nie 500 i nie pustą stronę

**Uzupełnienie z raportu.** Raport podaje gotową tabelę widoczności rzeczy niedokończonych i warto wpisać ją do polityk od razu, bo inaczej wróci jako poprawka:

| Co jest niedokończone | Kto to widzi |
|---|---|
| Konkurs roboczy, nieopublikowany | operator i administrator; taki konkurs **nie ma publicznego adresu**, więc nie da się go podejrzeć ani przypadkiem, ani z linku |
| Wniosek roboczy, rozpoczęty i niezłożony | wyłącznie osoby z dostępem do karty tej organizacji; nie widzi go ani OCWIP, ani żaden ekspert, dopóki nie zostanie złożony |
| Karta oceny w toku, niezatwierdzona | ten ekspert, który ją wypełnia, i operator |
| Lista rankingowa przed rozstrzygnięciem | operator i administrator, do momentu ogłoszenia wyników |

Dwie rzeczy z raportu **wykraczają poza to, co jest dziś w modelu**, więc nie wdrażaj ich tutaj, tylko odnotuj: czwarta rola (administrator) i dostęp przypięty do organizacji, a nie do osoby. Oba opisane jako `R-01` i `R-02` w [`rozbieznosci.md`](rozbieznosci.md). Do czasu decyzji buduj politykę tak, żeby dodanie roli było wartością w enumie, a nie przepisaniem handlerów, i żeby sprawdzenie "czyj to wniosek" szło przez jedną metodę, którą da się później podmienić na sprawdzenie po organizacji.

---

## T-13.3 [P0 / Backend] Testy negatywne uprawnień

Karta: <https://trello.com/c/SJflHiIR>

**Kontekst.** Reguła postawiona wprost: wnioskodawca widzi tylko swój kawałek. Reguła bez testu automatycznego to życzenie, nie reguła. Po drugiej stronie są dane osobowe organizacji i osób fizycznych, docelowo także PESEL-e. Ta karta decyduje, czy w ogóle możemy ten system gdziekolwiek wystawić.

**Zakres.** Zestaw testów sprawdzających, że dostęp jest odmawiany dokładnie tam, gdzie ma być odmawiany.

**Na czym polega test podmiany identyfikatora.** Zalogowany wnioskodawca podmienia identyfikator w adresie na identyfikator cudzego wniosku i próbuje go pobrać. Jeśli zobaczy cudze dane, mamy wyciek. To najczęstszy błąd w aplikacjach tego typu i najłatwiejszy do przeoczenia, bo w interfejsie nie prowadzi do niego żaden link, więc podczas ręcznego klikania nikt tego nie znajdzie.

**Dlaczego te testy blokują merge.** Bo są jedyną rzeczą, która pilnuje tej reguły przy każdej kolejnej zmianie w kodzie. Test, który można pominąć, nie chroni niczego. Za pół roku ktoś doda nowy endpoint i to te testy mają zapalić czerwone światło, a nie telefon od organizacji, która zobaczyła cudzy wniosek.

**Zależności.** Blokuje nas: T-13.2, T-11.4, T-11.5. Blokujemy: nic formalnie, ale bez zielonych testów z tej karty aplikacja nie wychodzi poza lokalną maszynę.

**Kryteria akceptacji (checklista z karty):**

- [ ] Test: wnioskodawca A próbuje pobrać wniosek wnioskodawcy B po ID, więc 403
- [ ] Test: wnioskodawca próbuje wejść na endpoint operatora, więc 403
- [ ] Test: recenzent próbuje otworzyć nieprzypisany wniosek, więc 403
- [ ] Test: niezalogowany próbuje wejść na cokolwiek chronionego, więc 401
- [ ] Test: podmiana ID w URL-u nie daje dostępu do cudzych danych (IDOR)
- [ ] Testy wpięte w CI i blokują merge przy niepowodzeniu

**Dane testowe: kształt z seeda, ale odtworzony w kodzie.** `scripts/seed.py` opisuje właściwy układ (wniosek złożony należy do wnioskodawcy 1, roboczy do wnioskodawcy 2) i ten układ jest tym, co odtwarza `PermissionScenario`. Identyfikatorów z seeda **nie da się cytować w tej suicie**, z dwóch powodów: zasiane konta mają w `password_hash` jawny placeholder, więc nie potrafią się zalogować, a każdy test z tej karty przechodzi prawdziwe `POST /login`; do tego suita jedzie na `PostgresDatabaseFixture`, czyli bazie jednorazowej dzielonej przez kolekcję `postgres`, której seed nigdy nie dotyka. Zdanie o cytowaniu stałych identyfikatorów było w tej specyfikacji błędem, poprawionym przy realizacji karty.

---

## T-12.6 [P1 / Backend] Testy e2e ścieżki uwierzytelniania

Karta: <https://trello.com/c/YjDuR42n>

**Kontekst.** Uwierzytelnianie to obszar, w którym awaria kosztuje najwięcej. Konkurs ma twardy termin zamknięcia co do minuty, więc wnioskodawca, który nie może się zalogować ostatniego dnia naboru, po prostu przepada, a OCWIP dostaje telefon z pretensjami i nie ma jak pomóc. Testy poszczególnych kart nie wychwycą sytuacji, w której każdy element działa osobno, a cała ścieżka jest przerwana na styku.

**Zakres.** Jeden automatyczny scenariusz przechodzący pełną ścieżkę: rejestracja, weryfikacja adresu, logowanie, wylogowanie, reset hasła, ponowne logowanie nowym hasłem.

**Dlaczego nie rozbijamy tego na wiele testów.** Pojedyncze przypadki są już pokryte w kartach T-12.1 do T-12.5. Tutaj sprawdzamy wyłącznie, czy te elementy są ze sobą poprawnie połączone. Test ma być jeden i ma być szybki, bo będzie chodził przy każdej zmianie.

**Dlaczego na czystej bazie.** Test, który przechodzi tylko na bazie z ręcznie przygotowanym stanem, przestanie działać po pierwszej zmianie schematu i zostanie wyłączony przez kogoś, komu będzie się spieszyć.

**Zależności.** Blokuje nas: T-12.1 do T-12.5. To karta domykająca blok uwierzytelniania, bierzemy ją ostatnią.

**Kryteria akceptacji (checklista z karty):**

- [ ] Scenariusz przechodzi automatycznie: rejestracja, weryfikacja maila, logowanie, wylogowanie, reset hasła, logowanie nowym hasłem
- [ ] Test odpala się w CI na czystej bazie
- [ ] Czas wykonania poniżej 2 minut

---

## T-15.2 [P1 / Frontend] Shell panelu wnioskodawcy

Karta: <https://trello.com/c/cIONKupZ>

**Kontekst.** Panel, w którym wnioskodawca spędzi cały czas wypełniania wniosku liczącego 5 do 6 stron. Nikt nie robi tego za jednym posiedzeniem, więc ten ekran ludzie będą odwiedzać wielokrotnie przez kilka tygodni trwania naboru.

**Zakres.** Rama aplikacji: nagłówek, nawigacja, ochrona tras, responsywność. Ekrany docelowe mogą być na tym etapie puste.

**Zakres nawigacji.** Moje wnioski, aktualne konkursy, mój profil.

**Dlaczego responsywność nie jest opcjonalna.** Wśród wnioskodawców są grupy nieformalne, czyli trzy osoby fizyczne bez biura i bez firmowego sprzętu. Dla części z nich telefon będzie jedynym urządzeniem, na którym w ogóle otworzą tę stronę.

**Dlaczego nawigacja klawiaturą.** Klient publiczny, środki publiczne, dostępność będzie prawdopodobnie wymogiem formalnym. Sprawdzenie tego teraz, na pustej ramie, zajmuje kwadrans. Sprawdzenie tego na gotowej aplikacji to osobny projekt.

**Zależności.** Blokuje nas: T-15.1, T-13.2, T-17. Blokujemy: T-15.4.

**Kryteria akceptacji (checklista z karty):**

- [ ] Nagłówek z logo OCWIP, nazwą zalogowanego podmiotu i wylogowaniem
- [ ] Nawigacja: Moje wnioski, Aktualne konkursy, Mój profil
- [ ] Layout responsywny, działa na telefonie
- [ ] Trasy chronione: wejście bez logowania przekierowuje na logowanie
- [ ] Nawigacja klawiaturą przechodzi całą stronę w sensownej kolejności

**Uzupełnienie z raportu.** Docelowo w tej nawigacji stoją jeszcze dwie pozycje: **karta organizacji** (raport, krok 2.2) i **sprawozdania** (krok 7.1, pojawiają się na koncie organizacji same, obok umowy). Karta organizacji jest zmianą modelu danych i czeka na decyzję `R-01`, sprawozdania są poza MVP. Zostaw w nawigacji miejsce, ale nie buduj pustych tras na zapas.

---

## T-15.3 [P1 / Frontend] Shell panelu operatora

Karta: <https://trello.com/c/XBITHAH5>

**Kontekst.** Panel dla pracownika OCWIP. Osoba prowadząca konkurs mówi o sobie "ja się kompletnie na tym nie znam", więc prostota jest tutaj wymaganiem funkcjonalnym, a nie kwestią gustu. Jednocześnie to ona widzi w systemie wszystko: wszystkie konkursy, wszystkie wnioski, wszystkie umowy, na bieżąco w trakcie naboru.

**Zakres.** Rama panelu operatora: nawigacja, jednoznaczne oznaczenie trybu, blokada dostępu dla pozostałych ról. Ekrany docelowe mogą być puste.

**Zakres nawigacji.** Konkursy, wnioski, formularze, recenzenci.

**Dlaczego układ ma mieścić 120 wierszy.** Bo tyle ofert spływa w jednym konkursie. Jeśli tabela rozjeżdża się przy stu wierszach, to znaczy że nie działa dokładnie w tym jednym momencie, w którym operator naprawdę jej potrzebuje, czyli zaraz po zamknięciu naboru. Sprawdzamy to na danych testowych, nie na pustej tabeli.

**Dlaczego oznaczenie trybu.** Operator ogląda cudze dane osobowe. Nie może istnieć moment, w którym nie wie, czy patrzy na swój widok, czy na czyjś. To ma znaczenie także przy pokazywaniu ekranu na spotkaniu z kimś z zewnątrz.

**Zależności.** Blokuje nas: T-15.1, T-13.2, T-17. Blokujemy: T-15.4, T-22, T-26, T-35, T-41.

**Kryteria akceptacji (checklista z karty):**

- [ ] Nawigacja: Konkursy, Wnioski, Formularze, Recenzenci (pozycje mogą prowadzić do pustych ekranów)
- [ ] Widoczne oznaczenie, że jesteś w trybie operatora, nie ma dwuznaczności, czyj widok oglądasz
- [ ] Wejście na panel bez roli operator, więc 403
- [ ] Layout mieści tabelę około 120 wierszy bez rozjeżdżania się

**Uzupełnienie z raportu.** Wewnątrz pojedynczego konkursu operator ma dziś osiem zakładek i raport prosi wprost, żeby ten podział zachować: Informacje o konkursie, Nabór, Ocena, Wyniki, Zmiany, Umowy, Aneksy, Sprawozdania (krok 4.1). Zakładki puste w danym stanie konkursu **są wyszarzone z wyjaśnieniem, a nie ukryte**: "Umowy" w trwającym naborze mówi "pojawią się po rozstrzygnięciu". To jest tańsze do zrobienia teraz, w ramie, niż po zbudowaniu ekranów. Nawigacja z karty (Konkursy, Wnioski, Formularze, Recenzenci) jest poziomem wyżej i nie kłóci się z tym podziałem.

---

## T-15.4 [P1 / Frontend] Stany puste, ładowanie i błędy

Karta: <https://trello.com/c/3S2t9IdI>

**Kontekst.** Pierwszy konkurs zamawiający zobaczy na całkowicie pustym systemie: zero konkursów, zero wniosków, zero recenzentów. Jeśli puste ekrany będą po prostu puste, pierwsze wrażenie będzie takie, że coś się zepsuło. Puste stany mają mówić, co zrobić dalej. To samo dotyczy wnioskodawcy, który zaloguje się przed ogłoszeniem naboru i zobaczy pustą listę konkursów.

**Zakres.** Stany puste, stany ładowania i strony błędów w obu panelach.

**Dlaczego stany ładowania nie mogą przeskakiwać.** Operator będzie otwierał listy liczące ponad sto wniosków. Jeśli układ skacze w trakcie ładowania, kliknięcia trafiają w nie ten wiersz co trzeba. Przy przypisywaniu dotacji to kosztowna pomyłka.

**Dlaczego komunikaty bez treści technicznych.** Użytkownikami są organizacje pozarządowe i grupy nieformalne, a po stronie operatora osoba, która sama mówi, że nie zna się na technikaliach. Komunikat ze stosem wywołań nie pomoże nikomu, a przy okazji ujawnia na zewnątrz strukturę aplikacji.

**Zależności.** Blokuje nas: T-15.2 i T-15.3. To karta domykająca blok interfejsu.

**Kryteria akceptacji (checklista z karty):**

- [ ] Każda lista ma stan pusty z tekstem po polsku i podpowiedzią następnego kroku
- [ ] Stany ładowania nie powodują przeskakiwania layoutu
- [ ] Strony 403, 404 i 500 mają własny wygląd i drogę powrotną
- [ ] Komunikaty błędów pisane językiem użytkownika, bez treści technicznych i bez stack trace'ów

**Uzupełnienie z raportu.** Raport formułuje regułę komunikatów ostrzej i warto ją tu przyjąć na stałe, bo obowiązuje potem w całym formularzu wniosku: **podpowiedź stoi pod polem zawsze i cicho, błąd pojawia się tylko wtedy, gdy naprawdę jest, i znika, gdy zniknie przyczyna. Żadnych ostrzeżeń włączonych na stałe** (zasada 4 z kroku 3). Druga zasada stąd: na każdym ekranie widać, gdzie się jest, ile zostało i ile czasu do zamknięcia naboru (zasada 9).
