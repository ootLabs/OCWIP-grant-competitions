# Wdrożenie

Jak postawić system na serwerze i przygotować pierwszy konkurs. Compose produkcyjne z proxy i TLS to T-111 (niżej); kopie zapasowe i procedurę wydania dokładają dalsze zadania z [`runbook/plan-v1.md`](runbook/plan-v1.md), etap 2.

## Compose produkcyjne (T-111)

Jedna maszyna z Dockerem i domeną wskazującą na nią. `docker-compose.prod.yml` stawia bazę, migracje, API, front i Caddy. Z zewnątrz otwarte są tylko porty 80 i 443. Caddy sam pobiera certyfikat Let's Encrypt i dokłada HSTS. `/api/...` idzie na API ze zdjętym prefiksem, reszta na front. API ufa nagłówkom przekazanym tylko z wewnętrznej sieci compose, czyli od Caddy, więc limit logowania liczy się dla prawdziwego adresu klienta.

**Pierwsze uruchomienie.**

1. Skopiuj `.env.prod.example` do `.env.prod` i uzupełnij. Trzy różne hasła do bazy, klucz szyfrowania według sekcji "Klucz szyfrowania" niżej, dane przekaźnika poczty, `DOMAIN` bez `https://` i `CADDY_TLS` jako adres e-mail do Let's Encrypt. Brak wymaganej wartości zatrzymuje start z jej nazwą.
2. `docker compose -f docker-compose.prod.yml --env-file .env.prod up -d --build`. Na pustym wolumenie baza sama zakłada role `ocwip_migrator` i `ocwip_app` (`deploy/db/010-roles.sh`), `migrate` wykonuje migracje, a API startuje dopiero po nich.
3. Sprawdź: `docker compose -f docker-compose.prod.yml --env-file .env.prod ps` (wszystko `healthy`, `migrate` zakończone kodem 0) i `https://<domena>/api/health`.
4. Pierwszy operator i treść pierwszego konkursu: sekcja "Pierwszy konkurs na pustej bazie" niżej. Komendy idą przez `docker compose -f docker-compose.prod.yml --env-file .env.prod exec backend dotnet Ocwip.Api.dll ...`.

**Wdrożenie z GHCR (T-115).** Każdy push do `dev` i `main` buduje obrazy, skanuje je Trivy (krytyczna podatność z dostępną poprawką oblewa build), puszcza na nich smoke test przez Caddy i dopiero wtedy wypycha je do `ghcr.io/ootlabs/ocwip-<nazwa>` z tagiem pełnego SHA oraz `dev` albo `main`. Front używa względnego `/api`, więc jeden obraz pasuje do stagingu i produkcji. Wdrożenie uruchamia człowiek: Actions, "Deploy", środowisko i SHA, opcjonalnie wymuszenie. Workflow sprawdza blokadę kalendarza (`scripts/deploy_guard.py`: odmowa, gdy otwarty nabór kończy się w ciągu 3 dni), łączy się po SSH i uruchamia na serwerze `scripts/deploy.sh <SHA>`. Skrypt robi kopię, ściąga obrazy, uruchamia migrację przed API, czeka na zdrowe usługi, a gdy nie wstaną w 5 minut, wraca do poprzedniego commita.

Jednorazowo, **administrator repozytorium** (konto zespołu ma tylko Write):

1. Settings, Environments: `staging` i `production`, oba z "Required reviewers" (dla `production` co najmniej jedna osoba z OCWIP albo z zespołu, która nie uruchamia wdrożenia sama sobie).
2. W każdym środowisku zmienne `DOMAIN`, `DEPLOY_HOST`, `DEPLOY_USER`, `DEPLOY_PATH` (katalog klonu na serwerze) i sekrety `DEPLOY_SSH_KEY` (klucz tylko do wdrożeń) oraz `DEPLOY_KNOWN_HOSTS` (`ssh-keyscan <host>`).
3. Na serwerze: użytkownik wdrożeń w grupie `docker`, klucz publiczny w jego `authorized_keys`, klon repozytorium w `DEPLOY_PATH` z `.env.prod` i `IMAGE_REGISTRY=ghcr.io/ootlabs/` w tym pliku; jeśli pakiety GHCR są prywatne, `docker login ghcr.io` tokenem z samym `read:packages`.

Próba wdrożenia i wycofania wersji na stagingu czeka na staging (T-48, T-117).

