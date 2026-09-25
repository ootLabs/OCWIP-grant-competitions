# Blokery

Czego brakuje, co przez to stoi i **co mimo to wolno zrobić**. Ostatnia kolumna jest najważniejsza: bloker rzadko blokuje całą kartę, a "zablokowane" wpisane bez sprawdzenia zatrzymuje pracę, której nic nie zatrzymuje.

Zasada nadrzędna: **nie zgadujemy w modelu danych.** Brakujący dokument to karta w liście "Zablokowane: czeka na klienta" z komentarzem, czego konkretnie brakuje, a nie wymyślona encja. Ale zawężenie zakresu do części, która nie zależy od dokumentu, nie jest zgadywaniem i jest dokładnie tym, co masz robić.

---

## B-01 · Brak spisu pól formularza wniosku

Karta: <https://trello.com/c/uOnJviAY>

**Stan: w dużej części nieaktualny.** Karta powstała, gdy nie mieliśmy żadnego realnego wzoru. Dziś mamy trzy wzory wniosku na 2026 razem z komentarzami zamawiającego, rozebrane pole po polu w [`pola.md`](pola.md). Spis pól **jest**.

Co zostało otwarte: czwarta tabela budżetu ze źródłami finansowania (patrz pytanie w [`pola.md`](pola.md), część III) oraz to, czy REGON jest potrzebny umowie.

**Co wolno robić:** T-24, T-26, T-28 w całości. Schemat projektujemy tak, żeby brak ostatecznej listy go nie blokował, a mamy listę znacznie lepszą niż zakładała karta.

**Do zrobienia na karcie:** zaktualizować opis, bo w obecnym brzmieniu blokuje rzeczy, które nie są zablokowane.

---

## B-02 · Brak karty oceny i zasad punktacji

Karta: <https://trello.com/c/Ch6545Yd> · **Bloker najcięższy.**

**Stan 2026-09-25: dokumenty są, czekają na rozebranie.** Karta oceny formalnej (załącznik 2), karta oceny merytorycznej (załącznik 3), regulamin konkursu i regulamin komisji (załącznik 6) konkursu Kierunek NOWE FIO 2026 leżą publicznie na stronie ogłoszenia: <https://ocwip.pl/ruszyl-konkurs-kierunek-nowe-fio-2026/>. Odpowiadają na wszystko, czego brakuje niżej: 8 kryteriów formalnych spełnia albo nie spełnia, 4 kryteria merytoryczne z punktacją 20, 16, 10 i 4 (razem 50) i uzasadnieniem przy każdym, kwestionowane pozycje budżetu z kwotą rekomendowaną, 3 kryteria strategiczne po 1 punkcie. Dwóch ekspertów, wynik to **suma** (maksimum 100), próg 50 bez punktów strategicznych, **remis rozstrzyga wcześniejsze złożenie wniosku**, odwołanie tylko od oceny formalnej (3 dni robocze). Rozjazdy z raportem (suma albo średnia jako ustawienie, ostrzeżenie przy rozbieżności 30%, której regulamin nie zna, brak kryterium nr 4 na karcie) idą do `rozbieznosci.md` razem z rozebraniem. Model karty oceny jako **dane w bazie** (D16) do przeglądu, zanim trafi do kodu; dopiero wtedy odblokowujemy T-38 i dalej w `kolejka.md`.

**Czego brakuje:** wzoru karty oceny formalnej i merytorycznej, czyli konkretnych pytań, kryteriów i punktacji. Do tego odpowiedź, jak rozstrzygamy remis.

**Czego już nie brakuje, wbrew opisowi karty.** Raport odpowiada na trzy z czterech pytań otwartych: ilu recenzentów (RD12: dwóch, jako ustawienie konkursu), co przy rozbieżnych ocenach (RD12: ostrzeżenie powyżej 30% skali, decyduje operator z uzasadnieniem), czy ocena jest anonimowa (krok 5.5: wnioskodawca nie widzi danych oceniającego, system widzi).

