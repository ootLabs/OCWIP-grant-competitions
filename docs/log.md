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
## 2026-09-30 - zapis do wniosku pod blokadą i tylko dla wnioskodawcy
**Zrobione:** Autozapis i załączniki biorą blokadę wiersza na czas sprawdzeń, złożenie odmawia (409), gdy odpowiedzi zmieniły się po walidacji, a zapis do wniosku wymaga roli wnioskodawcy. Ekspert nie zostaje przypisany do szkicu, a podmiana załącznika trzyma się formatów wymogu.
**Decyzje:** Przy rozjeździe odpowiedzi 409 zamiast cichego zamrożenia: zamrożonej wersji wnioskodawca już nie poprawi. Uzasadnienie w [`architektura.md`](architektura.md).
**Uwaga:** Czystego wyścigu dwóch równoległych żądań nie ma w testach, bo byłby niestabilny; sprawdzana jest bramka, nie splot. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-30 - rejestracja pyta o adres i hasło dwa razy (T-124, R-19)
**Zrobione:** Formularz rejestracji ma powtórzenie adresu i hasła, sprawdzane przy wysłaniu; niezgodność zatrzymuje żądanie i mówi to przy właściwym polu. R-19 zostaje otwarte wyłącznie na telefonie kontaktowym.
**Decyzje:** Powtórzenia zostają na froncie, `RegisterRequest` bez zmian: to pytanie "czy na pewno to wpisałeś", a nie dana o człowieku. Adres porównywany bez rozróżniania wielkości liter (jeden indeks, jedno konto), hasło dosłownie. Uzasadnienie w [`architektura.md`](architektura.md) (T-124).
**Uwaga:** `getByLabel` w Playwright dopasowuje po fragmencie i bez wielkości liter, więc "Adres e-mail" łapie też "Powtórz adres e-mail". Scenariusze e2e pytają teraz o etykietę dokładną; następna etykieta zaczynająca się od istniejącej zepsuje je tak samo.

## 2026-09-30 - nieczytelne ciało JSON kończy się 400, nie 500 (T-123, R-43)
**Zrobione:** Ucięty JSON, pole o złym typie i bajty spoza UTF-8 na dowolnej trasie z ciałem dają 400 jako ProblemDetails z polskim komunikatem, a w logu nie ma wpisu `Error`, więc monitoring (T-116) ani test obciążenia (T-118) nie liczą cudzego skanera jako awarii.
**Decyzje:** `IExceptionHandler` oddaje status z samego wyjątku zamiast stałego 400, więc ciało ponad limit Kestrela też przestaje być 500 (413). Komunikat stały, bo wyjątek potrafi zacytować ciało. Uzasadnienie w [`architektura.md`](architektura.md) (T-111).

## 2026-09-29 - kreator buduje formularz od zera i przestawia sekcje (T-26a)
**Zrobione:** Operator dodaje, usuwa i przestawia sekcje, zaczyna formularz od pustej sekcji zamiast kopiować konkurs, i nadaje wiersze tabeli o stałej liczbie wierszy, której kreator dotąd potrafił tylko dodać i nigdy naprawić. Ruch i usunięcie sekcji są zablokowane zdaniem o tym, co by się zepsuło, zamiast ścieżką JSON przy publikacji.
**Decyzje:** Blokada ruchu liczy dokument PO ruchu i odejmuje naruszenia, które dokument miał wcześniej, zamiast zakazywać ruchu sekcji z warunkiem: formularz zepsuty wcześniej nie zamraża się w miejscu. Uzasadnienie w [`architektura.md`](architektura.md) (T-26a).
**Uwaga:** Karta ruszona bez odpowiedzi na cztery pytania z B-10, decyzją Piotra. B-10 zostaje otwarty i zawęzi ten kreator, a nie przestawi.

