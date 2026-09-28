# Wdrożenie

Jak postawić system na serwerze i przygotować pierwszy konkurs. Plik zakłada T-96 (treść startowa); compose produkcyjne, proxy, kopie zapasowe i procedurę wydania dokładają T-111 i dalsze zadania z [`runbook/plan-v1.md`](runbook/plan-v1.md), etap 2.

Obrazy produkcyjne opisuje [`map/infra.md`](map/infra.md) (`backend/Dockerfile.prod`, `frontend/Dockerfile.prod`, T-110).

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
   docker compose exec backend dotnet Ocwip.Api.dll grant-role --email adres@ocwip.pl --role Operator
   ```

   Rola nigdy nie jest nadawana przez HTTP ([`architektura.md`](architektura.md), "Rola operatora nadawana komendą").
3. **Konkurs.** Operator zakłada konkurs kreatorem w panelu i zapisuje szkic. Identyfikator konkursu jest w adresie strony konkursu: `/panel/operator/competitions/<id>`.
4. **Treść startowa.** Formularz wniosku i obie karty oceny z plików, które obraz API ma w `/app/seed`:

   ```bash
   docker compose exec backend dotnet Ocwip.Api.dll import-content \
     --competition <id> \
     --application seed/forms/application-2026.json \
     --formal seed/evaluation-cards/formal-2026.json \
     --merit seed/evaluation-cards/merit-2026.json
   ```

   Każdy plik przechodzi najpierw bramkę kontraktu formularza. Jeśli któryś nie przechodzi, komenda wypisuje powody i niczego nie publikuje. Plik identyczny z wersją w mocy niczego nie zmienia, więc komendę można powtórzyć. Wzór sprawozdania (`--report`) dojdzie z T-95; do tego czasu operator kopiuje go na stronie konkursu z innego konkursu albo publikuje później.
5. **Publikacja.** Operator sprawdza na stronie konkursu, że lista braków jest pusta, i publikuje.

Następny konkurs zaczyna się zwykle od poprzedniego: na stronie konkursu sekcja "Karty oceny i wzór sprawozdania" kopiuje wersje w mocy z wybranego konkursu, a formularz wniosku kopiuje kreator formularza.

Lokalnie, na stosie deweloperskim, ta sama komenda idzie przez `dotnet run`:

```bash
docker compose exec backend dotnet run --project src/Ocwip.Api/Ocwip.Api.csproj --no-launch-profile \
  -- import-content --competition <id> --application seed/forms/application-2026.json
```