**Aktualizacja bez GHCR.** `git pull` na tagu wydania, potem ta sama komenda `up -d --build`. `migrate` wykonuje nowe migracje przed nowym API. Wolumeny (baza, załączniki, klucze sesji, certyfikaty) zostają.

**Wycofanie wersji.** `git checkout <poprzedni tag>` i `up -d --build`. Jeśli nowa wersja miała migrację, sam kod poprzedniej wersji jej nie cofa. Najpierw przywróć bazę z kopii zrobionej przed aktualizacją, bo migracji nie cofa się na danych produkcyjnych bez kopii.

**Nagłówki bezpieczeństwa (T-112).** API odpowiada z `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer` i `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'`. Strony frontu dostają CSP z nonce na każde żądanie, `Permissions-Policy`, `nosniff` i `Referrer-Policy`, a Caddy dokłada HSTS. Sprawdzenie po wdrożeniu:

```bash
curl -sI https://<domena>/ | grep -iE 'content-security-policy|permissions-policy|x-content-type-options|referrer-policy|strict-transport-security'
curl -sI https://<domena>/api/health | grep -iE 'content-security-policy|x-content-type-options|referrer-policy'
```

Wynik sprawdzenia: compose produkcyjne przez Caddy w CI (zadanie `production`, 2026-09-29) ma wszystkie nagłówki na stronach i w API, a strony publiczne działają pod polityką bez żadnego naruszenia (`e2e/tests/public-pages.spec.ts`). Staging jeszcze nie istnieje (T-48, B-06), więc sprawdzenie na nim i wpis wyniku tutaj należą do przeglądu T-119.

**Maszyna testowa bez publicznej domeny.** `CADDY_TLS=internal` daje certyfikat z własnego urzędu Caddy, a `HTTP_PORT` i `HTTPS_PORT` zmieniają porty na hoście, gdy kontenery nie mogą zająć portów poniżej 1024. Tak chodzi zadanie `production` w CI: domena `ocwip.test` i smoke test przez Caddy (`SMOKE_*` w `scripts/smoke_test.py`). `localhost` nie przejdzie, bo `Production` wymaga publicznego adresu.

Obrazy produkcyjne opisuje [`map/infra.md`](map/infra.md) (`backend/Dockerfile.prod`, `frontend/Dockerfile.prod`, T-110).

## Deklaracja dostępności (T-121)

Strona `/deklaracja-dostepnosci` ma strukturę wersji 2.0 "Warunków technicznych" (Ministerstwo Cyfryzacji), a jej dane siedzą w `frontend/lib/accessibility-statement.ts`. **Co roku do 31 marca** trzeba przejrzeć deklarację i zmienić w tym pliku datę aktualizacji; zmiana treści to zmiana danych, nie kodu. Przed publikacją (i po każdej zmianie) stronę sprawdza walidator v2 pod <https://deklaracja-dostepnosci.info/walidator>, który potrzebuje publicznego adresu: dziś czeka na staging (B-11). Wartości robocze do podmiany przez OCWIP opisuje ZR-18.

## Staging (T-117)

Serwer przedprodukcyjny na koncie zespołu: Hetzner Cloud CX23 (2 vCPU, 4 GB) w UE. Te same obrazy z GHCR i ten sam compose co produkcja, `Production` włącznie; różnice siedzą w nakładce `docker-compose.staging.yml`: Mailpit zamiast przekaźnika poczty, hasło na każdej stronie i `X-Robots-Tag: noindex`. Wyłącznie dane fikcyjne, nigdy kopia z produkcji. Własne sekrety: inne hasła bazy, inny klucz szyfrowania, osobne repozytorium restic.

**Przed startem: kroki człowieka** (konto, płatność i domena należą do zespołu, nie do agenta):

