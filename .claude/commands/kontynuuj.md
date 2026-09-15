---
description: Weź następne zadanie z runbooka i doprowadź je do zmergowanego pull requesta
---

Pracujesz według [`runbook.md`](../../runbook.md). Ten plik jest skrótem, nie zamiennikiem: przy każdej sprzeczności wygrywa runbook, a nad nim `AGENTS.md`.

Wykonaj pełną pętlę dla jednego zadania. Nie pytaj, co robić. Nie proponuj wariantów. Zatrzymujesz się i pytasz wyłącznie w przypadkach z sekcji "Kiedy naprawdę pytasz" w runbooku.

## Co robisz teraz

1. `python scripts/runbook.py doctor` i napraw to, co wskaże. Potem `git checkout dev && git pull --ff-only`.
2. `python scripts/runbook.py next`. To jest twoje zadanie. Nie bierz innego.
3. Przeczytaj **minimum kontekstu**: specyfikację zadania w `docs/runbook/M<n>-*.md`, kartę na Trello razem z komentarzami, jedną mapę obszaru z `docs/map/`, trzy górne wpisy z `docs/log.md` oraz sekcje wskazane w specyfikacji (`pola.md`, `proces.md`, `decyzje.md`). Nie czytaj całego `docs/` i nie rób ślepego grepa.
4. Zapisz plan w bloku `<plan> ... </plan>`: przypadki brzegowe, prawdopodobne błędy, alternatywy, wybór.
5. Odbij gałąź od `dev` z prefiksem `feat/` `fix/` `refactor/` `chore/` albo `docs/`.
6. Przenieś kartę na listę **W trakcie**. Karta bez checklisty kryteriów akceptacji dostaje ją najpierw, z `docs/runbook/M<n>-*.md`. Komendy: `docs/runbook/trello.md`.
7. Zrób robotę małymi commitami. Nowe zachowanie ma test. Nie wychodzisz poza ZAKRES z karty.
8. `python scripts/runbook.py gate`. Czerwona bramka znaczy, że nie skończyłeś: wracasz do punktu 7.
9. `/code-review high` na własnym diffie. Przy zmianie dotykającej danych osobowych, uprawnień albo uwierzytelniania dodatkowo `/security-review`. Ustalenia albo poprawiasz, albo zapisujesz w opisie PR jako świadomą decyzję z uzasadnieniem, i wtedy bramka leci jeszcze raz.
10. Domknij dokumentację: mapa obszaru, `architektura.md` przy decyzji projektowej, `model-danych.md` przy zmianie schematu, `.env.example` przy nowej zmiennej, `slownik.md` przy nowym pojęciu, wpis w `docs/log.md`.
11. `git push -u origin <gałąź>`, `gh pr create --base dev --fill`, kartę na listę **Do weryfikacji**, potem `gh pr checks --watch`.
12. Zielone CI, więc `gh pr merge --merge --delete-branch`. Repozytorium nie wymaga cudzego review na `dev`, mergujesz sam. Merge commit, nie squash i nie rebase.
13. Odhacz całą checklistę na Trello, przenieś kartę na **Zrobione**, przestaw stan w `docs/runbook/kolejka.md` na `gotowe`, zacommituj.
14. Wracasz do punktu 2 i bierzesz następne zadanie, chyba że użytkownik powiedział inaczej.

## Czego nie robisz nigdy

- Nie commitujesz bezpośrednio na `dev` ani na `main`.
- Nie robisz release do `main` z własnej inicjatywy.
- Nie zgadujesz w modelu danych. Brakujący dokument to karta w liście **Zablokowane: czeka na klienta**, a nie wymyślona encja.
- Nie naprawiasz przy okazji rzeczy spoza zakresu karty. Zapisujesz je w `docs/runbook/rozbieznosci.md` i idziesz dalej.
- Nie zostawiasz żadnego śladu autorstwa AI w commitach, pull requestach, kodzie ani dokumentacji.
- Nie używasz myślnika typograficznego. Nigdzie, łącznie z odpowiedzią do użytkownika.
- Nie obchodzisz hooka przez `--no-verify`.

## Gdy coś pęknie

Katalog awarii jest w runbooku, sekcja "Samonaprawa": czerwone CI, rozjazd mapy, konflikt przy merge, kontener, który nie wstaje, karta, która okazała się zablokowana albo dwa razy większa, niż wygląda, oraz degradacja kontekstu sesji. Szukasz objawu, robisz to, co pod nim, wracasz do pętli. Nie improwizujesz.

Gdy kolejka stoi, bo wszystko dostępne jest zrobione: sekcja "Gdy kolejka stoi". Kolejność jest tam ustalona i kończy się jednym akapitem do użytkownika, a nie pytaniem, co teraz.
