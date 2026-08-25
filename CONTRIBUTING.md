# Jak pracujemy

Najpierw przeczytaj [`AGENTS.md`](AGENTS.md), to konstytucja projektu i drogowskaz do reszty dokumentacji. Ten plik opisuje mechanikę: gałęzie, commity, review.

---

## Język

- **Kod: angielski.** Identyfikatory, komentarze, nazwy plików źródłowych, komunikaty commitów, ścieżki API, kolumny w bazie.
- **Dokumentacja i UI: polski.** Wszystko w `docs/`, teksty w interfejsie, etykiety, dane testowe widoczne w produkcie.
- **Czat i rozmowa: cokolwiek wam pasuje.** Nie zmienia to dwóch reguł powyżej.

Bez mieszania w obrębie jednego artefaktu. Polski identyfikator w kodzie albo angielska etykieta w polskim UI wracają z review.

Nazwy domenowe biorą się ze [`docs/slownik.md`](docs/slownik.md). To nie jest sugestia: zamawiający opisuje system językiem Witkaca, a nasze własne słownictwo oznacza tłumaczenie pojęć na każdym spotkaniu.

---

## Pierwsze uruchomienie

```bash
cp .env.example .env
git config core.hooksPath .githooks
docker compose up --build
```

Adresy są w [`README.md`](README.md). Wszystko chodzi przez Dockera. Lokalna instalacja .NET SDK czy Node nie jest wspierana i rozjedzie się z tym, co widzą pozostali.

Linia z `core.hooksPath` włącza hook pre-commit, który blokuje commit, gdy `docs/map/` rozjeżdża się z repozytorium albo gdy wśliznął się myślnik typograficzny. Git nie współdzieli hooków, więc robi się to raz na każdy klon. Zrób to, albo oba checki stoją na twoim honorze.

**Porty są zajęte?** Nie zabijaj cudzych kontenerów. Nadpisz porty w swoim `.env`:

```bash
POSTGRES_PORT=55432
BACKEND_PORT=8180
FRONTEND_PORT=3100
```

---

## Codzienne komendy

```bash
docker compose up -d              # start w tle
docker compose logs -f backend    # podglądanie logów jednej usługi
docker compose restart backend    # po zmianie konfiguracji
docker compose down               # stop, dane przeżywają
docker compose down -v            # stop i skasowanie wolumenu bazy
docker compose exec db psql -U ocwip -d ocwip   # konsola psql
```

Przebudowy, bo restart nie wystarczy, gdy zmieniają się zależności:

```bash
docker compose up -d --build backend                          # zmiana pakietów NuGet
docker compose up -d --build --renew-anon-volumes frontend    # zmiana package.json
```

Flaga `--renew-anon-volumes` ma znaczenie: `node_modules` żyje w anonimowym wolumenie, który przeżywa zwykłą przebudowę, więc bez niej kontener trzyma stare pakiety i debugujesz ducha.

`db/init/*.sql` uruchamia się **tylko** na pustym wolumenie. Po zmianie zrób `docker compose down -v` i wystartuj ponownie, inaczej zmiana pozornie nic nie robi. Tabele aplikacji tworzą migracje EF przy starcie backendu, nie ten katalog.

### Migracje (EF Core)

Schemat odtwarza się od zera po resecie wolumenu: `docker compose down -v && docker compose up --build`. API samo aplikuje oczekujące migracje.

Każda migracja ma działający `Down()` albo w `Down()` rzuca z komentarzem, dlaczego cofnięcie jest niemożliwe (utrata danych, brak odwrócenia DROP kolumny z PESEL-ami itd.). Pusty `Down()` bez komentarza jest błędem, chyba że `Up()` też nic nie robi.

```bash
# Nowa migracja (po zmianie AppDbContext / encji)
docker compose exec backend dotnet ef migrations add ShortName \
  --project src/Ocwip.Api/Ocwip.Api.csproj \
  --output-dir Data/Migrations

# Ręczne zastosowanie (zwykle zbędne: start API robi to sam)
docker compose exec backend dotnet ef database update \
  --project src/Ocwip.Api/Ocwip.Api.csproj

# Cofnięcie schematu do poprzedniej migracji (wymaga działającego Down)
docker compose exec backend dotnet ef database update PreviousMigrationName \
  --project src/Ocwip.Api/Ocwip.Api.csproj

# Cofnięcie ostatniej migracji z dysku, zanim trafi do gita
docker compose exec backend dotnet ef migrations remove \
  --project src/Ocwip.Api/Ocwip.Api.csproj
```

Zmieniłeś `package.json`? Wygeneruj lockfile na nowo, bo obraz i CI używają `npm ci`:

```bash
docker compose exec frontend npm install --package-lock-only
```

Testy: [`docs/testy.md`](docs/testy.md). Wszystkie chodzą w CI przy każdym pull requeście.

---

## Gałęzie

Dwie gałęzie żyją na stałe. Do żadnej nie commitujemy bezpośrednio.

| Gałąź | Rola | Co tu ląduje |
|---|---|---|
| `main` | Produkcja. Zawsze wdrażalna. | Merge release z `dev` albo `hotfix/`. Nic więcej. |
| `dev` | Integracja. Gałąź domyślna. | Każda skończona gałąź funkcjonalna. |

Wszystko inne żyje krótko i jest **odbijane od `dev`**:

```
feat/<short>       nowa funkcjonalność
fix/<short>        poprawka błędu
refactor/<short>   zmiana zachowująca zachowanie
chore/<short>      narzędzia, konfiguracja, zależności
docs/<short>       wyłącznie dokumentacja
```