**Co blokuje:** treść kart oceny, czyli `T-38b` i część `T-39` dotyczącą remisu. Przez ranking pociąga za sobą `T-42`, `T-43` i `T-44` w części wynikowej.

**Czego NIE blokuje.** Pełna tabela jest w [`M5-ocena.md`](M5-ocena.md), sekcja "Co B-02 blokuje naprawdę". W skrócie: mechanizm oceny, dwa etapy, ustawienia oceny w konkursie, powołanie komisji, oświadczenie o konflikcie interesów, losowanie i przypisanie, kwota rekomendowana, dokumenty komisji.

**Co zrobić:** podzielić `T-38`, `T-39` i `T-44` na część mechanizmu i część treści, a potem ruszyć część mechanizmu. Podział idzie przez karty na Trello, nie po cichu.

---

## B-03 · Brak wzoru umowy

Karta: <https://trello.com/c/WQQFgssE>

**Stan 2026-09-25:** ramowy wzór umowy (załącznik 5, 20 paragrafów) leży publicznie na stronie ogłoszenia: <https://ocwip.pl/ruszyl-konkurs-kierunek-nowe-fio-2026/>. Przy grupie z patronem umowę podpisuje operator z patronem i członkami grupy. Otwarte: czy to wersja od prawnika OCWIP, o którą pyta karta, oraz aneksy.

**Czego brakuje:** wzoru umowy od prawnika OCWIP oraz wskazania, które pola zaciągają się automatycznie z wniosku, a które operator wpisuje ręcznie.

**Co blokuje:** `T-45` w części dotyczącej konkretnej umowy.

**Czego nie blokuje.** Raport wylicza **spis znaczników, które obecne narzędzie udostępnia**, wraz z podziałem na te mające pokrycie we wzorze wniosku na 2026 i te bez pokrycia ([`pola.md`](pola.md), sekcja "Znaczniki"). To wystarcza, żeby zbudować **mechanizm wzorów**: ustawienia wzoru, osadzanie znaczników, generowanie do PDF i RTF, generowanie hurtem. Mechanizm jest potrzebny wcześniej niż umowa, bo ten sam mechanizm produkuje protokół komisji i listę obecności (krok 5.6).

**Otwarte pytanie z karty, do zadania przy okazji:** czy zdarzają się aneksy do umów albo zmiany budżetu w trakcie realizacji.

---

## B-04 · Brak wzoru sprawozdania

Karta: <https://trello.com/c/bdcKt7iH>

**Stan 2026-09-25:** trzy wzory sprawozdania (załączniki 4a, 4b i 4c, po jednym na rodzaj wnioskodawcy) leżą publicznie na stronie ogłoszenia: <https://ocwip.pl/ruszyl-konkurs-kierunek-nowe-fio-2026/>. Część finansowa jest, w kategoriach A, B i C budżetu z wniosku, z numerem dokumentu księgowego, wartością planowaną i faktyczną.

**Czego brakuje:** wzoru sprawozdania oraz odpowiedzi, czy sprawozdanie ma część finansową.

**Co raport już rozstrzyga:** część finansowa **jest** (rozliczenie wydatków wobec budżetu z wniosku, kwota do zwrotu liczona przez system, uznawanie kosztów pozycja po pozycji). Sprawozdania częściowe i końcowe, przy czym częściowe włącza się w ustawieniach konkursu i domyślnie jest wyłączone. Zasada "było i jest". Wzór doda pola wykonania rzeczowego.

**Co blokuje:** `T-50`, czyli całą sprawozdawczość. Ta karta jest i tak pierwsza do wycięcia, więc bloker nie zatrzymuje niczego pilnego.

---

## B-05 · RODO, brak ustaleń formalnych, a w danych są PESEL-e

Karta: <https://trello.com/c/47DSAWe2>

**Czego brakuje:** kontaktu z inspektorem ochrony danych OCWIP, treści klauzul informacyjnych, zakresu umowy powierzenia przetwarzania.

**Co blokuje:** formalną część `T-47` oraz klauzule w formularzu.

