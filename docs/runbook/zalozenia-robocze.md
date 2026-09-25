# Założenia robocze

Miejsce na wszystko, co zbudowaliśmy **bez potwierdzenia w dokumentach**, żeby praca nie stała: interpretacje, wartości przyjęte na próbę i prototypy idące dalej niż to, co mówią raport, regulamin albo karty. Decyzja użytkownika z 2026-09-25: zamiast zatrzymywać pętlę przy każdej takiej luce, budujemy według najrozsądniejszej interpretacji i zapisujemy ją tutaj.

Różnica wobec pozostałych plików w `runbook/`:

- [`rozbieznosci.md`](rozbieznosci.md) to miejsca, gdzie **dwa źródła mówią co innego**;
- [`blokery.md`](blokery.md) to rzeczy, których **nie da się zrobić** bez dokumentu;
- ten plik to rzeczy, które **zrobiliśmy mimo braku odpowiedzi**, i które trzeba potwierdzić albo cofnąć.

Każda pozycja mówi, gdzie siedzi w kodzie, bo cofnięcie założenia to konkretna zmiana, a nie dyskusja. Pytanie do klientki, jeśli jest, trafia też na kartę blokera na Trello.

| ID | Założenie | Gdzie w kodzie | Skąd wątpliwość | Co potwierdzić |
|---|---|---|---|---|
| ZR-01 | Kwota rekomendowana na liście rankingowej to **średnia** kwot proponowanych przez ekspertów, zaokrąglona do grosza | `Services/Ranking/RankingCalculator.cs`, `RankingRow.RecommendedGrant` (T-39) | Regulamin 2026 mówi o "proponowanej kwocie" na każdej karcie, nie mówi, jak łączyć dwie. Raport mówi, że decyduje operator (T-42) | Czy operator widzi średnią, niższą z dwóch, czy obie osobno. Do czasu odpowiedzi T-41 może pokazać obie kwoty obok średniej |
| ZR-02 | Skala ostrzeżenia o rozbieżności to suma `maxValue` pól składających się na sumę merytoryczną karty (50 w 2026); karta bez tych widełek nie ostrzega wcale | `RankingCalculator.MeritScale` (T-39) | Raport: "próg rozbieżności domyślnie 30% skali", bez definicji skali; regulamin 2026 progu nie zna (P2 na B-02) | Czy skalą jest jedna karta (50), czy suma obu (100); czy ostrzeżenie w ogóle obowiązuje |
| ZR-03 | Próg punktowy i ostrzeżenie o rozbieżności nie mają wartości domyślnej dla istniejących konkursów (`null`); liczba ekspertów (2) i suma mają | migracja `AddEvaluationSettings` (T-39) | Regulamin 2026 podaje próg 50, ale konkursy utworzone wcześniej mogą mieć inny regulamin | Nic, dopóki nie ma drugiego konkursu; operator ustawia próg trasą ustawień oceny |
| ZR-04 | Panel recenzenta działa **bez bramy deklaracji bezstronności**: ekspert przypisany do wniosku widzi go od razu | panel `/panel/reviewer` i trasy ocen (T-40) | Raport (decyzja 11) i regulamin komisji 2026 każą podpisać deklarację przed pracą; nie ma jeszcze modelu (R-05) ani rozebranej treści deklaracji | Zbudować w T-40a; do tego czasu operator przypisuje tylko ekspertów, którzy deklarację podpisali na papierze |
