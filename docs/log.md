# Log projektu

Krótki, gęsty zapis tego, co się wydarzyło i dlaczego. Najnowsze na górze.

**Czytaj 3 do 5 górnych wpisów**, gdy bierzesz pracę, a nie cały plik. To jest log, nie podręcznik: trwała wiedza należy do [`architektura.md`](architektura.md) (decyzje), [`map/`](map/README.md) (gdzie co leży) albo [`konwencje.md`](konwencje.md) (jak piszemy). Jeśli fakt będzie ważny za trzy miesiące, ląduje tam, a tutaj zostaje wskaźnik.

## Format, trzymaj się ściśle

```
## RRRR-MM-DD - krótki tytuł
**Zrobione:** co teraz działa, jedna linia.
**Decyzje:** co wybrano i dlaczego, po jednej linii. Tylko nieoczywiste.
**Uwaga:** co ugryzie następnym razem. Pomiń linię, jeśli nic nie ugryzie.
```

**Limit: 20 wpisów.** Dodajesz dwudziesty pierwszy? Przenieś najstarszy do `docs/log-archiwum/<rok>.md` w tym samym commicie. Limit jest sensem tego pliku: nieograniczony dziennik to plik, którego nikt nie czyta, a każda sesja za niego płaci.

Każdy wpis maksymalnie 5 linii. Nie opowiadaj procesu, nie wypisuj zmienionych plików (git wie), nie powtarzaj tego, co już mówi mapa.

---
## 2026-08-20 - dodanie modelów danych
**Zrobione:** Modele danych: Entity, User.
**Decyzje:** Trzy typy podmiotów w enumie EntityType.cs. Trzy role użytkowników w enumie Role.cs
**Uwaga:** Trzeba zabezpieczyć dane wrażliwe. W przyszłości możliwe jest, że trzeba będzie dodać więcej pól.


## 2026-08-19 - design tokeny z brandingu OCWIP (T-15.1)

**Zrobione:** Realna paleta (pomarańcz `#CF4B0F`/`#9F3A0C`, nie niebieski), fonty (Playfair Display przez `next/font/google`, podzbiory `latin`/`latin-ext` pod polskie znaki), odstępy i promienie jako tokeny w `app/globals.css`, logo w `public/`, strona podglądu na `/design-tokens`, kalkulator kontrastu WCAG w `lib/contrast.ts` z testami na parach z researchu.

**Decyzje:** Tryb wysokiego kontrastu nadpisuje te same tokeny (`--color-bg`, `--color-text`, `--color-focus`, `--color-active-*`) pod `[data-contrast="true"]`, zamiast osobnego zestawu klas, więc komponent zbudowany na tokenach dostaje kontrast za darmo. `#CF4B0F` na białym to dokładnie 4.50:1, granica AA, więc niesie tylko duże UI (przyciski, obramowania), mały tekst i linki używają ciemniejszego `#9F3A0C`.

**Uwaga:** T-07 (blokujący) ma jeden otwarty punkt checklisty: logotypy źródeł finansowania konkretnych konkursów, zależne od T-06.1/T-06.2 albo klientki. Nie blokuje tej karty, tokeny nie ich dotyczą.

## 2026-08-18 - szkielet repozytorium

**Zrobione:** Trzy kontenery, które się budują i widzą nawzajem (Next.js z Tailwind, .NET minimal API, PostgreSQL 16), health endpointy z sondą bazy, testy backendu i frontu, smoke test po HTTP, CI na cztery zadania, hook pre-commit pilnujący mapy i myślników, oraz komplet dokumentacji z kontekstem projektu.

**Decyzje:** Dokumentacja po polsku, kod po angielsku, bo dokumentację czyta zespół i strona zamawiająca, która nie pracuje po angielsku. Sesja w ciasteczku HttpOnly, nie token w nagłówku, bo aplikacja jest wyłącznie przeglądarkowa. Mapa repozytorium pilnowana skryptem, nie dobrymi intencjami, bo mapa bez wymuszenia dezaktualizuje się i zaczyna aktywnie wprowadzać w błąd. Encje Ocena, Umowa i Sprawozdanie świadomie niezbudowane, bo nie mamy jeszcze realnych wzorów dokumentów.

**Uwaga:** `db/init/` uruchamia się tylko na pustym wolumenie, więc po zmianie potrzeba `docker compose down -v`. Hook pre-commit nie jest współdzielony przez gita, więc każdy klon potrzebuje `git config core.hooksPath .githooks`. Ochrona gałęzi i domyślna gałąź `dev` to ustawienia GitHuba, których nikt nie zacommituje, a dopóki nie są ustawione, CI jest tylko sugestią.
