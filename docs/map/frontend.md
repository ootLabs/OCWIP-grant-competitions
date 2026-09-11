# Mapa: frontend

Aplikacja Next.js (App Router, TypeScript, Tailwind CSS). Wzorce: [`../konwencje.md`](../konwencje.md).

| Plik | Co robi |
|---|---|
| `frontend/package.json` | Skrypty `dev`, `build`, `start`, `typecheck`, `api:generate` (klient z OpenAPI, adres nadpisywalny przez `OPENAPI_URL`), `test`. Next 15, React 19, Tailwind 4, Vitest, openapi-typescript |
| `frontend/tsconfig.json` | Tryb strict, alias `@/*` na katalog główny frontu |
| `frontend/next.config.mjs` | `reactStrictMode` plus watch przez polling, bo źródło jest bind mountem i zdarzenia inotify giną |
| `frontend/postcss.config.mjs` | Podpięcie `@tailwindcss/postcss` (Tailwind 4 nie potrzebuje pliku konfiguracyjnego) |
| `frontend/vitest.config.mts` | Vitest z jsdom i pluginem React, alias `@` zgodny z tsconfig |
| `frontend/app/globals.css` | Import Tailwinda plus tokeny w bloku `@theme` (kolory, fonty, odstępy, promienie). Tryb kontrastu przez `[data-contrast="true"]`, nadpisujący te same tokeny. **Jedyne miejsce na kolory**, komponenty ich nie wpisują |
| `frontend/app/layout.tsx` | Rama aplikacji, metadane, `lang="pl"`, wczytanie fontów `Playfair Display`/`Poppins` przez `next/font/google` (podzbiory `latin` i `latin-ext` pod polskie znaki) |
| `frontend/app/page.tsx` | Strona startowa szkieletu z linkami do sond zdrowia API i do `/design-tokens` |
| `frontend/app/design-tokens/page.tsx` | Podgląd tokenów brandingowych OCWIP (T-15.1): logo, kolory z liczonym na żywo kontrastem WCAG, typografia, odstępy, promienie, przyciski |
| `frontend/app/design-tokens/contrast-toggle.tsx` | Klientowy przełącznik podglądu trybu wysokiego kontrastu, ustawia `data-contrast="true"` na otaczającym `div` |
| `frontend/lib/api-schema.ts` | **Generowany**, nie edytuj: typy `paths`, `components` i `operations` z dokumentu OpenAPI backendu. Odtwarzany przez `npm run api:generate` |
| `frontend/lib/api-client.ts` | `apiBaseUrl`, `apiFetch`, `ApiError`, typy `ApiPath`, `ProblemDetails`, `ValidationProblemDetails`, `FieldErrors` z `api-schema.ts`. Jedyne wejście do API: `credentials: "include"` dla ciasteczka sesyjnego, komunikat błędu celowo generyczny, błędy pól z `problem+json` na `ApiError.fieldErrors`, puste ciało (202, 204) zwracane jako `undefined` |
| `frontend/lib/api-client.test.ts` | Testy klienta: fallback adresu API, wysyłanie poświadczeń, brak wycieku ciała odpowiedzi do komunikatu błędu, błędy walidacji przypięte do pól, odpowiedź bez ciała |
| `frontend/lib/contrast.ts` | `relativeLuminance`, `contrastRatio`, `meetsAA` - kalkulator kontrastu WCAG 2.1 użyty do weryfikacji tokenów narzędziem, nie ręcznie |
| `frontend/lib/contrast.test.ts` | Testy kalkulatora kontrastu na parach kolorów z researchu brandingu (karta T-07) |

`frontend/public/` trzyma statyczne pliki (na przykład `ocwip-logo.svg`, T-07/T-15.1), poza zakresem `scripts/check_map.py` razem z resztą frontu, bo to nie kod źródłowy.

## Czego tu jeszcze nie ma

`components/`, panel wnioskodawcy, panel operatora, ochrona tras, stany puste i błędów. Każde ma kartę na Trello. Katalogów nie zakładamy na zapas.
