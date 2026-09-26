# Kolejka zadań

Jedyne źródło prawdy o tym, **co jest zrobione i co robimy dalej**. Czyta to człowiek i czyta to `scripts/runbook.py`, więc format tabeli jest sztywny.

Kolejność wierszy nie jest przypadkowa: jest policzona z sekcji ZALEŻNOŚCI na kartach Trello. Bierzesz pierwsze zadanie ze stanem `kolejka`, którego wszystkie zależności są `gotowe`, a kolumna Bloker jest pusta (`-`).

```bash
python scripts/runbook.py next     # następne zadanie
python scripts/runbook.py status   # cała kolejka w skrócie
```

## Format, nie zmieniaj go

Siedem kolumn, dokładnie w tej kolejności. Skrypt parsuje je po pozycji, więc dodanie kolumny w środku psuje `next`.

| Kolumna | Dopuszczalne wartości |
|---|---|
| Stan | `gotowe`, `w toku`, `kolejka`, `zablokowane` |
| ID | identyfikator karty, na przykład `T-12.3` |
| Zadanie | tytuł, po polsku, bez priorytetu i obszaru |
| Kamień | `M1` do `M7` albo `poza MVP` |
| Trello | kod z adresu karty, czyli `<kod>` z `https://trello.com/c/<kod>` |
| Zależności | identyfikatory rozdzielone przecinkiem albo `-` |
| Bloker | `B-xx` albo `-` |

Stan przestawiasz w tym samym commicie, w którym zamykasz zadanie. Kolejka rozjechana z Trello jest gorsza niż jej brak.

---

## M1 · Fundament

Research, baza, encje, uwierzytelnianie, role, kontrakt API, tokeny i shelle paneli. Wąskie gardło było przy bazie i już nie istnieje.

| Stan | ID | Zadanie | Kamień | Trello | Zależności | Bloker |
|---|---|---|---|---|---|---|
| gotowe | T-06.1 | Witkac.pl: rekonesans bez logowania | M1 | o8NfD1QF | - | - |
| gotowe | T-06.2 | Witkac.pl: konto testowe i ścieżka wnioskodawcy | M1 | mWEu5NWp | T-06.1 | - |
| gotowe | T-07 | Research wizualny OCWIP: branding pod UI | M1 | YgjA879S | - | - |
| gotowe | T-07.1 | Moodboard | M1 | FQa7ZBxX | T-07 | - |
| gotowe | T-11.1 | Postgres lokalnie i narzędzie migracyjne | M1 | ZHksYfn9 | - | - |
| gotowe | T-11.2 | Encje Użytkownik i Podmiot | M1 | WWhJrubw | T-11.1 | - |
| gotowe | T-11.3 | Encje Konkurs i DefinicjaFormularza | M1 | u1BIcJR6 | T-11.1 | - |
| gotowe | T-11.4 | Encje Wniosek i Załącznik | M1 | VX0vQZQg | T-11.2, T-11.3 | - |
| gotowe | T-11.5 | ERD i dane testowe | M1 | ABKkdXCp | T-11.4 | - |
| gotowe | T-12.0 | Fundament kont na ASP.NET Core Identity | M1 | tg0e82xM | T-11.2 | - |
| gotowe | T-12.1 | Rejestracja konta | M1 | PpXqULSi | T-12.0 | - |
| gotowe | T-12.2 | Weryfikacja adresu e-mail | M1 | d4DKHGAf | T-12.1 | - |
| gotowe | T-13.1 | Model ról | M1 | iDRvclFH | T-11.2 | - |
| gotowe | T-15.1 | Design tokeny z brandingu OCWIP | M1 | mFbQQBCa | T-07 | - |
| gotowe | T-17 | Kontrakt API między .NET a Next.js | M1 | EVEF5QIk | T-11.1 | - |
| gotowe | T-12.3 | Logowanie, sesja, wylogowanie | M1 | 7MkNtmIH | T-12.1, T-12.2, T-17 | - |
| gotowe | T-12.4 | Reset hasła | M1 | mRVXGg2U | T-12.1, T-12.2 | - |
| gotowe | T-12.5 | Ochrona przed brute force | M1 | MZoxJ6zk | T-12.3 | - |
| gotowe | T-13.2 | Warstwa autoryzacji | M1 | RDyoUhkh | T-13.1, T-12.3 | - |
| gotowe  | T-13.3 | Testy negatywne uprawnień | M1 | SJflHiIR | T-13.2, T-11.4, T-11.5 | - |
| gotowe | T-12.6 | Testy e2e ścieżki uwierzytelniania | M1 | YjDuR42n | T-12.3, T-12.4, T-12.5 | - |
| gotowe | T-12.7 | Ekran logowania | M1 | h5Dz9yj2 | T-12.3, T-15.1 | - |
| gotowe | T-12.8 | Ekrany konta: rejestracja, potwierdzenie adresu, reset hasła | M1 | xkyhzw7r | T-12.7, T-12.1, T-12.2, T-12.4 | - |
| gotowe | T-15.2 | Shell panelu wnioskodawcy | M1 | cIONKupZ | T-15.1, T-13.2, T-17 | - |
| gotowe | T-15.3 | Shell panelu operatora | M1 | XBITHAH5 | T-15.1, T-13.2, T-17 | - |
| gotowe | T-15.4 | Stany puste, ładowanie i błędy | M1 | 3S2t9IdI | T-15.2, T-15.3 | - |
| gotowe | T-51 | Zniekształcony link weryfikacyjny daje 500 | M1 | cUn3qdrU | T-12.2 | - |

