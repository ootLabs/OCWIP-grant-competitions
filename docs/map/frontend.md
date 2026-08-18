# Mapa: frontend

Aplikacja Next.js (App Router, TypeScript, Tailwind CSS). Wzorce: [`../konwencje.md`](../konwencje.md).

| Plik | Co robi |
|---|---|
| `frontend/package.json` | Skrypty `dev`, `build`, `start`, `typecheck`, `test`. Next 15, React 19, Tailwind 4, Vitest |
| `frontend/tsconfig.json` | Tryb strict, alias `@/*` na katalog główny frontu |
| `frontend/next.config.mjs` | `reactStrictMode` plus watch przez polling, bo źródło jest bind mountem i zdarzenia inotify giną |
| `frontend/postcss.config.mjs` | Podpięcie `@tailwindcss/postcss` (Tailwind 4 nie potrzebuje pliku konfiguracyjnego) |
| `frontend/vitest.config.mts` | Vitest z jsdom i pluginem React, alias `@` zgodny z tsconfig |
| `frontend/app/globals.css` | Import Tailwinda plus tokeny w bloku `@theme` (kolory, `--color-brand-*`) i globalny `:focus-visible`. **Jedyne miejsce na kolory**, komponenty ich nie wpisują |
| `frontend/app/layout.tsx` | Rama aplikacji, metadane, `lang="pl"` (wymowa w czytnikach ekranu i dzielenie wyrazów) |
| `frontend/app/page.tsx` | Strona startowa szkieletu z linkami do sond zdrowia API |
| `frontend/lib/api-client.ts` | `apiBaseUrl`, `apiFetch`, `ApiError`. Jedyne wejście do API: `credentials: "include"` dla ciasteczka sesyjnego, komunikat błędu celowo generyczny |
| `frontend/lib/api-client.test.ts` | Testy klienta: fallback adresu API, wysyłanie poświadczeń, brak wycieku ciała odpowiedzi do komunikatu błędu |

## Czego tu jeszcze nie ma

`components/`, panel wnioskodawcy, panel operatora, ochrona tras, stany puste i błędów, typowany klient generowany z OpenAPI. Każde ma kartę na Trello. Katalogów nie zakładamy na zapas.
