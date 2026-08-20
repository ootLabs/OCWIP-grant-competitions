# Branding OCWIP - research pod UI

**Stan: 6/7 kryteriów karty T-07 zamkniętych, tokeny wpięte w kodzie w T-15.1.** Pełny wynik i uzasadnienie: Notion, `ootLabs / OCWIP / Research / Branding OCWIP`. Ten plik to tylko lokalny wskaźnik, nie duplikuje treści.

Jedyny otwarty punkt karty T-07: logotypy źródeł finansowania konkretnych, aktualnie prowadzonych konkursów (nie logotypy widoczne na samej stronie ocwip.pl, to już sprawdzone). Zależne od T-06.1/T-06.2 (Witkac) albo bezpośredniego pytania do klientki, wraca przy T-32 (generowanie umów). Nie blokuje T-15.1.

Aplikacja ma wyglądać jak część OCWIP, bo wnioskodawca trafi do niej ze strony ocwip.pl, klikając w menu pozycję "Generator wniosków". Ma kliknąć i nie poczuć, że wylądował gdzie indziej.

## Co już wiemy

- Pełna nazwa: Opolskie Centrum Wspierania Inicjatyw Pozarządowych.
- Hasło: "Istniejemy, by pomagać".
- **Paleta: dominanta pomarańczowo-biała** (`#CF4B0F`/`#9F3A0C` na białym). Wcześniejsza notatka w tym pliku i na Trello mówiła "niebiesko-biała", to było niedokładne: jedyny niebieski w arkuszu CSS to domyślny kolor frameworka Bootstrap, nie świadomy wybór marki. Poprawione po bezpośredniej analizie CSS motywu, patrz Notion.
- Strona ma tryb wysokiego kontrastu.

## Dlaczego tryb wysokiego kontrastu jest ważny

Skoro sami go u siebie wdrożyli, dostępność jest u nich świadomym tematem, a nie dodatkiem. Do tego są organizacją publiczną wydającą środki publiczne, więc zgodność z WCAG prawdopodobnie okaże się wymogiem formalnym, a nie naszą dobrą wolą. Taniej jest wbudować to teraz niż poprawiać na koniec.

## Tokeny w kodzie

Wdrożone w `frontend/app/globals.css` (blok `@theme` plus `[data-contrast="true"]`), podgląd pod trasą `/design-tokens`. Kontrast WCAG 2.1 AA zweryfikowany narzędziem, patrz `frontend/lib/contrast.ts` i jego testy.