Specyfikacje: [`M1-fundament.md`](M1-fundament.md).

---

## M2 · Konkurs i publikacja

Pierwszy kamień, który zamawiający zobaczy jako działający produkt. `T-22` stoi za M3, bo kreator ogłoszenia wybiera opublikowaną wersję formularza, a ta powstaje dopiero w `T-27`.

| Stan | ID | Zadanie | Kamień | Trello | Zależności | Bloker |
|---|---|---|---|---|---|---|
| gotowe | T-20 | Konkurs: cykl życia, stany i publikacja | M2 | dcj9E3qW | T-11.3, T-13.2, T-17 | - |
| gotowe | T-20a | Parametry konkursu z kroków 1.2 do 1.6 kreatora | M2 | VMtb4Zq1 | T-20 | - |
| gotowe | T-21 | Twarde odcięcie terminu naboru | M2 | jU68qLLX | T-20 | - |
| gotowe | T-23 | Publiczna lista konkursów i strona konkursu | M2 | 7PRbbgV5 | T-20, T-15.1 | 2026-09-21 |
| gotowe | T-22 | Kreator ogłoszenia konkursu (operator) | M2 | 3S8truZC | T-20, T-15.3, T-27 | - |

`T-20a` to druga połowa karty `dcj9E3qW`, wydzielona przy jej realizacji: `T-20` zamknął cykl życia konkursu (stany, przejścia, API operatora, widok publiczny, retencja), a parametry z kroków 1.2 do 1.6 kreatora (limity i procenty, kategorie kosztów, wymagane załączniki, osoby kontaktowe, forma papierowa, data usunięcia danych osobowych) czekają tutaj. Granica podziału i powód są opisane w [`M2-konkurs.md`](M2-konkurs.md). Zrobione 2026-09-18: parametry siedzą w API, wymagane załączniki bez wzoru pliku (to `T-32`), a zaplanowana data publikacji nadal czeka na rozstrzygnięcie `R-27`. Karta na Trello założona i zamknięta 2026-09-23 (`VMtb4Zq1`).

Specyfikacje: [`M2-konkurs.md`](M2-konkurs.md).

---

## M3 · Kreator formularzy

Najtrudniejszy technicznie kamień i jednocześnie główny argument sprzedażowy. Zaczyna się od kontraktu, bo kreator i renderer to dwie strony tej samej umowy.

