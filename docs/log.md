# Log projektu

Krótki, gęsty zapis tego, co się wydarzyło i dlaczego. Najnowsze na górze.

**Czytaj 3 do 5 górnych wpisów**, gdy bierzesz pracę, a nie cały plik. To jest log, nie podręcznik: trwała wiedza należy do [`architektura.md`](architektura.md) (decyzje), [`map/`](map/README.md) (gdzie co leży) albo [`konwencje.md`](konwencje.md) (jak piszemy). Jeśli fakt będzie ważny za trzy miesiące, ląduje tam, a tutaj zostaje wskaźnik.

## Format, trzymaj się ściśle

```
## RRRR-MM-DD - krótki tytuł
**Zrobione:** co teraz działa, jedna linia.
**Decyzje:** co wybrano i dlaczego, po jednej linii. Tylko nieoczywiste.
**Uwaga:** co ugryzie następnym razem. Pomiń linię, jeśli nic nie ugryzie.
```

**Limit: 20 wpisów.** Dodajesz dwudziesty pierwszy? Przenieś najstarszy do `docs/log-archiwum/<rok>.md` w tym samym commicie. Limit jest sensem tego pliku: nieograniczony dziennik to plik, którego nikt nie czyta, a każda sesja za niego płaci.

Każdy wpis maksymalnie 5 linii. Nie opowiadaj procesu, nie wypisuj zmienionych plików (git wie), nie powtarzaj tego, co już mówi mapa.