1. Konto Hetzner Cloud i metoda płatności; projekt `ocwip-staging`.
2. Para kluczy SSH tylko do wdrożeń: `ssh-keygen -t ed25519 -C deploy@ocwip-staging`. Klucz publiczny wpisz w `infra/staging/cloud-init.yaml` w miejsce `REPLACE_WITH_THE_DEPLOY_PUBLIC_KEY` (na kopii pliku, bez commitowania klucza).
3. Serwer: CX23, Ubuntu 24.04, lokalizacja w UE, "Cloud config" z tego pliku. Po kilku minutach `ssh deploy@<adres>` działa, a `ssh root@<adres>` nie.
4. Cloud Firewall przypięty do serwera: przychodzące tylko TCP 22 z adresów zespołu, TCP 80 i TCP 443 z każdego adresu.
5. DNS: rekord A `staging.<domena>` na adres serwera.
6. Na serwerze jako `deploy`: `git clone https://github.com/ootLabs/OCWIP-grant-competitions /opt/ocwip`, w nim `.env.prod` według `.env.prod.example` z częścią "Staging only" i `IMAGE_REGISTRY=ghcr.io/ootlabs/`; jeśli pakiety GHCR są prywatne, `docker login ghcr.io` tokenem z samym `read:packages`.
7. W GitHubie (administrator): środowisko `staging` ze zmiennymi `DOMAIN`, `DEPLOY_HOST`, `DEPLOY_USER=deploy`, `DEPLOY_PATH=/opt/ocwip` i sekretami `DEPLOY_SSH_KEY`, `DEPLOY_KNOWN_HOSTS`.
8. Pierwsze wdrożenie: Actions, "Deploy", środowisko `staging`, SHA ostatniego obrazu z `dev`. Potem `https://staging.<domena>/` pyta o hasło, a poczta jest pod `https://staging.<domena>/mailpit/`.

**Wynik próby nakładki.** 2026-09-29, lokalnie (compose produkcyjny z nakładką stagingu, domena testowa, certyfikat wewnętrzny Caddy): bez hasła i ze złym hasłem 401, z hasłem 200 i `X-Robots-Tag: noindex, nofollow`, `/api/health/db` odpowiada, a mail z rejestracji trafił do Mailpit pod `/mailpit/`. Sam serwer, zapora i wdrożenie z GHCR czekają na kroki 1 do 8.

## Logi i monitoring (T-116)

**Logi.** Poza Development API pisze jedną linię JSON na wpis, z zakresem żądania (`RequestId`, `RequestPath`, `TraceId`) i czasem w UTC. Każda odpowiedź ma nagłówek `X-Request-Id`, a każda odpowiedź błędu (ProblemDetails) pole `traceId`: z jednego albo drugiego da się znaleźć wszystkie linie danego żądania, na przykład `docker compose -f docker-compose.prod.yml --env-file .env.prod logs backend | grep <id>`. Docker trzyma najwyżej 5 plików po 10 MB na usługę (`x-logging` w compose produkcyjnym), więc logi nie zapełnią dysku.

**Monitoring.** Z innej maszyny niż serwer aplikacji, bo monitor na tym samym serwerze zamilknie razem z nim. `scripts/monitor.py` odpytuje osobno `/health` (API) i `/health/db` (połączenie z bazą) i wysyła mail przy awarii i przy powrocie, nigdy pomiędzy:

```bash
# crontab na maszynie monitorującej, co 5 minut
*/5 * * * * MONITOR_SMTP_HOST=... MONITOR_FROM=... MONITOR_TO=dyzur@... python3 /opt/ocwip/monitor.py https://<domena>/api
```

Zamiast skryptu wystarczy Uptime Kuma na innym serwerze albo darmowy monitor zewnętrzny z dwoma sondami HTTP, każda z alertem mailem.

**Wynik próby.** 2026-09-29, na stosie lokalnym z Mailpitem: przy działającym stosie brak maila. Po zatrzymaniu bazy `/health` dalej odpowiadał, a `/health/db` zwrócił 503: przyszedł dokładnie jeden mail "ALARM OCWIP: /health/db nie odpowiada", także po drugim przebiegu. Po starcie bazy przyszedł mail o powrocie. Próba na stagingu czeka na T-48 i T-117.

## Kopie zapasowe (T-114)

Usługa `backup` compose produkcyjnego robi co noc (`BACKUP_SCHEDULE`, domyślnie 2:00 czasu polskiego) zrzut bazy `pg_dump -Fc` oraz kopię załączników i kluczy DataProtection do repozytorium **restic**. Restic szyfruje po stronie serwera aplikacji, więc magazyn nie widzi PESEL-i ani załączników w jawnej postaci.

**Magazyn.** Poza serwerem, w UE. Na staging Hetzner Storage Box BX11 albo Backblaze B2 EU; wybór dla produkcji zapada razem z hostingiem (PK-C). Serwer dostaje klucz, który może tylko **dopisywać**, więc przejęty serwer nie skasuje kopii.

**Retencja.** 7 kopii dziennych, 4 tygodniowe, 12 miesięcznych i 6 rocznych, czyli okres retencji danych (5 lat) z zapasem. Usuwanie starych kopii wymaga klucza, który może kasować, więc nie działa na serwerze. Raz w miesiącu, z zaufanej maszyny z pełnym kluczem magazynu:

```bash
docker run --rm --entrypoint restic -e RESTIC_REPOSITORY=... -e RESTIC_PASSWORD=... -e AWS_ACCESS_KEY_ID=... -e AWS_SECRET_ACCESS_KEY=... \
  ocwip-backup:prod forget --host ocwip --tag nightly --prune --keep-daily 7 --keep-weekly 4 --keep-monthly 12 --keep-yearly 6
```

Ręczna kopia, na przykład przed aktualizacją: `docker compose -f docker-compose.prod.yml --env-file .env.prod exec backup backup.sh`.

**Hasło restic** jest tak samo ważne jak klucz szyfrowania pól: bez niego kopie są bezużyteczne. Kopia hasła poza serwerem, razem z `FIELD_ENCRYPTION_KEY`, osobno od kopii.

**Odtworzenie na pustą maszynę.** Repozytorium z gita, `.env.prod` z tymi samymi sekretami co serwer, który zrobił kopię, i żadnych woluminów:

```bash
scripts/restore.sh            # albo scripts/restore.sh <id migawki>
```

Skrypt stawia pustą bazę z rolami, odmawia, jeśli baza ma już tabele, odtwarza pliki i bazę, uruchamia całość i podaje czas.

**Wynik próby.** 2026-09-29, lokalnie na compose produkcyjnym (Podman, obrazy już zbudowane), na małej bazie: odtworzenie trwało **62 s**; baza (28 tabel, 31 migracji) i pliki zgodne sumą kontrolną z danymi sprzed usunięcia woluminów, smoke test przez Caddy przechodzi. Repozytorium ze złym hasłem odmawia (`wrong password`), a w jego plikach nie ma sygnatury zrzutu ani nazw ról. W CI zadanie `backup` powtarza to przy każdym PR na danych ze scenariusza przeglądarki i po odtworzeniu loguje się jako wnioskodawca, pobiera załącznik, PDF wniosku i umowę. Próba na docelowym magazynie czeka na staging (T-48).

## Baza: dwie role i migracje (T-113)

API nie ma praw do zmiany schematu. Migracje uruchamia osobny obraz, osobną rolą.

1. **Role**, raz, jako właściciel bazy (na zarządzanym PostgreSQL: rola administracyjna dostawcy):

   ```sql
   CREATE ROLE ocwip_migrator LOGIN PASSWORD '<sekret>';
   CREATE ROLE ocwip_app LOGIN PASSWORD '<inny sekret>';
   GRANT USAGE, CREATE ON SCHEMA public TO ocwip_migrator;
   REVOKE CREATE ON SCHEMA public FROM PUBLIC;
   ```

   Ostatnia linia jest potrzebna na PostgreSQL 14 i starszym, gdzie każda rola może domyślnie tworzyć tabele w `public`; od wersji 15 nic nie zmienia.

   Role muszą istnieć przed pierwszą migracją: to ona nadaje `ocwip_app` prawa do wierszy i prawa domyślne na kolejne tabele.
2. **Migracje** przed każdym startem nowej wersji, obrazem z celu `migrate`:

   ```bash
   docker build -f backend/Dockerfile.prod --target migrate -t ocwip-migrate backend
   docker run --rm -e "ConnectionStrings__Postgres=Host=<baza>;Database=ocwip;Username=ocwip_migrator;Password=<sekret>" ocwip-migrate
   ```

   Compose produkcyjne (T-111) robi z tego usługę `migrate` uruchamianą przed API.
3. **API** łączy się jako `ocwip_app`, z `Database__MigrateOnStartup` wyłączonym (domyślnie poza Development).
4. **Klucze sesji** leżą w `/data/keys` obrazu API. Zamontuj tam wolumen, inaczej każdy nowy kontener wyloguje wszystkich i unieważni linki z maili konta.

## Klucz szyfrowania (T-47a)

Adresy, telefony, e-maile, konta, reprezentanci, PESEL-e, wartości umów i odpowiedzi pól oznaczonych jako dane osobowe są w bazie zaszyfrowane kluczem spoza bazy ([`architektura.md`](architektura.md), "Dane wrażliwe").

**Utrata klucza oznacza utratę tych danych. Kopia bazy bez klucza jest bezużyteczna, i o to chodzi.**