| Stan | ID | Zadanie | Kamień | Trello | Zależności | Bloker |
|---|---|---|---|---|---|---|
| gotowe | T-24 | Kontrakt JSON definicji formularza | M3 | gcslfR97 | T-11.3 | - |
| gotowe | T-25 | Wersjonowanie definicji formularza | M3 | bl59xg1v | T-24 | - |
| gotowe | T-26 | Kreator formularzy: sekcje, pola, walidacje | M3 | Xw5EirNk | T-24, T-15.3 | - |
| zablokowane | T-26a | Kreator formularzy: budowa od zera i przestawianie sekcji | M3 | 2Esmrx86 | T-26 | B-10 |
| gotowe | T-28 | Renderer formularza z definicji JSON | M3 | EJZABgdX | T-24, T-15.2 | - |
| gotowe | T-27 | Podgląd formularza i publikacja wersji | M3 | fYqlfSoR | T-25, T-26, T-28 | - |

Specyfikacje: [`M3-formularze.md`](M3-formularze.md).

---

## M4 · Składanie wniosków

Rdzeń produktu, dziewięć kart. Domyka go `T-36`, czyli testy izolacji danych, bez których aplikacja nie wychodzi poza lokalną maszynę.

| Stan | ID | Zadanie | Kamień | Trello | Zależności | Bloker |
|---|---|---|---|---|---|---|
| gotowe | T-29 | Wersja robocza wniosku i autozapis | M4 | zw48liiX | T-11.4, T-25, T-21 | - |
| gotowe | T-30 | Walidacja odpowiedzi względem definicji formularza | M4 | m5SPETCx | T-24, T-25 | - |
| gotowe | T-31 | Limit kwoty dotacji przy budżecie wniosku | M4 | dWHtvzvX | T-30, T-20 | - |
| gotowe | T-32 | Załączniki: przesyłanie, limity, przechowywanie | M4 | K4ouKUD6 | T-11.4, T-13.2 | - |
| gotowe | T-33 | Złożenie oferty i historia zmian statusu | M4 | uG9aepGO | T-29, T-30, T-31, T-32, T-21 | - |
| gotowe | T-35 | Lista wniosków i statusów dla operatora | M4 | GCyfm14r | T-33, T-15.3 | - |
| gotowe | T-34 | Ścieżka wnioskodawcy: robocze, złożenie, potwierdzenie | M4 | 0OWDa8wR | T-28, T-29, T-33, T-23, T-15.4, T-12.8 | - |
| gotowe | T-36 | Testy izolacji danych wnioskodawcy | M4 | eKXNBKtF | T-33, T-32, T-13.3 | - |

Specyfikacje: [`M4-wnioski.md`](M4-wnioski.md).

---

## M5 · Ocena

Zablokowany przez B-02. Przed dokumentami od zamawiającego da się ruszyć wyłącznie `T-37`, bo dotyczy przypisania, a nie punktacji.

| Stan | ID | Zadanie | Kamień | Trello | Zależności | Bloker |
|---|---|---|---|---|---|---|
| gotowe | T-37 | Przypisanie wniosków recenzentom | M5 | 1EG1Ngzv | T-33, T-13.1, T-13.2 | - |
| gotowe | T-38.0 | Model oceny jako dane w bazie: propozycja do przeglądu | M5 | k62rcCaN | T-37 | - |
| gotowe | T-38 | Karta oceny i punktacja: mechanizm | M5 | OPGJGeOo | T-37, T-38.0 | - |
| gotowe | T-38b | Karty oceny NOWE FIO 2026 jako dane | M5 | li2HDFNw | T-38 | - |
| gotowe | T-39 | Lista rankingowa | M5 | j6yKe6Z6 | T-38 | - |
| gotowe | T-40 | Panel recenzenta | M5 | eHJJ5x2w | T-37, T-38, T-28 | - |
| gotowe | T-40a | Deklaracja bezstronności przed oceną | M5 | qvGo5fNu | T-40 | - |
| gotowe | T-41 | Ocena i ranking w panelu operatora | M5 | NYI7jUxv | T-37, T-39, T-35 | - |
| gotowe | T-41a | Karta formalna operatora i wgląd w pojedyncze oceny | M5 | AVQdoH8h | T-38, T-41 | - |
| gotowe | T-41b | Udostępnienie kart oceny wnioskodawcom | M5 | ziBUSMum | T-41a, T-34 | - |

