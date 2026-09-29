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
## 2026-09-29 - przegląd wszystkich tras bez sesji
**Zrobione:** `AnonymousRouteSweepTests` czyta tabelę endpointów aplikacji: 18 tras publicznych to przejrzana lista z powodami, każda inna odpowiada 401 bez sesji. Pod strażą `PermissionSuiteCiGuardTests`.
**Decyzje:** Lista w teście, nie w kodzie produkcyjnym: nowa trasa publiczna jest widoczna w review jako zmiana testu. Sprawdzone mutacją (`AllowAnonymous` na `/me`, polityka przepuszczająca każdego na `GET /applications`).
**Uwaga:** Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-29 - CI pilnuje wygenerowanego klienta API (R-32)
**Zrobione:** Job smoke generuje `frontend/lib/api-schema.ts` z dokumentu OpenAPI i odmawia przy różnicy z zacommitowanym. `npm run api:generate` w kontenerze działa, R-32 zamknięte.
**Uwaga:** Po zmianie sygnatury endpointu nadal `docker compose restart backend` przed generowaniem (README). Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-29 - jedna pisownia nazw w kontrakcie formularza (R-34)
**Zrobione:** Sposób obliczenia, rodzaj limitu i format pliku w definicji formularza są czytane z rozróżnianiem wielkości liter; `"Ratio"` jest odrzucane przy zapisie ze ścieżką pola.
**Decyzje:** Ustąpił backend, nie front: jedno miejsce zamiast siedmiu i jedna pisownia w zapisanym JSON-ie. Uzasadnienie w [`architektura.md`](architektura.md), sekcja T-24.
**Uwaga:** Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-29 - dokumentacja dogania kod (T-92)
**Zrobione:** "Stan repozytorium" w `AGENTS.md` i "Czego tu jeszcze nie ma" w `architektura.md` opisują stan faktyczny i odsyłają do `kolejka.md`; README i `seed.py` mówią, jak zalogować się na konto z seeda (reset hasła, mail w Mailpit); D16 w `decyzje.md`; `npm ci` w mapie infra.
**Decyzje:** Rozjazd "recenzent" kontra "ekspert" zapisany jako R-39, bez zmiany UI: to nazewnictwo do potwierdzenia z OCWIP.
**Uwaga:** Reset hasła na koncie z seeda sprawdzony na lokalnym stosie (mail, reset, logowanie). Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-29 - deklaracja dostępności i strony informacyjne (T-121)
**Zrobione:** `/deklaracja-dostepnosci` według wzoru 2.0 (nagłówki, obowiązkowe `id`, daty w `<time>`), `/regulamin`, `/klauzula-informacyjna` i `/kontakt`, stopka z czterema linkami na każdej stronie, `zakres.md` z DZ-6.
**Decyzje:** Stan "częściowo zgodna" przez PDF-y bez znaczników; teksty prawne z `/public/consents`. Uzasadnienie w [`architektura.md`](architektura.md), dane robocze w ZR-18.
**Uwaga:** Walidator v2 potrzebuje publicznego adresu, więc czeka na staging (B-11). Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-29 - staging po stronie repozytorium (T-117)
**Zrobione:** `docker-compose.staging.yml` (Mailpit, hasło i `noindex` przez fragment Caddy), `infra/staging/cloud-init.yaml`, `DEPLOY_COMPOSE_FILES` w `deploy.sh` i lista kroków człowieka w `wdrozenie.md`. Nakładka sprawdzona lokalnie: 401 bez hasła, `noindex`, mail w Mailpit.
**Decyzje:** Nakładka na compose produkcyjne zamiast kopii; dodatki Caddy przez `site.d`. Uzasadnienie w [`architektura.md`](architektura.md).
**Uwaga:** Serwer czeka na zespół (B-11), T-117 w kolejce jako zablokowane, a z nim T-118 do T-120. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-29 - logi JSON i monitoring (T-116)
**Zrobione:** Poza Development log JSON z zakresem żądania i `X-Request-Id` w odpowiedzi; `scripts/monitor.py` odpytuje osobno `/health` i `/health/db` i pisze przy awarii i powrocie. Próba z wyłączoną bazą: jeden alarm i jeden mail o powrocie.
**Decyzje:** Monitor poza serwerem, mail tylko przy zmianie stanu. Uzasadnienie w [`architektura.md`](architektura.md).
**Uwaga:** Maszynę monitora i adres dyżuru trzeba wybrać razem z hostingiem (PK-C). Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-29 - obrazy w GHCR, skan i wdrożenie (T-115)
**Zrobione:** Zadanie CI `production` skanuje obrazy Trivy i po smoke teście wypycha je do GHCR z tagiem SHA i gałęzi; `deploy.yml` z blokadą kalendarza, SSH i `scripts/deploy.sh` z powrotem do poprzedniego commita; front z względnym `/api`.
**Decyzje:** Jeden obraz na commit dla każdej domeny; wdrożenie ręczne w środowisku z akceptacją. Uzasadnienia w [`architektura.md`](architektura.md).
**Uwaga:** Środowiska, sekrety i akceptację ustawia administrator repozytorium (instrukcja w `wdrozenie.md`); próba na stagingu czeka na T-48 i T-117. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-29 - kopie zapasowe i odtworzenie (T-114)
**Zrobione:** Usługa `backup` (restic co noc: zrzut bazy, załączniki, klucze), `scripts/restore.sh` na pustą maszynę, zadanie CI `backup` z odtworzeniem i logowaniem po nim. Lokalna próba: 62 s, dane zgodne, złe hasło odmawia.
**Decyzje:** Serwer tylko dopisuje, retencja z zaufanej maszyny. Uzasadnienie w [`architektura.md`](architektura.md).
**Uwaga:** Magazyn produkcyjny czeka na hosting (PK-C). Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-29 - nagłówki bezpieczeństwa i CSP (T-112)
**Zrobione:** API: `nosniff`, `no-referrer`, `default-src 'none'; frame-ancestors 'none'` na każdej odpowiedzi, także po błędzie. Strony: CSP z nonce z `middleware.ts`, `Permissions-Policy`, `nosniff`, `no-referrer`. Test przeglądarki zbiera naruszenia CSP, a strony publiczne sprawdza też na compose produkcyjnym przez Caddy.
**Decyzje:** Nonce z `'strict-dynamic'` zamiast samego `'self'`; style z `'unsafe-inline'`. Uzasadnienia w [`architektura.md`](architektura.md).
**Uwaga:** Sprawdzenie na stagingu czeka na T-48 i T-119, stan zapisany w `wdrozenie.md`. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-29 - compose produkcyjne, Caddy i TLS (T-111)
**Zrobione:** `docker-compose.prod.yml` z bazą z rolami, migracjami, API w `Production`, frontem i Caddy (TLS, HSTS, `/api` ze zdjętym prefiksem), tylko porty 80 i 443; API ufa nagłówkom przekazanym od sieci Caddy; błędy jako ProblemDetails; jeden `NpgsqlDataSource` w `/health/db`; limit pliku konkursu do 25 MB; zadanie CI `production` przez Caddy.
**Decyzje:** Lista zaufanych proxy z konfiguracji zamiast `ASPNETCORE_FORWARDEDHEADERS_ENABLED`; prefiks zdejmuje Caddy. Uzasadnienia w [`architektura.md`](architektura.md).
**Uwaga:** Trasa zapasowa pomijała ścieżki z rozszerzeniem (401 zamiast 404), poprawione. Kopie zapasowe i wydanie to dalsze zadania. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-29 - test w przeglądarce do podpisanej umowy (T-100b)
**Zrobione:** Scenariusz kończy się umową: umowa organizacji u wnioskodawcy, rezygnacja, dofinansowanie grupy z listy rezerwowej, umowa grupy z członkami z wniosku i zapis podpisania. Etapy w `e2e/steps/`. Lokalnie około 43 s.
**Decyzje:** Grupa ma teraz 60 punktów bez kwoty, więc trafia na listę rezerwową zamiast odrzucenia; ścieżkę odrzucenia sprawdzają testy backendu.
**Uwaga:** Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-28 - test w przeglądarce do publicznych wyników (T-100a)
**Zrobione:** Scenariusz `e2e/tests/process.spec.ts` idzie dalej: ocena formalna, dwóch ekspertów z deklaracją, karty merytoryczne, kwota, zamknięcie naboru i zatwierdzenie wyników na ekranie, maile o wynikach w Mailpicie, publiczna lista bez odrzuconego. Lokalnie około 36 s.
**Decyzje:** Przyciski z potwierdzeniem na ekranie, karty oceny przez API. Uzasadnienie w [`architektura.md`](architektura.md).
**Uwaga:** Umowa i rezygnacja w przeglądarce to T-100b. Log przekroczył limit, najstarszy wpis w archiwum.

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