**Czego nie blokuje:** technicznej części `T-47`, czyli szyfrowania pól wrażliwych, przeglądu logów, weryfikacji braku kaskad i twardych DELETE, poszerzenia kolumn `nip` i `pesel` pod szyfrogram. To jest robota, którą trzeba wykonać niezależnie od tego, co ustali prawnik, i **nie da się jej dokleić na końcu**.

**Czego raport dokłada do tej karty:** klauzula informacyjna dla osób trzecich (członkowie grupy nieformalnej i osoby uprawnione do reprezentowania, których dane zbieramy, a które nie są wnioskodawcą) oraz retencja karty organizacji, która nie należy do żadnego konkursu. Patrz `R-15` i `R-16` w [`rozbieznosci.md`](rozbieznosci.md).

---

## B-06 · Brak deadline'u i budżetu

Karta: <https://trello.com/c/NSaJwkUJ>

**Czego brakuje:** kiedy najbliższy konkurs miałby ruszyć na naszym narzędziu (to wyznacza deadline MVP), budżetu i modelu rozliczenia projektu, oraz kto płaci za hosting w roku czwartym.

**Co blokuje:** planowanie dalej niż najbliższy sprint, `T-48` i `T-49`.

**Czego nie blokuje:** niczego w kodzie. To jest bloker projektowy, nie techniczny.

---

## B-07 · Nieznany los danych historycznych z Witkaca

Karta: <https://trello.com/c/nOeb6e9h>

**Czego brakuje:** odpowiedzi, czy trzeba przenieść dane historyczne i czy obecne narzędzie pozwala je wyeksportować.

**Co blokuje:** ewentualną migrację, której dziś nie ma w zakresie.

**Co zrobić, jeśli odpowiedź brzmi nie:** zapisać to jako świadomą decyzję w [`../zakres.md`](../zakres.md), a nie przemilczeć, bo za dwa lata ktoś zapyta. Raport nazywa to wprost jako pozycję poza zakresem.

---

## B-08 · Logotypy źródeł finansowania na dokumentach

Karta: <https://trello.com/c/WrXHp8iv> · Karta nie ma opisu.

Odnotowane, gdzie na dokumentach pojawiają się logotypy źródeł finansowania. **Wchodzi w T-32** (załączniki) i w mechanizm wzorów dokumentów. Przed startem T-32 sprawdź komentarze na tej karcie, bo opis jest pusty i cała treść może siedzieć tam.

---

## B-09 · Model danych stoi na ośmiu niepotwierdzonych założeniach

Karta: <https://trello.com/c/nF5CePKJ> · **Bloker o najkrótszym terminie przydatności.**

Model danych jest zbudowany i siedzi w migracjach. Osiem decyzji w nim to założenia, nie ustalenia. Pełna tabela z kolumną "co się stanie, jeśli jest błędne" jest w [`../model-danych.md`](../model-danych.md).

**Cztery są już wypalone w schemacie:** relacja użytkownik do podmiotu, zakres unikalności numeru wniosku, moment nadania numeru oraz zachowanie dezaktywowanego konta. Każde z nich, jeśli jest błędne, oznacza migrację, a nie poprawkę w kodzie. **Dziś migracja jest bezkosztowa, bo baza jest pusta. Po pierwszych prawdziwych danych przestaje być.**

Termin, do którego odwoływały się karty T-11.2, T-11.3 i T-11.4 (spotkanie 27.08), minął, a założenia zostały niepotwierdzone.

**Raport odpowiada na jedno z nich, i to na niekorzyść schematu.** Relacja użytkownik do podmiotu jeden do jednego jest w raporcie zastąpiona kartą organizacji z dostępem wielu osób (RD7). To nie jest drobiazg do dopisania: to tabela pośrednicząca, prośby o dostęp, zatwierdzanie przez założyciela i awaryjnie przez administratora, oraz siedmiodniowa ścieżka eskalacji. Patrz `R-01`.

