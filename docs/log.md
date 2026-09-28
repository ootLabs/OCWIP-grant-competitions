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
## 2026-09-28 - test w przeglądarce do złożenia wniosku (T-100)
**Zrobione:** `e2e/` z Playwrightem: operator z rejestracji i `grant-role`, konkurs, `import-content`, publikacja, dwóch wnioskodawców do złożenia na ekranie i maila z potwierdzeniem; Mailpit w profilu `test`, zadanie `e2e` w CI z nagraniem przy porażce. Lokalnie około 20 s.
**Decyzje:** Konta i złożenie przez ekrany, dane przez API, serwer przez komendy, bez SQL. Uzasadnienie w [`architektura.md`](architektura.md).
**Uwaga:** Pierwsze kliknięcie na stronie, którą serwer deweloperski dopiero kompiluje, bywa przed hydracją; scenariusz klika do skutku. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-28 - podzbiór czcionki w PDF (T-45c)
**Zrobione:** Każdy PDF osadza tylko użyte glify Noto; umowa 2026 ma około 140 KB zamiast około 430 KB, polskie znaki i kopiowanie tekstu bez zmian.
**Decyzje:** Puste kontury zamiast przenumerowania glifów, więc reszta PDF-a bez zmian. Uzasadnienie w [`architektura.md`](architektura.md).
**Uwaga:** Reszta rozmiaru to nieskompresowane strumienie treści, które czyta `PdfTextReader` w testach. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-28 - umowy hurtem i wzór umowy 2026 (T-45b)
**Zrobione:** `seed/templates/contract-2026.txt` z paragrafami 1 do 20 i klauzulą, `import-content --contract`, ZIP umów kompletnych z `braki.txt` dla reszty, członkowie grupy w umowie z roli `groupMembers`, przycisk ZIP pod wzorem umowy.
**Decyzje:** ZIP przez tę samą ścieżkę co jedna umowa, w pamięci; członkowie przez rolę, nie klucz. Uzasadnienia w [`architektura.md`](architektura.md), założenia w ZR-17.
**Uwaga:** `seed.py` nie publikuje jeszcze wzoru umowy; podzbiór czcionki to T-45c. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-28 - wzór sprawozdania 2026 jako dane (T-95)
**Zrobione:** `seed/forms/report-2026.json` z wariantami 4a, 4b i 4c, wartościami z wniosku 2026 obok wykonania i trzema tabelami budżetu; rozliczenie liczy każdą tabelę budżetu. `seed.py` publikuje wzór i znów działa na świeżej bazie.
**Decyzje:** Kilka tabel z rolą `reportBudget` zamiast jednej, ocena kosztu z kluczem tabeli. Odstępstwa od wzorów w ZR-16. Uzasadnienia w [`architektura.md`](architektura.md).
**Uwaga:** `seed.py` nie ma testu w CI, a lista `TABLES` rozjechała się z migracjami przez pięć zadań. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-28 - archiwum wyników (T-108)
**Zrobione:** `/archive` pokazuje rozstrzygnięte konkursy z dofinansowanymi projektami (nazwa, tytuł, kwota) i linkiem do pełnych wyników; dane z anonimowego `GET /public/results`.
**Decyzje:** Archiwum składane z opublikowanych wyników, więc nie pokaże więcej niż one; konkurs archiwalny zostaje w archiwum. Uzasadnienia w [`architektura.md`](architektura.md).
**Uwaga:** Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-28 - zgody przy rejestracji i klauzula dla osób trzecich (T-107)
**Zrobione:** Rejestracja pokazuje regulamin i klauzulę w całości i wymaga zaznaczenia obu; `consent_acceptances` trzyma pełny widziany tekst, wersję i chwilę. Formularz 2026 ma oświadczenie dla osób trzecich wskazanych we wniosku.
**Decyzje:** Teksty w `seed/consents/*.md`, wersja to skrót treści, więc podmiana pliku sama wymusza nową akceptację. Uzasadnienia w [`architektura.md`](architektura.md).
**Uwaga:** Wszystkie trzy teksty są robocze (ZR-15), treść od IOD (PK-E). Konta sprzed T-107 nie mają akceptacji. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-28 - zmiana hasła i adresu e-mail po zalogowaniu (T-106)
**Zrobione:** Strona "Moje konto" każdej roli zmienia hasło (z obecnym, inne sesje wylogowane) i adres (z hasłem, link na nowy adres, powiadomienie na stary, zmiana dopiero po potwierdzeniu). Adres zajęty dostaje tę samą odpowiedź co wolny. R-08 zamknięte, placeholdery T-122x zastąpione.
**Decyzje:** Błędne obecne hasło liczy się jak nieudane logowanie. Potwierdzenie przyciskiem, nie przy otwarciu strony. Uzasadnienia w [`architektura.md`](architektura.md).
**Uwaga:** Testy sesji czekają sekundę, bo walidator znacznika sprawdza dopiero, gdy czas minie chwilę wydania ciasteczka. Log przekroczył limit, najstarszy wpis w archiwum.

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
