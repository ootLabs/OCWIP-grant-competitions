# Mapa: test w przeglądarce (e2e)

Osobny projekt Node obok aplikacji (T-100): Playwright przechodzi cały proces na postawionym stosie, `docker compose --profile test` z Mailpitem. Uruchamianie i zmienne w [`../testy.md`](../testy.md), sekcja "Test w przeglądarce".

| Plik | Co robi |
|---|---|
| `e2e/package.json` | Zależności testu: `@playwright/test`, TypeScript; `npm test` uruchamia Playwrighta, `npm run typecheck` sprawdza typy |
| `e2e/tsconfig.json` | TypeScript bez emisji, typy Node |
| `e2e/playwright.config.ts` | Jeden worker, limit 4 minut na scenariusz, nagranie wideo, ślad i zrzuty przy porażce; adres z `E2E_BASE_URL`, kanał przeglądarki z `E2E_BROWSER_CHANNEL` (lokalnie `chrome`, gdy nie da się pobrać Chromium); `E2E_IGNORE_HTTPS_ERRORS` i `E2E_HOST_RULES` do przebiegu przez Caddy compose produkcyjnego (T-112) |
| `e2e/lib/env.ts` | Adresy API (`E2E_API_URL`) i Mailpita (`E2E_MAILPIT_URL`) oraz przyrostek przebiegu `run`, dzięki któremu scenariusz działa też na niepustej bazie |
| `e2e/lib/mailpit.ts` | `waitForMail` czyta najnowszy mail do adresu o danym temacie z API Mailpita, `linkTo` wyciąga z treści link do ścieżki serwisu, `plainSpaces` zamienia spacje nierozdzielające z kwot (`PolishNumbers.Amount`) na zwykłe, żeby asercja mogła zapisać kwotę tak, jak się ją czyta |
| `e2e/lib/admin.ts` | `admin(...)`: komenda administracyjna (`grant-role`, `import-content`) w kontenerze backendu przez zbudowany `Ocwip.Api.dll`, bez drugiego builda obok `dotnet watch`; `E2E_COMPOSE_ARGS` i `E2E_ADMIN_DLL` dla compose produkcyjnego (T-114) |
| `e2e/lib/accounts.ts` | `registerAndVerify` (formularz rejestracji z obiema zgodami i powtórzeniami adresu oraz hasła z T-124, po etykiecie dokładnej, bo "Adres e-mail" łapie też "Powtórz adres e-mail"; link z maila weryfikacyjnego) i `signIn` przez ekran logowania |
| `e2e/lib/api.ts` | Wspólne klocki scenariusza: `person` (adres przebiegu), `minute`, `json` (odpowiedź albo porażka z treścią serwera), `pdf` (najmniejszy PDF), `onScreen` (krok na ekranie ponawiany do skutku) i typ `Submitted`; `newContext` i `cspViolations` (T-112): każda przeglądarka scenariusza zbiera to, co polityka CSP zablokowała, a `forgetCspViolations` przed każdym testem przypisuje naruszenie testowi, który je spowodował |
| `e2e/fixtures/answers-2026.ts` | Pełny wniosek na formularzu 2026 dla organizacji i grupy nieformalnej, te same odpowiedzi co `Application2026FormTests` |
| `e2e/tests/process.spec.ts` | Scenariusz T-100, T-100a i T-100b w jednym teście: operator z rejestracji i `grant-role`, konkurs z pulą przez API, treść przez `import-content`, publikacja, potem kroki z `steps/` po kolei; na końcu zamyka przeglądarki wnioskodawców; na końcu oczekuje zera naruszeń CSP (T-112) |
| `e2e/tests/public-pages.spec.ts` | T-112: strony publiczne (od T-121 także deklaracja dostępności i strony informacyjne) pod CSP bez żadnej konfiguracji danych, więc chodzi też na compose produkcyjnym przez Caddy; nagłówki na każdej stronie, przełącznik kontrastu dowodzi, że skrypty działają, zero naruszeń |
| `e2e/tests/restore-check.spec.ts` | T-114, tylko przy `E2E_RESTORE_CHECK=1`: po `scripts/restore.sh` grupa z tego samego przebiegu (`E2E_RUN`) loguje się i otwiera załącznik, PDF wniosku i podpisaną umowę, więc klucze i pliki przetrwały razem z bazą |
| `e2e/steps/submission.ts` | `submit`: wnioskodawca od rejestracji przez kartę podmiotu, wniosek 2026 i załącznik do złożenia na ekranie i maila z potwierdzeniem; przeglądarka zostaje otwarta na dalsze kroki |
| `e2e/steps/evaluation.ts` | `evaluate` (T-100a): ocena formalna, dwóch ekspertów z deklaracją na ekranie i kartą merytoryczną, kwota dla organizacji, zamknięcie naboru, ocena i zatwierdzenie na ekranie, maile o wynikach, publiczna lista z dofinansowanym i rezerwowym |
| `e2e/steps/contract.ts` | `contractAndResignation` (T-100b): umowa organizacji widoczna u wnioskodawcy, rezygnacja potwierdzona na ekranie, dofinansowanie grupy z listy rezerwowej na ekranie, umowa grupy z członkami z wniosku, podpisanie zapisane na ekranie, stany `Resigned` i `ContractSigned` |