1. **Wygeneruj klucz** raz, na zaufanej maszynie: `openssl rand -base64 32`.
2. **Podaj go API** jako `FieldEncryption__Keys__1` (w `.env` produkcyjnym `FIELD_ENCRYPTION_KEY`, T-111). Bez niego `Production` nie wystartuje. Klucz ma być osobny dla stagingu i dla produkcji.
3. **Zrób kopię poza serwerem**, osobno od kopii bazy: w menedżerze haseł zespołu i na wydruku w sejfie OCWIP. Kopia bazy i klucz w jednym miejscu to to samo, co brak szyfrowania.
4. **Baza z danymi sprzed T-47a:** po wdrożeniu uruchom raz

   ```bash
   docker compose exec backend dotnet Ocwip.Api.dll reencrypt-data
   ```

   Komenda przepisuje każdą wartość wrażliwą bieżącym kluczem i nie zmienia dat "dane zaktualizowane". Na pustej bazie nie jest potrzebna.

**Rotacja klucza.**
1. Wygeneruj nowy klucz i dodaj go obok starego jako `FieldEncryption__Keys__2`. Stary zostaje jako `__1`.
2. Zrestartuj API. Nowe zapisy idą kluczem 2, a stare dane nadal się czytają.
3. Uruchom `reencrypt-data`.
4. Dopiero gdy komenda skończy się sukcesem, a kopia zapasowa zrobiona po niej jest sprawdzona, usuń klucz 1 z konfiguracji. Kopie sprzed rotacji dalej potrzebują klucza 1, więc jego kopia poza serwerem zostaje tak długo jak one.

**Kto czytał dane osobowe.** Każdy udany odczyt wniosku, jego PDF-u, załącznika, umowy i sprawozdania zostawia wiersz w tabeli `personal_data_reads`: konto, zasób, trasa, czas. Odpowiedź na pytanie osoby "kto widział moje dane" to zapytanie do tej tabeli.

## Pierwszy konkurs na pustej bazie (T-96)

Na świeżej instalacji konkurs nie ma formularza ani kart oceny, a bez nich nie da się go opublikować (T-97). Kolejność:

1. **Konto operatora.** Pracownik OCWIP zakłada konto na stronie rejestracji i potwierdza adres z maila.
2. **Rola operatora.** Z powłoki serwera, w kontenerze API:

   ```bash
   docker compose -f docker-compose.prod.yml --env-file .env.prod exec backend dotnet Ocwip.Api.dll grant-role --email adres@ocwip.pl --role Operator
   ```

   Rola nigdy nie jest nadawana przez HTTP ([`architektura.md`](architektura.md), "Rola operatora nadawana komendą").
3. **Konkurs.** Operator zakłada konkurs kreatorem w panelu i zapisuje szkic. Identyfikator konkursu jest w adresie strony konkursu: `/panel/operator/competitions/<id>`.
4. **Treść startowa.** Formularz wniosku, obie karty oceny, wzór sprawozdania i wzór umowy z plików, które obraz API ma w `/app/seed`:

   ```bash
   docker compose -f docker-compose.prod.yml --env-file .env.prod exec backend dotnet Ocwip.Api.dll import-content \
     --competition <id> \
     --application seed/forms/application-2026.json \
     --formal seed/evaluation-cards/formal-2026.json \
     --merit seed/evaluation-cards/merit-2026.json \
     --report seed/forms/report-2026.json \
     --contract seed/templates/contract-2026.txt
   ```

   Każdy plik przechodzi najpierw bramkę kontraktu formularza. Jeśli któryś nie przechodzi, komenda wypisuje powody i niczego nie publikuje. Plik identyczny z wersją w mocy niczego nie zmienia, więc komendę można powtórzyć. Wzór umowy przechodzi to samo sprawdzenie znaczników co ekran operatora. Numer i datę umowy z NIW (§ 1 ust. 1) operator może wpisać wprost we wzór konkursu i opublikować nową wersję, zamiast wpisywać je przy każdej umowie (ZR-17).
5. **Publikacja.** Operator sprawdza na stronie konkursu, że lista braków jest pusta, i publikuje.

Następny konkurs zaczyna się zwykle od poprzedniego: na stronie konkursu sekcja "Karty oceny i wzór sprawozdania" kopiuje wersje w mocy z wybranego konkursu, a formularz wniosku kopiuje kreator formularza.

Lokalnie, na stosie deweloperskim, ta sama komenda idzie przez `dotnet run`:

```bash
docker compose exec backend dotnet run --project src/Ocwip.Api/Ocwip.Api.csproj --no-launch-profile \
  -- import-content --competition <id> --application seed/forms/application-2026.json
```
