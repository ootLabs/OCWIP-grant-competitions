# Runbook: OCWIP Generator konkursów

Ten plik jest instrukcją obsługi projektu dla agenta. Nie opisuje, co system ma robić (to jest w [`AGENTS.md`](AGENTS.md) i w `docs/`), tylko **co robisz, w jakiej kolejności i co robisz, kiedy coś pęknie**.

> **Protokół jednego słowa.** Użytkownik pisze `kontynuuj`. To znaczy: wykonaj pełną pętlę z sekcji [Pętla](#pętla) dla następnego zadania z [`docs/runbook/kolejka.md`](docs/runbook/kolejka.md), aż do zamkniętego pull requesta i odhaczonej karty na Trello. Nie pytaj, co robić. Nie proponuj wariantów. Pytanie do człowieka zadajesz wyłącznie w przypadkach wypisanych w sekcji [Kiedy naprawdę pytasz](#kiedy-naprawdę-pytasz).

---

## Spis

| Chcę | Idę do |
|---|---|
| Wystartować sesję | [Bootstrap](#bootstrap-raz-na-sesję) |
| Wiedzieć, co robić teraz | [`docs/runbook/kolejka.md`](docs/runbook/kolejka.md) |
| Zobaczyć pełną specyfikację zadania | `docs/runbook/M<n>-*.md` |
| Sprawdzić, czy skończyłem | [Bramka ukończenia](#bramka-ukończenia) |
| Naprawić to, co pękło | [Samonaprawa](#samonaprawa) |
| Poznać spis pól formularza | [`docs/runbook/pola.md`](docs/runbook/pola.md) |
| Poznać proces krok po kroku | [`docs/runbook/proces.md`](docs/runbook/proces.md) |
| Sprawdzić, czy wolno zgadywać | [`docs/runbook/blokery.md`](docs/runbook/blokery.md) |
| Zobaczyć, gdzie Trello kłóci się z raportem | [`docs/runbook/rozbieznosci.md`](docs/runbook/rozbieznosci.md) |
| Ruszyć kartę na Trello | [`docs/runbook/trello.md`](docs/runbook/trello.md) |

---

## Bootstrap, raz na sesję

Cztery komendy. Nie pomijaj ich, bo pierwsza z nich wykryła już raz lokalną gałąź 43 commity za zdalną.

```bash
python scripts/runbook.py doctor      # stan środowiska i repozytorium
git checkout dev && git pull --ff-only
python scripts/runbook.py next        # następne zadanie z kolejki
docker compose up -d                  # stack, jeśli będziesz uruchamiał testy
```

`doctor` sprawdza: gałąź, czystość drzewa roboczego, rozjazd z `origin`, `core.hooksPath`, obecność `.env`, stan kontenerów i spójność kolejki. Kończy się zerem, gdy nic nie wymaga uwagi. `python scripts/runbook.py doctor --fix` naprawia to, co da się naprawić bez decyzji (hooki, `.env` ze wzoru).

---

## Pętla

Dwanaście kroków. Wykonujesz je po kolei dla jednego zadania i wracasz na początek.

### 1. Weź zadanie

```bash
python scripts/runbook.py next
```

Zwraca pierwsze zadanie ze stanem `kolejka`, którego wszystkie zależności są `gotowe`, a kolumna blokera jest pusta. Nie bierz innego. Kolejność w [`docs/runbook/kolejka.md`](docs/runbook/kolejka.md) jest policzona z sekcji ZALEŻNOŚCI na kartach Trello i wzięcie zadania poza kolejnością kończy się przepisywaniem.

Jeśli `next` mówi, że wszystko dostępne jest zrobione, a reszta czeka na bloker: [Gdy kolejka stoi](#gdy-kolejka-stoi).

### 2. Przeczytaj minimum kontekstu

Dokładnie tyle i nic więcej:

1. Specyfikacja zadania w `docs/runbook/M<n>-*.md` (sekcja o tym `T-xx`). Jest tam kontekst, zakres, granica zadania, zależności i kryteria akceptacji 1:1 z Trello, plus to, czego Trello nie ma, a jest w raporcie.
2. Karta na Trello pod adresem z tej sekcji. Czytasz **komentarze**, bo tam lądują ustalenia nowsze niż opis.
3. Mapa obszaru, którego dotykasz: [`docs/map/backend.md`](docs/map/backend.md) albo [`docs/map/frontend.md`](docs/map/frontend.md) albo [`docs/map/infra.md`](docs/map/infra.md). Jedna, nie trzy.
4. Trzy górne wpisy z [`docs/log.md`](docs/log.md).
5. Sekcje wskazane w specyfikacji zadania: pola z [`pola.md`](docs/runbook/pola.md), krok procesu z [`proces.md`](docs/runbook/proces.md), decyzje z [`decyzje.md`](docs/runbook/decyzje.md).

Nie czytaj całego `docs/`. Nie rób ślepego grepa po repozytorium. Mapa najpierw.

### 3. Zapisz plan

W bloku `<plan> ... </plan>`: przypadki brzegowe, prawdopodobne błędy, alternatywy, wybór. Pomijasz wyłącznie przy prawdziwych jednolinijkowcach.

Plan ma odpowiedzieć na trzy pytania: które pliki ruszasz, jakie testy powstaną, co w tej karcie może okazać się większe, niż wygląda.

### 4. Odbij gałąź

```bash
git checkout dev && git pull --ff-only
git checkout -b feat/<krotki-kebab>
```

Prefiksy: `feat/` `fix/` `refactor/` `chore/` `docs/`. Nazwa krótka, po angielsku, z dywizami. Nigdy nie commitujesz na `dev` ani na `main`.

### 5. Przenieś kartę na Trello

Na listę **W trakcie**. Maksymalnie trzy karty naraz na tej liście. Komendy w [`docs/runbook/trello.md`](docs/runbook/trello.md).

### 6. Zrób robotę

- Jeden commit to jedna logiczna zmiana. Tytuł `type: short summary`, po angielsku, tryb rozkazujący, do około 60 znaków, bez kropki.
- Nowe zachowanie ma test. Poprawka błędu ma test, który bez poprawki nie przechodzi.
- Nie wychodzisz poza ZAKRES z karty. Sekcja CZEGO NIE ROBIMY TUTAJ istnieje po to, żebyś nie zrobił dwa razy tej samej rzeczy w dwóch kartach.
- Natrafiłeś na coś zepsutego poza zakresem? Zapisujesz to w `docs/runbook/rozbieznosci.md` i idziesz dalej. Nie naprawiasz przy okazji.

### 7. Bramka ukończenia

```bash
python scripts/runbook.py gate
```

Szczegóły i tryb ręczny: [Bramka ukończenia](#bramka-ukończenia). Czerwona bramka znaczy, że nie skończyłeś. Wracasz do kroku 6.

### 8. Sam zrób review

```
/code-review high
```

Review na własnym diffie, zanim otworzysz pull requesta. Każde ustalenie albo poprawiasz, albo zapisujesz w opisie PR jako świadomą decyzję z uzasadnieniem. Po poprawkach bramka leci jeszcze raz.

Osobno, jeśli zmiana dotyka danych osobowych, uprawnień albo uwierzytelniania:

```
/security-review
```

To nie jest opcjonalne dla kart z bloku T-12.x, T-13.x, T-32, T-36, T-45 i T-47.

### 9. Domknij dokumentację

Zanim otworzysz pull requesta, przejdź tę listę i popraw to, co zmiana unieważniła:

| Zmieniło się | Aktualizujesz |
|---|---|
| Dodałeś, przeniosłeś, zmieniłeś nazwę albo skasowałeś plik | wiersz w `docs/map/<obszar>.md`, w tym samym commicie |
| Podjąłeś decyzję projektową | [`docs/architektura.md`](docs/architektura.md) |
| Ruszyłeś schemat albo założenie o danych | [`docs/model-danych.md`](docs/model-danych.md), potwierdzone założenie przenosisz z tabeli do treści |
| Doszła zmienna środowiskowa | `.env.example`, z bezpiecznym placeholderem |
| Doszło nowe pojęcie domenowe | [`docs/slownik.md`](docs/slownik.md) |
| Zadanie było nietrywialne | jeden wpis na górze [`docs/log.md`](docs/log.md), format i limit 20 wpisów opisane w tym pliku |
| Zmienił się sposób pracy albo kolejność zadań | [`docs/runbook/kolejka.md`](docs/runbook/kolejka.md) i ten plik |

### 10. Pull request

```bash
git push -u origin feat/<krotki-kebab>
gh pr create --base dev --fill
```

Opis PR: co się zmieniło i dlaczego, w kilku zdaniach, plus link do karty Trello i odhaczona checklista z `.github/pull_request_template.md`. Kartę na Trello przenieś na listę **Do weryfikacji**.

**Nigdzie żadnego śladu autorstwa AI ani narzędzi.** Ani w commitach, ani w opisie PR, ani w kodzie, komentarzach czy dokumentacji.

Potem czekasz na CI:

```bash
gh pr checks --watch
```

### 11. Merge

Cztery zadania CI na zielono, więc:

```bash
gh pr merge --merge --delete-branch
git checkout dev && git pull --ff-only
```

**Merge commit, nie squash i nie rebase.** Repozytorium nie wymaga cudzego review na `dev`, więc mergujesz sam. To nie znaczy, że review nie było: zrobiłeś je w kroku 8 i jego wynik jest w opisie PR.

Do `main` idzie wyłącznie release, czyli pull request z `dev`, mergowany z `--no-ff`. Release robisz, gdy poprosi o to człowiek, nie z własnej inicjatywy.

### 12. Zamknij kartę

1. Odhacz całą checklistę kryteriów akceptacji na karcie Trello. Pozycji, której nie zrobiłeś, **nie odhaczasz**, tylko dopisujesz komentarz, czego brakuje, i karta zostaje na liście **W trakcie**.
2. Przenieś kartę na listę **Zrobione**.
3. Przestaw stan zadania w [`docs/runbook/kolejka.md`](docs/runbook/kolejka.md) na `gotowe` i zacommituj to razem z resztą (albo osobnym commitem `docs:`, jeśli PR jest już zmergowany).
4. Wracasz do kroku 1.

---

## Bramka ukończenia

Zmiana jest skończona, gdy **wszystkie** poniższe są prawdziwe. `python scripts/runbook.py gate` odpala punkty 1 do 6 i wypisuje, który padł.

```bash
python scripts/check_map.py                          # 1
python scripts/check_text.py                         # 2
docker compose exec -T backend dotnet test           # 3
docker compose exec -T frontend npm run typecheck    # 4
docker compose exec -T frontend npm test             # 5
python scripts/smoke_test.py                         # 6
```

7. Uruchomiłeś zmianę i jej użyłeś, a nie tylko przeczytałeś diff.
8. Nowe zachowanie ma test, a poprawka błędu ma test, który bez poprawki nie przechodzi.
9. Dokumentacja z kroku 9 pętli jest zaktualizowana.
10. Nowe zmienne środowiskowe są w `.env.example`.
11. Wpis w `docs/log.md` przy zadaniu nietrywialnym.
12. Checklista kryteriów akceptacji na Trello odhaczona w całości.

Punkt 6 uruchamiaj tylko wtedy, gdy ruszałeś sposób startu stacku. W pozostałych przypadkach `gate --fast` pomija smoke test.

---

## Samonaprawa

Katalog awarii. Szukasz objawu, robisz to, co pod nim, wracasz do pętli. Nie improwizujesz.

### CI czerwone, lokalnie zielone

```bash
gh run view --log-failed
```

Trzy najczęstsze przyczyny, w tej kolejności:

1. **Testy bazodanowe.** CI daje prawdziwego PostgreSQL, a lokalnie `[RequiresDatabaseFact]` je pomija, gdy nie ma connection stringa. Odpal `docker compose up -d db`, ustaw `ConnectionStrings__Postgres` i powtórz lokalnie. Zielony test, który został pominięty, nie jest zielonym testem.
2. **`npm ci` kontra `npm install`.** Zmieniłeś `package.json` i nie przegenerowałeś lockfile'a: `docker compose exec frontend npm install --package-lock-only`, potem commit obu plików.
3. **Bootstrap bazy w CI.** Usługi kontenerowe w GitHub Actions nie uruchamiają `db/init/`, więc workflow aplikuje ten katalog osobno przez `psql`. Przeniesienie bootstrapu gdzie indziej wymaga zmiany w `.github/workflows/ci.yml`.

### Bramka czerwona na `check_map.py`

Skrypt wypisuje trzy rodzaje rozjazdu i ścieżkę pliku przy każdym.

- *Missing from the map*: dopisz wiersz do wskazanego `docs/map/<obszar>.md`. Jedna linia, opis mówi co plik robi i jakie symbole w nim siedzą.
- *Stale*: usuń wiersz wskazujący na plik, którego nie ma.
- *Misfiled*: przenieś wiersz do właściwego obszaru.
- *New top-level directories*: dopisz wzorce do `AREAS` i nazwę do `KNOWN_TOP_LEVEL` w `scripts/check_map.py` oraz załóż `docs/map/<obszar>.md`.

### Bramka czerwona na `check_text.py`

Gdzieś wśliznął się myślnik typograficzny. Skrypt podaje plik, wiersz i kolumnę. Zamień na przecinek, dwukropek, nawias albo zwykły dywiz `-`. Nie ma wyjątku wartego dyskusji i nie używasz `--no-verify`.

### Testy backendu nie wstają

```bash
docker compose ps
docker compose logs --tail 100 backend
```

- Kontener nie żyje: `docker compose up -d --build backend`.
- Zmieniły się pakiety NuGet: przebudowa obowiązkowa, restart nie wystarczy.
- Migracja nie przechodzi na istniejącym wolumenie: `docker compose down -v && docker compose up --build`. `db/init/*.sql` uruchamia się **tylko** na pustym wolumenie, więc bez tego zmiana w tym katalogu pozornie nic nie robi.
- Hot reload nie odświeżył metadanych tras po zmianie sygnatury endpointu: `docker compose restart backend`, inaczej dokument OpenAPI opisuje poprzedni kształt.

### Front nie wstaje albo widzi stare pakiety

`node_modules` żyje w anonimowym wolumenie, który przeżywa zwykłą przebudowę:

```bash
docker compose up -d --build --renew-anon-volumes frontend
```

### Porty zajęte

Nie zabijasz cudzych kontenerów. Nadpisujesz porty w swoim `.env`: `POSTGRES_PORT`, `BACKEND_PORT`, `FRONTEND_PORT`.

### Konflikt przy merge z `dev`

```bash
git checkout dev && git pull --ff-only
git checkout feat/<twoja>
git merge dev
```

Rozwiązujesz konflikt, bramka leci od nowa w całości. Konflikt w `docs/map/*.md` rozwiązujesz tak, żeby zostały **oba** wiersze, chyba że opisują ten sam plik. Konflikt w `docs/log.md` rozwiązujesz zachowując oba wpisy w kolejności dat, a potem sprawdzasz limit 20 wpisów.

Nie robisz `git rebase` na gałęzi, która jest już na `origin`.

### Kontekst sesji się degraduje, model zaczyna się zapętlać

Nie przepychaj tego siłą. Zrzuć stan i otwórz nową sesję:

> Zrób pełny zrzut techniczny naszego obecnego stanu: 1) co działa, 2) na czym utknęliśmy, 3) następne kroki, 4) kluczowe decyzje i zmienione pliki. Sformatuj to do wklejenia w nowy czat.

To samo podsumowanie wrzuć do `docs/log.md`, zanim skończysz. Kartę zostaw na liście **W trakcie** z komentarzem, gdzie stanąłeś.

### Karta okazała się zablokowana w trakcie pracy

Zatrzymujesz się. Nie zgadujesz i nie robisz obejścia.

1. Zacommituj to, co już działa i przechodzi bramkę, na swojej gałęzi.
2. Przenieś kartę na listę **Zablokowane: czeka na klienta** z komentarzem, czego konkretnie brakuje i kto ma to dostarczyć.
3. Dopisz wiersz do [`docs/runbook/blokery.md`](docs/runbook/blokery.md).
4. W `docs/runbook/kolejka.md` przestaw stan zadania na `zablokowane` i wpisz identyfikator blokera w ostatniej kolumnie.
5. Wracasz do kroku 1 pętli po następne zadanie.

### Karta okazała się dwa razy większa, niż wygląda

Nie rozdymasz jednego pull requesta. Dzielisz zadanie: część, którą da się zamknąć z testami i dokumentacją, idzie jako pull request pod tym samym numerem karty, a reszta dostaje nowy wiersz w kolejce z sufiksem (na przykład `T-26a`) i komentarz na karcie Trello z granicą podziału. Podział opisujesz w `docs/log.md`.

### Trello mówi co innego niż repozytorium

Repozytorium ma rację co do kodu, Trello ma rację co do zakresu. Rozjazd zapisujesz w [`docs/runbook/rozbieznosci.md`](docs/runbook/rozbieznosci.md) i:

- karta oznaczona jako zrobiona, a kodu nie ma: przestaw ją z powrotem, dopisz komentarz, popraw kolejkę;
- kod jest, a karta wisi w backlogu: odhacz checklistę, przenieś kartę, popraw kolejkę.

Nie usuwasz kart i nie przepisujesz cudzych opisów.

### Raport kłóci się z Trello albo z modelem danych

Nie rozstrzygasz tego sam, bo to zmiana zakresu. Wpisujesz pozycję do [`docs/runbook/rozbieznosci.md`](docs/runbook/rozbieznosci.md) i budujesz zgodnie z tym, co jest **węższe**, chyba że raport jest nowszy i mówi wprost inaczej. Datę i źródło zapisujesz przy pozycji.

### Brakuje dostępu do Trello albo GitHuba

Praca nie staje. Robisz zadanie do końca lokalnie, zapisujesz w `docs/log.md`, co trzeba dokliknąć, i mówisz o tym w podsumowaniu. Nie wymyślasz stanu kart.

### Bramka przechodzi, a zmiana i tak nie działa w przeglądarce

Testy sprawdzają to, co ktoś kazał im sprawdzić. Uruchom stack, wejdź na `http://localhost:3000` i użyj zmiany ręcznie. Punkt 7 bramki nie jest formalnością.

---

## Gdy kolejka stoi

Wszystkie dostępne zadania są zrobione, a reszta czeka na dokument od zamawiającego. Wtedy, w tej kolejności:

1. **Domknij rozjazdy.** Pozycje z [`docs/runbook/rozbieznosci.md`](docs/runbook/rozbieznosci.md), które da się rozwiązać bez pytania nikogo.
2. **Uzupełnij testy.** Reguły z [`docs/testy.md`](docs/testy.md), które nie mają jeszcze pokrycia, zwłaszcza testy negatywne uprawnień.
3. **Popraw mapę i dokumentację.** Pliki `docs/`, które zostały w tyle za kodem.
4. **Przygotuj kartę zablokowaną do startu**: spisz w jej komentarzu dokładnie, co zrobisz w dniu, w którym dokument przyjdzie, i jakie pytania trzeba zadać.
5. Dopiero potem powiedz człowiekowi, że kolejka stoi, i wypisz **jednym akapitem**, na który dokument czeka co.

Czego nie robisz nigdy: nie wymyślasz encji, nie projektujesz karty oceny z głowy, nie piszesz treści umowy, nie dodajesz funkcji spoza [`docs/zakres.md`](docs/zakres.md).

---

## Kiedy naprawdę pytasz

Pytanie do człowieka zadajesz tylko w tych przypadkach:

1. Zadanie wymaga dokumentu, którego nie ma, i nie da się go obejść zawężeniem zakresu (lista w [`blokery.md`](docs/runbook/blokery.md)).
2. Zmiana wymagałaby migracji burzącej istniejące dane albo odwrócenia decyzji z [`docs/architektura.md`](docs/architektura.md).
3. Raport i Trello dają dwie sprzeczne odpowiedzi, a wybór zmienia schemat bazy.
4. Ktoś prosi o rzecz z listy świadomych cięć w [`docs/zakres.md`](docs/zakres.md).
5. Release na `main`.

Poza tą piątką rozstrzygasz sam, zapisujesz decyzję i idziesz dalej.

---

## Reguły twarde

Te nie negocjują się z terminem ani z pośpiechem. Pełne uzasadnienia są w [`AGENTS.md`](AGENTS.md) i [`docs/reguly-biznesowe.md`](docs/reguly-biznesowe.md).

1. **Brak reguły oznacza brak dostępu.** Domyślną odpowiedzią jest odmowa. Endpoint, o którym ktoś zapomniał, ma być niedostępny.
2. **Wnioskodawca nigdy nie widzi cudzego wniosku.** Reguła bez testu automatycznego to życzenie, nie reguła.
3. **Nie ujawniamy, czy konto istnieje.** Rejestracja, logowanie i reset hasła odpowiadają tak samo dla adresu zajętego i wolnego.
4. **Nie logujemy haseł, tokenów sesji, PESEL-i ani treści wniosków.**
5. **Nie kasujemy twardo.** Retencja minimum 5 lat. Usunięcie to oznaczenie jako nieaktywne. Zero `ON DELETE CASCADE`, także dla klucza, którego nie napisaliśmy sami.
6. **Odcięcie naboru tnie co do minuty**, czas w UTC, konwersja wyłącznie na brzegach.
7. **Struktura formularza jest danymi w bazie, nie kodem.** Od pierwszego dnia, nawet zanim powstanie ekran do jej edycji.
8. **Kod po angielsku, dokumentacja i UI po polsku.** Bez mieszania w obrębie jednego artefaktu.
9. **Zero myślników typograficznych** w całym repozytorium, łącznie z tekstami UI i odpowiedziami do użytkownika.
10. **Zero śladu autorstwa AI** w commitach, pull requestach, kodzie i dokumentacji.
11. **Każde pole trzymające dane wrażliwe oznaczone komentarzem w kodzie.**
12. **Nie zgadujemy w modelu danych.** Brakujący dokument to karta w liście Zablokowane, a nie wymyślona encja.

---

## Czego ten runbook nie zastępuje

[`AGENTS.md`](AGENTS.md) jest konstytucją i wygrywa z tym plikiem przy każdej sprzeczności. [`CONTRIBUTING.md`](CONTRIBUTING.md) opisuje mechanikę gałęzi i commitów szerzej. `docs/` trzyma wiedzę trwałą: architekturę, model danych, konwencje, słownik i zakres. Runbook jest warstwą wykonawczą nad nimi, nie ich kopią.

Znalazłeś w runbooku regułę sprzeczną z `AGENTS.md`? Runbook jest zepsuty, popraw runbook.
