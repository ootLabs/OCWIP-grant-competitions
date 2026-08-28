# Konwencje

> Zdecyduj raz, trzymaj się zawsze. Spójność wygrywa z osobistymi preferencjami.

Przypomnienie z [`AGENTS.md`](../AGENTS.md): kod po angielsku, dokumentacja i UI po polsku, zero myślników typograficznych w całym repozytorium.

## Nazewnictwo

| Rzecz | Konwencja | Przykład |
|---|---|---|
| Pliki i typy C# | `PascalCase` | `HealthEndpoints.cs` |
| Metody i właściwości C# | `PascalCase` | `MapHealthEndpoints` |
| Pola prywatne C# | `_camelCase` | `_factory` |
| Zmienne lokalne C# | `camelCase` | `connectionString` |
| Pliki TypeScript (nie komponenty) | `kebab-case` | `api-client.ts` |
| Komponenty React (plik i symbol) | `PascalCase` | `CompetitionList.tsx` |
| Funkcje i zmienne TS | `camelCase` | `apiFetch` |
| Stałe (oba języki) | `UPPER_SNAKE` | `NEXT_PUBLIC_API_URL` |
| Tabele i kolumny w bazie | `snake_case`, tabele w liczbie mnogiej | `applications.created_at` |
| Gałęzie | `type/kebab-case` | `feat/competition-endpoint` |
| Ścieżki API | małe litery, rzeczowniki w liczbie mnogiej, dywizy | `/api/form-definitions` |

Nazwy domenowe biorą się ze [`slownik.md`](slownik.md), nie z wyobraźni. "Oferta" w UI to `application` w kodzie, konsekwentnie.

## Struktura folderów

**Backend, warstwowo, jeden plik na domenę.**

```
backend/
  Ocwip.slnx
  src/Ocwip.Api/
    Program.cs        wyłącznie składanie aplikacji: middleware, endpointy. Zero logiki biznesowej.
    Endpoints/        warstwa HTTP: request, response, walidacja wejścia. Zero logiki biznesowej.
    Services/         logika biznesowa (tworzymy przy pierwszym serwisie)
    Models/           encje domenowe i enumy
    Data/             DbContext i migracje (tworzymy w karcie T-11.1)
    Contracts/        modele request i response wystawiane na zewnątrz
  tests/Ocwip.Api.Tests/
```

Endpoint woła serwis, serwis używa encji. Nigdy odwrotnie i nigdy endpoint sięgający do bazy poza sondą zdrowia.

**Frontend, Next.js App Router, feature first.**

```
frontend/
  app/
    layout.tsx        rama aplikacji, metadane
    page.tsx          strona startowa
    <feature>/        jeden folder na trasę funkcjonalną
  components/         komponenty współdzielone (tworzymy, gdy coś jest użyte drugi raz)
  lib/                klient API, funkcje pomocnicze
```

Nie twórz pustych folderów na zapas. Zakładasz je razem z pierwszym plikiem i dopisujesz wiersz do [`map/`](map/README.md).

## Wzorce

- **Konfiguracja:** każde ustawienie pochodzi ze zmiennej środowiskowej, czytanej przez `IConfiguration` (backend) albo `process.env.NEXT_PUBLIC_*` (front). Zero magicznych wartości w kodzie. Każda nowa zmienna ląduje w `.env.example` z bezpiecznym placeholderem.
- **Autoryzacja:** polityki oparte o wymagania i handlery, zebrane w jednym miejscu. Nie warunki rozsypane po endpointach. Dostęp do wniosku zależy nie tylko od roli, ale od tego, czyj jest ten wniosek, a dwóch wnioskodawców ma tę samą rolę i różne uprawnienia do tych samych zasobów.
- **Błędy:** ProblemDetails na brzegu API. Serwis rzuca wyjątek domenowy, warstwa HTTP go tłumaczy. Nigdy 200 z błędem w ciele. Odmowa dostępu to 403, nie 500 i nie pusta strona.
- **Komunikaty błędów:** bez treści technicznych. Użytkownikami są organizacje pozarządowe i osoba, która sama mówi, że nie zna się na technikaliach. Stos wywołań nikomu nie pomoże, a ujawnia strukturę aplikacji.
- **Walidacja:** na brzegu API. Za warstwą HTTP dane są uznane za poprawne.
- **Style:** wyłącznie Tailwind CSS plus tokeny w `app/globals.css`. Zero kolorów wpisanych na sztywno w komponent, zero osobnych plików CSS na komponent. Zmiana decyzji o kolorze ma być jedną edycją.
- **Jeden sposób na jedną rzecz.** Znalazłeś dwa sposoby w kodzie? Wybierz jeden, przerób drugi, zacommituj osobno.

## Styl kodu

- Małe pliki, jedna odpowiedzialność. Dwie role albo powyżej około 300 linii, więc dziel.
- Komentarz tłumaczy **dlaczego**, nigdy **co**. Kod mówi, co robi.
- Ostrzeżenia kompilatora są traktowane jak błędy (`TreatWarningsAsErrors`). Ostrzeżenie, którego nikt nie naprawia, to ostrzeżenie, które wszyscy ignorują.
- Pola trzymające dane wrażliwe (PESEL, NIP, adres) oznaczamy komentarzem w kodzie, żeby przy szyfrowaniu nikt niczego nie przeoczył.
- Sprawdź [`map/`](map/README.md), zanim zbudujesz. To może już istnieć, a mapa znajdzie to szybciej niż wyszukiwanie.