Krótko, małymi literami, z dywizami: `feat/competition-endpoint`, `fix/cors-origins`.

### Codzienny rytm

```bash
git checkout dev && git pull
git checkout -b feat/competition-endpoint
# praca, commity, push
git push -u origin feat/competition-endpoint
# otwórz pull request do dev
```

### Wydanie

Pull request z `dev` do `main`, mergowany z `--no-ff`. Jeden commit na `main` na wydanie, żeby `git log main` czytało się jak historia wydań, a nie jak strumień pojedynczych zmian.

### Hotfixy

Błąd na produkcji, który nie może czekać do następnego wydania:

```bash
git checkout main && git pull
git checkout -b hotfix/broken-health-probe
# poprawka, commit, PR do main
```

Po zmergowaniu do `main` **od razu zmerguj `main` z powrotem do `dev`.** Pominięcie tego kroku sprawia, że następne wydanie po cichu cofa poprawkę. To najczęstszy sposób, w jaki psuje się model dwóch gałęzi.

---

## Commity

- Jeden commit = jedna logiczna zmiana. Commituj często, małymi krokami.
- Tytuł: `type: krótkie, konkretne podsumowanie`, do około 60 znaków, tryb rozkazujący, po angielsku, bez kropki na końcu.
- Typy: `feat`, `fix`, `refactor`, `docs`, `style`, `test`, `chore`, `perf`.
- **Bez opisu**, chyba że zmiana jest duża, ważna funkcjonalnie albo nieoczywista, wtedy 2-4 punkty.

```
feat: competition publishing endpoint
fix: cors origins parsing
refactor: extract form definition loader
chore: bump npgsql to 9.0.3
```

### Reguła twarda: zero autorstwa narzędzi i AI

Nic w tym repozytorium nie może wspominać ani przypisywać autorstwa asystentowi AI czy narzędziu generującemu kod: ani w commitach, ani w pull requestach, kodzie, komentarzach czy dokumentacji. Żadnego `Co-Authored-By`, żadnego "generated with". `.claude/settings.json` ustawia `includeCoAuthoredBy: false`, zostaw to tak.

---

## Zanim otworzysz pull request

1. Zmiana działa, czyli uruchomiłeś stack i jej użyłeś, a nie tylko przeczytałeś diff. `docker compose exec backend dotnet test` i `docker compose exec frontend npm test` przechodzą, a nowe zachowanie ma test. Zobacz [`docs/testy.md`](docs/testy.md).
2. `python scripts/check_map.py` kończy się zerem, czego pilnuje hook pre-commit. Dodałeś, przeniosłeś, zmieniłeś nazwę albo skasowałeś plik, więc jego wiersz w `docs/map/` zmienił się w tym samym commicie. Dodałeś zupełnie nowy katalog najwyższego poziomu, więc naucz o nim `AREAS` i `KNOWN_TOP_LEVEL` w skrypcie i załóż mu własny plik w `docs/map/`.
3. Pliki w `docs/`, których zmiana dotyczy, są zaktualizowane. Nieaktualna dokumentacja jest gorsza niż jej brak.
4. `.env.example` zawiera każdą nową zmienną środowiskową (z bezpiecznym placeholderem, nigdy prawdziwym sekretem).
5. Większe zadanie, więc jeden wpis na górze [`docs/log.md`](docs/log.md), w formacie opisanym w tym pliku.
6. Karta na Trello ma odhaczoną checklistę kryteriów akceptacji.

Pull request opisuje **co się zmieniło i dlaczego**, w kilku zdaniach. CI chodzi przy każdym pull requeście: checki repozytorium, testy backendu przeciwko prawdziwemu PostgreSQL, typecheck z testami i buildem frontu oraz smoke test startujący wszystkie trzy kontenery i rozmawiający z nimi po HTTP. Czerwony pipeline oznacza, że gałąź się nie merguje.

Używamy **merge commit**, nie squash ani rebase. Wydanie ma być jednym rozpoznawalnym commitem na `main`, a squash spłaszczyłby w nie całą historię.

---

## Do ustawienia na GitHubie

To są ustawienia repozytorium, nie pliki, więc nikt ich nie zacommituje. Do zrobienia raz, przez właściciela:

| | `main` | `dev` |
|---|---|---|
| Cztery zadania CI muszą przejść | tak | tak |
| Wymagany pull request | tak | nie |
| Gałąź musi być aktualna przed mergem | tak | nie |
| Force push i usunięcie | zablokowane | zablokowane |
| Reguły obowiązują też adminów | **tak** | nie |

Gałąź domyślna: `dev`. Dopóki ochrona gałęzi nie jest ustawiona, CI jest tylko sugestią.

---

## Dyscyplina zakresu

To repozytorium jest celowo szkieletem. Nie dodawaj encji domenowych, uwierzytelniania, kreatora formularzy ani modułu oceny bez wzięcia odpowiedniej karty z Trello. Lista świadomych cięć jest w [`docs/zakres.md`](docs/zakres.md) i istnieje po to, żeby projekt nie rozrósł się w pełny system grantowy.

Nie zgadujemy w modelu danych. Brakuje dokumentu od zamawiającego? Karta idzie do listy "Zablokowane: czeka na klienta" z komentarzem, czego konkretnie brakuje.

---

## Sekrety i dane osobowe

Nigdy nie commituj `.env`, kluczy, zrzutów bazy ani danych osobowych. `.env.example` dokumentuje *nazwy* zmiennych i nic więcej.

System przetwarza dane organizacji i osób fizycznych, a przy umowach pojawiają się PESEL-e. W razie wątpliwości zbieraj mniej, loguj mniej i pokazuj mniej.
