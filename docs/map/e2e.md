# Mapa: test w przeglądarce (e2e)

Osobny projekt Node obok aplikacji (T-100): Playwright przechodzi cały proces na postawionym stosie, `docker compose --profile test` z Mailpitem. Uruchamianie i zmienne w [`../testy.md`](../testy.md), sekcja "Test w przeglądarce".

| Plik | Co robi |
|---|---|
| `e2e/package.json` | Zależności testu: `@playwright/test`, TypeScript; `npm test` uruchamia Playwrighta, `npm run typecheck` sprawdza typy |
| `e2e/tsconfig.json` | TypeScript bez emisji, typy Node |
| `e2e/playwright.config.ts` | Jeden worker, limit 4 minut na scenariusz, nagranie wideo, ślad i zrzuty przy porażce; adres z `E2E_BASE_URL`, kanał przeglądarki z `E2E_BROWSER_CHANNEL` (lokalnie `chrome`, gdy nie da się pobrać Chromium) |
| `e2e/lib/env.ts` | Adresy API (`E2E_API_URL`) i Mailpita (`E2E_MAILPIT_URL`) oraz przyrostek przebiegu `run`, dzięki któremu scenariusz działa też na niepustej bazie |
| `e2e/lib/mailpit.ts` | `waitForMail` czyta najnowszy mail do adresu o danym temacie z API Mailpita, `linkTo` wyciąga z treści link do ścieżki serwisu |
| `e2e/lib/admin.ts` | `admin(...)`: komenda administracyjna (`grant-role`, `import-content`) w kontenerze backendu przez zbudowany `Ocwip.Api.dll`, bez drugiego builda obok `dotnet watch` |
| `e2e/lib/accounts.ts` | `registerAndVerify` (formularz rejestracji z obiema zgodami, link z maila weryfikacyjnego) i `signIn` przez ekran logowania |
| `e2e/fixtures/answers-2026.ts` | Pełny wniosek na formularzu 2026 dla organizacji i grupy nieformalnej, te same odpowiedzi co `Application2026FormTests` |
| `e2e/tests/submission.spec.ts` | Scenariusz T-100: operator z rejestracji i `grant-role`, konkurs przez API, treść przez `import-content`, publikacja, dwóch wnioskodawców (karta, wniosek, załącznik, złożenie na ekranie, mail z potwierdzeniem), lista wniosków operatora; bez SQL z boku |