Specyfikacje: [`M5-ocena.md`](M5-ocena.md).

---

## M6 · Wyniki i umowa

| Stan | ID | Zadanie | Kamień | Trello | Zależności | Bloker |
|---|---|---|---|---|---|---|
| gotowe | T-42 | Decyzja o dofinansowaniu i kwoty dotacji | M6 | kJPn2EOF | T-39, T-41 | - |
| gotowe | T-42a | Eksport i publikacja listy rankingowej | M6 | aaIcNYYr | T-42 | - |
| gotowe | T-43 | Powiadomienia o wynikach konkursu | M6 | EFTVE59t | T-42, T-12.2 | - |
| gotowe | T-43a | Prawdziwa wysyłka maili (SMTP) | M6 | RMHWS5Ht | T-43 | - |
| gotowe | T-44 | Eksport wniosku i wyników do PDF | M6 | qRlz6aCv | T-33, T-42, T-25 | - |
| gotowe | T-45.0 | Umowa i sprawozdanie jako dane: propozycja do przeglądu | M6 | RKlIyDp5 | T-42 | - |
| gotowe | T-45a | Polskie znaki w PDF: osadzona czcionka | M6 | 5sI9dyPT | - | - |
| zablokowane | T-45 | Generowanie umowy ze wzoru | M6 | kbHK5Nsk | T-42 | B-03 |

`T-44` w części dotyczącej samego wniosku nie potrzebuje B-02: eksport złożonego wniosku do PDF da się zrobić po `T-33`. Podział opisany w [`M6-wyniki.md`](M6-wyniki.md).

---

## M7 · Zgodność i wdrożenie

| Stan | ID | Zadanie | Kamień | Trello | Zależności | Bloker |
|---|---|---|---|---|---|---|
| gotowe | T-46 | Audyt dostępności WCAG AA | M7 | GbFFOBnp | T-34, T-35, T-23 | - |
| zablokowane | T-47 | Ochrona danych wrażliwych: szyfrowanie, logi, retencja | M7 | tG3SiRzy | T-45 | B-05 |
| zablokowane | T-48 | Środowisko produkcyjne, kopie zapasowe, wdrożenie | M7 | RyzKqp6D | T-36, T-46, T-47 | B-06 |
| zablokowane | T-49 | Instrukcja obsługi dla operatora OCWIP | M7 | Tonpp3Uy | T-48 | B-06 |

Specyfikacje: [`M7-wdrozenie.md`](M7-wdrozenie.md).

---

## Poza MVP

| Stan | ID | Zadanie | Kamień | Trello | Zależności | Bloker |
|---|---|---|---|---|---|---|
| gotowe | T-50a | Sprawozdanie: formularz, wypełnianie, złożenie, przyjęcie albo zwrot | poza MVP | Qu1iIPTf | T-42 | - |
| zablokowane | T-50b | Rozliczenie: uznawanie kosztów, kwota do zwrotu, termin, historia projektu | poza MVP | JcsVwexF | T-50a, T-45 | B-04 |

Kolejność cięcia, gdy zabraknie czasu: pierwsza wypada sprawozdawczość (`T-50`), druga generowanie umowy (`T-45`). M2, M3, M4 i M5 to rdzeń.

---

## Zakres z raportu bez karty na Trello

Raport `RAPORT-proces-i-pola.docx` opisuje rzeczy, których żadna karta nie pokrywa. Nie są w tej kolejce, bo kolejka odwzorowuje tablicę, a nie nasze pomysły. Komplet z uzasadnieniem i propozycją, do której karty każda z nich należy, jest w [`rozbieznosci.md`](rozbieznosci.md). Zanim weźmiesz taką pozycję do pracy, musi dostać kartę na Trello.
