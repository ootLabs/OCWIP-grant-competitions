# Plan do pierwszej działającej wersji (v1)

Stan na 2026-09-27:

- `dev` na `8961fe0`, z PR #86 (umowy, T-45) i PR #87 (rozliczenie, T-50b);
- z T-50b wydzielone T-50c;
- decyzje zespołu DZ-1 do DZ-6 rozstrzygnięte 2026-09-27 (sekcja 7).

Ten plik to **mapa drogi od dzisiejszego stanu do pierwszego prawdziwego naboru w systemie**. Nie zastępuje [`kolejka.md`](kolejka.md): kolejka mówi, co jest następne, a ten plik mówi, **dlaczego w tej kolejności, co po drodze pęknie i co wtedy robić**.

Jak czytać:

- Agent w pętli czyta sekcje 1, 5 i 6 raz na sesję, a potem specyfikację swojego zadania w sekcji 4.
- Człowiek czyta sekcje 1, 2, 3 i 7. Sekcja 7 to decyzje, na które plan czeka.
- Zadania od T-90 w górę są **propozycją**. Do kolejki wchodzą dopiero z kartą na Trello (zasada z [`rozbieznosci.md`](rozbieznosci.md), punkt 1).

**Dlaczego numeracja zaczyna się od T-90.** Numery T-80 do T-84 występują w opisach kart B-05 i D8 na Trello, a `T-80` jest w komentarzach kodu jako nazwa szyfrowania. Oznaczają tę samą robotę, co zadania tutaj:

- T-80 i T-82 (log dostępu do danych osobowych) to T-47a;
- T-83 (zgody i klauzule) to T-107;
- T-84 (umowa powierzenia) to B-05, poza kodem.

T-47a przepina komentarze `T-80` w kodzie na `T-47a`.

Oznaczenia w tym pliku: **RY** to ryzyka z sekcji 6, a **PK** to pytania do klientki z sekcji 7. Nie mylić z `R-xx` z `rozbieznosci.md` ani z decyzjami `D1` do `D16`.

---

## 1. Czym jest v1

**v1 to wersja, na której OCWIP przeprowadza jeden prawdziwy nabór: od ogłoszenia, przez ocenę, do podpisanych umów. Działa na produkcji z kopiami zapasowymi, a na co dzień nie potrzebuje programisty.**

Warunki wyjścia, wszystkie mierzalne:

1. Na **pustej** bazie produkcyjnej zespół wdrożeniowy stawia pierwszy konkurs jedną udokumentowaną procedurą: interfejs plus komendy administracyjne, bez ręcznego SQL. Od drugiego konkursu operator robi wszystko sam (D2).
2. Nowy użytkownik z ulicy rejestruje się, podaje dane podmiotu, składa kompletny wniosek i dostaje potwierdzenie mailem.
3. Operator przeprowadza ocenę formalną, przypisuje dwóch ekspertów, a eksperci oceniają. Operator zatwierdza wyniki, publikuje listę i wysyła maile.
4. Operator sporządza umowy, a wnioskodawca je widzi (to jest już w T-45). Rezygnacja przesuwa środki na listę rezerwową (T-109).
5. Test w przeglądarce przechodzi punkty 1 do 4 na pustej bazie, w CI, przy każdym PR (T-100 do T-100b).
6. Produkcja ma TLS, sekrety spoza repozytorium, szyfrowane dane wrażliwe, kopię zapasową z **przetestowanym** odtworzeniem i monitoring.
7. Operator ma instrukcję (T-49) i przeszedł próbę generalną (T-120).

**Czego v1 nie potrzebuje na dzień startu naboru:** reszty sprawozdawczości. Sprawozdania pojawiają się miesiące po umowach, więc T-50c i T-95 mogą wejść **po** starcie, przez zwykłe wdrożenie. To najważniejsza dźwignia tego planu: **dostarczamy w kolejności osi czasu naboru, a nie w kolejności kamieni**.

Warunek tej dźwigni: wszystko, co wymaga migracji burzącej dane, jest rozstrzygnięte przed pierwszymi prawdziwymi danymi (bramka G1).

---

## 2. Stan wyjściowy i trzy luki krytyczne

Zrobione jest 66 z 72 zadań kolejki. Ekrany istnieją dla prawie każdego kroku procesu. Przejście całego cyklu po kodzie (2026-09-27) pokazało jednak, że **na świeżej bazie proces pęka w trzecim kroku**. Nie widać tego, bo zarówno `seed.py`, jak i testy backendu (`TestApplicationChain.cs`) wstawiają podmioty z pominięciem API.

| # | Luka | Skutek | Dowód |
|---|---|---|---|
| L1 | **Nic w systemie nie zakłada podmiotu.** Zarejestrowany użytkownik ma `EntityId = null` | `POST /competitions/{id}/applications` zawsze daje 403 `NoEntity`, więc **nikt nie złoży wniosku**. Ekran pokazuje tylko "Spróbuj ponownie". `GET /applications` też daje 403, więc strona startowa nowego wnioskodawcy ("Moje wnioski") od razu pokazuje błąd | `Services/ApplicationService.cs:50`, `ApplicationOverviewService.cs:27`, brak `Entities.Add` w `backend/src`, `Authorization/ResourceOwnership.cs` ("registration does not create a Podmiot yet (B-09)"), `panel/applicant/profile/page.tsx` to zaślepka |
| L2 | **Brak treści startowej.** Kreator formularzy umie tylko kopiować, a w repo nie ma formularza wniosku 2026 ani wzoru sprawozdania. Karty oceny wgrywa `seed.py` albo ręczne wywołanie API | Na pustej bazie nie powstanie pierwszy formularz, a wniosek do konkursu bez formularza kończy się 409 `NoFormDefinition`. Bez kart ocena kończy się 409 `NoCard`, bez wzoru sprawozdanie kończy się 409 `NoForm` | `forms/[competitionId]/source-picker.tsx:24`; `POST /competitions/{id}/evaluation-cards/{stage}` i `POST /competitions/{id}/report-form` nie mają wywołania z frontu |
| L3 | **Konkurs nie ma strony w panelu operatora.** Kreator ogłoszenia wznawia szkic tylko z localStorage jednej przeglądarki | Szkicu z innego komputera nie da się otworzyć ani opublikować, a opublikowanego nie da się poprawić. Zamknięcie naboru i dezaktywacja mają API, ale nie mają UI. Wyniki da się zatwierdzić przy otwartym naborze | `lib/competition-wizard/draft-storage.ts`, `panel/operator/page.tsx:99` (lista linkuje tylko do formularza), `GrantDecisionService.ApproveAsync` nie sprawdza stanu konkursu |

**Drugi rząd.** Te luki są ważne, ale nie zatrzymują procesu:

- brak powiązania załącznika z wymogiem (R-33);
- brak zwrotu do poprawy (R-03);
- strona główna jest deweloperska, a publiczny nagłówek nie ma linku "Zaloguj";
- "Aktualne konkursy" w panelu wnioskodawcy pokazują stały pusty stan także w trakcie naboru. Komentarz w pliku zakłada, że ekran ogląda się tylko między naborami.

**Produkcja: zero.**

- Oba Dockerfile to obrazy deweloperskie (`dotnet watch`, `next dev`, bez `USER`).
- Compose publikuje bazę i API na wszystkich interfejsach i domyślnie ustawia Development.
- Poza Development API nigdy nie migruje bazy (`Database:MigrateOnStartup` jest wyłączone), a innej ścieżki migracji nie ma.
- Nie ma kopii zapasowych, a klucze DataProtection giną razem z kontenerem (`Program.cs:50`).

Pełna lista jest w specyfikacjach T-110 do T-116.

