# Plan do pierwszej działającej wersji (v1)

Stan na 2026-09-27, `dev` na `cf7f39a` plus otwarty PR #86 (umowy, T-45). Ten plik to **mapa drogi od dzisiejszego stanu do pierwszego prawdziwego naboru w systemie**. Nie zastępuje [`kolejka.md`](kolejka.md): kolejka mówi, co jest następne, a ten plik mówi, **dlaczego w tej kolejności, co po drodze pęknie i co wtedy robić**.

Jak czytać:

- Agent w pętli czyta sekcje 1, 5 i 6 raz na sesję, a potem specyfikację swojego zadania w sekcji 4.
- Człowiek czyta sekcje 1, 2, 3 i 7. Sekcja 7 zawiera decyzje, na które czeka plan.
- Zadania od T-90 w górę są **propozycją**. Do kolejki wchodzą dopiero z kartą na Trello (zasada z [`rozbieznosci.md`](rozbieznosci.md)). Numeracja zaczyna się od T-90, bo numery T-60 do T-84 krążą w starych opisach kart B-xx i znaczą tam coś innego.

---

## 1. Czym jest v1

**v1 to wersja, na której OCWIP przeprowadza jeden prawdziwy nabór: od ogłoszenia, przez ocenę, do podpisanych umów. Działa na produkcji z kopiami zapasowymi, a na co dzień nie potrzebuje programisty.**

Warunki wyjścia, wszystkie mierzalne:

1. Na **pustej** bazie produkcyjnej zespół wdrożeniowy stawia pierwszy konkurs jedną udokumentowaną procedurą. Nie ma przy tym ręcznego SQL. Od drugiego konkursu operator robi wszystko sam (D2).
2. Nowy użytkownik z ulicy rejestruje się, uzupełnia dane podmiotu, składa kompletny wniosek i dostaje potwierdzenie mailem.
3. Operator przeprowadza ocenę formalną, przypisuje dwóch ekspertów, a eksperci oceniają. Operator zatwierdza wyniki, publikuje listę i wysyła maile.
4. Operator sporządza umowy, a wnioskodawca je widzi. Rezygnacja przesuwa środki na listę rezerwową.
5. Test w przeglądarce przechodzi punkty 1 do 4 na pustej bazie, w CI, przy każdym PR (T-100).
6. Produkcja ma TLS, sekrety spoza repozytorium, szyfrowane PESEL-e, kopię zapasową z **przetestowanym** odtworzeniem i monitoring.
7. Operator ma instrukcję (T-49) i przeszedł próbę generalną (T-120).

**Czego v1 nie potrzebuje na dzień startu naboru:** sprawozdań i rozliczenia. Pojawiają się miesiące po umowach, więc T-50b i T-95 mogą wejść **po** starcie, przez zwykłe wdrożenie. To najważniejsza dźwignia tego planu: **dostarczamy w kolejności osi czasu naboru**, a nie w kolejności kamieni. Warunek: wszystko, co wymaga migracji burzącej dane, jest rozstrzygnięte przed pierwszymi prawdziwymi danymi (bramka G1).

---

## 2. Stan wyjściowy i trzy luki krytyczne

Zrobione jest 64 z 70 zadań kolejki. Ekrany istnieją dla prawie każdego kroku procesu. Przejście całego cyklu po kodzie (2026-09-27) pokazało jednak, że **na świeżej bazie proces pęka w trzecim kroku**. Testy tego nie widzą, bo seed wstawia dane z pominięciem API.

| # | Luka | Skutek | Dowód |
|---|---|---|---|
| L1 | **Nic w systemie nie zakłada podmiotu.** Zarejestrowany użytkownik ma `EntityId = null` | `POST /competitions/{id}/applications` zawsze daje 403 `NoEntity`, więc **nikt nie złoży wniosku**. Ekran pokazuje tylko "Spróbuj ponownie" | `Services/ApplicationService.cs:50`, brak `Entities.Add` w `backend/src`, `panel/applicant/profile/page.tsx` to zaślepka |
| L2 | **Brak treści startowej.** Kreator formularzy umie tylko kopiować, a w repo nie ma formularza wniosku 2026 ani wzoru sprawozdania. Karty oceny publikuje tylko `seed.py` | Na pustej bazie nie powstanie pierwszy formularz. Bez kart ocena formalna i merytoryczna kończy się 409 `NoCard`, bez wzoru sprawozdanie kończy się 409 `NoForm` | `forms/[competitionId]/source-picker.tsx:24`, trasy `evaluation-cards` i `report-form` bez wywołania z frontu |
| L3 | **Konkurs nie ma strony w panelu operatora.** Kreator ogłoszenia wznawia szkic tylko z localStorage jednej przeglądarki | Szkicu z innego komputera nie da się otworzyć ani opublikować, a opublikowanego nie da się poprawić. Nie ma UI dla zamknięcia naboru, dezaktywacji ani stanów po ocenie. Wyniki da się zatwierdzić przy otwartym naborze | `lib/competition-wizard/draft-storage.ts`, lista konkursów linkuje tylko do formularza |

Drugi rząd, ważny, ale nie zatrzymuje procesu: brak powiązania załącznika z wymogiem (R-33), brak zwrotu do poprawy (R-03), strona główna jest deweloperska, a publiczny nagłówek nie ma linku "Zaloguj". "Aktualne konkursy" w panelu wnioskodawcy są wpisane na sztywno jako puste.

**Produkcja: zero.** Oba Dockerfile to obrazy deweloperskie (`dotnet watch`, `next dev`, root), compose publikuje bazę na 0.0.0.0 i ustawia Development. Nie ma kopii zapasowych, a klucze DataProtection giną razem z kontenerem. Pełna lista jest w specyfikacjach T-110 do T-116.

