# Mapa: infrastruktura

Docker, bootstrap bazy, skrypty jakości, CI, zmienne środowiskowe.

| Plik | Co robi |
|---|---|
| `docker-compose.yml` | Trzy usługi: `db` (postgres:16-alpine, healthcheck, wolumen `postgres-data`), `backend` (.NET, `dotnet watch`, port 8080), `frontend` (Next.js dev, port 3000). Wolumeny nazwane `backend-obj` i `backend-bin` chronią build kontenera przed hostowym `bin/obj` |
| `.env.example` | Nazwy wszystkich zmiennych środowiskowych z bezpiecznymi placeholderami. Nigdy nie trafia tu prawdziwy sekret |
| `.editorconfig` | Kodowanie, końce linii, wcięcia. 4 spacje w C#, 2 w reszcie |
| `backend/Dockerfile` | Obraz deweloperski na `mcr.microsoft.com/dotnet/sdk:10.0`. Restore w osobnej warstwie, żeby edycja kodu nie unieważniała cache pakietów |
| `frontend/Dockerfile` | Obraz deweloperski na `node:22-alpine`. `npm install` w osobnej warstwie z tego samego powodu |
| `db/init/001_extensions.sql` | Rozszerzenia `pgcrypto` i `unaccent` oraz ustawienie strefy czasowej bazy na UTC. Uruchamia się **tylko na pustym wolumenie** |
| `scripts/check_map.py` | Wykrywa rozjazd między repozytorium a `docs/map/`: pliki niezmapowane, wiersze wskazujące na nieistniejące pliki, wiersze w złym obszarze, nieznane katalogi najwyższego poziomu. `AREAS` i `KNOWN_TOP_LEVEL` to miejsca do rozszerzania |
| `scripts/check_text.py` | Blokuje myślniki typograficzne (em dash, en dash, kreska pozioma) w całym repozytorium. Znaki trzyma jako `\u` escape, żeby nie wywracać się na własnym źródle |
| `scripts/smoke_test.py` | Rozmawia z działającym stackiem po HTTP: `/health`, `/health/db`, strona startowa frontu. Łapie awarię, której testy jednostkowe nie złapią |
| `.githooks/pre-commit` | Uruchamia oba checki przed commitem. Włączany raz na klon: `git config core.hooksPath .githooks` |
| `.github/workflows/ci.yml` | Cztery zadania na każdym pull requeście: checks, backend przeciwko prawdziwemu PostgreSQL, frontend (typecheck, testy, build), smoke test na trzech kontenerach |