---
## 2026-09-28 - konta zespołu OCWIP bez SDK (T-104)
**Zrobione:** `grant-role`, nowe `deactivate-account` i `list-accounts` działają w obrazie produkcyjnym jako `dotnet Ocwip.Api.dll ...` (sprawdza to CI na obrazie), a README ma ten wariant. Operator widzi listę zespołu z rolami i stanem kont, tylko do odczytu. Ekspert dostaje mail po nowym przypisaniu wniosku.
**Decyzje:** Wyłączenie konta przez nowy znacznik bezpieczeństwa kończy sesje od razu. Lista i komenda nie pokazują wnioskodawców. Mail jest jeden na przypisanie. Uzasadnienia w [`architektura.md`](architektura.md).
**Uwaga:** Procedura "nowy ekspert" trafia do T-49; czwarta rola administratora (R-02, PK-B) to osobna karta po odpowiedzi. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-28 - wzory załączników do pobrania (T-102)
**Zrobione:** Operator wgrywa, podmienia i wycofuje plik wzoru przy wymogu załącznika na stronie konkursu. Publiczna strona konkursu linkuje wzór, który pobiera się bez logowania. Format rozpoznawany po bajtach, limit 10 MB. R-30 zamknięte.
**Decyzje:** Osobna tabela `attachment_templates` z jednym aktywnym wzorem na wymóg, podmiana i wycofanie tylko dezaktywują, ten sam magazyn co załączniki. Uzasadnienia w [`architektura.md`](architektura.md).
**Uwaga:** Kopia konkursu (T-98) nie przenosi wzorów; w kopii trzeba je wgrać ponownie. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-28 - kopia konkursu z poprzedniej edycji (T-98)
**Zrobione:** "Skopiuj konkurs" na stronie konkursu tworzy szkic z ustawieniami, ustawieniami oceny, maile o wyniku, listy załączników, kosztów i kontaktów, formularz, obie karty, wzór sprawozdania i wzór umowy, każdy jako wersja 1 nowego konkursu. Numer i daty podaje operator, więc kopia jest gotowa do publikacji. R-11 zamknięte.
**Decyzje:** Ustawienia przez to samo żądanie i walidator co kreator, dokumenty przez te same serwisy publikacji, jedna transakcja. Uzasadnienia w [`architektura.md`](architektura.md).
**Uwaga:** Dokument źródła niezgodny z dzisiejszym kontraktem formularza odmawia całej kopii z nazwą części. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-28 - rezygnacja i lista rezerwowa (T-109)
**Zrobione:** Po 14 dniach od ogłoszenia wyników operator dostaje mail z listą niepodpisanych umów, a ekran oznacza je "termin minął". Operator potwierdza rezygnację (`Funded` na `Resigned`), a system proponuje pierwszy wniosek z listy rezerwowej z kwotą w granicach wolnej puli. Obie zmiany są w historii, wnioskodawcy dostają maile. Pytanie otwarte o rezygnację zamknięte.
**Decyzje:** Kwota zrezygnowanego zostaje na wierszu, ale nie liczy się do puli. Awans pod blokadą doradczą konkursu. Przypomnienie jest jednym przebiegiem na odbiorcę. Uzasadnienia w [`architektura.md`](architektura.md).
**Uwaga:** ZR-09 (czy lista rezerwowa jest ogłaszanym wynikiem, PK-L) zostaje otwarte. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-28 - zadania w tle i przypomnienie przed końcem naboru (T-105)
**Zrobione:** Jeden `BackgroundService` z rejestrem przebiegów `scheduled_job_runs`, unikalny klucz (zadanie, obiekt, termin). Pierwszy konsument (R-09) to przypomnienie trzy dni przed końcem naboru, raz, tylko do rozpoczętych i niezłożonych wniosków. `BACKGROUND_JOBS_ENABLED` wyłącza harmonogram, a testy go nie odpalają.
**Decyzje:** Najwyżej raz: przebieg zostawiony przez martwy proces nie jest wysyłany drugi raz. Założenie jednej instancji API zapisane raz, dla wszystkich miejsc. Uzasadnienia w [`architektura.md`](architektura.md).
**Uwaga:** Treść przypomnienia jest stała (R-09 otwarte w tej części). Termin podpisania umowy dojdzie jako zadanie przy T-109. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-28 - audyt widoków i placeholdery dla brakujących (T-122x)
**Zrobione:** Każdy widok z `kontekst-projektu.md`, `reguly-biznesowe.md` i `pola.md` zestawiony ze stanem repozytorium w `docs/map/frontend.md`. Klasa "komponenty gotowe, brak strony" okazała się pusta: front jest dojrzalszy niż sugeruje `AGENTS.md`. Trzy widoki bez własnej strony, każdy z osobną kartą w kolejce, dostały placeholder "To jeszcze nie jest gotowe" (`components/not-ready-view.tsx`) i trasę: Moje konto (T-106) we wszystkich panelach, Archiwum wyników (T-108) linkowane ze strony głównej, Deklaracja dostępności (T-121) linkowana z każdej stopki publicznej.
**Decyzje:** Placeholder tylko tam, gdzie brakujący widok ma już kartę w kolejce; enumeracje bez własnej trasy (T-98, T-102, T-104, T-105, T-109, T-45b, T-95) zostają rozszerzeniem istniejącego ekranu, nie nową stroną, więc bez placeholdera.
**Uwaga:** Nieaktualny akapit "Stan repozytorium" w `AGENTS.md` zapisany w `docs/runbook/rozbieznosci.md`, poza zakresem tej karty. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-28 - zwrot wniosku do poprawy (T-103)
**Zrobione:** Operator zwraca złożony wniosek ze wskazanymi sekcjami, opisem i terminem, a wnioskodawca dostaje mail. Wniosek przechodzi w stan `Returned` z zachowanym numerem. Serwer odrzuca zmiany poza odblokowanymi sekcjami i po terminie. Ponowne złożenie daje nową sumę kontrolną, a poprzednia wersja zostaje w `application_versions`. Historia statusów ma oba przejścia. R-03 zamknięte.
**Decyzje:** Nowy stan zamiast powrotu do `Draft`. Kopia wersji przy zwrocie zamiast wersjonowania każdego zapisu. Jedno okno edycji (`ApplicationEditWindow`) dla autozapisu, załączników i złożenia. PK-H przyjęte domyślnie (nabór i ocena). Uzasadnienia w [`architektura.md`](architektura.md).
**Uwaga:** Zwrot dezaktywuje dotychczasowe oceny, a zwrócony wniosek blokuje zatwierdzenie wyników do czasu ponownego złożenia i oceny. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-28 - szyfrowanie danych wrażliwych i log odczytów (T-47a)
**Zrobione:** Adresy, kontakt, konto i reprezentanci podmiotu, PESEL, wartości umów, kopia karty oraz odpowiedzi pól `sensitive` są zaszyfrowane AES-GCM kluczem z `FieldEncryption__Keys__1`. Próba "zrzut bez klucza jest bezużyteczny" to test. PESEL w umowie maskowany. Odczyty danych osobowych trafiają do `personal_data_reads`. `reencrypt-data` szyfruje stare wiersze i obsługuje rotację. Test po całym schemacie: brak kaskad.
**Decyzje:** Szyfrowanie w konwerterach EF, dokumenty jsonb szyfrowane w środku, NIP jawny (DZ-2), log jako filtr endpointu. Uzasadnienia w [`architektura.md`](architektura.md).
**Uwaga:** Bez klucza `Production` nie startuje; kopia klucza poza serwerem według [`wdrozenie.md`](wdrozenie.md). `seed.py` pisze z pominięciem modelu, więc jawnie; takie wiersze szyfruje dopiero `reencrypt-data`. Retencja po terminie (T-47b) i klauzule dla osób trzecich (R-16) otwarte. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-28 - klucze DataProtection i migracje osobnym krokiem (T-113)
**Zrobione:** Klucze sesji na wolumenie (`DataProtection__KeysPath`, nazwa aplikacji `ocwip`), więc sesje i linki z maili przeżywają nowy kontener. Cel `migrate` w `Dockerfile.prod` (bundel EF) migruje rolą `ocwip_migrator`, a API działa na `ocwip_app` bez praw DDL; CI sprawdza to na obrazach. `db/init` bez rozszerzeń i bez nazwy bazy, UTC ustawia połączenie.
**Decyzje:** Klucze w katalogu, nie w bazie; szyfrowanie danych (T-47a) nie opiera się na DataProtection. Uzasadnienia w [`architektura.md`](architektura.md).
**Uwaga:** Na lokalnym stosie `docker compose up -d` zakłada nowy wolumen kluczy, więc pierwsze uruchomienie wyloguje wszystkich jeszcze raz. Role bazy trzeba założyć przed pierwszą migracją ([`wdrozenie.md`](wdrozenie.md)). Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-28 - załącznik przypięty do wymogu (T-101)
**Zrobione:** Edycja konkursu zachowuje identyfikatory wymogów, kontaktów i kategorii i oznacza zdjęte wiersze jako nieaktywne zamiast kasować. Upload przyjmuje `requirementId`, złożenie odmawia z każdym brakującym wymaganym załącznikiem z nazwy, a ekran ma kafelek na każdy wymóg. R-33 zamknięte.
**Decyzje:** Dopasowanie wierszy po `id` (wymogi), koncie (kontakty) i rodzaju (kategorie); `RequiredOutsideKrs` wymagany, gdy karta nie wskazuje KRS. Uzasadnienia w [`architektura.md`](architektura.md).
**Uwaga:** Pliki przesłane przed T-101 nie mają wymogu i nie liczą się przy złożeniu; wnioskodawca dodaje je ponownie w kafelku. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-28 - wejście do systemu: strona główna i nagłówek (T-99)
**Zrobione:** Strona główna to portal z otwartymi naborami i wynikami; publiczny nagłówek ma "Zaloguj", "Załóż konto" albo "Mój panel"; "Aktualne konkursy" pokazują prawdziwe nabory; krok startu wniosku zaczyna się od "Co przygotować" (R-10); `/design-tokens` tylko w Development.
**Decyzje:** Strona główna renderowana na serwerze z publicznej listy (D6), linki konta pytają `GET /me` w przeglądarce, bo tylko tam jest ciasteczko sesji.
**Uwaga:** Wyniki na stronie głównej to konkursy w stanie `Resolved`, bo od T-97 ten stan znaczy zatwierdzone wyniki. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-28 - treść startowa konkursu: import i karty na stronie (T-96)
**Zrobione:** `import-content --competition <id> --application --formal --merit --report` publikuje pliki z `backend/seed/` przez `FormDefinitionService`, wszystko albo nic, bez zmian przy powtórzeniu. Strona konkursu pokazuje wersje kart i wzoru sprawozdania i kopiuje je z innego konkursu. Procedura w [`wdrozenie.md`](wdrozenie.md).
**Decyzje:** Komenda obok `grant-role`, nie w `seed.py`: to treść produkcyjna bez kont. Uzasadnienia w [`architektura.md`](architektura.md).
**Uwaga:** Pliku wzoru sprawozdania 2026 jeszcze nie ma (T-95), więc na pustej bazie wzór sprawozdania trzeba skopiować albo opublikować później. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-28 - obrazy produkcyjne (T-110)
**Zrobione:** `backend/Dockerfile.prod` (publish Release, `aspnet:10.0`, użytkownik `app`, `backend/seed/`, strefa `Europe/Warsaw` sprawdzana przy buildzie) i `frontend/Dockerfile.prod` (standalone, `node server.js` jako `node`). Nowy job CI buduje oba i puszcza na nich smoke test.
**Decyzje:** Osobne pliki zamiast celów w deweloperskich Dockerfile. Uzasadnienia w [`architektura.md`](architektura.md).
**Uwaga:** Job CI uruchamia backend w `Staging`, bo `Production` wymaga pełnej konfiguracji z T-91; compose produkcyjne dokłada T-111. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-28 - formularz wniosku NOWE FIO 2026 jako dane (T-94)
**Zrobione:** `backend/seed/forms/application-2026.json`, jeden warunkowy formularz dla wzorów 1a, 1b i 1c; wniosek każdego rodzaju przechodzi walidację złożenia, drukuje się bez pól technicznych, a lista operatora bierze tytuł, koszt i dotację z ról. Seed publikuje ten formularz.
**Decyzje:** Rodzaj wnioskodawcy to pole wniosku (rola `applicantType`), zamrażane przy złożeniu w `applications.applicant_type`; ocena i reszta czytają `KindOfApplicant`, blokada typu karty z T-93 zniknęła. Formularz idzie za opublikowanymi wzorami 2026, nie za `pola.md` (R-38). Uzasadnienia w [`architektura.md`](architektura.md).
**Uwaga:** Po `pull` przeładuj seed (`down -v`): formularz i odpowiedzi seeda są nowe. Karta formalna nie czyta jeszcze pola `rodzaj_organizacji` (R-36). Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-28 - strona konkursu operatora i publikacja z listą braków (T-97)
**Zrobione:** `panel/operator/competitions/[id]`: stan, braki przed publikacją, przyciski z tabeli przejść, dezaktywacja i przywrócenie (R-26), odnośniki do formularza, wniosków i oceny. Kreator edytuje zapisany konkurs z serwera (`/[id]/edit`), `localStorage` tylko buforuje. Publikacja wymaga formularza i obu kart; zatwierdzenie wyników tylko w `UnderReview` i rozstrzyga konkurs.
**Decyzje:** `UnderReview` do `Resolved` ma własny wyzwalacz `ResultsApproval`, więc trasa statusu go nie wykona. Uzasadnienia w [`architektura.md`](architektura.md), sekcja T-97.
**Uwaga:** Panel nie wgrywa jeszcze kart oceny, więc do T-96 nowy konkurs publikuje się tylko z kartami z API albo seeda. W testach `CompetitionTestHost.ChangeStatusAsync` dopina brakujący formularz i karty przed publikacją, a testy zatwierdzenia wołają `StartReviewAsync`. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-27 - karta podmiotu przy pierwszym wniosku (T-93)
**Zrobione:** Nowe konto samo zakłada kartę podmiotu na kroku przed szkicem (`/me/entity`, pola z `pola.md` 2.2, sumy NIP, REGON, KRS i NRB) i składa wniosek; "Moje wnioski" bez podmiotu to pusta lista. Złożony wniosek trzyma kopię karty (`entity_snapshot`), a "Mój profil" pokazuje i poprawia kartę.
**Decyzje:** DZ-1 (1:1 przed B-09) z planem wyjścia w [`model-danych.md`](model-danych.md); grupa bez patrona ma podmiot z samą nazwą, rodzaj wnioskodawcy do T-94 w karcie i zamarza po złożeniu (R-37). Uzasadnienia w [`architektura.md`](architektura.md).
**Uwaga:** `contact_information` jest teraz `email`. Złożenie odmawia (409) przy niepełnej karcie, więc testy biorą `TestEntity.New()` z kompletną kartą. Po `pull` przeładuj seed (`down -v`), bo stare wiersze nie przejdą reguł. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-27 - odmowa startu przy złej konfiguracji produkcyjnej (T-91)
**Zrobione:** W `Production` API nie startuje przy pustym `DATABASE_URL` albo `SMTP_HOST`, adresie frontu lub originie CORS innym niż publiczne https i `ALLOWED_HOSTS=*`; jeden komunikat wymienia wszystkie błędne klucze. `FRONTEND_BASE_URL` i `ALLOWED_HOSTS` są w compose i `.env.example`.
**Decyzje:** Tylko `Production`, nie wszystko poza Development; staging stawiamy z `Production`, żeby przechodził tę samą kontrolę. Uzasadnienia w [`architektura.md`](architektura.md).
**Uwaga:** Na innym `FRONTEND_PORT` zmień też `FRONTEND_BASE_URL`, inaczej linki w mailach Development wskazują port 3000. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-27 - łatka Next.js i audyt zależności w CI (T-90)
**Zrobione:** `next` 15.5.26, `react` i `react-dom` 19.1.9, `vitest` 4.1.11; `overrides` podnosi `postcss` (8.5.26) i `sharp` (0.35.5), które Next przypina w podatnych wersjach. `npm audit` zero. CI oblewa się przy podatności wysokiej w zależnościach produkcyjnych frontu i w pakietach NuGet.
**Decyzje:** Zostajemy na 15.5.x (16 zmienia API). Audyt frontu bez zależności deweloperskich, żeby dziura w narzędziu testowym nie blokowała każdego PR; te łata Dependabot.
**Uwaga:** Lockfile przegenerowany przez `npx npm@11 install`: npm 10 z obrazu `node:22` wywraca się przy tej zmianie (`Cannot read properties of null (reading 'edgesOut')`). Działający stos lokalny zostaje na starym Next, dopóki nie przebudujesz go z nowym wolumenem: `docker compose up -d --build --renew-anon-volumes frontend` (bez flagi anonimowy wolumen `node_modules` przeżywa przebudowę, [`runbook.md`](../runbook.md)). `overrides` zdejmij, gdy Next sam podniesie te pakiety.

