# Mapa: frontend

Aplikacja Next.js (App Router, TypeScript, Tailwind CSS). Wzorce: [`../konwencje.md`](../konwencje.md).

| Plik | Co robi |
|---|---|
| `frontend/package.json` | Skrypty `dev`, `build`, `start`, `typecheck`, `api:generate` (klient z OpenAPI, adres nadpisywalny przez `OPENAPI_URL`), `test`. Next 15, React 19, Tailwind 4, Vitest, openapi-typescript |
| `frontend/tsconfig.json` | Tryb strict, alias `@/*` na katalog główny frontu |
| `frontend/next.config.mjs` | `reactStrictMode` plus watch przez polling, bo źródło jest bind mountem i zdarzenia inotify giną |
| `frontend/postcss.config.mjs` | Podpięcie `@tailwindcss/postcss` (Tailwind 4 nie potrzebuje pliku konfiguracyjnego) |
| `frontend/vitest.config.mts` | Vitest z jsdom i pluginem React, alias `@` zgodny z tsconfig |
| `frontend/app/globals.css` | Import Tailwinda plus tokeny w bloku `@theme` (kolory, fonty, odstępy, promienie). Tryb kontrastu przez `[data-contrast="true"]`, nadpisujący te same tokeny. **Jedyne miejsce na kolory**, komponenty ich nie wpisują. Reguły bazowe (`body`, nagłówki, `a`) siedzą w `@layer base`, więc klasa narzędziowa na pojedynczym elemencie je nadpisuje; obramowanie fokusu celowo zostaje poza warstwami |
| `frontend/app/layout.tsx` | Rama aplikacji, metadane, `lang="pl"`, wczytanie fontów `Playfair Display`/`Poppins` przez `next/font/google` (podzbiory `latin` i `latin-ext` pod polskie znaki) |
| `frontend/app/page.tsx` | Strona startowa szkieletu z linkami do sond zdrowia API i do `/design-tokens` |
| `frontend/app/not-found.tsx` | Strona 404 dla całej aplikacji (T-15.4): własny wygląd i powrót na stronę główną, bo trafia tu też osoba niezalogowana |
| `frontend/app/error.tsx` | Granica błędu Next.js, czyli 500 widziane przez użytkownika (T-15.4). **Nie renderuje ani `message`, ani `digest`.** Dwa wyjścia: `reset()` na miejscu i powrót na stronę główną |
| `frontend/app/global-error.tsx` | Ten sam 500 dla awarii samego layoutu głównego: własne `html`, `body` i import `globals.css`, bo zastępuje layout. Wyjściem jest `reset()`, router padł razem z layoutem. Fonty marki nie przeżywają tej awarii (`next/font` woła layout), więc jawne `font-sans` |
| `frontend/app/error-pages.test.tsx` | Testy stron błędów: droga powrotna z 404, brak wycieku treści błędu na 500, ponowienie na miejscu, ten sam ekran przy awarii layoutu |
| `frontend/app/design-tokens/page.tsx` | Podgląd tokenów brandingowych OCWIP (T-15.1): logo, kolory z liczonym na żywo kontrastem WCAG, typografia, odstępy, promienie, przyciski |
| `frontend/app/design-tokens/contrast-toggle.tsx` | Klientowy przełącznik podglądu trybu wysokiego kontrastu, ustawia `data-contrast="true"` na otaczającym `div` |
| `frontend/app/panel/applicant/layout.tsx` | Trasa `/panel/applicant` (cel przekierowania z `/login`): serwerowa obwoluta trzymająca tytuł strony, bo rama panelu jest klientowa i nie może wystawić `metadata` |
| `frontend/app/panel/panel-gate.tsx` | **Strażnik sesji wspólny dla obu paneli** (T-15.2, wydzielony w T-15.3): pyta `GET /me`, brak sesji przekierowuje na `/login` z `returnUrl` niosącym query string, cudza rola dostaje odmowę zamiast przekierowania, padnięty backend osobny komunikat. Na czas pytania rysuje szkielet ramy podany przez panel w `skeleton` (T-15.4), zamiast wyśrodkowanego komunikatu, który przesuwał layout. Wpuszczoną sesję oddaje ramie przez `children(session)`, a ekranowi odmowy dokłada link do własnego panelu odmówionego konta, jeśli taki istnieje. Nie jest kontrolą dostępu, tą jest domyślna odmowa z T-13.2 |
| `frontend/app/panel/panel-skeleton.tsx` | Kształt panelu rysowany, dopóki `GET /me` nie odpowie (T-15.4): te same odstępy co prawdziwy nagłówek, szerokość wiersza z `rowClassName` i po jednym miejscu na każdy realny link z `links`, pasek trybu rezerwowany pustym blokiem przez `modeBar`, zero nazw kont, pulsowanie tylko przy `motion-safe` |
| `frontend/app/panel/panel-skeleton.test.tsx` | Testy szkieletu: komunikat dla czytnika ekranu, dekoracja ukryta przez `aria-hidden`, pasek trybu tylko dla operatora i bez napisu, liczba miejsc w nawigacji równa liczbie linków, brak animacji przy ograniczonym ruchu |
| `frontend/app/panel/navigation.ts` | Kształt pozycji nawigacji (`PanelLink`), `isCurrentLink` z jawnym korzeniem panelu oraz `panelRootForRole`, czyli frontowe lustro `Services/LoginLandingPath.cs`, celowo niepełne (rola bez zbudowanego panelu nie dostaje linku). Listy linków zostają przy panelach |
| `frontend/app/panel/navigation.test.ts` | Testy dopasowania bieżącej trasy, w tym że korzeń jest parametrem, a nie wpisaną na sztywno ścieżką wnioskodawcy |
| `frontend/app/panel/empty-screens.test.tsx` | Test wspólny dla wszystkich siedmiu ekranów obu paneli: każdy ma tytuł, stan pusty z podpowiedzią następnego kroku i nic technicznego w treści |
| `frontend/app/panel/applicant/applicant-panel.tsx` | Rama panelu wnioskodawcy (T-15.2): wpuszcza `PanelGate` z rolą `Applicant`, link "przejdź do treści", nagłówek, `<main id="tresc">` o szerokości formularza |
| `frontend/app/panel/applicant/panel-header.tsx` | Nagłówek panelu: logo, nazwa zalogowanego podmiotu, wylogowanie, nawigacja z `aria-current` na bieżącej pozycji. Dziś żadna ścieżka w produkcie nie przypina podmiotu do konta (R-01), więc w praktyce widać imię i nazwisko z konta |
| `frontend/app/panel/applicant/navigation.ts` | Pozycje nawigacji panelu wnioskodawcy jako dane. **Jedyne miejsce ze ścieżkami tego panelu** |
| `frontend/app/panel/applicant/page.tsx` | Moje wnioski, stan pusty z następnym krokiem prowadzącym do konkursów (lista wniosków to T-34) |
| `frontend/app/panel/applicant/competitions/page.tsx` | Aktualne konkursy, stan pusty na czas między naborami (lista to T-23) |
| `frontend/app/panel/applicant/profile/page.tsx` | Mój profil, stan pusty mówiący, skąd wezmą się dane podmiotu; zawartość czeka na decyzję R-01 |
| `frontend/app/panel/applicant/applicant-panel.test.tsx` | Testy ramy: nazwa podmiotu i wylogowanie w nagłówku, komplet nawigacji, link pomijający przed nagłówkiem, brak sesji na logowanie z `returnUrl`, brak mignięcia panelu, wylogowanie po stronie serwera, odmowa dla operatora, awaria backendu nie jest wylogowaniem |
| `frontend/app/panel/applicant/navigation.test.ts` | Test, że każda pozycja nawigacji zostaje wewnątrz panelu wnioskodawcy |
| `frontend/app/panel/operator/layout.tsx` | Trasa `/panel/operator` (cel przekierowania z `/login` dla roli Operator): serwerowa obwoluta trzymająca tytuł strony, bo rama panelu jest klientowa |
| `frontend/app/panel/operator/operator-panel.tsx` | Rama panelu operatora (T-15.3): wpuszcza `PanelGate` z rolą `Operator`, cudza rola widzi 403 z wyjaśnieniem. `<main id="tresc">` bez ograniczenia szerokości, z `overflow-x-auto`, żeby tabela szersza od okna przewijała się wewnątrz zamiast poszerzać dokument i wywozić przyklejony nagłówek w lewo |
| `frontend/app/panel/operator/operator-header.tsx` | Nagłówek panelu operatora: przyklejony, zaczyna się paskiem trybu na tokenach `active-bg`/`active-text` (przeżywa tryb wysokiego kontrastu), potem logo, "Zalogowano jako", wylogowanie i nawigacja z `aria-current` |
| `frontend/app/panel/operator/navigation.ts` | Pozycje nawigacji panelu operatora jako dane. **Jedyne miejsce ze ścieżkami tego panelu.** Osiem zakładek wewnątrz konkursu to poziom niżej i należy do T-22 |
| `frontend/app/panel/operator/page.tsx` | Konkursy, stan pusty pierwszego dnia pracy z systemem (ogłaszanie konkursu to T-22) |
| `frontend/app/panel/operator/applications/page.tsx` | Wnioski, stan pusty z następnym krokiem prowadzącym do konkursów (lista i statusy to T-35) |
| `frontend/app/panel/operator/forms/page.tsx` | Formularze, stan pusty (kreator formularzy to T-26) |
| `frontend/app/panel/operator/reviewers/page.tsx` | Recenzenci, stan pusty (obsługa recenzentów to T-37) |
| `frontend/app/panel/operator/operator-panel.test.tsx` | Testy ramy operatora: komplet nawigacji, pasek trybu i jego miejsce w kolejności czytania, 403 dla wnioskodawcy i dla recenzenta, brak mignięcia ramy, `returnUrl` z query string, wylogowanie po stronie serwera, awaria backendu nie jest wylogowaniem, link wyjścia dla wnioskodawcy i jego brak dla recenzenta, tabela 120 wierszy razem z ograniczeniem przewijania |
| `frontend/app/panel/operator/navigation.test.ts` | Test, że każda pozycja nawigacji zostaje wewnątrz panelu operatora |
| `frontend/components/empty-state.tsx` | `EmptyState`: tytuł, zdanie o tym, czego brakuje, i opcjonalny następny krok. Używany przez wszystkie puste ekrany obu paneli (T-15.4) |
| `frontend/components/status-page.tsx` | `StatusPage`: komunikat na cały ekran zamiast treści (odmowa, awaria backendu, 404, 500), `aria-live`, droga powrotna w `actions` i wspólny styl tej drogi w `statusActionClassName`. Wydzielony z `panel-notice.tsx` w T-15.4 |
| `frontend/lib/api-schema.ts` | **Generowany**, nie edytuj: typy `paths`, `components` i `operations` z dokumentu OpenAPI backendu. Odtwarzany przez `npm run api:generate` |
| `frontend/lib/api-client.ts` | `apiBaseUrl`, `apiFetch`, `ApiError`, typy `ApiPath`, `ProblemDetails`, `ValidationProblemDetails`, `FieldErrors` z `api-schema.ts`. Jedyne wejście do API: `credentials: "include"` dla ciasteczka sesyjnego, komunikat błędu celowo generyczny, błędy pól z `problem+json` na `ApiError.fieldErrors`, puste ciało (202, 204) zwracane jako `undefined` |
| `frontend/lib/api-client.test.ts` | Testy klienta: fallback adresu API, wysyłanie poświadczeń, brak wycieku ciała odpowiedzi do komunikatu błędu, błędy walidacji przypięte do pól, odpowiedź bez ciała |
| `frontend/lib/session.ts` | `fetchCurrentUser` (401 to brak sesji, nie awaria), `logout` (błąd przełknięty, żeby dało się opuścić ekran), `accountLabel`, typy `CurrentUser` i `Role`. Jedyne miejsce czytające `GET /me` |
| `frontend/lib/session.test.ts` | Testy sesji: żywa sesja, 401 jako brak sesji, 500 nadal wyjątkiem, wylogowanie po stronie serwera i jego odporność na błąd, etykieta konta |
| `frontend/lib/contrast.ts` | `relativeLuminance`, `contrastRatio`, `meetsAA` - kalkulator kontrastu WCAG 2.1 użyty do weryfikacji tokenów narzędziem, nie ręcznie |
| `frontend/lib/contrast.test.ts` | Testy kalkulatora kontrastu na parach kolorów z researchu brandingu (karta T-07) |

`frontend/public/` trzyma statyczne pliki (na przykład `ocwip-logo.svg`, T-07/T-15.1), poza zakresem `scripts/check_map.py` razem z resztą frontu, bo to nie kod źródłowy.

## Czego tu jeszcze nie ma

`components/`, ekran logowania (brak karty, `R-25` w [`../runbook/rozbieznosci.md`](../runbook/rozbieznosci.md)), panel recenzenta (T-40, zablokowany przez B-02), stany puste i błędów. Każde poza ekranem logowania ma kartę na Trello. Katalogów nie zakładamy na zapas.