**Co robić do czasu potwierdzenia:** każdą nową zależność od relacji użytkownik do podmiotu przepuszczać przez jedną metodę, którą da się później podmienić na sprawdzenie po organizacji. Nie rozsypywać `user.EntityId` po serwisach.

---

## B-10 · Cztery pytania o wielkość kreatora formularzy bez odpowiedzi

Karta: <https://trello.com/c/lOrnpYeE>, wcześniej brak, wpisane przy realizacji T-26 2026-09-22.

**Stan 2026-09-25: T-26a odłożone świadomie decyzją D16** (<https://trello.com/c/WH7px80E>): najpierw cały proces jako dane w bazie (formularz, karty oceny, umowa, sprawozdanie), kreator sekcji i budowa od zera dopiero na tym modelu. Cztery pytania nadal warto zadać, razem z doprecyzowaniem, czy "zbudować" w D2 oznacza budowę od zera.

Raport zadaje zamawiającemu cztery pytania, zanim da się zbudować edytor większy niż kopiowanie z poprawkami (`docs/runbook/M3-formularze.md`, sekcja T-26): co realnie zmienia się między konkursami, jak duża jest zmiana, czy kolejność tego, co OCWIP chce zmieniać sam, jest dobrze odczytana, i w jakiej formie edycja jest dla nich naturalna. Trzy warianty, od najprostszego: wypełnianie pól ustawień, kopia z poprawkami, pełny edytor.

**Co blokuje:** `T-26a` (dodawanie, usuwanie i przestawianie sekcji, budowanie formularza od zera). Dowolna implementacja bez odpowiedzi byłaby zgadywaniem dokładnie tego, o co pytają cztery pytania, nie zawężeniem zakresu.

**Czego nie blokuje:** `T-26` w zakresie zawężonym (kopiowanie formularza z konkursu, edycja pól w istniejących sekcjach, kolumny w istniejących tabelach) jest zrobione i nie czeka na to ustalenie, bo mieści się w każdym z trzech wariantów raportu.

**Co zrobić, gdy odpowiedź przyjdzie:** dopisać ją jako decyzję (kolejny numer `D`), zaktualizować `M3-formularze.md` i dopiero wtedy ruszyć `T-26a` z konkretnym zakresem zamiast domyślnego "pełny edytor".

---

## Dokumenty, na które czekamy

Raport wypisuje pięć. Dwa pierwsze są blokerami, reszta nie zatrzymuje prac. Wystarczy każda wersja, jaką zamawiający ma pod ręką, choćby robocza albo zeszłoroczna: do pracy nie potrzebujemy dokumentu podpisanego, tylko takiego, z którego widać strukturę i pola.

| Dokument | Co z niego rozstrzygamy | Pilność | Bloker |
|---|---|---|---|
| Karta oceny formalnej i merytorycznej | konkretne pytania, kryteria i punktacja | bloker | B-02 |
| Regulamin konkursu | ścieżka odwoławcza albo zastrzeżenia do oceny, co przy rezygnacji po przyznaniu dotacji, czy istnieje lista rezerwowa | bloker | B-02, częściowo nowy |
| Umowa o dofinansowanie | które znaczniki są realnie potrzebne i czy umowa nie zaciąga danych, o które wniosek nie pyta | wysoka | B-03 |
| Wzór sprawozdania | pola wykonania rzeczowego oraz kto i gdzie oznacza wydatek jako nieuznany | średnia | B-04 |
| Fragment umowy z grantodawcą (na przykład z NIW przy środkach FIO) | co wniosek musi zawierać z narzuconych wymogów i jakie obowiązki dostępności oraz sprawozdawczości OCWIP przejmuje | średnia | brak karty |

**Regulamin nie ma dziś własnego blokera**, a rozstrzyga trzy rzeczy, których nie umiemy opisać: ścieżkę odwoławczą, zachowanie przy rezygnacji po przyznaniu dotacji i istnienie listy rezerwowej. Warto założyć osobną kartę zamiast doklejać to do B-02.

**Fragment umowy z grantodawcą** też nie ma karty, a może narzucić wymogi na kształt wniosku i na obowiązki sprawozdawcze.