## 2026-09-27 - plan do pierwszej wersji (plan v1)
**Zrobione:** [`runbook/plan-v1.md`](runbook/plan-v1.md): definicja v1, bramki G0 do G5, 36 zadań od T-90 z kryteriami, tory pracy, ryzyka i pakiet pytań do klientki. 35 kart w Backlogu, sekcja v1 w kolejce przed M1.
**Decyzje:** DZ-1 do DZ-6 (sekcja 7 planu): karta podmiotu przed odpowiedzią na B-09, NIP jawny, staging, T-45b odblokowane, wzór sprawozdania 2026 do T-95, deklaracja dostępności w zakresie. T-47 podzielone na T-47a i T-47b.
**Uwaga:** Na świeżej bazie nikt nie złoży wniosku, bo nic nie zakłada podmiotu (L1, T-93); testy i seed wstawiają podmioty z pominięciem API. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-27 - rozliczenie sprawozdania (T-50b)
**Zrobione:** Wzór sprawozdania oznacza budżet (`reportBudget`) i wydatek z dotacji (`grantSpent`); operator przy złożonym sprawozdaniu nie uznaje kosztów kwotą z powodem, serwer liczy kwotę do zwrotu, obie strony ją widzą; przyjęcie daje wniosek `Settled`.
**Decyzje:** Ocena w `reports.cost_review`, nie w odpowiedziach wnioskodawcy; kwota do zwrotu liczona przy odczycie. Uzasadnienia w [`architektura.md`](architektura.md), ZR-14.
**Uwaga:** Termin, sprawozdanie częściowe, załączniki, historia projektu i wzór 2026 przeszły do T-50c (B-04). `IsGranted` obejmuje teraz też `Settled`. Log przekroczył limit, najstarszy wpis w archiwum.
