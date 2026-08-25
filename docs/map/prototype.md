# Mapa: prototyp wizualny

Samodzielna aplikacja Next.js pokazująca **kierunek C ("prowadzenie za rękę")** na dziewięciu ekranach ścieżki wnioskodawcy. Powstała z artefaktu z makietami, żeby dało się klikać po ekranach w przeglądarce, a nie tylko oglądać obrazki.

**To nie jest część produktu.** Nie ma backendu, nie ma klienta API, nie stoi w `docker-compose.yml` i nie wchodzi do CI. Uzasadnienie w [`../architektura.md`](../architektura.md), sekcja "Prototyp wizualny obok produktu".

Uruchomienie: `cd prototype && npm install && npm run dev`, potem `http://localhost:3100`. Port 3100, żeby nie zderzał się z frontem produktu na 3000.

| Plik | Co robi |
|---|---|
| `prototype/package.json` | Skrypty `dev`, `build`, `start`, `typecheck`. Te same wersje Next, React i Tailwinda co front produktu, bez zależności testowych |
| `prototype/tsconfig.json` | Tryb strict, alias `@/*` na katalog główny prototypu |
| `prototype/next.config.mjs` | Wyłącznie `reactStrictMode`. Bez pollingu watchera, bo prototyp nie stoi w kontenerze |
| `prototype/postcss.config.mjs` | Podpięcie `@tailwindcss/postcss` |
| `prototype/app/globals.css` | Tokeny w bloku `@theme`, skopiowane z `frontend/app/globals.css`. **Jedyne miejsce na kolory.** Bez bloku trybu wysokiego kontrastu, prototyp pokazuje wyłącznie paletę zwykłą |
| `prototype/app/layout.tsx` | Rama prototypu: fonty `Playfair Display`/`Poppins` (podzbiory `latin`, `latin-ext`), `lang="pl"`, boczna nawigacja plus obszar ekranu |
| `prototype/app/page.tsx` | Przegląd: lista dziewięciu ekranów z opisem i oznaczeniem, które są klikalne |
| `prototype/app/competition/page.tsx` | Ekran 1, strona konkursu: warunki naboru, termin, limit, zapowiedź sześciu kroków |
| `prototype/app/registration/page.tsx` | Ekran 2, rejestracja podmiotu: trzy typy podmiotu, NIP zależny od typu, komunikat nieujawniający istnienia konta |
| `prototype/app/applications/page.tsx` | Ekran 3, lista wniosków wnioskodawcy: statusy, historia, dwie oferty w tym samym konkursie |
| `prototype/app/wizard/page.tsx` | Ekran 4, trasa kreatora. Sam renderuje komponent klientowy |
| `prototype/app/wizard/application-wizard.tsx` | Klientowa powłoka kreatora: stan kroku, licznik znaków, wskaźnik autozapisu, panel boczny z terminem i limitem |
| `prototype/app/wizard/wizard-steps.tsx` | `WIZARD_STEPS` (etykiety, pytania, podpowiedzi) plus `WizardStepBody` z treścią sześciu kroków |
| `prototype/app/mobile/page.tsx` | Ekran 5, ten sam krok kreatora w ramce 390 na 844. Bez rysowanego paska stanu i klawiatury |
| `prototype/app/budget/page.tsx` | Ekran 6, trasa budżetu. Sam renderuje komponent klientowy |
| `prototype/app/budget/budget-editor.tsx` | Klientowy edytor budżetu: edycja kwot, suma, wskaźnik limitu, komunikat wskazujący konkretną pozycję |
| `prototype/app/attachments/page.tsx` | Ekran 7, załączniki: wymagane i nieobowiązkowe, pole zrzutu plików, nota o retencji |
| `prototype/app/submission/page.tsx` | Ekran 8, podsumowanie i złożenie: przegląd sześciu kroków, odcięcie terminu, oświadczenie |
| `prototype/app/confirmation/page.tsx` | Ekran 9, potwierdzenie: numer oferty, godzina co do minuty, eksport PDF, co dalej |
| `prototype/components/AppBar.tsx` | Górna belka ekranu: logo, kontekst, konto. Wariant `compact` na ekran telefonu |
| `prototype/components/ScreenFrame.tsx` | Obramowanie, które sprawia, że trasa czyta się jako jeden ekran, a nie jako cała strona |
| `prototype/components/ScreenNav.tsx` | Klientowa nawigacja po ekranach, podświetla aktywną trasę przez `usePathname` |
| `prototype/components/StatusChip.tsx` | Znacznik statusu w trzech wariantach: `accent`, `muted`, `solid` |
| `prototype/components/Stepper.tsx` | Pasek postępu sześciu kroków. Bez `onSelect` jest tylko wskaźnikiem, z `onSelect` przełącza kroki |
| `prototype/lib/screens.ts` | `SCREENS`, `screenBySlug`. Jedna lista ekranów zasilająca nawigację i przegląd |
| `prototype/lib/budget.ts` | `parseAmount`, `formatAmount`, `summarise`. Arytmetyka limitu wyjęta z komponentu, żeby reguła była czytelna |

`prototype/public/` trzyma `ocwip-logo.svg`, kopię pliku z `frontend/public/`, poza zakresem `scripts/check_map.py` tak samo jak reszta statycznych plików.

## Czego tu nie ma i nie będzie

Testów, klienta API, uwierzytelniania, widoków operatora i recenzenta. Ekrany oceny, umowy i sprawozdania nie powstały, bo nie mamy wzorów tych dokumentów. Prototyp ma pokazywać wygląd, a każda dołożona tu logika to logika, której nikt nie przetestuje.
