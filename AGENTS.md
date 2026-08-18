# OCWIP Generator konkursów - instrukcje dla agentów

Webowa platforma obsługująca pełny cykl życia konkursu dotacyjnego dla Opolskiego Centrum Wspierania Inicjatyw Pozarządowych. OCWIP występuje po stronie ORGANIZATORA konkursu, nie wnioskodawcy.

**Stack:** Next.js (TypeScript, Tailwind CSS) - .NET - PostgreSQL - Docker Compose

> **Jedno źródło prawdy.** `CLAUDE.md` importuje ten plik, `.cursor/rules/` na niego wskazuje. Edytuj **ten** plik, nigdy nie kopiuj reguł do pozostałych.

---

## Stan repozytorium

To jest **szkielet**. Świadomie nie ma tu jeszcze:

brak encji domenowych - brak migracji - brak logowania i ról - brak kreatora formularzy - brak modułu oceny - brak generowania umów - brak sprawozdawczości - brak wysyłki maili

Co jest: trzy kontenery, które się budują i widzą nawzajem, health endpointy, testy z CI oraz dokumentacja i kontekst projektu. Każdy z brakujących elementów ma swoją kartę na Trello. Nie buduj ich "przy okazji".

Zakres MVP i świadome cięcia: [`docs/zakres.md`](docs/zakres.md). Jeśli zaczynasz robić coś, czego tam nie ma, przerwij i zapytaj.

---

## Kontekst domenowy - przeczytaj przed pierwszą zmianą

| Chcę zrozumieć | Czytaj |
|---|---|
| Kto jest klientem i jaki problem rozwiązujemy | [`docs/kontekst-projektu.md`](docs/kontekst-projektu.md) |
| Słownik pojęć (oferta, nabór, konkurs, generator) | [`docs/slownik.md`](docs/slownik.md) |
| Role, typy podmiotów, twarde reguły biznesowe | [`docs/reguly-biznesowe.md`](docs/reguly-biznesowe.md) |
| Co świadomie NIE wchodzi do MVP | [`docs/zakres.md`](docs/zakres.md) |

Używaj słownika z `docs/slownik.md` w UI, w nazwach endpointów i w rozmowie z zamawiającym. Zamawiający opisuje system językiem Witkaca. Jeśli nasze nazwy będą inne, każde spotkanie zacznie się od tłumaczenia pojęć.

---

## Gdzie szukać - zrób to zamiast przeszukiwania repo

Ślepy grep zamienia pięciominutową zmianę w godzinę. Zacznij stąd za każdym razem:

| Co robisz | Czytaj najpierw |
|---|---|
| Szukasz pliku, który robi X | `docs/map/README.md`, potem mapa obszaru |
| Zmiana w backendzie | `docs/map/backend.md` |
| Zmiana we froncie | `docs/map/frontend.md` |
| Docker, baza, skrypty, zmienne środowiskowe | `docs/map/infra.md` |
| Jak system trzyma się do kupy | `docs/architektura.md` |
| Nazewnictwo, struktura folderów, styl | `docs/konwencje.md` |
| Model danych i jawne założenia | `docs/model-danych.md` |
| Pisanie i uruchamianie testów, CI | `docs/testy.md` |
| Co się ostatnio zmieniło i dlaczego | `docs/log.md` - **tylko kilka górnych wpisów** |

**Reguła:** najpierw mapa, grep drugi. Jeśli mapa czegoś nie miała, mapa była zła, więc popraw ją w ramach swojej zmiany.

Ładuj tylko obszar, którego dotykasz. Czytanie wszystkich map po to, żeby zmienić jedną zmienną CSS, to dokładnie to marnotrawstwo, przed którym ta struktura ma chronić.

---

## Język (zabija "Ponglish")

- **Kod: angielski.** Identyfikatory, komentarze, nazwy plików źródłowych, komunikaty commitów, ścieżki API, kolumny w bazie.
- **Dokumentacja i UI: polski.** Cała treść w `docs/`, teksty w interfejsie, etykiety, dane testowe widoczne w produkcie.
- **Czat: dowolny język.** Nie zmienia niczego powyżej.

Powód tego podziału jest konkretny: kod czytają narzędzia i agenci, a dokumentację czyta zespół i strona zamawiająca, która nie pracuje po angielsku. Bez mieszania w obrębie jednego artefaktu. Polski identyfikator w kodzie albo angielska etykieta w polskim UI wracają z review.

## Interpunkcja (reguła twarda)

**Nigdy nie używaj myślnika typograficznego (em dash) ani półpauzy (en dash).** Ani w kodzie, ani w komentarzach, dokumentacji, commitach, tekstach UI czy odpowiedziach do użytkownika. Użyj przecinka, dwukropka, nawiasu albo zwykłego dywizu `-`.

`scripts/check_text.py` tego pilnuje, a hook pre-commit blokuje commit. Nie ma wyjątku wartego dyskusji.

---

## Bezpieczeństwo i dane osobowe - to nie jest dodatek

System przetwarza dane organizacji i osób fizycznych, a przy umowach pojawiają się PESEL-e. To podnosi poprzeczkę dla każdej karty, nie tylko dla tych oznaczonych jako bezpieczeństwo.

