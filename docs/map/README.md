# Mapa repozytorium

Mapa odpowiada szybko na jedno pytanie: **który plik otworzyć, żeby zmienić X?** Czytaj mapę obszaru, którego dotykasz, a nie całe repo i nie ślepy grep.

| Obszar | Plik | Obejmuje |
|---|---|---|
| Backend | [`backend.md`](backend.md) | Usługa .NET: endpointy, serwisy, konfiguracja, testy |
| Frontend | [`frontend.md`](frontend.md) | Aplikacja Next.js: strony, komponenty, klient API, style |
| Infrastruktura | [`infra.md`](infra.md) | Docker, bootstrap bazy, skrypty, zmienne środowiskowe, CI |
| Prototyp wizualny | [`prototype.md`](prototype.md) | Samodzielna makieta kierunku C. Nie jest częścią produktu |

Podział na obszary jest celowy: zmiana we froncie nie ma prawa wciągać do kontekstu wierszy backendu.

## Format

Jeden wiersz na plik. Opis mówi, **co plik robi i co w nim siedzi**, czyli symbole, których ktoś będzie szukał, w jednej linii. Nie proza, nie changelog.

```
| `backend/src/Ocwip.Api/Endpoints/HealthEndpoints.cs` | `GET /health` (liveness), `GET /health/db` (sonda PostgreSQL) |
```

Zasady:

- Ścieżka w backtickach, względem repozytorium, ukośniki w przód. `scripts/check_map.py` to parsuje i wywróci się na czymkolwiek innym.
- Jedna linia na plik. Jeśli plik potrzebuje akapitu, robi za dużo. Dziel plik, nie wiersz.
- Grupuj wiersze pod nagłówkiem `###` per katalog, gdy obszar przekroczy około 20 plików.
- Plików generowanych (`node_modules/`, `.next/`, `bin/`, `obj/`, pliki lock) nigdy nie mapujemy.

## Utrzymanie, czyli część, która ma znaczenie

Nieaktualna mapa jest gorsza niż jej brak: wysyła następnego agenta do pliku, który się przeniósł. Dlatego mapa nie stoi na dobrych intencjach, tylko jest sprawdzana:

```bash
python scripts/check_map.py
```

Zgłasza pliki brakujące w mapie, wiersze wskazujące na skasowane pliki, wiersze wpisane do złego obszaru i nowe katalogi najwyższego poziomu, których skryptu nikt nie nauczył. Kod wyjścia 1 oznacza rozjazd.

Nie musisz o tym pamiętać: `.githooks/pre-commit` zablokuje commit, gdy mapa się rozjedzie. Włącza się raz na klon: `git config core.hooksPath .githooks`.

**Reguła:** dodajesz, zmieniasz nazwę, przenosisz albo kasujesz plik, więc aktualizujesz jego wiersz **w tym samym commicie**. Zmiana nie jest skończona, dopóki check nie przechodzi.

Dodanie zupełnie nowego katalogu najwyższego poziomu (powiedzmy `worker/`) wymaga jednego dodatkowego kroku: dopisz jego wzorce do `AREAS` i nazwę do `KNOWN_TOP_LEVEL` w skrypcie, i załóż `docs/map/<obszar>.md`. Skrypt nie przepuści zmiany, dopóki tego nie zrobisz, bo inaczej raportowałby "w synchronizacji", ignorując każdy plik w środku.