**Pilne niezależnie od planu:** `next 15.5.4` jest podatny na CVE-2025-66478, czyli zdalne wykonanie kodu przez protokół React Server Components (CVSS 10). Podatny kod siedzi w pakietach `react-server-dom-*` dołączonych do Next. Poprawka jest od `next 15.5.7` ([advisory](https://nextjs.org/blog/CVE-2025-66478)). Dopóki nic nie stoi publicznie, to nie jest pożar, ale luka musi zniknąć, zanim cokolwiek stanie pod publicznym adresem, także staging (T-90).

---

## 3. Oś czasu i hipoteza terminu

Nabór 2026 zamknął się 19.04.2026. **Hipoteza robocza, do potwierdzenia w PK-D: kolejny nabór OCWIP rusza w marcu 2027.** Z niej wynikają daty:

| Kiedy | Bramka | Co musi być prawdą |
|---|---|---|
| do 2026-10-16 | **G0 · Odpowiedzi klientki** | Pakiet z sekcji 7 jest wysłany do 2026-10-02. Czego nie ma do 16.10, przyjmujemy według kolumny "domyślnie" |
| do 2026-11-06 | **G1 · Zamrożenie schematu** | PK-A, PK-H i PK-I są rozstrzygnięte albo przyjęte domyślnie. Zmergowane: T-93, T-94, T-101, T-103, T-105, T-109, T-113 i T-47a. Po G1 każda migracja musi być addytywna |
| do 2026-11-27 | **G2 · Gotowość produkcyjna** | T-110 do T-117 zmergowane, staging stoi, odtworzenie z kopii zmierzone. Przegląd bezpieczeństwa (T-119) nie ma otwartych ustaleń wysokich. **Do tej bramki nic nie czeka na klientkę** |
| do 2026-12-18 | **G3 · Kandydat v1** | T-100 do T-100b zielone w CI, test obciążenia (T-118) zaliczony. Próba generalna z OCWIP (T-120) zrobiona, a poprawki z niej zmergowane. Ta bramka wymaga obecności klientki na próbie (PK-P) |
| do 2027-01-31 | **G4 · Produkcja** | Hosting klientki (PK-C), T-48 domknięte na docelowym serwerze. T-107 i T-121 mają treść od IOD (PK-E). T-49 instrukcja gotowa. Release `dev` do `main` i tag `v1.0.0` (decyzja człowieka) |
| luty 2027 | **G5 · Pierwszy konkurs** | Operator zakłada konkurs 2027 z kopii (T-98) i ogłasza nabór. Zamrożenie wdrożeń na 3 dni przed terminem naboru i w jego dniu |
| po umowach | v1.1 | Wzór sprawozdania 2026 (T-95), T-50c, retencja (T-47b) |

Przez ostatnie dwa tygodnie zespół zamykał kilka kart dziennie. Do G3 zostają 32 zadania (sekcja 8), czyli 3 do 5 tygodni pracy. **Wąskim gardłem nie jest kod, tylko odpowiedzi klientki i hosting.** Dlatego pakiet z sekcji 7 idzie do klientki pierwszy.

**Ryzyko poza nami:** NIW wybrał lokalnych operatorów NOWEFIO na lata 2024-2026. Jeśli OCWIP nie będzie operatorem w 2027, najbliższy nabór może być innym konkursem, z innym regulaminem. Planu to nie zmienia, bo cała treść jest danymi (D2, D16). Zmienia się tylko to, który wzór wgrywamy w T-96. To pytanie PK-D.

---

## 4. Zadania

Siedem etapów. W obrębie etapu kolejność jest zalecana, ale tory z sekcji 5 mogą iść równolegle.

**Rozmiar:**

- **S** to jedna sesja;
- **M** to dwie albo trzy sesje;
- **L** to więcej. Wtedy dzielisz zadanie według runbooka ("Karta okazała się dwa razy większa"): nowy wiersz z sufiksem, na przykład T-100a, z własną kartą.

Znacznik **przed G1** oznacza zadanie z migracją, które musi wejść przed pierwszymi prawdziwymi danymi. Każde zadanie ma wiersz w sekcji 8.

### Etap 0 · Pilne i porządki

#### T-90 · Łatka bezpieczeństwa Next.js, audyt zależności w CI
M7 · S · tor C · zależności: brak

**Po co.** CVE-2025-66478 i towarzyszące mu podatności RSC. Bez tej łatki nic nie wystawiamy publicznie.

**Zakres.**

- Podbij `next` do najnowszej łatki 15.5.x (co najmniej 15.5.7; w dniu pracy sprawdź aktualne advisory). Podbij `react` i `react-dom` do wersji zalecanych w [dyskusji Next.js](https://github.com/vercel/next.js/discussions/86939).
- Przegeneruj lockfile w kontenerze.
- W CI dodaj `npm audit --omit=dev --audit-level=high` oraz `dotnet list package --vulnerable --include-transitive`. Znalezisko oblewa job.

**Kryteria.**
- [ ] `npm ls next react react-dom` pokazuje wersje z łatką
- [ ] Typecheck, testy i build frontu przechodzą
- [ ] Oba skany chodzą w CI i oblewają się przy podatności wysokiej
- [ ] Wpis w `docs/testy.md`, sekcja CI

**Pułapki.**

- `npm ci` w CI oblewa się, gdy lockfile nie był przegenerowany (runbook, "CI czerwone, lokalnie zielone", przyczyna 2).
- Zostajemy na 15.5.x, bo 16.x zmienia API.

#### T-91 · Konfiguracja produkcyjna z odmową startu przy błędach
M7 · S · tor C · zależności: brak

**Po co.** Linki weryfikacji i resetu hasła biorą adres z `EmailVerification:FrontendBaseUrl`. Tego klucza nie ma w compose ani w `.env.example`, a kod ma na sztywno wpisany zapasowy `http://localhost:3000` (`EmailVerificationService.cs:17`, `PasswordResetService.cs:168`). Brak SMTP poza Development kończy się tym, że maile konta nie wychodzą. Użytkownik niczego nie widzi, a w logu jest tylko ostrzeżenie.

**Zakres.**

- Zmienna `FRONTEND_BASE_URL` trafia do compose i `.env.example`.
- W `Production` API odmawia startu z komunikatem, w którym jest nazwa klucza, gdy:
  - adres frontu albo `Cors:Origins` wskazuje localhost;
  - brakuje SMTP;
  - connection string jest pusty;
  - `AllowedHosts` to `*`.
- W Development nic się nie zmienia.
- Wzorem jest istniejąca odmowa przy SMTP na porcie 465 (`Program.cs:122`).

**Kryteria.**
- [ ] Test: start w `Production` z każdą ze złych wartości kończy się wyjątkiem z nazwą klucza
- [ ] Test: link w mailu weryfikacyjnym używa skonfigurowanego adresu
- [ ] `.env.example` opisuje nowe zmienne, bez wartości przypominających sekret
- [ ] Decyzja w `architektura.md`

#### T-92 · Dokumentacja dogania kod
bez karty (rozjazd dokumentacji z kodem jest dozwolony w pętli, `CLAUDE.local.md`) · S · tor D · zależności: brak

**Zakres.**

- `AGENTS.md`, sekcja "Stan repozytorium", nadal mówi "brak logowania, oceny, umów". Przepisz na stan faktyczny i wskaż ten plik.
- `architektura.md`, sekcja "Czego tu jeszcze nie ma": to samo.
- `README.md:85`: nieaktualny powód, dla którego konta z seeda się nie logują. Opisz obejście przez reset hasła i log maili w Development.
- `README.md:66`: `grant-role` przez `dotnet run` zakłada SDK. Wariant dla obrazu produkcyjnego dopisuje T-104.
- `docs/map/infra.md:11`: `npm install` zamiast `npm ci`.
- `decyzje.md` zna tylko D1 do D15. Dopisz D16 z B-10 (<https://trello.com/c/WH7px80E>).
- Rozjazd do `rozbieznosci.md` jako nowa pozycja: słownik mówi "Recenzent" (`slownik.md:25`), a UI i ten plan mówią "ekspert", tak jak regulamin 2026.

Wpis w `docs/log.md`.

### Etap 1 · Proces na pustej bazie (ścieżka krytyczna)

Ten etap zamyka L1, L2 i L3. Bez niego każda inna praca buduje na procesie, którego nikt nie przejdzie.

#### T-93 · Karta podmiotu wnioskodawcy
M4 · L · tor A · zależności: brak · **przed G1** · **budujemy przed odpowiedzią klientki (DZ-1)**

**Po co.** L1. Dziś nikt nie złoży wniosku.

**Uczciwie o modelu.** Ta karta **nie jest** zgodna z dzisiejszym schematem.

- `Entity` ma pięć kolumn: typ, nazwę, jeden napis kontaktowy, NIP i adres.
- Karta organizacji z [`pola.md`](pola.md) (krok 2.2) ma formę prawną, rejestr i numer, REGON, adres korespondencyjny, telefon, e-mail, rachunek bankowy i tabelę osób uprawnionych do reprezentowania.
- Złożony wniosek ma zachować **kopię** danych podmiotu z chwili złożenia (pola.md, "Kopia danych w złożonym wniosku"), a `applications` takiej kopii nie ma.

Źródłem pól jest raport, a nie nasz domysł, więc to nie jest zgadywanie modelu. Jest to jednak świadome odejście od ostrożności z `model-danych.md` ("zakładanie Podmiotu ... ma osobną kartę zależną od B-09"), na które człowiek zgodził się 2026-09-27 (DZ-1). Zapisz to w `model-danych.md` przy tym założeniu, razem z planem wyjścia RY4.

**Zakres (wariant domyślny: jedno konto na podmiot, PK-A).**

1. Migracja:
   - kolumny karty według kolumny "Gdzie" w `pola.md` (tylko pola z "karta");
   - kopia danych podmiotu przy wniosku (`applications.entity_snapshot`, jsonb, stemplowana przy złożeniu);
   - poszerzenie `nip` zostaje dla T-47a.
2. Karta powstaje tak, jak opisuje `pola.md` (część I wniosku): **przy pierwszym wniosku** użytkownik wypełnia pusty blok danych, a po zapisie dane stają się kartą. Przy każdym następnym wniosku widzi blok wypełniony, z datą aktualizacji i przyciskami "dane są aktualne" oraz "popraw", a poprawka trafia do karty. "Mój profil" pokazuje kartę i pozwala ją poprawić w każdej chwili.
3. `POST /me/entity` i `PUT /me/entity`. Poprawka karty **nie zmienia** złożonych wniosków, bo te pokazują swoją kopię.
4. **Grupa nieformalna bez patrona nie ma karty organizacji** (pola.md, typ 3), ale każdy wniosek musi należeć do podmiotu (`IEntityScoped`). Wariant domyślny: minimalny podmiot typu `InformalGroup` z nazwą grupy, zakładany z pierwszego wniosku. Rozjazd z `pola.md` zapisz jako nową pozycję `R-xx`.
5. Własność zawsze przez istniejące `ResourceOwnership.BelongsTo`. Trzy miejsca, które czytają `user.EntityId` bezpośrednio (`ApplicationService.cs:50`, `ApplicationOverviewService.cs:27`, `SessionService.cs:236`), przechodzą przez jedną metodę. Wtedy R-01 (wiele osób) podmienia się w jednym miejscu.
6. `NoEntity` przestaje być błędem ekranu. "Moje wnioski" dla nowego użytkownika to pusta lista z zaproszeniem, a "Wypełnij wniosek" prowadzi do bloku danych.

**Kryteria.**
- [ ] Nowo zarejestrowany użytkownik bez udziału operatora zakłada kartę przy pierwszym wniosku i składa wniosek, dla każdego z trzech rodzajów wnioskodawcy
- [ ] Nowy użytkownik widzi pustą listę "Moje wnioski", nie błąd (test)
- [ ] Test negatywny: nie da się odczytać ani zmienić cudzej karty
- [ ] Test: po poprawce karty złożony wniosek dalej pokazuje dane z chwili złożenia
- [ ] Walidacja NIP, KRS i rachunku (sumy kontrolne) na krawędzi API, bo schemat tego nie pilnuje (`Entity.cs`: "checked at the API edge")
- [ ] Pola z danymi wrażliwymi oznaczone komentarzem

**Pułapki.**

- NIP w `seed.py:192` celowo nie przechodzi sumy kontrolnej, więc albo seed dostaje poprawny NIP, albo walidacja działa tylko przy zapisie nowej wartości.
- Jeśli PK-A przyjdzie jako "wiele osób" (RD7), zadanie dostaje T-93a: tabela pośrednicząca, prośby o dostęp i eskalacja po 7 dniach. Migrację T-93a trzeba wtedy zrobić przed G1. **Zrobione 2026-10-08**, specyfikacja w [`M4-wnioski.md`](M4-wnioski.md).
- Rodzaj wnioskodawcy **nie należy do karty** (pola.md: "rodzaj wnioskodawcy jest polem wniosku, nie karty"). Mimo to dziś karta oceny wybiera kryteria po `Entity.Type`. Patrz T-94, pułapka 1.

#### T-93a · Dostęp kilku osób do karty organizacji
M4 · L · tor A · zależności: T-93 · **przed G1** · zrobione 2026-10-08

**Po co.** RD7, zatwierdzone z raportem 2026-09-21, i MVP raportu: "karta organizacji wypełniana raz, dostęp kilku osób do jednej organizacji". DZ-1 budowało wariant jednego konta na podmiot, zanim przyszła akceptacja.

**Zakres.**

1. `entity_members` zamiast `users.entity_id`, z założycielem; `entity_access_requests`; unikalny NIP aktywnych kart. Migracja przenosi istniejące konta jako założycieli.
2. Zajęty NIP przy zakładaniu karty: "ta organizacja jest już zarejestrowana" i prośba o dostęp. Zatwierdza założyciel, po 7 dniach bez odpowiedzi operator z notatką, jak sprawdzono osobę.
3. Członek widzi wszystkie wnioski karty, także robocze, i może je dokończyć.
4. Osoba z kilkoma kartami wskazuje kartę przy rozpoczęciu wniosku.
5. Maile: do założyciela przy prośbie, do operatorów po 7 dniach, do proszącego o decyzji.

**Kryteria.**
- [x] Druga osoba z tym samym NIP-em nie zakłada drugiej karty, tylko prosi o dostęp (test)
- [x] Po zatwierdzeniu współpracownik dokańcza szkic założyciela (test)
- [x] Tylko założyciel decyduje; członek dostaje 403, obcy 404 (test)
- [x] Operator decyduje dopiero po 7 dniach i tylko z notatką (test)
- [x] Prośba bez odpowiedzi trafia mailem do operatora raz (test zadania)
- [x] Konto z dwiema kartami wskazuje kartę przy starcie wniosku (test)
- [ ] Karta na Trello: tablica niedostępna z konta, które robiło zadanie

**Czego nie robimy tutaj.** Odbieranie dostępu i przekazanie roli założyciela (`R-45`), powołanie eksperta z ekranu (`R-44`).

#### T-94 · Formularz wniosku NOWE FIO 2026 jako dane
M3 · L · tor B · zależności: T-93 (blok danych podmiotu) · **przed G1** (zmiana kontraktu)

**Po co.** L2. To pierwszy formularz w systemie i wzorzec, z którego operator będzie kopiował kolejne.

**Zakres.**

- Jeden formularz warunkowy dla trzech wzorów (RD6), jako `backend/seed/forms/application-2026.json`, wzorem kart oceny z T-38b.
- Źródło: [`pola.md`](pola.md), części I do IV i tabela różnic między wzorami. Materiał pierwotny leży w `../research/ocwip/nowe-fio-2026/`, poza repo.
- Role pól `projectTitle`, `totalCost` i `requestedGrant` (T-35).
- Dotacja jako pole wyliczane (D11). Limit 7000 zł jest limitem konkursu, a nie stałą w formularzu.
- Flagi `printed` (D14).
- Warianty przez `visibleWhen` na polu rodzaju wnioskodawcy. `appliesTo` jest odrzucane w formularzu wniosku (`FormPurpose.cs:118`).
- Test w stylu `EvaluationCards2026Tests`: plik przechodzi bramkę kontraktu, a przykładowe odpowiedzi dla każdego z trzech wariantów przechodzą walidację złożenia.
- `seed.py` przechodzi z dwupolowego formularza na ten.

**Kryteria.**
- [ ] Każde pole z `pola.md` jest w definicji albo ma zapisany powód pominięcia
- [ ] Trzy przykładowe wnioski (organizacja, grupa z patronem, grupa bez patrona) przechodzą walidację złożenia
- [ ] Wniosek 2026 renderuje się w panelu i drukuje do PDF (T-44) bez pól technicznych
- [ ] Lista operatora pokazuje tytuł, koszt i dotację z ról
- [ ] Karta formalna 2026 zadaje kryteria właściwe dla rodzaju wnioskodawcy **z wniosku** (pułapka 1)

**Pułapki. Pierwsze dwie to zmiany kontraktu, więc idą przed JSON-em.**

1. **Rodzaj wnioskodawcy jest w dwóch miejscach.** Według `pola.md` jest polem wniosku, bo ta sama fundacja raz startuje sama, a raz jako patron grupy. Tymczasem `appliesTo` na kartach oceny czyta `Entity.Type` (`AnswerCalculator.cs:44`), a `EntityType` ma wartość `PatronInformalGroup`. Fundacja jako patron grupy dostałaby więc kryteria organizacji.
   - Wariant domyślny: nowa rola pola `applicantType` w kontrakcie; `appliesTo` czyta odpowiedź z tej roli, a `Entity.Type` tylko zawęża dopuszczalne opcje.
   - Zapisz jako ZR, a rozjazd z `EntityType` jako `R-xx`.
2. **R-12 nie da się zrobić samym `visibleWhen`.** Warunek widzi tylko odpowiedzi (`FormDocument.cs:181`), a nie ustawienia kosztów konkursu. W v1 wystarcza to, czego wymagają wzory 2026: sekcja rozwoju instytucjonalnego ukryta dla grup nieformalnych przez `visibleWhen` na rodzaju wnioskodawcy. Przełącznik kategorii w konkursie (pełne R-12) to osobna karta po v1.
3. `prefillFrom` umie dziś tylko kopiować z wniosku do sprawozdania. Blok danych podmiotu w części I pochodzi z karty (T-93), nie z `prefillFrom`.
4. Czwarta tabela budżetu i REGON to otwarte pytania z `rozbieznosci.md`. Zrób zgodnie z kolumną "co bez niej robimy".
5. R-34: pisz nazwy małymi literami, jak kreator.
6. PK-I (organizacja młoda i lokalna) domyślnie jest polem we wniosku (R-36).

#### T-95 · Wzór sprawozdania 2026 jako dane
poza MVP · M · tor B · zależności: T-94 · **v1.1** · **wydzielone z T-50c (krrJXt3n), DZ-5**

T-50c ma w zakresie "wzór sprawozdania 2026 w seedzie" i jest zablokowane przez B-04. Ta część zależy jednak tylko od T-94, bo wzory 4a, 4b i 4c są publiczne.

**Zakres.** Trzy warianty przez `appliesTo`, jako `backend/seed/forms/report-2026.json`, z `prefillFrom` na klucze z T-94. Test jak w T-94. Ta pozycja zniknęła z zakresu T-50c w tym samym PR, który wprowadził plan (opis w `M7-wdrozenie.md` i na karcie).

**Kryteria.**
- [ ] Plik przechodzi bramkę kontraktu wzoru sprawozdania (test)
- [ ] Sprawozdanie dla każdego z trzech rodzajów wnioskodawcy pokazuje właściwy wariant 4a, 4b albo 4c
- [ ] Wartości z wniosku 2026 przepisują się obok pól wykonania (`prefillFrom`)
- [ ] Zakres T-50c na karcie i w `M7-wdrozenie.md` nie zawiera już wzoru 2026

#### T-96 · Treść startowa na produkcji: import i podpięcie kart
M5 · M · tor A · zależności: T-94, T-97, T-110

**Po co.** Druga połowa L2. `seed.py` jest deweloperski i odmawia pracy na niepustej bazie, a produkcja potrzebuje tej samej treści.

**Zakres.**

1. Komenda administracyjna obok `grant-role` (`Admin/`):
   `import-content --competition <id> --application <plik> --formal <plik> --merit <plik> --report <plik>`
   - Publikuje przez istniejący `FormDefinitionService` (potrzebuje tylko `AppDbContext`), więc przez te same bramki kontraktu co API.
   - Komendy administracyjne rozgałęziają się przed budową hosta webowego (`Program.cs:20`), więc komenda sama składa minimalny kontener usług.
   - Jest idempotentna: plik identyczny z wersją w mocy niczego nie zmienia.
2. W panelu operatora, na stronie konkursu (T-97), sekcja "Karty oceny i wzór sprawozdania": co jest podpięte, w jakiej wersji, i "skopiuj z konkursu...". Trasy już istnieją. Precedensem jest edytor wzoru umowy z T-45 na ekranie oceny konkursu.

**Kryteria.**
- [ ] Na pustej bazie wystarczą: rejestracja operatora, `grant-role`, konkurs z kreatora i `import-content`. Konkurs ma wtedy formularz, obie karty i wzór sprawozdania
- [ ] Drugie uruchomienie z tymi samymi plikami nic nie zmienia i mówi to
- [ ] Plik niezgodny z kontraktem jest odrzucany z listą błędów, a baza zostaje nietknięta
- [ ] Procedura spisana w `docs/wdrozenie.md` (plik zakłada T-111)

**Pułapki.**

- `backend/seed/` leży poza projektem, więc `dotnet publish` go nie zabierze. Pliki trzeba jawnie skopiować do obrazu (T-110).
- `architektura.md` ("Dane testowe jako skrypt obok aplikacji, nie jako komenda w API") dotyczy danych testowych. To jest treść produkcyjna bez kont i haseł. Zapisz tę różnicę jako decyzję, żeby nikt nie uznał jej za naruszenie.

#### T-97 · Konkurs w panelu operatora: strona, edycja, stany
M2 · L · tor A · zależności: brak

**Po co.** L3.

**Zakres.**

- **Strona konkursu** `panel/operator/competitions/[id]`: podgląd z API i edycja. Edycja zostaje otwarta w każdym stanie, bo to zapisana decyzja (`CompetitionService.cs:135`). Gdy są już wnioski, ekran ostrzega, jak zakłada T-22.
- **Szkic z serwera.** Kreator ogłoszenia wznawia szkic z serwera, a localStorage zostaje tylko buforem niezapisanych zmian.
- **Akcje stanu przez istniejącą tabelę przejść:**
  - publikuj;
  - zamknij nabór (backend już to umie, także przy naborze ciągłym, R-20);
  - rozpocznij ocenę (`Closed` do `UnderReview`);
  - dezaktywuj;
  - przywróć dezaktywowany (R-26, **nowa trasa**, dziś jest tylko `DELETE`).
- **Publikacja** odmawia bez opublikowanej wersji formularza i bez obu kart, z listą braków.
- **Zatwierdzenie wyników** (`results/approve`) wymaga stanu `UnderReview` i w tej samej transakcji przestawia konkurs na `Resolved`. Para `UnderReview` do `Resolved` już jest w tabeli, więc tabela zostaje jedynym źródłem przejść. Przy okazji zamyka to zatwierdzanie przy otwartym naborze.
- **Lista konkursów** linkuje do strony konkursu, a ta do formularza, wniosków i oceny.

**Kryteria.**
- [ ] Szkic założony w jednej przeglądarce otwiera się i publikuje w innej
- [ ] Publikacja bez formularza albo kart jest odrzucana z listą braków (test)
- [ ] Zatwierdzenie wyników poza `UnderReview` jest odrzucane, a udane przestawia konkurs na `Resolved` (test)
- [ ] Dezaktywowany konkurs da się przywrócić (test); R-26 przeniesione do `architektura.md`

**Pułapki.**

- **Nowa reguła publikacji psuje dzisiejszy kreator.** Kreator zapisuje i publikuje jednym krokiem (`competitions/new/page.tsx:154`), a formularz da się ułożyć dopiero dla istniejącego konkursu. Kreator kończy się więc zapisem szkicu, a publikacja przenosi się na stronę konkursu. Zmień sekcję T-22 w `architektura.md`.
- Seed i testy, które publikują konkurs bez kart, trzeba dostosować.

#### T-98 · Kopia konkursu z poprzedniej edycji
M2 · M · tor A · zależności: T-97

R-11. "Skopiuj" na stronie konkursu tworzy szkic, który przenosi:

- ustawienia;
- formularz, jako nową wersję w nowym konkursie;
- obie karty;
- wzór sprawozdania;
- wzór umowy. Wzór umowy z T-45 jest przypisany do konkursu i nic go dziś nie kopiuje, więc to nowa praca.

Kopia **nie przenosi** dat, numeru, wniosków, przypisań ani ocen. To podstawowy sposób pracy operatora od drugiego naboru. Zastępuje ręczne kopiowanie formularza (T-26) i kart (T-96) po kolei.

**Kryteria.**
- [ ] Kopia konkursu z pełnym zestawem daje szkic gotowy do publikacji po ustawieniu dat
- [ ] Zmiana w kopii nie zmienia oryginału (test na wersjach formularza i wzoru umowy)

#### T-99 · Wejście do systemu: strona główna, nagłówek, co przygotować
M4 · M · tor B · zależności: T-93

**Zakres.**

- `app/page.tsx` przestaje być stroną deweloperską i staje się portalem z otwartymi naborami i wynikami.
- Publiczny nagłówek dostaje "Zaloguj" i "Załóż konto", a po zalogowaniu "Mój panel".
- "Aktualne konkursy" w panelu wnioskodawcy pokazują stan prawdziwy. Gdy nabór jest otwarty, jest odnośnik do publicznej listy albo jej skrót. D6 zostaje: lista jest jedna i publiczna, a panel tylko do niej prowadzi.
- `/design-tokens` jest niedostępne poza Development.
- Ekran "co przygotować" (R-10) stoi przed pierwszym polem wniosku, generowany z załączników i terminów konkursu.

**Kryteria.**
- [ ] Ze strony głównej da się dojść do logowania, rejestracji, konkursów i wyników bez wpisywania adresu
- [ ] axe nie zgłasza naruszeń na nowych ekranach (sprawdza to istniejący `vitest.setup.ts`)

#### T-100, T-100a, T-100b · Test całego procesu w przeglądarce, od pustej bazy
M7 · trzy karty po M · tor C

| Karta | Zależności | Zakres |
|---|---|---|
| T-100 | T-93, T-94, T-96, T-97 | infrastruktura testu, od rejestracji do złożenia wniosku |
| T-100a | T-100 | ocena formalna i merytoryczna, wyniki, maile, lista publiczna |
| T-100b | T-100a, T-109 | umowa i rezygnacja |

**Po co.** Warunek wyjścia 5. Ten test złapałby L1, zanim zrobiło to przejście po kodzie.

**Zakres.**

- Playwright w osobnym katalogu `e2e/`, z własną mapą. `check_map.py` musi poznać nowy obszar (runbook, "Bramka czerwona na `check_map.py`").
- Mailpit w compose, w profilu `test`. Test czyta linki weryfikacji z jego API.
- Scenariusz przez UI, publiczne API i komendy administracyjne (`grant-role`, `import-content`): rejestracja operatora, konkurs, import treści, publikacja, dwóch wnioskodawców z kartami i załącznikiem, złożenie, dwóch ekspertów z deklaracją, ocena, zatwierdzenie, maile, lista publiczna, umowa. **Zero SQL z boku.**
- Job w CI na każdym PR, z nagraniem przebiegu przy porażce.

**Pułapki.**

- Konkurs dostaje daty względem "teraz". Test nie może czekać na minutę odcięcia.
- Cel to poniżej 5 minut. Job idzie równolegle do reszty, nie po niej.

**Kryteria.**
- [ ] Scenariusz od rejestracji do złożenia wniosku przechodzi w CI na pustej bazie
- [ ] Mailpit w profilu `test`, linki z maili czytane przez API skrzynki
- [ ] Zero SQL z boku w scenariuszu
- [ ] Nagranie przebiegu przy porażce jako artefakt CI

**Kryteria T-100a.**
- [ ] Scenariusz przechodzi dalej: ocena formalna, dwóch ekspertów z deklaracją, ocena merytoryczna, zatwierdzenie wyników, maile w Mailpit, lista publiczna
- [ ] Wnioskodawca widzi wynik i udostępnione karty bez danych ekspertów

**Kryteria T-100b.**
- [ ] Scenariusz przechodzi dalej: umowa sporządzona, widoczna dla wnioskodawcy, podpisanie zapisane
- [ ] Rezygnacja przesuwa środki na wniosek z listy rezerwowej

### Etap 2 · Luki procesu w zakresie MVP

#### T-101 · Załącznik przypięty do wymogu i komplet przy złożeniu
M4 · M · tor A · zależności: brak · **przed G1**

R-33.

- Nullable `attachments.competition_attachment_id`.
- Upload przyjmuje identyfikator wymogu.
- Złożenie odmawia, gdy brakuje wymaganego załącznika, i wylicza braki w stylu D12.
- Ekran wnioskodawcy pokazuje kafelek na każdy wymóg (kryterium T-34).

**Najpierw, pierwszym commitem:** `CompetitionService.ApplyAttachments` przy każdej edycji usuwa wiersze wymogów i dodaje je od nowa, z nowymi identyfikatorami. Robi to przez `Drop`, czyli **twarde** `_context.Remove` (`CompetitionService.cs:428`). Tak samo traktuje osoby kontaktowe i kategorie kosztów. To łamie regułę "nie kasujemy twardo" i osierociłoby każde powiązanie załącznika.

Kontrakt edycji musi więc dopasowywać wiersze po `id` z żądania. Wiersz usunięty z listy dostaje `is_active = false`, zamiast znikać. Zapisz to w `architektura.md` i jako pozycję do przeglądu T-47a.

**Kryteria.**
- [ ] Edycja konkursu zachowuje identyfikatory wymogów, osób kontaktowych i kategorii kosztów, a usunięty wiersz jest oznaczony jako nieaktywny, nie skasowany (test)
- [ ] Upload z identyfikatorem wymogu zapisuje powiązanie; wymóg z innego konkursu jest odrzucany (test negatywny)
- [ ] Złożenie bez wymaganego załącznika jest odrzucane z listą braków (test)
- [ ] Ekran wnioskodawcy pokazuje kafelek na każdy wymóg
- [ ] Decyzja o dopasowaniu wierszy po `id` w `architektura.md`

#### T-102 · Wzory załączników do pobrania
M2 · M · tor A · zależności: T-101

R-30.

- Operator wgrywa plik wzoru do wymogu przez istniejący `IAttachmentStorage`.
- Strona konkursu udostępnia wzór anonimowo.
- Format decyduje bajt, nie deklaracja (T-32). Limit 10 MB, tak jak `Competition.DefaultMaxAttachmentSizeInBytes`.

**Kryteria.**
- [ ] Operator wgrywa, podmienia i wycofuje plik wzoru przy wymogu
- [ ] Strona konkursu pozwala pobrać wzór bez logowania (test)
- [ ] Plik o niezgodnym formacie albo ponad limit jest odrzucany (test)

#### T-103 · Zwrot wniosku do poprawy
M4 · L · tor A · zależności: T-101 · **przed G1**

R-03, RD10. Operator wskazuje sekcje, opis i termin. Wniosek wraca do edycji z odblokowanymi tylko tymi sekcjami. Poprawka wymaga ponownego złożenia. Dostępne w naborze i po ocenie formalnej, chyba że PK-H mówi inaczej. Historia statusów zapisuje oba przejścia. Mail do wnioskodawcy idzie przez istniejącego nadawcę.

**Model.** Dziś odpowiedzi są nadpisywane w miejscu, a nie ma wersji wniosku. Suma kontrolna liczy się z `Id`, `UpdatedAt` i `Answers` (`ApplicationSubmissionService.cs:178`). "Nowa wersja z nową sumą" wymaga więc tabeli kopii złożonych wersji (odpowiedzi, suma, chwila złożenia) oraz nowego stanu wniosku.

**Pułapki.**

- Serwer odrzuca zapis poza odblokowanymi sekcjami. Blokada tylko w ekranie nie wystarczy (ta sama zasada co w T-50a).
- Poprawka po zamknięciu naboru jest dozwolona tylko do terminu zwrotu. Zapisz to jako jawną gałąź w `CompetitionIntake`, a nie jako drugą regułę obok.
- Po ponownym złożeniu operator robi nową ocenę formalną, a stara zostaje. Indeks unikalny ocen jest filtrowany po `is_active`, więc to się mieści.
- Nowy stan musi świadomie przejść przez constrainty `status <> 'Draft'` (`ApplicationConfiguration.cs:105`) i przez pomocnika `ApplicationStatuses.IsGranted`.

**Kryteria.**
- [ ] Operator zwraca wniosek z wybranymi sekcjami, opisem i terminem; wnioskodawca dostaje mail
- [ ] Zapis poza odblokowanymi sekcjami jest odrzucany przez serwer (test)
- [ ] Ponowne złożenie tworzy nową wersję z nową sumą kontrolną, a poprzednia zostaje odczytywalna (test)
- [ ] Po terminie zwrotu poprawka jest odrzucana (test na granicy minuty)
- [ ] Historia statusów pokazuje zwrot i ponowne złożenie
- [ ] Nowy stan przechodzi przez constrainty i `ApplicationStatuses` (test migracji)

#### T-104 · Konta zespołu OCWIP bez SDK
M1 · M · tor A · zależności: T-110

Decyzja "rola nadawana komendą, nigdy przez HTTP" (`architektura.md`) zostaje. To zadanie sprawia, że da się z niej korzystać na produkcji:

- `grant-role` (odbiera rolę przez `--role Applicant`, README), `deactivate-account` i `list-accounts` działają w obrazie runtime (`dotnet Ocwip.Api.dll ...`). README dostaje ten wariant.
- Istniejąca lista (`OperatorDirectoryService`, `panel/operator/reviewers`) rośnie do listy kont zespołu z rolami, nadal tylko do odczytu.
- Ekspert dostaje mail po przypisaniu wniosków.

Procedura "nowy ekspert" trafia do T-49. Czwarta rola administratora (R-02, PK-B) to osobna karta po odpowiedzi.

**Kryteria.**
- [ ] `grant-role`, `deactivate-account` i `list-accounts` działają w obrazie produkcyjnym bez SDK (test w CI na obrazie)
- [ ] Lista kont zespołu z rolami w panelu operatora, tylko do odczytu
- [ ] Ekspert dostaje mail po przypisaniu wniosków (test)
- [ ] README opisuje wariant dla obrazu produkcyjnego

#### T-105 · Zadania w tle: przypomnienia i terminy
M4 · M · tor A · zależności: brak · **przed G1**

Jeden `BackgroundService` z tabelą wykonanych zadań. Idempotencję daje unikalny klucz (zadanie, obiekt, termin). Wszystko w UTC.

Pierwsi konsumenci:

- R-09: przypomnienie 3 dni przed końcem naboru, tylko dla rozpoczętych i niezłożonych wniosków;
- termin podpisania umowy (14 dni, regulamin 2026) dla T-109;
- później termin sprawozdania dla T-50c.

**Pułapki.**

- Tylko jedna instancja API. Tak samo zakłada dziś cache ponownej wysyłki maila (`Program.cs:104`). Zapisz to założenie w `architektura.md` raz, dla wszystkich.
- Restart w trakcie wysyłki nie może dać podwójnego maila. Wzorem jest warunkowy UPDATE z T-43 (`ResultNotificationService.cs:122`).
- Harmonogram jest wyłączany zmienną, żeby testy go nie odpalały.

**Kryteria.**
- [ ] Przypomnienie wychodzi raz do każdego rozpoczętego i niezłożonego wniosku 3 dni przed końcem naboru (test z zegarem)
- [ ] Restart w trakcie wysyłki nie daje podwójnego maila (test)
- [ ] Harmonogram da się wyłączyć zmienną, a testy go nie uruchamiają
- [ ] Założenie jednej instancji API zapisane w `architektura.md`

#### T-106 · Zmiana hasła i adresu e-mail po zalogowaniu
M1 · M · tor A · zależności: brak

R-08.

- Zmiana hasła wymaga starego hasła i obraca `SecurityStamp`.
- Zmiana adresu wymaga potwierdzenia z nowego adresu, a stary adres dostaje powiadomienie.
- Odpowiedzi nie zdradzają, czy nowy adres jest zajęty (reguła 3).

**Kryteria.**
- [ ] Zmiana hasła wymaga starego hasła i wylogowuje inne sesje (test)
- [ ] Zmiana adresu działa dopiero po potwierdzeniu z nowego adresu, a stary adres dostaje powiadomienie (test)
- [ ] Odpowiedź na zajęty adres jest taka sama jak na wolny (test)

#### T-107 · Zgody i klauzule informacyjne
M1 · M · tor A · zależności: brak · treść: PK-E

R-19 i R-16. Mechanizm budujemy teraz, a treść dostaniemy później.

- Rejestracja zbiera akceptację regulaminu i klauzuli. Zapisuje pełny tekst, który użytkownik widział, i chwilę akceptacji, tak jak deklaracja bezstronności (`ReviewerDeclaration`, T-40a).
- Formularz wniosku ma miejsce na klauzulę dla osób trzecich.
- Teksty są robocze i oznaczone jako ZR. Podmiana to zmiana danych, nie kodu.

**Kryteria.**
- [ ] Rejestracja wymaga akceptacji regulaminu i klauzuli, a zapis zawiera pełny tekst i chwilę akceptacji (test)
- [ ] Formularz wniosku ma miejsce na klauzulę dla osób trzecich
- [ ] Teksty robocze oznaczone jako ZR w `zalozenia-robocze.md`; podmiana to zmiana danych

#### T-108 · Archiwum wyników
M6 · S · tor B · zależności: T-97

R-14 i R-31. Publiczna lista rozstrzygniętych konkursów z dofinansowanymi projektami: nazwa, tytuł i kwota. Przy grupach nieformalnych jest nazwa grupy, bez imion i nazwisk (RD3). Źródłem jest istniejąca publikacja z T-42a (`RankingPublication`), a konkurs rozstrzygnięty rozpoznajemy po stanie `Resolved` z T-97.

**Kryteria.**
- [ ] Publiczna strona archiwum pokazuje rozstrzygnięte konkursy z dofinansowanymi projektami
- [ ] Przy grupie nieformalnej nie ma imion ani nazwisk członków (test)
- [ ] Link do archiwum ze strony głównej

#### T-109 · Rezygnacja i przejście środków na listę rezerwową
M6 · M · tor A · zależności: T-105 · **przed G1** (nowy stan wniosku)

Regulamin 2026: umowa niepodpisana w 14 dni od publikacji listy oznacza rezygnację, a środki przechodzą na kolejny wniosek spełniający próg (także `M6-wyniki.md`, T-42).

- Zegar tylko przypomina. Rezygnację potwierdza operator.
- System proponuje pierwszy wniosek z `Reserve` i jego kwotę w granicach puli, a operator zatwierdza.
- Obie zmiany trafiają do historii statusów, a wnioskodawcy dostają maile.

Zamyka wiersz "Co przy rezygnacji po przyznaniu dotacji" w pytaniach otwartych `rozbieznosci.md`, bo odpowiedź daje regulamin. **ZR-09 zostaje otwarte** (PK-L: czy lista rezerwowa jest ogłaszanym wynikiem).

Nowy stan rezygnacji przechodzi przez te same constrainty co w T-103 i musi pasować do `ContractSigned` z T-45.

**Kryteria.**
- [ ] Po 14 dniach bez podpisu operator dostaje przypomnienie o możliwej rezygnacji
- [ ] Operator potwierdza rezygnację, a system proponuje pierwszy wniosek z listy rezerwowej w granicach puli (test)
- [ ] Zmiany statusów w historii i maile do obu wnioskodawców
- [ ] Wiersz o rezygnacji w pytaniach otwartych `rozbieznosci.md` zamknięty

### Etap 3 · Umowa i sprawozdanie

**T-45 jest zrobione (PR #86).** Dwa pytania, które plan zadawał, mają już odpowiedź:

1. **Wiele wypłat (R-24).** Model umowy nie ma kwoty ani wypłat, bo kwota pochodzi z `applications.awarded_grant`. Tabela wypłat dołożona później będzie więc addytywna: jeden wiersz na każdą istniejącą umowę. Ryzyko z R-24 jest niskie, ale warto je zapisać w R-24.
2. **PESEL-e w umowie** lądują w `contracts.values`: obiekt jsonb, w którym kluczami są dowolne nazwy znaczników wpisane przez autora wzoru. Komentarz w `Models/Contract.cs` mówi, że umowy nie idą na produkcję przed szyfrowaniem. Obejmuje to T-47a.

**Dalsze zadania w tym etapie:**

- **T-45b** (umowy hurtem, wzór 2026 jako dane) jest **odblokowane na założeniu (DZ-4)**, tak jak T-38b: wzór 2026 jest publiczny, a wersja od prawnika (P15) to po prostu nowa wersja wzoru. Specyfikacja jest w `M6-wyniki.md`, a wiersz w kolejce stoi na `kolejka` bez blokera, zaraz po sekcji v1. Zakres i zalecenia:
  - **wzór 2026 jako dane:** `backend/seed/templates/contract-2026.txt`, rozpisany na znaczniki ze słownika `TemplatePlaceholders`. Import idzie przez `import-content` (T-96). Znaczniki spoza słownika to pola operatora, a każdy znacznik z PESEL-em jest oznaczony jako wrażliwy (T-47a);
  - **umowy hurtem:** jedno działanie tworzy brakujące umowy dla wszystkich wniosków `Funded` i oddaje ZIP z PDF-ami. Przy tej skali (do około 60 umów) wystarcza strumieniowy ZIP w żądaniu, bez kolejki zadań w tle. Umowa, która nie ma kompletu pól operatora, nie blokuje reszty: trafia na listę braków w odpowiedzi;
  - **podzbiór czcionki:** przy pełnej czcionce Noto (około 300 KB na plik) 60 umów to kilkanaście MB. Podzbiór glifów użytych w dokumencie zmniejsza plik do kilkudziesięciu KB. Wymaga przepisania tabel `glyf`, `loca` i `hmtx` w `TrueTypeFont` z testem, że tekst dalej się kopiuje (ToUnicode). Jeśli to się rozrośnie, wydziel jako T-45c i zostaw ZIP z pełną czcionką;
  - **grupa z patronem:** lista podpisujących członków grupy (P17) pochodzi z tabeli członków we wniosku 2026 (T-94), więc T-45b zależy od T-94.
- **T-50b** (rozliczenie) jest zrobione (PR #87).
- **T-50c** (termin, sprawozdanie częściowe, załączniki, historia projektu) i **T-95** wchodzą w v1.1. Wzór 2026 przeszedł z T-50c do T-95 (DZ-5).

### Etap 4 · Produkcja technicznie (T-48 bez części zależnej od B-06)

Nic w tym etapie nie zależy od wyboru hostingu. Cel: `docker compose -f docker-compose.prod.yml up` na dowolnym Linuksie z Dockerem daje działającą i bezpieczną usługę. Wybór dostawcy zmienia wtedy tylko DNS i serwer, na który idzie wdrożenie.

#### T-110 · Obrazy produkcyjne
M7 · M · tor C · zależności: T-90

- **Backend:** obraz wieloetapowy. Etap `sdk` robi `dotnet publish -c Release`, a runtime to `aspnet:10.0` z użytkownikiem bez roota. Obraz zawiera dane strefy czasowej i pliki treści startowej z `backend/seed/` (T-96). Czcionki PDF są zasobem wbudowanym w dll, więc przychodzą same.
- **Front:** `output: "standalone"`, `node server.js` jako użytkownik `node`, bez pollingu webpacka.

**Pułapki.**

- **Strefa czasowa.** Bez `Europe/Warsaw` w obrazie eksporty i komunikaty naboru przechodzą na UTC. Robią to jawnie (etykieta "czasu UTC", `ApplicationListLabels.cs:67`), ale godziny różnią się wtedy od tego, co widzi operator. Test w obrazie: `TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw")` przechodzi. ICU jest prawie niepotrzebne, bo kod celowo omija kulturę `pl-PL` (`PolishNumbers.cs`).
- **Adres API we froncie.** `NEXT_PUBLIC_API_URL` jest wpisywany w bundle przy `next build` (`lib/api-client.ts:13`). Jest argumentem builda, dopóki front i API nie staną pod jednym originem (T-111). Wtedy ta zmienna znika. `API_SERVER_URL` dla renderowania po stronie serwera zostaje w obu wariantach.
- **Generowanie typów.** Obraz produkcyjny nie może potrzebować `openapi-typescript`.

**Kryteria.**
- [ ] Obraz backendu na `aspnet:10.0`, bez SDK, jako użytkownik bez roota, z plikami z `backend/seed/`
- [ ] Obraz frontu w trybie `standalone`, jako użytkownik `node`, bez pollingu
- [ ] Test w obrazie: strefa `Europe/Warsaw` jest dostępna
- [ ] Oba obrazy budują się w CI, a smoke test przechodzi na nich
- [ ] Wiersze w `docs/map/infra.md`

#### T-111 · Compose produkcyjne, reverse proxy i TLS
M7 · L · tor C · zależności: T-110

**Plik `docker-compose.prod.yml`:**

- bez bind mountów, z `ASPNETCORE_ENVIRONMENT=Production`;
- baza i API bez portów na hoście;
- wymagane zmienne przez `${VAR:?}`;
- healthchecki dla wszystkich usług, `restart: unless-stopped` i limity logów.

**Caddy jako proxy.** Przy jednej maszynie ma najmniej ruchomych części: automatyczny TLS i HSTS. Limit ciała żądania jest równy największemu limitowi załącznika plus zapas. `/api` idzie na backend, reszta na front.

**Pułapka ścieżek.** Trasy backendu nie mają prefiksu `/api`, a `/competitions` istnieje i jako trasa API, i jako strona frontu. Caddy musi zdejmować prefiks (`handle_path /api/*`), albo backend dostaje `UsePathBase`.

**API:**

- `UseForwardedHeaders` z `KnownProxies` z konfiguracji.
  - **Nie** `ASPNETCORE_FORWARDEDHEADERS_ENABLED`, bo ta zmienna czyści listę zaufanych i ufa każdemu nadawcy. Limiter logowania bierze partycję z adresu IP, więc bez poprawki wszyscy dzielą jeden limit albo atakujący podrabia nagłówek.
  - Tę zmienną **zalecają dziś trzy miejsca**: komentarz w `RateLimitingConfiguration.cs`, `architektura.md` (sekcja o brute force) i `.env.example`. Wszystkie trzy do poprawy.
- `AllowedHosts` ustawione na domenę.
- `UseExceptionHandler` i `UseStatusCodePages`, żeby błąd 500 w Production był ProblemDetails.
- `/health/db` łapie każdy wyjątek, nie tylko `NpgsqlException`, i używa jednego współdzielonego `NpgsqlDataSource`. Dziś go nie ma, więc trzeba go utworzyć.
- `MaxRequestBodySize` spójny z proxy, plus górna granica limitu załącznika w `CompetitionRequestValidator`, gdzie dziś jej nie ma.
- Sprawdzenie w smoke teście, że `/openapi` nie odpowiada w Production. Włączone jest dziś tylko w Development, a test ma pilnować, żeby tak zostało.

**`docs/wdrozenie.md`:** pierwsze uruchomienie, aktualizacja, wycofanie wersji, pierwszy operator i import treści.

**Kryteria.**
- [ ] Smoke test przechodzi na compose produkcyjnym (lokalnie z certyfikatem wewnętrznym Caddy)
- [ ] Z zewnątrz na hoście otwarte są tylko porty 80 i 443
- [ ] Test: limiter logowania rozróżnia dwóch klientów za proxy i ignoruje nagłówek od niezaufanego nadawcy

#### T-112 · Nagłówki bezpieczeństwa i CSP
M7 · M · tor C · zależności: T-111

Kryterium z T-47.

- **W API:**
  - `X-Content-Type-Options: nosniff`, dziś go nie ma;
  - `frame-ancestors 'none'`;
  - `Referrer-Policy: no-referrer`, bo tokeny resetu siedzą w adresie.

  Pliki wnioskodawców już idą jako `Content-Disposition: attachment` (`AttachmentEndpoints.cs:164`), więc to tylko sprawdzenie.
- **W Next:** CSP z nonce albo `'self'`, `connect-src` na własny origin i `Permissions-Policy`.

Sprawdzenie odbywa się na stagingu, w T-119.

**Kryteria.**
- [ ] Nagłówki z listy obecne na odpowiedziach API i frontu (test)
- [ ] CSP nie blokuje żadnego ekranu (przejście T-100 zielone)
- [ ] Wynik sprawdzenia na stagingu zapisany w `docs/wdrozenie.md`

#### T-113 · Klucze DataProtection i migracje osobnym krokiem
M7 · M · tor C · zależności: T-110 · **przed T-47a**

- `PersistKeysToFileSystem` na wolumen albo zapis do bazy, plus `SetApplicationName("ocwip")`. Dziś każdy nowy kontener wylogowuje wszystkich i unieważnia linki z maili.
- Migracje przez `dotnet ef migrations bundle`, jako usługa `migrate` uruchamiana raz przed `up`, z rolą bazodanową z prawami DDL. Aplikacja łączy się rolą z samym DML (kryterium T-48, `architektura.md`, "Migracje przy starcie").
- `db/init/001_extensions.sql` ma na sztywno nazwę bazy (`ALTER DATABASE ocwip`), a zarządzany Postgres nie uruchamia `db/init`.
  - Strefę czasową bazy przenieś do connection stringa albo do migracji. `ALTER DATABASE` wymaga właściciela bazy.
  - Usuń nieużywane rozszerzenia (`unaccent`, `pgcrypto`; `gen_random_uuid()` jest wbudowane od PostgreSQL 13).
  - **Zmień też `.github/workflows/ci.yml`**, bo CI aplikuje `db/init/` przez psql (runbook, "CI czerwone", przyczyna 3).

**Pułapka.** Jeśli T-47a oprze szyfrowanie na DataProtection, utrata katalogu kluczy oznacza utratę danych. Dlatego T-47a używa osobnego klucza z sekretu.

**Kryteria.**
- [ ] Nowy kontener nie wylogowuje użytkowników (test: sesja przeżywa restart)
- [ ] Migracje uruchamia usługa `migrate` rolą DDL, a aplikacja nie ma praw DDL (sprawdzone)
- [ ] `db/init` bez nazwy bazy na sztywno, CI zmienione i zielone
- [ ] Decyzja w `architektura.md`, sekcja "Migracje przy starcie" zaktualizowana

#### T-114 · Kopie zapasowe i przetestowane odtworzenie
M7 · M · tor C · zależności: T-111, T-113

- Kontener z cronem co noc robi `pg_dump -Fc` i kopię wolumenu załączników oraz kluczy DataProtection. Wszystko trafia do repozytorium **restic**, na magazyn poza serwerem, w UE, z poświadczeniami tylko do dopisywania. Restic szyfruje po stronie klienta, co ma znaczenie przy PESEL-ach.
- Retencja: 7 kopii dziennych, 4 tygodniowe, 12 miesięcznych i roczne przez okres retencji danych.
- `scripts/restore.sh` odtwarza wszystko na pustą maszynę.

**Kryteria.**
- [ ] Odtworzenie przeprowadzone na czystym środowisku, czas zapisany w `docs/wdrozenie.md`
- [ ] Po odtworzeniu da się zalogować, otworzyć załącznik i wygenerować PDF (klucze i pliki przetrwały, nie tylko baza)
- [ ] Kopia bez hasła restic jest bezużyteczna (sprawdzone)

**Magazyn na staging:** Hetzner Storage Box BX11 (3,20 EUR miesięcznie) albo Backblaze B2 EU (6 USD za TB). Wybór dla produkcji zapada razem z hostingiem (PK-C).

#### T-115 · CI/CD: obrazy, skan, wdrożenie
M7 · M · tor C · zależności: T-110, T-111

- Na push do `dev` i `main` powstają obrazy w GHCR z tagiem SHA oraz `dev` albo `main`, skanowane przez Trivy.
- Smoke test chodzi drugi raz, na compose produkcyjnym.
- Workflow wdrożenia jest wyzwalany ręcznie i wymaga akceptacji w środowisku GitHub. Łączy się przez SSH kluczem służącym tylko do wdrożeń, robi `pull`, `migrate` i `up --wait`, a przy nieudanym healthchecku wycofuje wersję.
- **Blokada kalendarza.** Wdrożenie odmawia, gdy któryś opublikowany konkurs kończy nabór w ciągu 3 dni. Przepuszcza tylko z jawnym parametrem wymuszenia (T-48, "Termin naboru a wdrożenie"). Workflow sprawdza to w publicznym API.

**Kryteria.**
- [ ] Obrazy w GHCR z tagiem SHA po każdym pushu do `dev` i `main`
- [ ] Trivy oblewa build przy podatności krytycznej
- [ ] Wdrożenie wymaga akceptacji i wycofuje wersję przy nieudanym healthchecku (próba na stagingu)
- [ ] Blokada kalendarza odmawia wdrożenia 3 dni przed końcem naboru (test)

#### T-116 · Obserwowalność
M7 · S · tor C · zależności: T-111

- Logi JSON (`AddJsonConsole`) z identyfikatorem żądania.
- Rotacja logów Dockera.
- Zewnętrzny monitoring dostępności odpytuje `/health` i `/health/db` osobno i wysyła alert mailem. Przy tej skali wystarczy darmowy zewnętrzny monitor albo Uptime Kuma na innym serwerze niż aplikacja.

**Kryteria.**
- [ ] Logi w formacie JSON z identyfikatorem żądania
- [ ] Rotacja logów Dockera ustawiona
- [ ] Monitor odpytuje `/health` i `/health/db` osobno i alarmuje mailem (próba z wyłączoną bazą)

#### T-117 · Staging
M7 · M · tor C · zależności: T-111, T-114 · **przyjęte przez zespół (DZ-3)**

Serwer przedprodukcyjny na naszym koncie: Hetzner Cloud CX23 (2 vCPU, 4 GB) w UE, około 6 EUR miesięcznie. T-118 pokaże, czy to wystarcza, a przejście na CX33 to zmiana typu serwera, nie przeprowadzka.

**Czego agent nie zrobi sam:** konto u dostawcy, płatność, token API i domena należą do człowieka. Agent przygotowuje wszystko, co da się zapisać w repozytorium, a człowiek wykonuje kroki z listy "przed startem" w `docs/wdrozenie.md`.

**Zasady, bo staging ma sens tylko wtedy, gdy przypomina produkcję:**

- **Te same obrazy i ten sam compose co produkcja.** Obrazy z GHCR z tym samym tagiem, który potem idzie na produkcję. Także `ASPNETCORE_ENVIRONMENT=Production`, żeby staging przechodził kontrolę konfiguracji z T-91 (Mailpit jako `SMTP_HOST`, adres stagingu w `FRONTEND_BASE_URL`, `CORS_ORIGINS` i `ALLOWED_HOSTS`). Różnice (adres, sekrety, Mailpit, hasło na proxy) siedzą w `docker-compose.staging.yml` jako nakładka, nie w kopii pliku.
- **Maszyna z pliku.** `infra/staging/cloud-init.yaml` w repo (nowy obszar mapy):
  - użytkownik wdrożeniowy bez sudo, logowanie tylko kluczem SSH, bez roota i bez hasła;
  - `unattended-upgrades` dla łatek bezpieczeństwa;
  - Docker z oficjalnego repozytorium.

  Zapora Hetzner Cloud Firewall przepuszcza tylko porty 22 (z wybranych adresów), 80 i 443.
- **Nic publicznego.** Caddy wymaga hasła (`basic_auth`) i wysyła `X-Robots-Tag: noindex`. Adres nie trafia do żadnego dokumentu dla klientki poza zaproszeniem na próbę.
- **Wyłącznie dane fikcyjne.** Żadnych kopii z produkcji, nigdy, także "na chwilę". Poczta idzie do Mailpit, więc żaden mail ze stagingu nie wyjdzie do prawdziwej osoby.
- **Własne sekrety.** Inne hasła bazy, inny klucz szyfrowania i inny magazyn kopii niż na produkcji. Kopie stagingu idą do osobnego repozytorium restic, bo tam ćwiczymy odtworzenie (T-114).
- **Koszt pod kontrolą.** Jeden serwer, bez usług zarządzanych. Po G4 staging zostaje jako miejsce prób przed każdym wdrożeniem.

Tu odbywają się próba generalna, test obciążenia i przegląd bezpieczeństwa. **Bez tego serwera T-118 do T-120 nie mają gdzie się odbyć, a hosting klientki nie będzie gotowy przed G4.**

**Kryteria.**
- [ ] `infra/staging/cloud-init.yaml` i `docker-compose.staging.yml` w repozytorium, w mapie
- [ ] Staging stoi z obrazów z GHCR, za hasłem, z nagłówkiem `noindex`
- [ ] Zapora przepuszcza tylko 22 (wybrane adresy), 80 i 443
- [ ] Maile trafiają do Mailpit
- [ ] Kroki człowieka (konto, płatność, token, DNS) spisane w `docs/wdrozenie.md`

### Etap 5 · Dane wrażliwe (T-47 bez części formalnej)

T-47 dzielimy na dwie karty:

- **T-47a**, część techniczną, której B-05 nie blokuje (`blokery.md`, B-05, "Czego nie blokuje");
- **T-47b**, retencję, której termin zależy od umowy OCWIP z NIW.

Wiersz T-47 zostaje kartą nadrzędną i przechodzi w `gotowe`, gdy obie są zmergowane.

#### T-47a · Szyfrowanie danych wrażliwych i przegląd wycieków
M7 · L · tor A · zależności: T-113 · **przed G1, przed pierwszą prawdziwą daną osobową**

**Szyfrowanie po stronie aplikacji.**

- AES-GCM (`System.Security.Cryptography.AesGcm`), losowy nonce, format z numerem wersji klucza.
- Klucz pochodzi z sekretu: nie z repozytorium, nie z bazy i nie z DataProtection. Rotacja idzie przez numer wersji.

**Zakres pól według kryterium T-47** ("PESEL, NIP, adres osoby fizycznej"), zawężonego o NIP decyzją DZ-2, i komentarzy w kodzie:

- `users.pesel`. Kolumna istnieje, ale nic jej dziś nie zapisuje;
- wartości w `contracts.values`. Szyfrujemy wartości wewnątrz obiektu, nie kolumnę (T-47, pułapka 2). Znacznik wzoru dostaje oznaczenie "wrażliwy" w `TemplatePlaceholders`;
- pola karty podmiotu z T-93: `entities.address`, `correspondence_address`, `phone`, `email` (dawne `contact_information`), `bank_account` i `representatives`, oraz kopia karty w `applications.entity_snapshot`; każde ma w komentarzu kolumny "T-47a";
- **przegląd T-101:** wiersze list konkursu (`competition_attachments`, `competition_contacts`, `competition_cost_categories`) nie są już kasowane, tylko oznaczane `is_active = false`; sprawdzić, czy żaden odczyt nie pokazuje nieaktywnych i czy retencja nie wymaga ich kiedyś zanonimizować;
- dane członków grupy w `applications.answers`. Kontrakt formularza dostaje flagę wrażliwości pola, analogicznie do `printed`, i szyfrowane są tylko te wartości;
- **NIP zostaje jawny (DZ-2).** RODO chroni tylko osoby fizyczne (motyw 14), a u nas NIP ma zawsze organizacja: grupa nieformalna go nie ma, a patron jest organizacją. NIP organizacji jest publiczny (KRS, biała lista VAT) i jest kluczem rozpoznania "ta organizacja jest już zarejestrowana" (R-01), a szyfrogram wymagałby do tego osobnego indeksu HMAC. Chronią go kontrola dostępu, szyfrowanie dysku i kopii. Komentarz kolumny (`HasComment`, "encrypted at rest in T-80") zmienia się migracją, a komentarze w `Entity.cs` i `EntityConfiguration.cs` razem z nią. **Warunek:** jeśli kiedyś wnioskodawcą będzie osoba fizyczna z NIP-em (jednoosobowa działalność), decyzja wraca do przeglądu, bo wtedy NIP jest daną osobową (opinia rzecznika generalnego TSUE w sprawie C-496/17).

Kolumny poszerzamy pod szyfrogram (T-47, pułapka 1).

**Dostęp.** PESEL w UI jest maskowany. Odszyfrowanie następuje tylko przy generowaniu umowy, dla roli, która tego potrzebuje. Log dostępu do danych osobowych prowadzimy dla odczytów wrażliwych (D8 na Trello, dawne T-82).

**Przegląd:**

- logi pod kątem haseł, tokenów, PESEL-i i treści wniosków;
- test po całym modelu potwierdza brak `ON DELETE CASCADE`. Dziś są tylko testy pojedynczych relacji;
- żadnego twardego DELETE na danych domenowych, łącznie z `CompetitionService.Drop`, jeśli T-101 go jeszcze nie usunął;
- komentarze `T-80` w kodzie przepięte na `T-47a`.

**Kryterium z karty T-47, bez retencji:** zrzut bazy bez klucza jest bezużyteczny, co potwierdzamy próbą.

**Pułapki.**

- Wyszukiwanie po zaszyfrowanym polu, jeśli kiedyś będzie potrzebne, idzie przez osobny indeks HMAC z innym kluczem.
- **Utrata klucza oznacza utratę danych.** Klucz ma kopię poza serwerem, osobno od kopii bazy, zgodnie z procedurą z `docs/wdrozenie.md`.
- To zadanie dotyka bezpieczeństwa, więc filtr może zatrzymać długą odpowiedź. Rób je jednym plikiem na raz (RY5).

**Kryteria.**
- [ ] PESEL, wartości wrażliwe umowy, adres i kontakt grupy nieformalnej oraz dane członków grupy zaszyfrowane, klucz poza repozytorium
- [ ] Zrzut bazy bez klucza jest bezużyteczny (próba)
- [ ] Kolumny poszerzone, komentarze `T-80` przepięte, komentarz NIP zmieniony (DZ-2)
- [ ] Przegląd logów bez haseł, tokenów, PESEL-i i treści wniosków
- [ ] Test po całym modelu: brak `ON DELETE CASCADE`; brak twardego DELETE na danych domenowych
- [ ] Odczyt danych wrażliwych trafia do logu dostępu
- [ ] Procedura kopii klucza w `docs/wdrozenie.md`

#### T-47b · Retencja i usuwanie danych osobowych po terminie
M7 · M · tor A · zależności: T-47a · bloker: B-05 (okres z umowy z NIW) · **v1.1**

- Ekran pokazuje konkursy po terminie retencji i wnioski do anonimizacji. Decyzja zapisuje, kto ją podjął.
- Anonimizacja zeruje dane osób, a wniosek zostaje (M7, T-47, "Uzupełnienie z raportu", punkt 3).
- Retencja karty organizacji (R-15) zależy od R-01.
- Termin to rok realizacji plus 1 plus N lat. N wynika z umowy z NIW, domyślnie 5.

Pierwszy termin minie za kilka lat, więc zadanie może wejść po starcie naboru. Musi jednak wejść przed pierwszym takim terminem.

**Kryteria.**
- [ ] Ekran z konkursami po terminie retencji i wnioskami do anonimizacji
- [ ] Anonimizacja zeruje dane osób, a wniosek zostaje (test)
- [ ] Decyzja zapisuje, kto ją podjął i kiedy

### Etap 6 · Pilot

#### T-118 · Test obciążenia pod termin naboru
M7 · S · tor C · zależności: T-117

Scenariusz k6 na stagingu, oparty na historii 2026: 149 wniosków, większość w ostatnich godzinach naboru.

- **Symulacja:** 150 wnioskodawców w ostatnich 2 godzinach, autozapis co 30 sekund, upload 5 MB, złożenia w ostatnich 10 minutach.
- **Cel:** p95 autozapisu poniżej 1 s, zero błędów 5xx i żadnego złożenia odrzuconego przed terminem.

Wynik trafia do `docs/wdrozenie.md` razem z rozmiarem serwera, który to wytrzymał.

**Kryteria.**
- [ ] Scenariusz k6 w repozytorium
- [ ] Wynik na stagingu spełnia cele: p95 autozapisu poniżej 1 s, zero 5xx, żadne złożenie przed terminem nie odrzucone
- [ ] Wynik i rozmiar serwera zapisane w `docs/wdrozenie.md`

#### T-119 · Przegląd bezpieczeństwa przed wystawieniem
M7 · M · tor C · zależności: T-117, T-112, T-47a

- `/security-review` na całym API, nie na diffie.
- OWASP ZAP baseline na stagingu.
- Przejście po `testy.md` ("Co musi mieć test", punkt 1) dla każdej trasy dodanej po T-36.

Ustalenia trafiają na listę z wagą. Wysokie blokują G2.

**Kryteria.**
- [ ] Przegląd całego API zrobiony, ustalenia na liście z wagą
- [ ] ZAP baseline na stagingu bez ustaleń wysokich
- [ ] Każda trasa dodana po T-36 ma test negatywny uprawnień

#### T-120 · Próba generalna z OCWIP
M7 · M · zależności: T-117, T-96, T-98 · wymaga obecności klientki (PK-P; w opisie karty, bo kolumna Bloker przyjmuje tylko `B-xx`)

Operator OCWIP na stagingu, na fikcyjnych danych, odtwarza nabór 2026:

- zakłada konkurs z kopii i ogłasza go;
- trzy osoby z zespołu składają wnioski;
- operator prowadzi ocenę z dwoma ekspertami, zatwierdza wyniki i sporządza umowę.

Obserwujemy, nie pomagamy. Każde zacięcie staje się kartą. To zarazem test roboczej wersji instrukcji T-49.

**Kryteria.**
- [ ] Operator OCWIP przeszedł nabór od kopii konkursu do umowy bez naszej pomocy
- [ ] Każde zacięcie zapisane jako karta
- [ ] Uwagi do roboczej instrukcji T-49 przekazane

#### T-121 · Deklaracja dostępności i strony informacyjne
M7 · M · tor B · zależności: T-99 · **dopisane do zakresu decyzją DZ-6** (T-121 dopisuje to do `zakres.md`) · treść: PK-E

**Deklaracja dostępności** według "Warunków technicznych publikacji oraz struktury dokumentu elektronicznego deklaracji dostępności" w wersji 2.0 (Ministerstwo Cyfryzacji, 31.07.2024; obowiązuje nowe deklaracje od 1.08.2024):

- **Miejsce.** Strona `/deklaracja-dostepnosci`, link o treści dokładnie "Deklaracja dostępności" w stopce każdej strony.
- **Nagłówki.** `h1` "Deklaracja dostępności", a pod nim sekcje `h2` w tej kolejności:
  1. Stan dostępności cyfrowej;
  2. Niedostępne treści (tylko przy zgodności częściowej albo braku zgodności);
  3. Przygotowanie deklaracji dostępności;
  4. Udogodnienia, ograniczenia i inne informacje;
  5. Skróty klawiszowe;
  6. Informacje zwrotne i dane kontaktowe;
  7. Obsługa wniosków i skarg związanych z dostępnością;
  8. Pozostałe informacje, z podsekcjami `h3`: Aplikacje mobilne, Dostępność architektoniczna, Dostępność komunikacyjno-informacyjna.
- **Identyfikatory `id` wymagane przez walidator:**
  - obowiązkowe: `a11y-wstep`, `a11y-podmiot`, `a11y-zakres`, `a11y-url`, `a11y-data-publikacja`, `a11y-data-aktualizacja`, `a11y-status`, `a11y-kontakt`, `a11y-email`, `a11y-telefon`, `a11y-procedura`, `a11y-data-sporzadzenie`, `a11y-architektura`, `a11y-komunikacja`;
  - opcjonalne: `a11y-ocena`, `a11y-aplikacje`, `a11y-architektura-url`;
  - `a11y-data-przeglad` staje się obowiązkowe od pierwszego przeglądu.
- **Daty** w `<time datetime="rrrr-mm-dd">5 lutego 2027 r.</time>`.
- **Stan zgodności** to jedno z trzech zdań wzoru, przepisane dosłownie. Zmienia się tylko lista niezgodności. Wynik bierzemy z audytu T-46 (`docs/dostepnosc.md`).
- **Treść jako dane.** Osoba do kontaktu, telefon, daty i opis budynku (dostępność architektoniczna) pochodzą od OCWIP (PK-E). Siedzą w jednym pliku konfiguracji strony, więc coroczna aktualizacja do 31 marca to zmiana danych, nie kodu.
- **Test.** Wszystkie obowiązkowe `id` są na stronie. Przed publikacją sprawdzamy stronę walidatorem v2 (<https://deklaracja-dostepnosci.info/walidator>).

**Strony informacyjne:** klauzula informacyjna RODO, kontakt i regulamin serwisu, linkowane ze stopki obok deklaracji. Treść prawną daje OCWIP. My dajemy strukturę i miejsce na stronie.

**Dlaczego mimo wątpliwości, czy OCWIP jest podmiotem publicznym.** Ustawa obejmuje podmioty publiczne. Przy zadaniu finansowanym ze środków publicznych umowy z NIW zwykle wymagają jednak dostępności (ustawa o zapewnianiu dostępności osobom ze szczególnymi potrzebami, art. 4), a sama deklaracja jest tania i zamyka temat.

Treść prawna przychodzi od OCWIP, a struktura i miejsce na stronie są po naszej stronie. Nawet jeśli OCWIP formalnie nie jest podmiotem publicznym, umowy z NIW zwykle wymagają dostępności przy zadaniach publicznych.

**Kryteria.**
- [ ] Strona `/deklaracja-dostepnosci` z nagłówkami i identyfikatorami z wersji 2.0 (test na obecność obowiązkowych `id`)
- [ ] Link "Deklaracja dostępności" w stopce każdej strony
- [ ] Walidator v2 nie zgłasza błędów
- [ ] Strony z klauzulą RODO, kontaktem i regulaminem w stopce
- [ ] `zakres.md` zawiera deklarację dostępności i strony informacyjne (DZ-6)

### Etap 7 · Przekazanie

- **T-48** domykamy na docelowym serwerze klientki, gdy PK-C da hosting: DNS, certyfikat, sekrety, pierwsza kopia i monitoring. Procedurę przechodzi ktoś, kto jej nie pisał.
- **T-49**, instrukcja operatora, powstaje na zrzutach z działającego systemu. Kryterium karty mówi "od ogłoszenia do rozliczenia". Dla v1 dochodzi ono do rozliczenia z T-50b, a części z T-50c dopisujemy w v1.1.
- **Release** `dev` do `main` z `--no-ff` i tag `v1.0.0`. To decyzja człowieka, nie agenta.
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
| **B · treść i strony** | T-94, T-95, T-99, T-108, T-121 | `backend/seed/`, kontrakt formularza, strony publiczne |
| **C · infrastruktura** | T-90, T-91, T-100 do T-100b, T-110 do T-119 | Dockerfile, compose, `.github/`, konfiguracja w `Program.cs`, `scripts/`, `e2e/` |
| **D · dokumentacja** | T-92, `docs/wdrozenie.md` | `docs/` |

**Zasady dla gorących plików.**

1. **Jedna migracja naraz.** Zadania z migracją to T-93, T-94 (jeśli kontrakt wymaga zmian w bazie), T-97 (przywracanie), T-101, T-103, T-105, T-109 i T-47a. Idą po kolei. Dwie gałęzie z migracjami rozjeżdżają `AppDbContextModelSnapshot.cs` w sposób, którego scalanie tekstowe nie naprawi. Gdy musisz scalić:
   - nie rozwiązuj konfliktu w snapshotcie ręcznie;
   - weź wersję z `dev`, usuń swoją migrację i wygeneruj ją od nowa (`dotnet ef migrations add`);
   - sprawdź `MigrationTests`.
2. **`frontend/lib/api-schema.ts` generujesz, nie scalasz.** Przy konflikcie weź dowolną stronę, zrestartuj backend (R-32) i wygeneruj plik od nowa komendą z `CLAUDE.local.md`.
3. **`docs/log.md`, `kolejka.md` i mapy:** zostają oba wpisy, a limit 20 wpisów w logu sprawdzasz po scaleniu (runbook, "Konflikt przy merge z `dev`").
4. **`Program.cs`:** tor C dopisuje konfigurację w osobnych metodach rozszerzających w `Configuration/`, tak jak `AddOcwipRateLimiting`. SMTP i CORS są dziś jeszcze w środku pliku. Wtedy dwie gałęzie dotykają jednej linii wywołania, a nie tego samego bloku.
5. **Trello:** najwyżej trzy karty na liście "W trakcie". Nie bierzesz karty, której gałąź jest świeża na `origin`, nawet jeśli karta wisi w backlogu.

---

## 6. Przewidziane blokery i co wtedy robić

| # | Bloker | Prawdopodobieństwo | Co robimy |
|---|---|---|---|
| RY1 | **Klientka nie odpowiada** na pakiet z sekcji 7 | wysokie | Każde pytanie ma odpowiedź domyślną i termin (G0). Po terminie budujemy wariant domyślny i zapisujemy go jako ZR. Do G2 plan nie czeka na nikogo. Przy wysyłce pakietu mówimy klientce wprost, że zmiana zdania po G1 kosztuje migrację na danych |
| RY2 | **Hosting nierozstrzygnięty** (PK-C) do G4 | wysokie | Compose działa na dowolnym Linuksie, a staging stoi na naszym koncie (T-117). W najgorszym razie startujemy produkcję na naszym koncie z umową powierzenia i przenosimy ją później przez odtworzenie z kopii (T-114). Tę procedurę i tak ćwiczymy |
| RY3 | **RODO nierozstrzygnięte** (B-05, PK-E) | wysokie | Część techniczna (T-47a, T-107, T-121) nie zależy od RODO. Klientce dajemy listę tego, co musi przyjść od jej IOD. **Nie piszemy treści prawnych za nią.** Bez klauzul nie startujemy produkcji z prawdziwymi danymi (G4) |
| RY4 | **RD7 (wiele osób przy organizacji) przyjdzie jako "tak" po G1** | średnie | T-93 trzyma dostęp do podmiotu za jedną metodą. Migracja z 1:1 do N:M na żywych danych jest addytywna: tabelę pośredniczącą wypełniamy z `users.entity_id`, a stara kolumna zostaje do następnej wersji. Ten plan wyjścia spisujemy w T-93. **Nie zaszło:** T-93a weszło przed G1 (2026-10-08), więc migracja nie musiała być addytywna |
| RY5 | **Filtr bezpieczeństwa zatrzymuje odpowiedź** przy T-47a, T-112 i T-119 | średnie | `CLAUDE.local.md`, sekcja 4: stop, raport stanu, mniejsze kroki. Zadania bezpieczeństwa z góry dzielimy na pliki, a review robimy kawałkami, jak przy T-35 |
| RY6 | **Limit sesji albo limit tygodniowy** w środku zadania | wysokie | Zadania L dzielimy według runbooka na karty z sufiksem (T-xxa) z własnymi wierszami. Przed końcem sesji zrzucamy stan do `docs/log.md`. Notatkę na karcie zostawiamy jako komentarz, a gdy MCP Trello nie ma komentarzy, w treści pozycji checklisty |
| RY7 | **Dwie sesje biorą to samo** | średnie; zdarzyło się dziś przy T-50b | Przy starcie karta idzie na "W trakcie". Świeża gałąź na `origin` znaczy "zajęte". Pomagają tory z sekcji 5 |
| RY8 | **Konflikty migracji i wygenerowanych typów** | wysokie przy pracy równoległej | Sekcja 5, zasady 1 i 2 |
| RY9 | **Daty na produkcji inne niż u operatora** | średnie | Obraz bez strefy czasowej przełącza się na UTC. Test w T-110 |
| RY10 | **Maile nie dochodzą albo lądują w spamie** | średnie | SPF, DKIM i DMARC na subdomenie nadawczej (np. `powiadomienia.ocwip.pl`), żeby nie ruszać poczty biurowej. `System.Net.Mail` nie obsługuje portu 465 (T-43a), więc dostawca musi dawać port 587 ze STARTTLS. Pasują Brevo (UE, 300 maili dziennie za darmo) i Amazon SES eu-central-1. Wysłanie wyników do 150 osób naraz mieści się w obu |
| RY11 | **Proxy tnie załączniki** | wysokie bez T-111 | nginx ma domyślnie limit 1 MB, Kestrel około 28 MB. Jeden limit w konfiguracji, sprawdzany przy zapisie konkursu |
| RY12 | **Nowy kontener wylogowuje wszystkich** | pewne bez T-113 | T-113 |
| RY13 | **Awaria w dniu terminu naboru** | niskie, ale kosztowne | Zamrożenie wdrożeń (T-115), test obciążenia (T-118), kopia z rana, dyżur. Jeśli awaria i tak przyjdzie, o przedłużeniu naboru decyduje operator zmianą daty, a nie my w bazie |
| RY14 | **Dane testowe ukrywają luki**, jak ukryły L1 | pewne bez T-100 | T-100 stawia wszystko przez API. Nowa funkcja, która działa tylko z seedem, nie przechodzi bramki |
| RY15 | **Kolejka rozjeżdża się z Trello** przy wielu nowych kartach | średnie | Karty zakłada jedna osoba jednym ruchem (sekcja 8). MCP Trello nie przypisuje osób do kart, więc przypisanie robi człowiek |
| RY16 | **OCWIP nie jest operatorem NOWEFIO w 2027** | nieznane | Treść jest danymi, więc zmienia się tylko plik w T-96 i kopia w T-98 (PK-D) |
| RY17 | **Kreator od zera (T-26a) okaże się potrzebny do pierwszego konkursu** | niskie | Do v1 formularz powstaje z kopii (T-26, T-98) albo z pliku (T-96). T-26a zostaje za B-10 i nie leży na ścieżce krytycznej |

---

## 7. Decyzje, na które plan czeka

### Pakiet dla klientki

Przeniesiony do [`pytania.md`](pytania.md), razem z pytaniami szczegółowymi serii `P`: paczka żyje dalej (dochodzą pytania, wracają odpowiedzi), a ten plan jest zapisem z 2026-09-27 i nie powinien być drugą listą do pilnowania.

Co plan z tej paczki bierze: **PK-A, PK-H i PK-I zamykają bramkę G1**, bo po nich każda migracja musi być addytywna. Pozostałe pytania nie wstrzymują żadnego etapu, a przy ciszy po G0 (2026-10-16) obowiązuje odpowiedź domyślna z tabeli.

### Decyzje zespołu (rozstrzygnięte 2026-09-27)

| ID | Decyzja | Rozstrzygnięcie | Gdzie działa |
|---|---|---|---|
| DZ-1 | Budujemy kartę podmiotu (T-93) przed odpowiedzią na B-09 i RD7 | **tak**, wariant 1:1 z planem wyjścia RY4 | T-93; wpis w `model-danych.md`. **Zastąpione 2026-10-08:** RD7 zatwierdzone z raportem, zbudowane w T-93a |
| DZ-2 | NIP poza szyfrowaniem | **tak, NIP zostaje jawny.** NIP organizacji nie jest daną osobową, jest publiczny i służy do rozpoznania organizacji; wraca do przeglądu, gdyby wnioskodawcą została osoba fizyczna z NIP-em | T-47a |
| DZ-3 | Staging na naszym koncie | **tak**, Hetzner CX23 w UE, konto i płatność po stronie człowieka | T-117 |
| DZ-4 | T-45b odblokowane na założeniu (wzór 2026) | **tak** | T-45b, `M6-wyniki.md`, `blokery.md` (B-03) |
| DZ-5 | Wzór sprawozdania 2026 przechodzi z T-50c do T-95 | **tak** | T-95, `M7-wdrozenie.md` (T-50c) |
| DZ-6 | T-121 (deklaracja dostępności, strony informacyjne) w zakresie v1 | **tak** | T-121; T-121 dopisuje to do `zakres.md` |

---

## 8. Jak plan wszedł do pracy

Wprowadzony 2026-09-27, w PR z tym plikiem:

1. Decyzje zespołu DZ-1 do DZ-6 rozstrzygnięte (sekcja 7).
2. Karty na Trello w liście Backlog, w kolejności etapów, dla T-90 do T-121, T-100a, T-100b, T-47a i T-47b. Opis karty to specyfikacja z sekcji 4, checklista "Kryteria akceptacji" to jej kryteria.
3. Specyfikacje zostają **tutaj**. Pliki kamieni (`M1-fundament.md` do `M7-wdrozenie.md`) mają na końcu sekcję "Zadania z planu v1" ze spisem i odsyłaczem, więc `runbook.py next` prowadzi do właściwego pliku, a treść nie jest w dwóch miejscach.
4. Wiersze niżej są w [`kolejka.md`](kolejka.md) z kodami kart, jako **jedna sekcja `## v1 · Droga do pierwszej wersji` przed `## M1`**. `runbook.py` zwraca gotowe zadania w kolejności pliku, więc rozłożenie wierszy po sekcjach M1 do M7 postawiłoby na początku T-106 zamiast T-90. Kolumna Kamień zostaje M1 do M7 albo `poza MVP`, bo po niej skrypt wskazuje plik specyfikacji.
5. Zmienione wiersze istniejących zadań:
   - T-48 zależy od `T-36, T-46, T-47a, T-111, T-114, T-115, T-116, T-119` (T-47 zastąpione przez T-47a) i zostaje z blokerem B-06. Bez tej zmiany T-48 czekałby na B-05 przez T-47;
   - T-47 zależy od `T-47a, T-47b` i zostaje zablokowane przez B-05 jako karta nadrzędna;
   - T-45b przechodzi na `kolejka` bez blokera, z zależnościami `T-45, T-94, T-96` (DZ-4);
   - T-50c traci z tytułu i zakresu wzór 2026 (DZ-5).
6. Pakiet PK z sekcji 7 idzie do klientki, a jego kopia na karty B-xx. **To zostaje do zrobienia przez człowieka.**

Wiersze w kolejce (tu bez kodów kart, które są w `kolejka.md`), w kolejności pod `runbook.py next`. Sprawdzone: parser przyjmuje je bez błędów, a `next` wskazuje T-90.

```
| Stan | ID | Zadanie | Kamień | Trello | Zależności | Bloker |
|---|---|---|---|---|---|---|
| kolejka | T-90 | Łatka bezpieczeństwa Next.js, audyt zależności w CI | M7 | - | - | - |
| kolejka | T-91 | Konfiguracja produkcyjna z odmową startu przy błędach | M7 | - | - | - |
| kolejka | T-93 | Karta podmiotu wnioskodawcy | M4 | - | - | - |
| kolejka | T-97 | Konkurs w panelu operatora: strona, edycja, stany | M2 | - | - | - |
| kolejka | T-94 | Formularz wniosku NOWE FIO 2026 jako dane | M3 | - | T-93 | - |
| kolejka | T-110 | Obrazy produkcyjne | M7 | - | T-90 | - |
| kolejka | T-96 | Treść startowa na produkcji: import i podpięcie kart | M5 | - | T-94, T-97, T-110 | - |
| kolejka | T-99 | Wejście do systemu: strona główna, nagłówek, co przygotować | M4 | - | T-93 | - |
| kolejka | T-101 | Załącznik przypięty do wymogu i komplet przy złożeniu | M4 | - | - | - |
| kolejka | T-113 | Klucze DataProtection i migracje osobnym krokiem | M7 | - | T-110 | - |
| kolejka | T-47a | Szyfrowanie danych wrażliwych i przegląd wycieków | M7 | - | T-113 | - |
| kolejka | T-103 | Zwrot wniosku do poprawy | M4 | - | T-101 | - |
| kolejka | T-105 | Zadania w tle: przypomnienia i terminy | M4 | - | - | - |
| kolejka | T-109 | Rezygnacja i przejście środków na listę rezerwową | M6 | - | T-105 | - |
| kolejka | T-100 | Test procesu w przeglądarce: od rejestracji do złożenia | M7 | - | T-93, T-94, T-96, T-97 | - |
| kolejka | T-111 | Compose produkcyjne, reverse proxy i TLS | M7 | - | T-110 | - |
| kolejka | T-112 | Nagłówki bezpieczeństwa i CSP | M7 | - | T-111 | - |
| kolejka | T-114 | Kopie zapasowe i przetestowane odtworzenie | M7 | - | T-111, T-113 | - |
| kolejka | T-115 | CI/CD: obrazy, skan, wdrożenie | M7 | - | T-110, T-111 | - |
| kolejka | T-116 | Obserwowalność | M7 | - | T-111 | - |
| kolejka | T-117 | Staging | M7 | - | T-111, T-114 | - |
| kolejka | T-100a | Test procesu w przeglądarce: ocena i wyniki | M7 | - | T-100 | - |
| kolejka | T-98 | Kopia konkursu z poprzedniej edycji | M2 | - | T-97 | - |
| kolejka | T-102 | Wzory załączników do pobrania | M2 | - | T-101 | - |
| kolejka | T-104 | Konta zespołu OCWIP bez SDK | M1 | - | T-110 | - |
| kolejka | T-106 | Zmiana hasła i adresu e-mail po zalogowaniu | M1 | - | - | - |
| kolejka | T-107 | Zgody i klauzule informacyjne | M1 | - | - | - |
| kolejka | T-108 | Archiwum wyników | M6 | - | T-97 | - |
| kolejka | T-100b | Test procesu w przeglądarce: umowa i rezygnacja | M7 | - | T-100a, T-109 | - |
| kolejka | T-121 | Deklaracja dostępności i strony informacyjne | M7 | - | T-99 | - |
| kolejka | T-118 | Test obciążenia pod termin naboru | M7 | - | T-117 | - |
| kolejka | T-119 | Przegląd bezpieczeństwa przed wystawieniem | M7 | - | T-117, T-112, T-47a | - |
| kolejka | T-120 | Próba generalna z OCWIP | M7 | - | T-117, T-96, T-98 | - |
| kolejka | T-95 | Wzór sprawozdania 2026 jako dane | poza MVP | - | T-94 | - |
| zablokowane | T-47b | Retencja i usuwanie danych osobowych po terminie | M7 | - | T-47a | B-05 |
```

**Liczby.** Wierszy jest 35. Z T-92, które nie ma wiersza, daje to 36 nowych zadań. Do G3 zostaje 32 (bez T-92, T-95 i T-47b oraz bez T-121, które zależy od treści z G4).

**Gdy kolejka znów stanie**, najpierw sprawdź, czy któraś bramka z sekcji 3 nie czeka na decyzję z sekcji 7, zanim napiszesz człowiekowi, że nie ma co robić.
