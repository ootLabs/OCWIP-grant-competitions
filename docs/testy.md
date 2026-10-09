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
| Testy frontu | `frontend/**/*.test.ts(x)` | Vitest plus jsdom: logika klienta API i komponenty. Każdy test z renderem jest też testem dostępności, patrz niżej |
| Typecheck | `frontend` | `tsc --noEmit`, bo błąd typu nie jest błędem stylu |
| Smoke test | `scripts/smoke_test.py` | Trzy kontenery naprawdę się widzą: API odpowiada, dosięga bazy, front się renderuje |
| Przejście ręczne | [`przejscie-gui.md`](przejscie-gui.md) | Człowiek klika cały cykl konkursu w przeglądarce przed wystawieniem na serwer; wyniki każdego przebiegu w [`przejscie-gui-bledy.md`](przejscie-gui-bledy.md) |

Smoke test łapie awarię, której żaden test jednostkowy nie złapie: wszystko działa osobno, a stack nie wstaje.

## Co musi mieć test

**Obowiązkowo, bez wyjątku:**

1. **Testy negatywne uprawnień.** Wnioskodawca podmienia identyfikator w adresie na cudzy wniosek i próbuje go pobrać. To najczęstszy błąd w aplikacjach tego typu i najłatwiejszy do przeoczenia, bo w interfejsie nie prowadzi do niego żaden link, więc przy ręcznym klikaniu nikt tego nie znajdzie. Te testy blokują merge. Do tego `AnonymousRouteSweepTests` przechodzi po każdej trasie aplikacji: nowa trasa publiczna oblewa test, dopóki nie trafi z powodem na listę w teście, a każda inna musi odpowiedzieć 401 bez sesji.
2. **Odcięcie po terminie.** Nabór zamyka się co do minuty. Test na granicy, nie "gdzieś po terminie".
3. **Ścieżka uwierzytelniania end to end**, jeden scenariusz: rejestracja, weryfikacja adresu, logowanie, wylogowanie, reset hasła, ponowne logowanie nowym hasłem. Jeden test, szybki, bo będzie chodził przy każdej zmianie. Pojedyncze przypadki są pokryte gdzie indziej, tutaj sprawdzamy tylko, czy elementy są ze sobą poprawnie połączone.

4. **Dostępność (WCAG 2.1 AA, T-46).** Nie piszesz jej osobno, dostajesz ją za darmo: `frontend/vitest.setup.ts` po każdym teście puszcza `axe-core` na tym, co ekran pokazał, i test z naruszeniem (pole bez nazwy, przeskok w nagłówkach, nieprawidłowe ARIA) oblewa się z listą naruszeń. Kontrastu jsdom nie policzy, więc pilnuje go `app/contrast-tokens.test.ts` na tokenach obu palet, a to, czego axe nie widzi (podkreślenie linków, krawędź pól, `outline-none`, dodatni `tabIndex`), `app/accessibility-source.test.ts` w źródłach. Nowy kolor to nowa para w `contrast-tokens.test.ts`. Raport z audytu i sposób powtórzenia go w przeglądarce: [`dostepnosc.md`](dostepnosc.md).

**Zasady:**

- Nowe zachowanie ma test. Poprawka błędu ma test, który bez poprawki nie przechodzi.
- Testy chodzą na czystej bazie. Test, który przechodzi tylko na bazie z ręcznie przygotowanym stanem, przestanie działać po pierwszej zmianie schematu i zostanie wyłączony przez kogoś, komu będzie się spieszyć.
- Testy integracyjne pomijają się (skip), a nie wywracają, gdy nie ma bazy. Zestaw ma być użyteczny bez uruchomionego stacku, a CI i tak zawsze daje prawdziwego PostgreSQL. Skip robi `[RequiresDatabaseFact]`, nie `return` w środku testu: puste `return` daje zielony test, który nic nie sprawdził (xUnit 2 nie ma dynamicznego pomijania).
- Test, który startuje aplikację, idzie przez `OcwipWebApplicationFactory`. Fabryka wyłącza `Database:MigrateOnStartup`, bo inaczej test HTTP robi DDL na wspólnej bazie `ocwip`. Za łańcuch migracji odpowiada `MigrationTests`, na bazie zakładanej na tę jedną próbę. Fabryka zeruje też `Smtp:Host`: kontener deweloperski wskazuje Mailpita, a `dotnet test` w tym kontenerze dziedziczy jego zmienne, więc bez tego przebieg testów gadałby ze skrzynką zamiast zbierać maile w pamięci. Test, który chce przekaźnika, ustawia `Smtp:Host` sam, przez `SessionTestHost`.
- Nie logujemy w testach haseł ani danych wrażliwych, tak samo jak w kodzie produkcyjnym.
- Klucz szyfrowania pól (T-47a) ustawia raz na cały przebieg `Data/TestFieldEncryption.cs`, własny, nie deweloperski. Test, który wkłada wiersz przez obecny model i potem cofa migracje, nie może mieć w nim danych szyfrowanych: `Down` migracji `EncryptSensitiveFields` celowo odmawia szyfrogramu.

## Test w przeglądarce (T-100)

