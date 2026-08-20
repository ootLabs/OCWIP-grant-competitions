# Testy

Wszystko chodzi w kontenerach. Lokalna instalacja .NET SDK ani Node nie jest wspierana.

## Uruchamianie

```bash
docker compose exec backend dotnet test
docker compose exec frontend npm test
docker compose exec frontend npm run typecheck
python scripts/smoke_test.py
```

Cztery komendy, cztery różne rzeczy. Kolejność ma znaczenie tylko przy ostatniej: smoke test wymaga wstającego stacku (`docker compose up -d`).

## Warstwy

| Warstwa | Gdzie | Co sprawdza |
|---|---|---|
| Testy backendu | `backend/tests/Ocwip.Api.Tests/` | Endpointy uruchomione w pamięci przez `WebApplicationFactory`, na prawdziwej aplikacji, nie na jej kopii; migracje na czystej bazie (`MigrationTests`) |
| Testy frontu | `frontend/**/*.test.ts(x)` | Vitest plus jsdom: logika klienta API i komponenty |
| Typecheck | `frontend` | `tsc --noEmit`, bo błąd typu nie jest błędem stylu |
| Smoke test | `scripts/smoke_test.py` | Trzy kontenery naprawdę się widzą: API odpowiada, dosięga bazy, front się renderuje |

Smoke test łapie awarię, której żaden test jednostkowy nie złapie: wszystko działa osobno, a stack nie wstaje.

## Co musi mieć test

**Obowiązkowo, bez wyjątku:**

1. **Testy negatywne uprawnień.** Wnioskodawca podmienia identyfikator w adresie na cudzy wniosek i próbuje go pobrać. To najczęstszy błąd w aplikacjach tego typu i najłatwiejszy do przeoczenia, bo w interfejsie nie prowadzi do niego żaden link, więc przy ręcznym klikaniu nikt tego nie znajdzie. Te testy blokują merge.
2. **Odcięcie po terminie.** Nabór zamyka się co do minuty. Test na granicy, nie "gdzieś po terminie".
3. **Ścieżka uwierzytelniania end to end**, jeden scenariusz: rejestracja, weryfikacja adresu, logowanie, wylogowanie, reset hasła, ponowne logowanie nowym hasłem. Jeden test, szybki, bo będzie chodził przy każdej zmianie. Pojedyncze przypadki są pokryte gdzie indziej, tutaj sprawdzamy tylko, czy elementy są ze sobą poprawnie połączone.

**Zasady:**

- Nowe zachowanie ma test. Poprawka błędu ma test, który bez poprawki nie przechodzi.
- Testy chodzą na czystej bazie. Test, który przechodzi tylko na bazie z ręcznie przygotowanym stanem, przestanie działać po pierwszej zmianie schematu i zostanie wyłączony przez kogoś, komu będzie się spieszyć.
- Testy integracyjne pomijają się (skip), a nie wywracają, gdy nie ma bazy. Zestaw ma być użyteczny bez uruchomionego stacku, a CI i tak zawsze daje prawdziwego PostgreSQL.
- Nie logujemy w testach haseł ani danych wrażliwych, tak samo jak w kodzie produkcyjnym.

## CI

`.github/workflows/ci.yml` chodzi przy każdym pull requeście i przy pushu do `main` oraz `dev`. Trzy zadania:

1. **checks** - `check_map.py` i `check_text.py`.
2. **backend** - `dotnet test` przeciwko prawdziwemu PostgreSQL w usłudze kontenerowej.
3. **frontend** - `npm ci`, typecheck, testy i build.
4. **smoke** - startuje wszystkie trzy kontenery i rozmawia z nimi po HTTP. Przy porażce wypisuje logi kontenerów.

Czerwony pipeline oznacza, że gałąź się nie merguje.

Uwaga na przyszłość: usługi kontenerowe w GitHub Actions nie uruchamiają `db/init/`, więc CI aplikuje ten katalog osobno przez psql. Przeniesienie bootstrapu bazy gdzie indziej wymaga zmiany w workflow.