**Pilne niezależnie od planu:** `next 15.5.4` i `react 19.1.1` są podatne na CVE-2025-66478 (RCE w React Server Components, CVSS 10, poprawka od `next 15.5.7`, [advisory](https://nextjs.org/blog/CVE-2025-66478)). Dopóki nic nie stoi publicznie, to nie jest pożar. Musi jednak zniknąć, zanim cokolwiek stanie pod publicznym adresem, także staging (T-90).

---

## 3. Oś czasu i hipoteza terminu

Nabór 2026 zamknął się 19.04.2026. **Hipoteza robocza (do potwierdzenia w B-06): kolejny nabór OCWIP rusza w marcu 2027.** Z niej wynikają daty:

| Kiedy | Bramka | Co musi być prawdą |
|---|---|---|
| do 2026-10-16 | **G0 · Odpowiedzi klientki** | Pakiet decyzji z sekcji 7 wysłany do 2026-10-02. Czego nie ma do 16.10, przyjmujemy według kolumny "domyślnie" |
| do 2026-11-06 | **G1 · Zamrożenie schematu** | R-01 i R-02 rozstrzygnięte, T-47a (szyfrowanie PESEL-i) zmergowane, T-93, T-101 i T-103 zmergowane. Po G1 każda migracja musi być addytywna |
| do 2026-11-27 | **G2 · Gotowość produkcyjna** | T-110 do T-117 zmergowane, staging stoi, odtworzenie z kopii zmierzone, przegląd bezpieczeństwa (T-119) bez otwartych ustaleń wysokich |
| do 2026-12-18 | **G3 · Kandydat v1** | T-100 zielony w CI, T-120 (próba generalna z OCWIP) zrobiona, poprawki z niej zmergowane |
| do 2027-01-31 | **G4 · Produkcja** | Hosting klienta (B-06), T-48 domknięte na docelowym serwerze, T-49 instrukcja, release `dev` do `main` i tag `v1.0.0` (decyzja człowieka) |
| luty 2027 | **G5 · Pierwszy konkurs** | Operator zakłada konkurs 2027 z kopii (T-98), ogłasza nabór. Zamrożenie wdrożeń na 3 dni przed terminem naboru i w jego dniu |
| po umowach | v1.1 | Sprawozdanie 2026 jako dane (T-95), rozliczenie (T-50b), przypomnienie o terminie sprawozdania |

Przez ostatnie dwa tygodnie zespół zamykał kilka kart dziennie. Techniczna część do G3 to około 30 zadań, czyli 3 do 5 tygodni pracy. **Wąskim gardłem nie jest kod, tylko odpowiedzi klientki i hosting.** Dlatego sekcja 7 idzie do klientki pierwsza, a plan jest ułożony tak, żeby do G3 nic nie czekało na jej odpowiedź.

**Ryzyko poza nami:** NIW wybrał lokalnych operatorów NOWEFIO na lata 2024-2026. Jeśli OCWIP nie będzie operatorem w 2027, najbliższy nabór może być innym konkursem, z innym regulaminem. Plan tego nie zmienia, bo cała treść jest danymi (D2, D16). Zmienia się tylko to, który wzór wgrywamy w T-96. To pytanie trafia do B-06.

---

## 4. Zadania

Siedem etapów. W obrębie etapu kolejność jest zalecana, ale tory z sekcji 5 mogą iść równolegle. Rozmiar: **S** to jedna sesja, **M** dwie lub trzy, **L** więcej i wtedy dzielisz według runbooka ("Karta okazała się dwa razy większa").

Każde zadanie ma wiersz gotowy do [`kolejka.md`](kolejka.md) w sekcji 8.

### Etap 0 · Pilne i porządki

#### T-90 · Łatka bezpieczeństwa Next.js i React, audyt zależności w CI
M7 · S · tor C · zależności: brak

**Po co.** CVE-2025-66478 i towarzyszące mu podatności RSC. Bez tej łatki nic nie wystawiamy publicznie.

**Zakres.** Podbij `next` do najnowszej łatki 15.5.x (co najmniej 15.5.7, sprawdź aktualne advisory w dniu pracy) oraz `react` i `react-dom` do wersji zalecanych w [dyskusji Next.js](https://github.com/vercel/next.js/discussions/86939). Przegeneruj lockfile w kontenerze. W CI dodaj krok `npm audit --omit=dev --audit-level=high` i `dotnet list package --vulnerable --include-transitive`, a znalezisko oblewa job.

**Kryteria.**
- [ ] `npm ls next react react-dom` pokazuje wersje z łatką
- [ ] Typecheck, testy i build frontu przechodzą
- [ ] Oba skany chodzą w CI i oblewają się na podatności wysokiej
- [ ] Wpis w `docs/testy.md`, sekcja CI

**Pułapki.** `npm ci` w CI oblewa się, jeśli lockfile nie był przegenerowany (runbook, "CI czerwone"). Przy wersji 16.x zmienia się API, więc zostajemy na 15.5.x.

#### T-91 · Konfiguracja, która nie pozwoli wystartować produkcji źle
M7 · S · tor C · zależności: brak

**Po co.** Linki weryfikacji i resetu hasła biorą adres z `EmailVerification:FrontendBaseUrl`, a tego klucza nie ma w compose ani w `.env.example`. Na produkcji wskazywałyby `http://localhost:3000`. Brak SMTP poza Development kończy się cichym niewysłaniem maili konta.

**Zakres.** Zmienna `FRONTEND_BASE_URL` trafia do compose i `.env.example`. W `Production` API odmawia startu z czytelnym komunikatem, gdy adres frontu albo `Cors:Origins` wskazuje localhost, gdy brak SMTP, gdy connection string jest pusty albo gdy `AllowedHosts` to `*`. W Development nic się nie zmienia. Wzorem jest istniejąca odmowa przy SMTP na porcie 465 (`Program.cs`).

**Kryteria.**
- [ ] Test: start w `Production` z każdą z pięciu złych wartości kończy się wyjątkiem z nazwą klucza
- [ ] Test: link w mailu weryfikacyjnym używa skonfigurowanego adresu
- [ ] `.env.example` opisuje nowe zmienne, bez wartości przypominających sekret
- [ ] Decyzja w `architektura.md`

#### T-92 · Dokumentacja dogania kod
bez karty (rozjazd dokumentacji z kodem, dozwolony w pętli) · S · tor D

**Zakres.**
- `AGENTS.md`, sekcja "Stan repozytorium": nadal mówi "brak logowania, oceny, umów". Przepisz na stan faktyczny i wskaż ten plik.
- `architektura.md`, sekcja "Czego tu jeszcze nie ma": to samo.
- `README.md:84`: nieaktualny powód, dla którego konta z seeda się nie logują. Opisz obejście przez reset hasła i log maili w Development.
- `README.md:68`: `grant-role` przez `dotnet run` zakłada SDK. Dopisz wariant dla obrazu produkcyjnego (po T-110).
- `docs/map/infra.md`: `npm install` zamiast `npm ci`.

Karta nie jest potrzebna, ale wpis w `docs/log.md` tak.

### Etap 1 · Proces na pustej bazie (ścieżka krytyczna)

Ten etap zamyka L1, L2 i L3. Bez niego każda inna praca buduje na procesie, którego nikt nie przejdzie.

#### T-93 · Dane podmiotu wnioskodawcy
M4 · M · tor A · zależności: brak · **bramka decyzji R-01 (sekcja 7, D-A)**

**Po co.** L1. Dziś nikt nie złoży wniosku.

**Zakres (wariant domyślny, zgodny z dzisiejszym schematem 1:1).**
- `POST /me/entity` zakłada podmiot i wiąże go z kontem. `PUT /me/entity` go poprawia, ale tylko dopóki podmiot nie ma złożonego wniosku. Potem zmiana idzie przez operatora, bo złożony wniosek musi zachować dane z chwili złożenia (R-01, ostatni punkt).
- "Mój profil" (`panel/applicant/profile/page.tsx`) dostaje formularz z polami z [`pola.md`](pola.md), krok 2.2, dla trzech typów podmiotu. Walidacja NIP (suma kontrolna) i KRS po stronie serwera.
- Wnioskodawca bez podmiotu, który kliknie "Wypełnij wniosek", ląduje w profilu z `returnUrl`, a po zapisie wraca do wniosku.
- 403 `NoEntity` dostaje w UI zdanie po polsku z linkiem do profilu.
- Całe sprawdzanie "czyj jest podmiot" idzie przez jedną metodę (B-09: "nie rozsypywać `user.EntityId`"), żeby R-01 dało się później podmienić w jednym miejscu.

**Kryteria.**
- [ ] Nowo zarejestrowany użytkownik zakłada podmiot i rozpoczyna wniosek bez udziału operatora
- [ ] Test negatywny: nie da się odczytać ani zmienić cudzego podmiotu
- [ ] Test: poprawka podmiotu po złożeniu wniosku jest odrzucana, a złożony wniosek pokazuje dane z chwili złożenia
- [ ] NIP i PESEL (jeśli się pojawi) oznaczone komentarzem jako dane wrażliwe

**Pułapki.** Kolumna `nip` ma 10 znaków (T-47, pułapka 1), więc przy szyfrowaniu T-47a poszerza ją. Nie rób tego tutaj. Grupa nieformalna nie ma NIP-u ani adresu organizacji: trzymaj się ograniczeń z `EntityConfiguration`. Jeśli klientka wybierze kartę organizacji z wieloma osobami (RD7), to zadanie rośnie do L i dostaje tabelę pośredniczącą. Wtedy dziel je na T-93 (jedna osoba) i T-93a (dostęp wielu osób, prośby, eskalacja), a przed G1 zmerguj przynajmniej migrację.

#### T-94 · Formularz wniosku NOWE FIO 2026 jako dane
M3 · L · tor B (treść) · zależności: brak

**Po co.** L2. Pierwszy formularz w systemie, a zarazem wzorzec, z którego operator będzie kopiował kolejne.

**Zakres.** Jeden formularz warunkowy dla trzech wzorów (RD6), zapisany jako `backend/seed/forms/application-2026.json`, analogicznie do kart oceny z T-38b. Źródłem jest [`pola.md`](pola.md), części I do IV i tabela różnic między wzorami. Materiał źródłowy leży w `../research/ocwip/nowe-fio-2026/` (poza repo). Wymagania:
- role pól `projectTitle`, `totalCost` i `requestedGrant` (T-35), żeby lista operatora i umowa miały dane;
- dotacja jako pole wyliczane (D11), limit 7000 zł jako limit konkursu, nie stała w formularzu;
- flagi `printed` (D14);
- warianty przez `visibleWhen` na polu rodzaju wnioskodawcy. `appliesTo` jest dozwolone tylko na kartach i we wzorze sprawozdania.

Test w stylu `EvaluationCards2026Tests`: plik przechodzi bramkę kontraktu, a przykładowe odpowiedzi dla każdego z trzech wariantów przechodzą walidację złożenia. `seed.py` przechodzi z formularza zabawki na ten.

**Kryteria.**
- [ ] Wszystkie pola z `pola.md` są w definicji albo mają zapisany powód pominięcia
- [ ] Trzy przykładowe wnioski (organizacja, grupa z patronem, grupa bez patrona) przechodzą walidację złożenia
- [ ] Wniosek 2026 renderuje się w panelu i drukuje do PDF (T-44) bez pól technicznych
- [ ] Lista operatora pokazuje tytuł, koszt i dotację z ról

**Pułapki.**
- **Rodzaj wnioskodawcy we wniosku i w podmiocie to dwa źródła tej samej prawdy.** Zapisz w ZR, które wygrywa. Domyślnie wygrywa podmiot, a pole we wniosku jest wypełniane z niego i tylko do odczytu. Jeśli kontrakt tego nie umie (`prefillFrom` jest dziś tylko w sprawozdaniu), to jest osobna, mała zmiana kontraktu. Zrób ją przed JSON-em.
- Czwarta tabela budżetu i REGON to otwarte pytania z `rozbieznosci.md`. Zrób zgodnie z kolumną "co bez niej robimy".
- R-12: wyłączenie kategorii kosztów chowa też sekcję opisową 6b. Sprawdź, że `visibleWhen` na sekcji to obsługuje.
- R-34: pisz nazwy małymi literami, jak kreator.

#### T-95 · Wzór sprawozdania 2026 jako dane
poza MVP · M · tor B · zależności: T-94 · **może wejść po starcie naboru (v1.1)**

Trzy warianty 4a, 4b i 4c przez `appliesTo`, jako `backend/seed/forms/report-2026.json`, z `prefillFrom` na klucze z T-94. Test jak w T-94. Opis w T-50b, zakres "wzór sprawozdania 2026 w seedzie" przechodzi tutaj.

#### T-96 · Treść startowa na produkcji: import i podpięcie kart do konkursu
M5 · M · tor A · zależności: T-94 (plik), T-110 (komenda w obrazie)

**Po co.** L2, druga połowa. `seed.py` jest deweloperski i odmawia pracy na niepustej bazie, a produkcja potrzebuje tej samej treści.

**Zakres.**
1. Komenda administracyjna obok `grant-role` (`Admin/`): `import-content --competition <id> --application <plik> --formal <plik> --merit <plik> --report <plik>`. Publikuje przez **te same serwisy co API**, więc przez te same bramki kontraktu. Jest idempotentna: plik identyczny z wersją w mocy niczego nie zmienia. Pliki JSON są w obrazie produkcyjnym.
2. W panelu operatora, na stronie konkursu (T-97), sekcja "Karty oceny i wzór sprawozdania": co jest podpięte, w jakiej wersji, oraz "skopiuj z konkursu...". Trasy już istnieją (`evaluation-cards`, `report-form`), brakuje tylko wywołania.

**Kryteria.**
- [ ] Na pustej bazie: rejestracja operatora, `grant-role`, konkurs z kreatora, `import-content`, i konkurs ma formularz, obie karty i wzór sprawozdania
- [ ] Drugie uruchomienie z tymi samymi plikami nic nie zmienia i mówi to
- [ ] Plik niezgodny z kontraktem jest odrzucany z listą błędów, a baza pozostaje nietknięta
- [ ] Procedura spisana w `docs/wdrozenie.md` (plik powstaje w T-111)

#### T-97 · Konkurs w panelu operatora: strona, edycja, stany
M2 · L · tor A · zależności: brak

**Po co.** L3.

**Zakres.**
- `panel/operator/competitions/[id]`: podgląd konkursu z API i edycja w stanach, w których tabela przejść na nią pozwala. Kreator ogłoszenia wznawia szkic **z serwera**, a localStorage zostaje tylko jako bufor niezapisanych zmian.
- Akcje stanu: publikuj (odmowa bez opublikowanej wersji formularza i bez obu kart, z listą braków), zamknij nabór (także ciągły, R-20), dezaktywuj, przywróć dezaktywowany (R-26).
- Zatwierdzenie wyników (`results/approve`) odmawia, dopóki nabór jest otwarty.
- Zatwierdzenie przestawia konkurs na `Resolved`. Archiwizacja jest ręczna.
- Lista konkursów linkuje do strony konkursu, a ta do formularza, wniosków i oceny.

**Kryteria.**
- [ ] Szkic założony w jednej przeglądarce otwiera się i publikuje w innej
- [ ] Publikacja bez formularza albo kart jest odrzucana z listą braków (test)
- [ ] Zatwierdzenie wyników przy otwartym naborze jest odrzucane (test)
- [ ] Dezaktywowany konkurs da się przywrócić (test), a R-26 jest przeniesione do `architektura.md`

**Pułapki.** Edycja opublikowanego konkursu, w którym są już wnioski, zmienia warunki w trakcie gry. Tabela przejść mówi, które pola wolno ruszać. Jeśli nie mówi, dopuść tylko teksty i osoby kontaktowe, a zapisz to w ZR.

#### T-98 · Kopia konkursu z poprzedniej edycji
M2 · M · tor A · zależności: T-97, T-45 (PR #86)

R-11. "Skopiuj" na stronie konkursu tworzy szkic z ustawieniami, formularzem (nowa wersja w nowym konkursie), obiema kartami, wzorem sprawozdania i wzorem umowy. **Bez** dat, numeru, wniosków, przypisań i ocen. To podstawowy sposób pracy operatora od drugiego naboru i jedyna droga do nowego formularza, dopóki T-26a stoi.

**Kryteria.**
- [ ] Kopia konkursu z pełnym zestawem daje szkic gotowy do publikacji po ustawieniu dat
- [ ] Zmiana w kopii nie zmienia oryginału (test na wersjach formularza)

#### T-99 · Wejście do systemu: strona główna, nagłówek, "co przygotować"
M4 · M · tor B · zależności: brak (T-93 dla linku do profilu)

**Zakres.**
- `app/page.tsx` przestaje być stroną deweloperską: to portal z otwartymi naborami i wynikami.
- W publicznym nagłówku pojawiają się "Zaloguj" i "Załóż konto", a po zalogowaniu "Mój panel".
- "Aktualne konkursy" w panelu wnioskodawcy czytają API zamiast pustego tekstu.
- `/design-tokens` jest niedostępne poza Development.
- Ekran "co przygotować" (R-10) przed pierwszym polem wniosku, generowany z załączników i terminów konkursu.

**Kryteria.**
- [ ] Z głównej strony da się dojść do logowania, rejestracji, konkursów i wyników bez wpisywania adresu
- [ ] axe bez naruszeń na nowych ekranach (dostajesz to w testach)

#### T-100 · Test całego procesu w przeglądarce, od pustej bazy
M7 · L · tor C · zależności: T-93, T-96, T-97 (dopisywany etapami, patrz niżej)

**Po co.** Warunek wyjścia 5. To test, który złapałby L1, zanim zrobiło to przejście po kodzie.

**Zakres.**
- Playwright w osobnym katalogu `e2e/`, z własną mapą (`check_map.py` uczy się nowego obszaru).
- Mailpit w compose, w profilu `test`, jako skrzynka do przechwytywania maili. Test czyta linki weryfikacji z jego API.
- Scenariusz tylko przez UI i publiczne API: rejestracja operatora i `grant-role`, konkurs, `import-content`, publikacja, rejestracja dwóch wnioskodawców, podmioty i wnioski z załącznikiem, złożenie, dwóch ekspertów z deklaracją, ocena formalna i merytoryczna, zatwierdzenie, maile, lista publiczna, umowa. **Zero SQL z boku.**
- Job w CI na każdym PR, z nagraniem przebiegu przy porażce.

**Etapy.** Najpierw od rejestracji do złożenia wniosku (po T-93, T-96 i T-97), potem ocena i wyniki, na końcu umowa. Każdy etap to osobny PR pod tym numerem.

**Pułapki.** Test zależny od zegara (termin naboru) musi dostać konkurs z datami względem "teraz" i nie może czekać na minutę. Czas CI rośnie, więc cel to poniżej 5 minut. Przy dłuższym przebiegu job idzie równolegle do reszty, nie po niej.

### Etap 2 · Luki procesu w zakresie MVP

#### T-101 · Załącznik przypięty do wymogu i komplet przy złożeniu
M4 · M · tor A · zależności: brak · **przed G1 (migracja)**

R-33. Nullable `attachments.competition_attachment_id`. Upload przyjmuje identyfikator wymogu, a złożenie odmawia, gdy brakuje wymaganego załącznika, i wylicza braki w stylu D12. Ekran wnioskodawcy pokazuje kafelek na każdy wymóg (kryterium T-34).

**Pułapka, którą trzeba rozwiązać najpierw:** `competition_attachments` wjeżdża przy edycji w całości i zastępuje zapisaną listę, więc wiersze nie mają stabilnego identyfikatora (R-30). Kontrakt edycji musi dopasowywać wiersze po `id` z żądania. Bez tego każda edycja konkursu osieroci powiązania. To pierwszy commit tego zadania.

#### T-102 · Wzory załączników do pobrania
M2 · M · tor A · zależności: T-101 (stabilne identyfikatory)

R-30. Operator wgrywa plik wzoru do wymogu przez istniejący `IAttachmentStorage`. Strona konkursu udostępnia go anonimowo. Format decyduje bajt, nie deklaracja (T-32), limit 10 MB.

#### T-103 · Zwrot wniosku do poprawy
M4 · L · tor A · zależności: T-101 · **przed G1 (migracja, nowy stan wniosku)**

R-03, RD10. Operator wskazuje sekcje, opis i termin. Wniosek wraca do edycji z odblokowanymi tylko tymi sekcjami. Poprawka wymaga ponownego złożenia i tworzy nową wersję z nową sumą kontrolną (D15). Dostępne w naborze i po ocenie formalnej. Historia statusów zapisuje oba przejścia. Mail do wnioskodawcy idzie przez istniejący nadawcę.

**Pułapki.**
- Serwer musi pilnować, że zapis poza odblokowanymi sekcjami jest odrzucany. Blokada tylko w ekranie nie wystarczy (ta sama zasada co w T-50a).
- Termin poprawki a odcięcie naboru: poprawka po zamknięciu naboru jest dozwolona tylko do terminu zwrotu. Zapisz to jako jawną gałąź w `CompetitionIntake`, nie drugą regułę obok.
- Karta formalna ocenia wersję sprzed poprawki. Po ponownym złożeniu operator robi nową ocenę formalną, a stara zostaje w historii.
- Constrainty liczą od `status <> 'Draft'` (log T-42): nowy stan musi przez nie przejść świadomie.

#### T-104 · Konta zespołu OCWIP bez SDK
M1 · M · tor A · zależności: T-110

Decyzja "rola nadawana komendą, nigdy przez HTTP" (`architektura.md`) zostaje. To zadanie sprawia, że da się z niej korzystać na produkcji:
- komendy `grant-role`, `revoke-role`, `deactivate-account` i `list-accounts` działają w obrazie runtime (`dotnet Ocwip.Api.dll ...`);
- panel operatora pokazuje listę kont zespołu z rolami, tylko do odczytu;
- ekspert dostaje mail po przypisaniu wniosków.

Procedura "nowy ekspert" jest w instrukcji (T-49). Jeśli klientka zechce czwartej roli administratora (R-02, sekcja 7, D-B), to osobna karta po G0.

#### T-105 · Zadania w tle: przypomnienia i terminy
M4 · M · tor A · zależności: brak · **przed G1 (tabela)**

Jeden `BackgroundService` z tabelą wykonanych zadań. Idempotencja przez unikalny klucz (zadanie, obiekt, termin), wszystko w UTC. Pierwsi konsumenci:
- R-09: przypomnienie 3 dni przed końcem naboru, tylko dla rozpoczętych i niezłożonych wniosków;
- termin podpisania umowy (14 dni, regulamin 2026) dla T-109;
- później termin sprawozdania dla T-50b.

**Pułapki.** Tylko jedna instancja API (założenie zapisane też przy limiterze i cache). Restart w trakcie wysyłki nie może dać podwójnego maila: wzorem jest warunkowy UPDATE z T-43. Wyłączane zmienną, żeby testy go nie odpalały.

#### T-106 · Zmiana hasła i adresu e-mail po zalogowaniu
M1 · M · tor A · zależności: brak

R-08. Zmiana hasła wymaga starego hasła i obraca `SecurityStamp`. Zmiana adresu wymaga potwierdzenia z nowego adresu, a stary dostaje powiadomienie. Odpowiedzi nie zdradzają, czy nowy adres jest zajęty (reguła 3).

#### T-107 · Zgody i klauzule informacyjne
M1 · M · tor A · zależności: brak · treść: B-05

R-19 i R-16. Mechanizm teraz, treść później:
- rejestracja zbiera akceptację regulaminu i klauzuli z zapisaną wersją tekstu i czasem, jak deklaracja bezstronności (T-40a);
- formularz wniosku ma miejsce na klauzulę dla osób trzecich;
- teksty są robocze i oznaczone ZR, a podmiana to zmiana danych, nie kodu.

#### T-108 · Archiwum wyników
M6 · S · tor B · zależności: T-97

R-14 i R-31. Publiczna lista rozstrzygniętych konkursów z listami dofinansowanych (nazwa, tytuł, kwota). Przy grupach nieformalnych jest nazwa grupy bez imion i nazwisk (RD3). Źródłem jest istniejąca publikacja z T-42a.

#### T-109 · Rezygnacja i przejście środków na listę rezerwową
M6 · M · tor A · zależności: T-45 (PR #86), T-105

Regulamin 2026: umowa niepodpisana w 14 dni od publikacji listy oznacza rezygnację, a środki przechodzą na kolejny wniosek spełniający próg. Operator potwierdza rezygnację, bo zegar tylko przypomina. System proponuje pierwszy wniosek z `Reserve` i jego kwotę w granicach puli, a operator zatwierdza. Historia statusów i mail. Rozstrzyga ZR-09 w części "co z listą rezerwową".

### Etap 3 · Umowa i sprawozdanie

- **T-45**, PR #86, robi inna sesja. Po merge'u sprawdź dwie rzeczy dla planu:
  1. czy model umowy dopuszcza wiele wypłat (R-24: dorobienie po wdrożeniu jest drogie);
  2. gdzie lądują PESEL-e wpisane w pola umowy, bo T-47a musi je objąć.
- **T-45b** (umowy hurtem, wzór 2026 jako dane). Dziś jest zablokowane przez B-03 (P15). Proponuję odblokować je na założeniu, tak jak T-38b: wzór 2026 jest publiczny, a podmiana na wersję prawnika to nowa wersja wzoru, nie zmiana kodu.
- **T-95** (wzór sprawozdania 2026) i **T-50b** (rozliczenie): v1.1, po starcie naboru.

### Etap 4 · Produkcja technicznie (T-48 bez części zależnej od B-06)

Wszystko tutaj jest niezależne od wyboru hostingu. Cel: `docker compose -f docker-compose.prod.yml up` na dowolnym Linuksie z Dockerem daje działającą, bezpieczną usługę. Wybór dostawcy zmienia wtedy tylko DNS i to, na który serwer idzie deploy.

#### T-110 · Obrazy produkcyjne
M7 · M · tor C · zależności: T-90

- **Backend:** wieloetapowo, `sdk` do `dotnet publish -c Release`, runtime `aspnet:10.0`, użytkownik bez roota. W obrazie są czcionki Noto (`Assets/Fonts`), pliki treści startowej (T-96), **dane strefy czasowej i ICU**.
- **Front:** `output: "standalone"`, `node server.js` jako `node`, bez pollingu.

**Pułapki, każda już raz kogoś ugryzła albo ugryzie.**
- `ApplicationListLabels.Zone` i komunikaty naboru spadają po cichu na UTC, gdy obraz nie ma `Europe/Warsaw`. Daty na eksportach przesuną się o godzinę albo dwie. **Test w obrazie:** `TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw")` przechodzi.
- `NEXT_PUBLIC_API_URL` jest wpisywany w bundle przy `next build`, a nie czytany przy starcie. Albo jest argumentem builda, albo, co jest prostsze i zalecane, front i API stoją pod jednym originem (`/api` przez proxy, T-111). Wtedy ta zmienna znika, a CORS i ciasteczka się upraszczają.
- `npm run api:generate` i `openapi-typescript` nie mogą być potrzebne w obrazie produkcyjnym.

#### T-111 · Compose produkcyjne, reverse proxy i TLS
M7 · L · tor C · zależności: T-110

- `docker-compose.prod.yml`:
  - bez bind mountów, `ASPNETCORE_ENVIRONMENT=Production`;
  - baza i API bez portów na hoście, wymagane zmienne przez `${VAR:?}`;
  - healthchecki dla wszystkich usług, `restart: unless-stopped`, limity logów.
- **Caddy** jako proxy (automatyczny TLS, najmniej ruchomych części przy jednej maszynie):
  - HSTS;
  - `request_body` z limitem równym największemu limitowi załącznika plus zapas;
  - `/api` na backend, reszta na front.
- API:
  - `UseForwardedHeaders` z `KnownProxies` z konfiguracji. **Nie** `ASPNETCORE_FORWARDEDHEADERS_ENABLED`, bo ta zmienna ufa każdemu nadawcy, a limiter logowania bierze partycję z adresu IP: bez poprawki wszyscy dzielą jeden limit albo atakujący podrabia nagłówek;
  - `AllowedHosts` na domenę;
  - `UseExceptionHandler` i `UseStatusCodePages`, żeby 500 w Production było ProblemDetails;
  - `/health/db` łapie każdy wyjątek i używa wspólnego `NpgsqlDataSource`;
  - `MaxRequestBodySize` spójny z proxy i z walidacją limitu w konkursie;
  - `/openapi` wyłączone poza Development.
- `docs/wdrozenie.md`: pierwsze uruchomienie, aktualizacja, wycofanie wersji, pierwszy operator, import treści.

**Kryteria.**
- [ ] Smoke test przechodzi na compose produkcyjnym (lokalnie z certyfikatem wewnętrznym Caddy)
- [ ] `nmap` na hoście widzi tylko 80 i 443
- [ ] Test: limiter logowania rozróżnia dwóch klientów za proxy i ignoruje nagłówek od niezaufanego nadawcy

#### T-112 · Nagłówki bezpieczeństwa i CSP
M7 · M · tor C · zależności: T-111

Kryterium z T-47. W API: `X-Content-Type-Options: nosniff` (ważne przy pobieraniu załączników), `Content-Disposition: attachment` na plikach wnioskodawców, `frame-ancestors 'none'`, `Referrer-Policy: no-referrer` (tokeny resetu siedzą w adresie). W Next CSP z nonce albo `'self'`, `connect-src` na własny origin i `Permissions-Policy`. Sprawdzenie na stagingu narzędziem (T-119).

#### T-113 · Klucze DataProtection i migracje osobnym krokiem
M7 · M · tor C · zależności: T-110 · **przed T-47a**

- `PersistKeysToFileSystem` na wolumen albo do bazy, `SetApplicationName("ocwip")`. Dziś każdy nowy kontener wylogowuje wszystkich i unieważnia linki z maili.
- Migracje przez `dotnet ef migrations bundle` jako usługa `migrate` uruchamiana raz przed `up`, z rolą bazodanową z prawami DDL. Aplikacja łączy się rolą z samym DML (kryterium T-48, `architektura.md`, "Migracje przy starcie").
- `db/init/001_extensions.sql` ma na sztywno `ALTER DATABASE ocwip` i wymaga superusera. Strefę czasową bazy przenieś do connection stringa albo migracji, a nieużywane rozszerzenia usuń. Zarządzany Postgres nie uruchomi `db/init`.

**Pułapka.** Jeśli T-47a oprze szyfrowanie na DataProtection, utrata katalogu kluczy oznacza utratę danych. Zalecenie w T-47a: osobny klucz z sekretu, a nie DataProtection.

#### T-114 · Kopie zapasowe i przetestowane odtworzenie
M7 · M · tor C · zależności: T-111, T-113

- Kontener z cronem: `pg_dump -Fc` co noc, plus wolumen załączników, plus klucze DataProtection, do repozytorium **restic** (szyfrowanie po stronie klienta, ważne przy PESEL-ach) na magazynie poza serwerem, w UE, z poświadczeniami tylko do dopisywania.
- Retencja 7 dziennych, 4 tygodniowe, 12 miesięcznych i roczne przez okres retencji danych.
- Skrypt `scripts/restore.sh` odtwarza wszystko na pustą maszynę.

**Kryteria.**
- [ ] Odtworzenie przeprowadzone na czystym środowisku, czas zapisany w `docs/wdrozenie.md`
- [ ] Po odtworzeniu da się zalogować, otworzyć załącznik i wygenerować PDF (klucze i pliki przetrwały, nie tylko baza)
- [ ] Kopia bez hasła restic jest bezużyteczna (sprawdzone)

Magazyn na staging: Hetzner Storage Box BX11 (3,20 EUR miesięcznie) albo Backblaze B2 EU (6 USD za TB). Wybór dla produkcji idzie razem z hostingiem (D-C).

#### T-115 · CI/CD: obrazy, skan, wdrożenie
M7 · M · tor C · zależności: T-110, T-111

- Na push do `dev` i `main`: build obrazów do GHCR z tagiem SHA oraz `dev` albo `main`, Trivy na obrazach.
- Smoke test drugi raz, na compose produkcyjnym.
- Workflow wdrożenia wyzwalany ręcznie, z akceptacją w środowisku GitHub. Przez SSH, kluczem tylko do deployu, robi `pull`, `migrate` i `up --wait`, a przy nieudanym healthchecku wycofuje wersję.
- **Blokada kalendarza:** wdrożenie odmawia, gdy któryś opublikowany konkurs kończy nabór w ciągu 3 dni, chyba że podano jawny parametr wymuszenia (kryterium T-48, "Termin naboru a wdrożenie"). Workflow pyta o to publiczne API.

#### T-116 · Obserwowalność
M7 · S · tor C · zależności: T-111

Logi JSON (`AddJsonConsole`) z identyfikatorem żądania, rotacja logów Dockera i zewnętrzny monitoring dostępności, który odpytuje `/health` i `/health/db` osobno i wysyła alert mailem. Przy tej skali wystarczy darmowy plan zewnętrznego monitora albo Uptime Kuma na innym serwerze niż aplikacja.

#### T-117 · Staging
M7 · S · tor C · zależności: T-111, T-114, T-90 · **decyzja zespołu, nie klientki (D-C)**

Serwer przedprodukcyjny na naszym koncie, około 6 do 9 EUR miesięcznie (Hetzner CX23 albo CX33). Dane wyłącznie fikcyjne, osobny SMTP testowy (Mailpit albo tryb sandbox dostawcy), adres niepublikowany, dostęp za hasłem na proxy. Tu odbywa się próba generalna (T-120), test obciążenia i przegląd bezpieczeństwa. **Bez tego serwera T-118 do T-120 nie mają gdzie się odbyć, a hosting klientki nie będzie gotowy wcześniej niż w G4.**

### Etap 5 · Dane wrażliwe (T-47 bez części formalnej)

Proponuję podzielić T-47 na część techniczną (T-47a), której B-05 nie blokuje (napisane w `blokery.md`), i retencję (T-47b), której parametr zależy od umowy OCWIP z NIW.

#### T-47a · Szyfrowanie PESEL-i i przegląd wycieków
M7 · L · tor A · zależności: T-45 (PR #86), T-113 · **przed G1, przed pierwszym prawdziwym PESEL-em**

- **Szyfrowanie po stronie aplikacji:** AES-GCM (`System.Security.Cryptography.AesGcm`), losowy nonce, format z numerem wersji klucza, klucz z sekretu (nie z repozytorium, nie z bazy, nie z DataProtection), rotacja przez numer wersji.
  - Obejmuje `users.pesel` i **każde miejsce, w którym umowa z PR #86 przechowuje PESEL wpisany przez operatora** (dziś pola wzoru umowy są wolnym tekstem, więc potrzebny jest znacznik pola jako wrażliwego).
  - Poszerzenie kolumn pod szyfrogram (T-47, pułapka 1).
  - NIP jest jawny w rejestrach publicznych, więc szyfrowanie go to decyzja do zapisania, nie odruch.
- **W UI** PESEL jest maskowany. Odszyfrowanie następuje tylko przy generowaniu umowy, dla roli, która tego potrzebuje, z wpisem w dzienniku dostępu.
- **Przegląd:** logi (hasła, tokeny, PESEL, treść wniosków), brak `ON DELETE CASCADE` sprawdzany testem po całym modelu, brak twardego DELETE na danych domenowych.

**Kryteria z karty T-47,** bez retencji: zrzut bazy bez klucza jest bezużyteczny, co zostaje potwierdzone próbą.

**Pułapki.**
- Szyfrogram nie jest ani obiektem, ani tablicą, więc nie szyfruj całej kolumny jsonb (T-47, pułapka 2).
- Wyszukiwanie po PESEL-u, jeśli kiedyś będzie potrzebne, idzie przez osobny indeks HMAC z innym kluczem.
- **Utrata klucza oznacza utratę danych.** Klucz ma swoją kopię poza serwerem, osobno od kopii bazy, w procedurze z `docs/wdrozenie.md`.
- To zadanie dotyka bezpieczeństwa, więc filtr bezpieczeństwa może zatrzymać długą odpowiedź. Rób je jednym plikiem na raz (sekcja 6, ryzyko R5).

#### T-47b · Retencja i usuwanie danych osobowych po terminie
M7 · M · tor A · zależności: T-47a · bloker: B-05 (okres z umowy z NIW)

Ekran dla operatora albo administratora: konkursy po terminie retencji, wnioski do anonimizacji i decyzja z zapisem, kto ją podjął. Anonimizacja zeruje dane osób i zostawia wniosek (M7, T-47, punkt 3). Retencja karty organizacji (R-15) zależy od R-01. Termin to rok realizacji plus 1 plus N lat, gdzie N zależy od umowy z NIW, domyślnie 5. **Może wejść po starcie naboru**, bo pierwszy termin minie za kilka lat. Musi wejść przed pierwszym takim terminem.

### Etap 6 · Pilot

#### T-118 · Test obciążenia pod termin naboru
M7 · S · tor C · zależności: T-117

Scenariusz k6 na stagingu, z historii 2026: 149 wniosków, większość w ostatnich godzinach. Symulacja 150 wnioskodawców w ostatnich 2 godzinach, z autozapisem co 30 sekund, uploadem 5 MB i złożeniem w ostatnich 10 minutach. Cel: p95 poniżej 1 s dla autozapisu, zero błędów 5xx, żadne złożenie odrzucone przed terminem. Wynik zapisany w `docs/wdrozenie.md`, razem z rozmiarem serwera, który to wytrzymał.

#### T-119 · Przegląd bezpieczeństwa przed wystawieniem
M7 · M · tor C · zależności: T-111, T-112, T-47a

`/security-review` na całym API (nie na diffie), OWASP ZAP baseline na stagingu, przejście po `testy.md`, punkt 1, dla każdej trasy dodanej po T-36. Ustalenia na liście z wagą. Wysokie blokują G2.

#### T-120 · Próba generalna z OCWIP
M7 · M · zależności: T-117, T-96, T-97, T-98 · bloker: dostępność klientki

Operator OCWIP na stagingu, na fikcyjnych danych, odtwarza nabór 2026: zakłada konkurs z kopii, ogłasza go, trzy osoby z zespołu składają wnioski, operator prowadzi ocenę z dwoma ekspertami, zatwierdza wyniki i sporządza umowę. Obserwujemy, nie pomagamy. Każde zacięcie zapisujemy jako kartę. To też test instrukcji T-49 w wersji roboczej.

#### T-121 · Deklaracja dostępności i strony informacyjne
M7 · S · tor B · zależności: T-99 · treść częściowo od OCWIP

Deklaracja dostępności według wzoru z ustawy o dostępności cyfrowej (aktualizacja do 31 marca każdego roku), strona z klauzulą informacyjną RODO, kontakt i regulamin serwisu. Treść prawna od OCWIP (B-05), struktura i miejsce od nas. Nawet jeśli OCWIP formalnie nie jest podmiotem publicznym, umowy NIW zwykle wymagają dostępności przy zadaniach publicznych.

### Etap 7 · Przekazanie

- **T-48** domknięte na docelowym serwerze klientki, gdy B-06 da hosting: DNS, certyfikat, sekrety, pierwsza kopia, monitoring, procedura przećwiczona przez kogoś, kto jej nie pisał.
- **T-49**, instrukcja operatora, na zrzutach z działającego systemu.
- **Release** `dev` do `main` z `--no-ff` i tag `v1.0.0`. Decyzja człowieka, nie agenta.
- **Lista kontrolna startu naboru** w `docs/wdrozenie.md`:
  - konkurs opublikowany z kompletem;
  - próbny wniosek złożony i wycofany;
  - kopia zapasowa z tego dnia;
  - kalendarz zamrożenia wdrożeń ustawiony;
  - kontakt dyżurny na dzień terminu.

---

## 5. Tory równoległe i gorące pliki

Przy dwóch albo trzech sesjach naraz ten podział trzyma konflikty w ryzach.

| Tor | Zadania | Dotyka głównie |
|---|---|---|
| **A · domena** | T-93, T-96, T-97, T-98, T-101 do T-107, T-109, T-47a, T-47b | `backend/src/Services`, `Endpoints`, migracje, panele |
| **B · treść i strony** | T-94, T-95, T-99, T-108, T-121 | `backend/seed/`, strony publiczne |
| **C · infrastruktura** | T-90, T-91, T-100, T-110 do T-119 | Dockerfile, compose, `.github/`, `Program.cs` (tylko konfiguracja), `scripts/`, `e2e/` |
| **D · dokumentacja** | T-92, `docs/wdrozenie.md` | `docs/` |

**Zasady dla gorących plików.**

1. **Jedna migracja naraz.** Dwie gałęzie z migracjami rozjeżdżają `AppDbContextModelSnapshot.cs` w sposób, którego merge tekstowy nie naprawia. Zadania toru A z migracją (T-93 przy RD7, T-101, T-103, T-105, T-47a) idą po kolei. Gdy musisz scalić, nie rozwiązuj konfliktu w snapshotcie ręcznie: weź wersję z `dev`, usuń swoją migrację, wygeneruj ją od nowa (`dotnet ef migrations add`), sprawdź `MigrationTests`.
2. **`frontend/lib/api-schema.ts` generujesz, nie scalasz.** Przy konflikcie weź dowolną stronę, zrestartuj backend (R-32) i wygeneruj plik od nowa komendą z `CLAUDE.local.md`.
3. **`docs/log.md`, `kolejka.md` i mapy:** oba wpisy zostają, a limit 20 wpisów w logu sprawdzasz po scaleniu (runbook, "Konflikt przy merge").
4. **`Program.cs`:** tor C dopisuje konfigurację w osobnych metodach rozszerzających (`Configuration/`), a nie w środku pliku. Wtedy dwie gałęzie dotykają jednej linii wywołania, nie tego samego bloku.
5. **Trello:** najwyżej trzy karty na "W trakcie". Nie bierzesz karty, której gałąź jest świeża na `origin`, nawet jeśli karta wisi w backlogu.

---

## 6. Przewidziane blokery i co wtedy robić

| # | Bloker | Prawdopodobieństwo | Co robimy |
|---|---|---|---|
| R1 | **Klientka nie odpowiada** na pakiet z sekcji 7 | wysokie | Każde pytanie ma odpowiedź domyślną i termin (G0). Po terminie budujemy domyślną i wpisujemy ją jako ZR. Plan do G3 nie czeka na nikogo. Zmiana zdania po G1 kosztuje migrację na danych, i to trzeba powiedzieć klientce wprost przy wysyłce pakietu |
| R2 | **Hosting nierozstrzygnięty** (B-06) do G4 | wysokie | Compose działa na dowolnym Linuksie, staging jest na naszym koncie (T-117). W najgorszym razie startujemy produkcję na naszym koncie z umową powierzenia i przenosimy ją później przez odtworzenie z kopii (T-114), bo tę procedurę i tak ćwiczymy |
| R3 | **RODO nierozstrzygnięte** (B-05) | wysokie | Część techniczna (T-47a, T-107, T-121) jest niezależna. Klientce dajemy listę tego, co musi przyjść od jej IOD: klauzule, umowa powierzenia, okres retencji z umowy z NIW, decyzja o analizie ryzyka. **Nie piszemy treści prawnych za nią.** Bez klauzul nie startujemy produkcji z prawdziwymi danymi (G4) |
| R4 | **RD7 (karta organizacji z wieloma osobami) przyjdzie "tak" po G1** | średnie | Dlatego T-93 trzyma dostęp do podmiotu za jedną metodą. Migracja 1:1 do N:M na żywych danych jest addytywna: tabela pośrednicząca wypełniona z `users.entity_id`, stara kolumna zostaje do następnej wersji. Do spisania w T-93 jako plan wyjścia |
| R5 | **Filtr bezpieczeństwa zatrzymuje odpowiedź** przy T-47a, T-112 i T-119 | średnie | `CLAUDE.local.md`, sekcja 4: stop, raport stanu, mniejsze kroki. Zadania bezpieczeństwa są dzielone na pliki z góry, a review robione kawałkami, jak przy T-35 |
| R6 | **Limit sesji albo tygodniowy** w środku zadania | wysokie | Zadania mają rozmiar najwyżej M. L dzielimy na PR-y pod tym samym numerem. Przed końcem sesji zrzut stanu do `docs/log.md` i komentarz na karcie |
| R7 | **Dwie sesje biorą to samo** | średnie | Karta na "W trakcie" przy starcie, a świeża gałąź na `origin` znaczy "zajęte". Tory z sekcji 5 |
| R8 | **Konflikty migracji i wygenerowanych typów** | wysokie przy równoległej pracy | Sekcja 5, zasady 1 i 2 |
| R9 | **Daty na produkcji przesunięte o godzinę** | średnie | Obraz bez `tzdata` i ICU. Test w T-110 |
| R10 | **Maile nie dochodzą albo lądują w spamie** | średnie | SPF, DKIM i DMARC na subdomenie nadawczej (np. `powiadomienia.ocwip.pl`), żeby nie ruszać poczty biurowej. `System.Net.Mail` nie umie portu 465 (T-43a), więc dostawca musi dawać 587 ze STARTTLS: Brevo (UE, 300 maili dziennie za darmo) albo Amazon SES eu-central-1. Wysłanie wyników do 150 osób naraz mieści się w obu |
| R11 | **Proxy tnie załączniki** | wysokie bez T-111 | nginx ma domyślnie limit 1 MB, Kestrel około 28 MB. Jeden limit w konfiguracji, sprawdzany przy zapisie konkursu |
| R12 | **Nowy kontener wylogowuje wszystkich** | pewne bez T-113 | T-113 |
| R13 | **Awaria w dniu terminu naboru** | niskie, ale kosztowne | Zamrożenie wdrożeń (T-115), test obciążenia (T-118), kopia z rana, dyżur. Awaria mimo to: decyzję o przedłużeniu naboru podejmuje operator zmianą daty, a nie my w bazie |
| R14 | **Seed ukrywa luki**, jak ukrył L1 | pewne bez T-100 | T-100 stawia wszystko przez API. Nowa funkcja, która działa tylko z seedem, nie przechodzi bramki |
| R15 | **Stan kolejki rozjeżdża się z Trello** przy wielu nowych kartach | średnie | Karty zakłada jedna osoba jednym ruchem (sekcja 8). MCP Trello nie przypisuje osób do kart, więc przypisanie robi człowiek |
| R16 | **OCWIP nie jest operatorem NOWEFIO w 2027** | nieznane | Treść jest danymi. Zmienia się tylko plik w T-96 i kopia w T-98. Pytanie w B-06 |
| R17 | **Kreator od zera (T-26a) okaże się potrzebny do pierwszego konkursu** | niskie | Do v1 formularz powstaje z kopii (T-98) albo z pliku (T-96). T-26a zostaje za B-10, a sam kreator nie jest na ścieżce krytycznej |

---

## 7. Pakiet decyzji dla klientki

Wysyłamy jednym dokumentem, pogrupowanym według tego, co odpowiedź odblokowuje. **Każde pytanie ma odpowiedź domyślną**, którą przyjmujemy po terminie (G0, 2026-10-16). Kolumna "kiedy za późno" mówi, od kiedy zmiana zdania kosztuje migrację na danych.

| ID | Pytanie | Domyślnie | Odblokowuje | Kiedy za późno |
|---|---|---|---|---|
| D-A | Czy organizację reprezentuje jedna osoba (jedno konto), czy kilka, z prośbą o dostęp (RD7, R-01)? | jedno konto na podmiot | T-93, T-36, T-47b | G1 |
| D-B | Czy dodawanie operatorów i usuwanie danych po terminie ma robić osobna rola administratora (R-02)? | nie, operator i komenda wdrożeniowa | T-104 | po v1 bez kosztu |
| D-C | Hosting: na czyim koncie, kto płaci w roku czwartym, czy dane muszą leżeć w Polsce? Czy OCWIP ma grant Azure (Microsoft for Nonprofits, 2000 USD rocznie przez TechSoup)? | VPS w UE (około 10 do 15 EUR miesięcznie z kopiami), na koncie OCWIP | T-48, T-114 | G4 |
| D-D | Kiedy rusza najbliższy nabór i jaki to konkurs? Czy OCWIP będzie operatorem NOWEFIO w 2027? | marzec 2027, Kierunek NOWE FIO 2027 na wzorach 2026 | cały harmonogram | G3 |
| D-E | Kontakt do IOD, klauzule (także dla osób trzecich, R-16), umowa powierzenia z nami i z hostingiem, okres retencji z umowy z NIW, zgoda na szyfrowanie PESEL-i po stronie aplikacji | klauzule robocze do podmiany, retencja 5 lat od końca roku realizacji | T-107, T-121, T-47b, G4 | G4 (bez klauzul nie ma startu) |
| D-F | Domena systemu i adres nadawcy maili (np. `wnioski.ocwip.pl`, `powiadomienia@...`), dostęp do DNS na rekordy SPF, DKIM i DMARC | subdomena `ocwip.pl` | T-48, R10 | G4 |
| D-G | Czy wzór umowy 2026 to wersja od prawnika? Numeracja umów, projekt umowy widoczny dla wnioskodawcy (P15 do P17, ZR-13) | wzór 2026, numer umowy to numer wniosku | T-45b | po v1 bez kosztu (nowa wersja wzoru) |
| D-H | Zwrot do poprawy: na obu etapach, czy tylko w naborze (RD10)? | na obu | T-103 | G1 |
| D-I | Organizacja młoda i lokalna: trwałe rozróżnienie czy tylko w 2026 (R-36)? | pole we wniosku, nie nowy typ podmiotu | T-94 | G1 |
| D-J | Publikacja konkursu: klikana czy z zaplanowaną datą (R-27)? | klikana | T-97 | po v1 bez kosztu |
| D-K | Lista publiczna: tylko dofinansowane i rezerwa, czy wszystkie z punktami (ZR-10)? Czy publikacja razem z zatwierdzeniem? | tylko dofinansowane i rezerwa, razem | T-108 | po v1 bez kosztu |
| D-L | Pytania z ocen P9 do P14 (ZR-01, ZR-02, ZR-05 do ZR-09, ZR-11) | jak w `zalozenia-robocze.md` | drobne poprawki | po v1 bez kosztu |
| D-M | Sprawozdanie: wydatek spoza budżetu, termin, część finansowa (P18, P19, ZR-12) | jak w ZR-12 | T-50b | przed pierwszym sprawozdaniem |
| D-N | Kreator formularzy: cztery pytania z B-10 | kopia z poprawkami wystarcza w v1 | T-26a | bez kosztu |
| D-O | Czy dane z Witkaca trzeba przenieść (B-07)? | nie, decyzja zapisana w `zakres.md` | nic w v1 | bez kosztu |
| D-P | Kto z OCWIP przejdzie próbę generalną i kiedy (T-120)? | operator, pierwsza połowa grudnia 2026 | G3 | G3 |

Pytania oznaczone G1 są **jedynymi, przy których cisza jest droga**. Przy wysyłce warto je wyróżnić.

---

## 8. Jak wprowadzić ten plan do pracy

1. Człowiek przegląda plan i sekcję 7 (dziś).
2. Zakładamy karty na Trello dla T-90 do T-121 oraz T-47a i T-47b, w liście Backlog, w kolejności etapów. Opis karty to specyfikacja z sekcji 4, a checklista to jej kryteria. T-47 dostaje komentarz o podziale.
3. Specyfikacje przenosimy do plików kamieni (`M1-fundament.md` do `M7-wdrozenie.md`), a ten plik zostaje mapą z odnośnikami.
4. Wiersze niżej trafiają do [`kolejka.md`](kolejka.md) z kodami kart. Kolejność w kolejce jest kolejnością z tego planu, a `runbook.py next` robi resztę.
5. Pakiet z sekcji 7 idzie do klientki, a jego kopia na karty B-xx.

Wiersze do kolejki. W kolumnie Trello jest `-`, dopóki karta nie powstanie. Kolejność jest ułożona pod `runbook.py next`: od pierwszego zadania bez blokera.

```
| kolejka | T-90 | Łatka bezpieczeństwa Next.js i React, audyt zależności w CI | M7 | - | - | - |
| kolejka | T-91 | Konfiguracja produkcyjna z odmową startu przy błędach | M7 | - | - | - |
| kolejka | T-93 | Dane podmiotu wnioskodawcy | M4 | - | - | - |
| kolejka | T-97 | Konkurs w panelu operatora: strona, edycja, stany | M2 | - | - | - |
| kolejka | T-94 | Formularz wniosku NOWE FIO 2026 jako dane | M3 | - | - | - |
| kolejka | T-110 | Obrazy produkcyjne | M7 | - | T-90 | - |
| kolejka | T-96 | Treść startowa na produkcji: import i podpięcie kart | M5 | - | T-94, T-97, T-110 | - |
| kolejka | T-99 | Wejście do systemu: strona główna, nagłówek, co przygotować | M4 | - | T-93 | - |
| kolejka | T-101 | Załącznik przypięty do wymogu i komplet przy złożeniu | M4 | - | - | - |
| kolejka | T-113 | Klucze DataProtection i migracje osobnym krokiem | M7 | - | T-110 | - |
| kolejka | T-47a | Szyfrowanie PESEL-i i przegląd wycieków | M7 | - | T-45, T-113 | - |
| kolejka | T-103 | Zwrot wniosku do poprawy | M4 | - | T-101 | - |
| kolejka | T-105 | Zadania w tle: przypomnienia i terminy | M4 | - | - | - |
| kolejka | T-100 | Test całego procesu w przeglądarce, od pustej bazy | M7 | - | T-93, T-96, T-97 | - |
| kolejka | T-111 | Compose produkcyjne, reverse proxy i TLS | M7 | - | T-110 | - |
| kolejka | T-112 | Nagłówki bezpieczeństwa i CSP | M7 | - | T-111 | - |
| kolejka | T-114 | Kopie zapasowe i przetestowane odtworzenie | M7 | - | T-111, T-113 | - |
| kolejka | T-115 | CI/CD: obrazy, skan, wdrożenie | M7 | - | T-110, T-111 | - |
| kolejka | T-116 | Obserwowalność | M7 | - | T-111 | - |
| kolejka | T-117 | Staging | M7 | - | T-111, T-114 | - |
| kolejka | T-98 | Kopia konkursu z poprzedniej edycji | M2 | - | T-97, T-45 | - |
| kolejka | T-102 | Wzory załączników do pobrania | M2 | - | T-101 | - |
| kolejka | T-104 | Konta zespołu OCWIP bez SDK | M1 | - | T-110 | - |
| kolejka | T-106 | Zmiana hasła i adresu e-mail po zalogowaniu | M1 | - | - | - |
| kolejka | T-107 | Zgody i klauzule informacyjne | M1 | - | - | - |
| kolejka | T-109 | Rezygnacja i przejście środków na listę rezerwową | M6 | - | T-45, T-105 | - |
| kolejka | T-108 | Archiwum wyników | M6 | - | T-97 | - |
| kolejka | T-121 | Deklaracja dostępności i strony informacyjne | M7 | - | T-99 | - |
| kolejka | T-118 | Test obciążenia pod termin naboru | M7 | - | T-117 | - |
| kolejka | T-119 | Przegląd bezpieczeństwa przed wystawieniem | M7 | - | T-111, T-112, T-47a | - |
| kolejka | T-120 | Próba generalna z OCWIP | M7 | - | T-117, T-96, T-97, T-98 | - |
| kolejka | T-95 | Wzór sprawozdania 2026 jako dane | poza MVP | - | T-94 | - |
| zablokowane | T-47b | Retencja i usuwanie danych osobowych po terminie | M7 | - | T-47a | B-05 |
```

T-92 nie ma wiersza, bo to porządek dokumentacji bez karty. T-48 i T-49 zostają w swoich wierszach. T-48 dostaje w zależnościach T-111, T-114, T-115, T-116, T-47a i T-119, a jego bloker B-06 zostaje.

**Gdy kolejka znów stanie,** najpierw sprawdź, czy któraś bramka z sekcji 3 nie czeka na decyzję z sekcji 7, zanim napiszesz człowiekowi, że nie ma co robić.