1. **Brak reguły oznacza brak dostępu.** Domyślną odpowiedzią jest odmowa. Endpoint, o którym ktoś zapomniał, ma być niedostępny, a nie otwarty dla wszystkich.
2. **Wnioskodawca nigdy nie widzi cudzego wniosku.** Reguła bez testu automatycznego to życzenie, nie reguła.
3. **Nie ujawniamy, czy konto istnieje.** Rejestracja, logowanie i reset hasła odpowiadają tak samo dla adresu zajętego i wolnego.
4. **Nie logujemy haseł ani danych wrażliwych.** Log z hasłami to gorszy wyciek niż ten, przed którym się bronimy.
5. **Nie kasujemy twardo.** Retencja minimum 5 lat. "Usunięcie" to oznaczenie jako nieaktywne.
6. **Każde pole trzymające dane wrażliwe oznacz komentarzem w kodzie**, żeby przy szyfrowaniu nikt niczego nie przeoczył.

---

## Git

**Dwie gałęzie żyją na stałe.** Nigdy nie commituj bezpośrednio do żadnej z nich.

- **`main` to produkcja.** Zawsze wdrażalna. Ląduje tu tylko merge release z `dev` albo `hotfix/`.
- **`dev` to gałąź integracyjna.** Domyślna. Odbijasz od niej i wracasz do niej.

**Gałęzie krótko żyjące, odbijane od `dev`:** `feat/` `fix/` `refactor/` `chore/` `docs/` plus krótki kebab-case.

**`hotfix/<short>` to jedyny wyjątek:** odbijany od `main`, mergowany do `main`, a potem od razu `main` z powrotem do `dev`, żeby kolejny release po cichu nie cofnął poprawki.

**Release:** pull request z `dev` do `main`, mergowany z `--no-ff`, żeby został jednym rozpoznawalnym commitem.

- Commit = jedna logiczna zmiana. Tytuł `type: short summary`, do około 60 znaków, po angielsku, w trybie rozkazującym, bez kropki na końcu.
- Bez opisu, chyba że zmiana jest duża albo nieoczywista, wtedy 2-4 punkty.
- **Nigdzie żadnego śladu autorstwa AI ani narzędzi.** Ani w commitach, ani w PR-ach, kodzie, komentarzach czy dokumentacji. Żadnego `Co-Authored-By`, żadnego "generated with".

Pełny workflow: `CONTRIBUTING.md`.

---

## Definicja ukończenia

Zmiana jest skończona, gdy **wszystkie** poniższe warunki są spełnione:

1. Kod działa, czyli uruchomiłeś go, a nie tylko przeczytałeś diff.
   - `docker compose exec backend dotnet test` i `docker compose exec frontend npm test` przechodzą.
   - Nowe zachowanie ma test. Poprawka błędu ma test, który bez poprawki nie przechodzi.
   - Ruszałeś sposób startu całego stacku, więc `python scripts/smoke_test.py` też przechodzi.
2. `python scripts/check_map.py` i `python scripts/check_text.py` kończą się zerem, inaczej hook pre-commit zablokuje commit. Dodałeś, przeniosłeś, zmieniłeś nazwę albo skasowałeś plik, więc jego wiersz w mapie zmienił się w tym samym commicie.
3. Zmieniło się zachowanie albo struktura, więc odpowiedni plik w `docs/` jest zaktualizowany. Podjąłeś decyzję projektową, więc siedzi w `docs/architektura.md`, a nie tylko w głowie.
4. Nowa zmienna środowiskowa jest w `.env.example`.
5. Zacommitowane.
6. Zadanie nietrywialne, więc jeden wpis w `docs/log.md` (format i limit są opisane w tym pliku).
7. Karta na Trello ma odhaczoną całą checklistę kryteriów akceptacji.

Punkty 2-4 są tym, co utrzymuje to repo tanim w pracy. Ich pominięcie przerzuca koszt na następną sesję.

---

## Reguły zdrowego wzrostu

- **Małe pliki, jedna odpowiedzialność.** Dwie role albo powyżej około 300 linii, więc dziel.
- **Jedna domena = jeden moduł.** Nie mieszaj UI, logiki i dostępu do danych.
- **Sprawdź mapę, zanim zbudujesz.** To może już istnieć.
- **Nie twórz pustych folderów** na zapas. Zakładasz je razem z pierwszym prawdziwym plikiem.
- **Refaktor to normalna praca**, commitowana osobno.
- **Nie zgaduj w modelu danych.** Nie mamy jeszcze wzoru karty oceny, umowy ani sprawozdania. Brakujący dokument oznacza kartę w liście "Zablokowane: czeka na klienta", a nie wymyśloną encję.

---

## Zanim zaczniesz nietrywialną pracę

Zapisz swoje rozumowanie w `<plan> ... </plan>`: przypadki brzegowe, prawdopodobne błędy, alternatywy. Rozwiązanie pod spodem. Pomijaj tylko przy prawdziwych jednolinijkowcach.

## Gdy sesja robi się długa

Kontekst się degraduje, a model zaczyna się zapętlać. Nie przepychaj tego siłą: zrzuć stan, otwórz nową sesję, kontynuuj.

> Zrób pełny zrzut techniczny naszego obecnego stanu: 1) co działa, 2) na czym utknęliśmy, 3) następne kroki, 4) kluczowe decyzje i zmienione pliki. Sformatuj to do wklejenia w nowy czat.

To samo podsumowanie wrzuć do `docs/log.md`, zanim skończysz.