`e2e/` to osobny projekt z Playwrightem. Przechodzi proces tak, jak robią to ludzie, na postawionym stosie: konta przez formularz rejestracji i link z maila, rolę operatora i treść konkursu przez komendy z `docs/wdrozenie.md` (`grant-role`, `import-content`), resztę przez API i ekrany. **Bez SQL z boku**: krok, którego produkt nie umie, jest krokiem, którego test też nie zrobi. Scenariusz sięga od rejestracji przez złożenie dwóch wniosków (T-100), ocenę dwóch ekspertów, zatwierdzenie wyników, maile o wynikach i publiczną listę (T-100a) do umowy, rezygnacji dofinansowanego i przejścia środków na wniosek z listy rezerwowej z podpisaną umową (T-100b). Każdy etap to osobny plik w `e2e/steps/`.

```bash
RATE_LIMIT_PERMIT_LIMIT=200 docker compose up -d
cd e2e && npm ci && npx playwright install chromium && npx playwright test
```

Mailpit stoi w zwykłym stosie i backend domyślnie do niego wysyła, więc poczty nie trzeba już nigdzie wskazywać. Limit żądań jest podniesiony, bo scenariusz rejestruje i loguje kilka osób z jednego adresu.

Od T-112 każda przeglądarka scenariusza zbiera naruszenia CSP z konsoli i scenariusz nie przechodzi przy żadnym. Osobny `e2e/tests/public-pages.spec.ts` sprawdza strony publiczne bez przygotowania danych, więc zadanie `production` w CI puszcza go przez Caddy na compose produkcyjnym, gdzie polityka jest najostrzejsza (`E2E_BASE_URL`, `E2E_HOST_RULES`, `E2E_IGNORE_HTTPS_ERRORS`).

Maszyna, która nie pobierze Chromium, uruchamia zainstalowanego Chrome: `E2E_BROWSER_CHANNEL=chrome npx playwright test`. Adresy stosu zmieniają `E2E_BASE_URL`, `E2E_API_URL` i `E2E_MAILPIT_URL`. Każdy przebieg ma własny przyrostek w adresach e-mail i numerze konkursu, więc scenariusz działa też na bazie z danymi; w CI zawsze na pustej. Konkurs startuje godzinę przed testem i kończy się za tydzień, więc test nigdy nie czeka na zegar.

## CI

`.github/workflows/ci.yml` chodzi przy każdym pull requeście i przy pushu do `main` oraz `dev`. Sześć zadań:

1. **checks** - `check_map.py` i `check_text.py`.
2. **backend** - skan pakietów NuGet (`dotnet list package --vulnerable --include-transitive`), potem `dotnet test` przeciwko prawdziwemu PostgreSQL w usłudze kontenerowej.
3. **frontend** - `npm ci`, `npm audit --omit=dev --audit-level=high`, typecheck, testy i build.
4. **smoke** - startuje wszystkie trzy kontenery i rozmawia z nimi po HTTP. Potem generuje klienta TypeScript z dokumentu OpenAPI i odmawia, gdy `frontend/lib/api-schema.ts` różni się od zacommitowanego (R-32). Przy porażce wypisuje logi kontenerów.
5. **images** - buduje `backend/Dockerfile.prod` i `frontend/Dockerfile.prod` (T-110), sprawdza, że żaden obraz nie działa jako root, i puszcza ten sam smoke test na samych obrazach produkcyjnych, bez deweloperskiego compose. Od T-113 baza ma dwie role: migruje obraz `migrate` rolą `ocwip_migrator`, job sprawdza, że rola API `ocwip_app` nie utworzy ani nie zmieni tabeli, i dopiero wtedy startuje API na `ocwip_app`, z kluczem szyfrowania pól wygenerowanym na ten jeden przebieg (T-47a). Na koniec uruchamia w obrazie komendy kont (`list-accounts`, `grant-role`, `deactivate-account`, T-104), żeby dowieść, że działają bez SDK. Backend w środowisku `Staging`, bo `Production` wymaga publicznego adresu i przekaźnika poczty (T-91), a to już sprawa compose produkcyjnego (T-111).

6. **e2e** - test w przeglądarce (T-100, niżej): stos z profilem `test`, Mailpit, Playwright z Chromium. Idzie równolegle do reszty. Przy porażce zostawia artefakt `e2e-recording` z nagraniem, śladem i zrzutami oraz logi kontenerów.

Czerwony pipeline oznacza, że gałąź się nie merguje.

Skany zależności (T-90) oblewają job przy podatności wysokiej albo krytycznej. `dotnet list package --vulnerable` kończy się zerem także wtedy, gdy coś znajdzie, więc krok liczy znaleziska w jego wyniku JSON. Audyt frontu pomija zależności deweloperskie: dziura w narzędziu testowym nie dociera do serwera, a te łata Dependabot. Znalezisko bez poprawki w gałęzi 15.5.x naprawia się przez `overrides` w `frontend/package.json`, nie przez wyłączenie kroku.

Uwaga na przyszłość: usługi kontenerowe w GitHub Actions nie uruchamiają `db/init/`, więc CI aplikuje ten katalog osobno przez psql. Przeniesienie bootstrapu bazy gdzie indziej wymaga zmiany w workflow.
