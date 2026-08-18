# OCWIP - generator konkursów dotacyjnych

Webowa platforma obsługująca pełny cykl życia konkursu dotacyjnego dla Opolskiego Centrum Wspierania Inicjatyw Pozarządowych: ogłoszenie konkursu, składanie wniosków, ocena, decyzja i umowa, sprawozdanie, archiwum.

OCWIP występuje tu po stronie **organizatora** konkursu, nie wnioskodawcy.

**Stan: szkielet.** Trzy kontenery, health endpointy, testy, CI. Zero encji domenowych, zero logowania, zero kreatora formularzy. Co świadomie nie wchodzi do MVP i dlaczego: [`docs/zakres.md`](docs/zakres.md).

## Uruchomienie

Potrzebny wyłącznie Docker z Compose v2. Nic więcej: .NET SDK i Node żyją w kontenerach.

```bash
cp .env.example .env
git config core.hooksPath .githooks
docker compose up --build
```

W PowerShellu pierwsza linia to `Copy-Item .env.example .env`. Druga włącza hook pre-commit i trzeba ją wykonać raz na każdy klon repozytorium.

| | | |
|---|---|---|
| Frontend | Next.js 15, Tailwind CSS | <http://localhost:3000> |
| Backend | .NET 10, minimal API | <http://localhost:8080> ([health](http://localhost:8080/health), [health/db](http://localhost:8080/health/db), [openapi](http://localhost:8080/openapi/v1.json)) |
| Baza | PostgreSQL 16 | `localhost:5432` |

Obie aplikacje przeładowują się po zmianie pliku na hoście. Codzienne komendy i pułapki: [`CONTRIBUTING.md`](CONTRIBUTING.md).

Sprawdzenie, że stack naprawdę wstał:

```bash
python scripts/smoke_test.py
```

## Gdzie co jest

| Potrzebuję | Czytaj |
|---|---|
| Jak pracujemy i jakie są reguły | [`AGENTS.md`](AGENTS.md) |
| Kto jest klientem i po co to robimy | [`docs/kontekst-projektu.md`](docs/kontekst-projektu.md) |
| Słownik pojęć używanych przez zamawiającego | [`docs/slownik.md`](docs/slownik.md) |
| Role i twarde reguły biznesowe | [`docs/reguly-biznesowe.md`](docs/reguly-biznesowe.md) |
| Zakres MVP i świadome cięcia | [`docs/zakres.md`](docs/zakres.md) |
| Który plik co robi | [`docs/map/`](docs/map/README.md) |
| Jak system trzyma się do kupy | [`docs/architektura.md`](docs/architektura.md) |
| Model danych i jawne założenia | [`docs/model-danych.md`](docs/model-danych.md) |
| Nazewnictwo, struktura, styl | [`docs/konwencje.md`](docs/konwencje.md) |
| Uruchamianie i pisanie testów, CI | [`docs/testy.md`](docs/testy.md) |
| Gałęzie, commity, pull requesty | [`CONTRIBUTING.md`](CONTRIBUTING.md) |
| Co się ostatnio zmieniło i dlaczego | [`docs/log.md`](docs/log.md) |

`CLAUDE.md` i `.cursor/rules/` wskazują na `AGENTS.md`, więc każde narzędzie agentowe pracuje na jednym zestawie reguł.

## Zadania

Tablica Trello: <https://trello.com/b/nP2dbEcK/ocwip-generator-konkursow>

Karty niosą kontekst w tytule: `T-XX.Y [PRIORYTET / OBSZAR] Nazwa`. Nie bierzemy karty, której zależności nie są zamknięte. Brakuje informacji od zamawiającego? Karta idzie do listy "Zablokowane: czeka na klienta", a nie do zgadywania.