## 2026-09-29 - umowa drukuje stronę ze złożenia, PDF nie pada na wcięciu
**Zrobione:** Nazwa, NIP i adres na umowie pochodzą z kopii karty zapisanej przy złożeniu, więc podpisana umowa drukuje się tak samo po zmianie karty. Wcięcie szersze niż pół linii nie wywraca już PDF-u wniosku, umowy ani pakietu umów. Zapis wartości umowy bez `values` to 400, rezygnacja wycofuje nieprzyjęte sprawozdanie, a podpisania nie da się zapisać na wycofanej umowie ani przy wniosku, który nie jest już dofinansowany.
**Uwaga:** Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-29 - testy PDF, pakietu umów i CSP mówią to, co obiecują
**Zrobione:** Test podzbioru czcionki sprawdza każdą składową glifu złożonego, test pakietu umów także brak wniosku odrzuconego, a naruszenie CSP w e2e oblewa test, który je spowodował. Nieaktualne komentarze o czcionce w całości poprawione.
**Decyzje:** Limit kosztów pośrednich w sprawozdaniu (od czego liczony, czy nadwyżka jest nieuznana sama) to pytanie do OCWIP, zapisane jako R-41; do odpowiedzi nadwyżkę odmawia operator.
**Uwaga:** Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-29 - przeszyfrowanie i log odczytów bez cichych luk
**Zrobione:** `reencrypt-data` czyta flagi `sensitive` z zapisanej definicji, gdy wersja formularza nie przechodzi dzisiejszego kontraktu, zamiast przepisać wrażliwe odpowiedzi jawnym tekstem. Log odczytów sprawdza wartość trasy przy starcie, nie liczy Forbid i przekierowań jako odczytu, a test trzyma listę ośmiu logowanych endpointów. Compose produkcyjne ma drugi klucz na czas rotacji.
**Decyzje:** Identyfikator wiersza poza danymi powiązanymi szyfrowania: podmiana między wierszami wymaga zapisu do bazy, a z nim atakujący zmienia i tak jawne kolumny. Uzasadnienie w [`architektura.md`](architektura.md) (T-47a).
**Uwaga:** Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-29 - konta zespołu, zmiana adresu, kopia i wzory bez luk
**Zrobione:** `reactivate-account` cofa wyłączenie konta, a ostatniego aktywnego operatora `deactivate-account` nie wyłączy. Zmiana adresu zapisuje adres i nazwę konta jednym zapisem, a adres zajęty dostaje powiadomienie bez linku, więc oba przypadki trwają tyle samo. Kopia konkursu pomija kontaktowego, który nie jest już operatorem, a dwie podmiany wzoru naraz idą po kolei.
**Decyzje:** Ponowna akceptacja regulaminu po jego zmianie to pytanie do OCWIP i IOD, zapisane jako R-40, nie poprawka.
**Uwaga:** Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-29 - terminy umów, maile po zapisie i odporny harmonogram
**Zrobione:** Awans z listy rezerwowej ma własne 14 dni i własne przypomnienie, a zadanie patrzy tylko na terminy z ostatniego tygodnia. Odmowa maila po rezygnacji albo awansie zostawia zmianę i mówi `mailSent: false` zamiast 500; mail o rezygnacji podaje prawdziwą przyczynę. Wysyłka SMTP ma limit czasu, pętlę zadań kończy tylko zatrzymanie hosta, a temat przypomnienia o naborze podaje chwilę końca.
**Decyzje:** Termin umowy od ostatniego przejścia na `Funded` w historii: dla awansowanego wynikiem jest awans. Założenie do potwierdzenia z OCWIP, opisane w [`architektura.md`](architektura.md) (T-109).
**Uwaga:** Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-29 - wdrożenie wraca po nieudanym starcie, staging przechodzi
**Zrobione:** `deploy.sh` wraca do poprzedniego commita także wtedy, gdy samo `up` się nie uda (migracja, usługa bez zdrowia). Workflow wdrożenia przyjmuje tylko commit zmergowany i pyta staging z hasłem (`SITE_BASIC_AUTH`). API ufa nagłówkom przekazanym tylko od stałego adresu Caddy. CI ma domyślnie `contents: read` i akcje przypięte do commita. `monitor.py --backup-max-age` alarmuje, gdy kopia nocna nie powstaje, a `restore.sh` nie restartuje działającej bazy.
**Decyzje:** Staging dostaje hasło w workflow, a nie wyjątek w Caddy dla `/api`: zostaje w całości prywatny. Wiek kopii sprawdza monitor poza serwerem, bo alarm z serwera zamilknie razem z nim.
**Uwaga:** Administrator: "Deployment branches" w obu środowiskach i sekret `SITE_BASIC_AUTH` na stagingu ([`wdrozenie.md`](wdrozenie.md)). Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-29 - zwrot do poprawy nie blokuje konkursu
**Zrobione:** Wniosek zwrócony i niezłożony do terminu poprawy zatwierdzenie wyników odrzuca, zamiast stać na nim w nieskończoność. Zwrot czyści roboczą decyzję o kwocie, ponowne złożenie zostawia kopię karty podmiotu z pierwszego złożenia, a otwarcie karty oceny blokuje wiersz wniosku przed równoczesnym zwrotem.
**Decyzje:** Po terminie odrzucenie, a nie przedłużenie ani ręczna decyzja operatora: okno poprawy już się nie otworzy, a nieuzupełniony wniosek i tak nie przeszedłby oceny formalnej. Uzasadnienie w [`architektura.md`](architektura.md), sekcja T-103.
**Uwaga:** Log przekroczył limit, najstarszy wpis w archiwum.

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
