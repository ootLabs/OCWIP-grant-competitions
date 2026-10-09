# Przejście ręczne GUI: przebieg 4

Wyniki przebiegu scenariusza z [`przejscie-gui.md`](przejscie-gui.md), wykonanego od nowa, bez opierania się na krokach zapisanych w poprzednich przebiegach.

Osobny plik na prośbę prowadzącego przebieg. Dziennikiem kanonicznym pozostaje [`przejscie-gui-bledy.md`](przejscie-gui-bledy.md) i stoi w nim odsyłacz tutaj; przebiegi 1 do 3 są tam i nie są ruszane.

| Pozycja | Wartość |
|---|---|
| Data | 2026-10-07 (krok 0), 2026-10-08 (ścieżki A do N) |
| Gałąź | `dev`, commit `f2eb0ae` |
| Stos | lokalny, `docker compose`, front `localhost:3000`, API `localhost:8080` |
| Przeglądarka | Chrome 152 na Linuksie, sterowany rozszerzeniem Claude in Chrome |
| Baza | wyczyszczona przed przebiegiem (wolumeny `postgres-data`, `attachments-data`, `dataprotection-keys` usunięte), migracje wykonane od zera |
| Zakres | ścieżki 0 do E (2026-10-07 i 08), F do N (2026-10-08, na prośbę po raporcie z 0 do E) |
| Numeracja znalezisk | `P4-01` i dalej |

---

## Stan przejścia

| Ścieżka | Stan | Uwaga |
|---|---|---|
| 0 przygotowanie stosu i kont | przeszło | patrz niżej |
| A gość | przeszło z uwagami | P4-02, P4-03 |
| B konto | przeszło z uwagami | P4-04, P4-05, P4-06, P4-10; B6 wykonane po ścieżce C, gdy zeszła blokada z B5 |
| C operator zakłada konkurs | przeszło z uwagami | konkurs 1/2026 opublikowany; P4-07, P4-08, P4-09, obserwacje O-01 do O-05 |
| D wniosek organizacji | przeszło z uwagami | wniosek 001 złożony; P4-11 do P4-15, obserwacje O-06 do O-09 |
| E wniosek grupy nieformalnej | przeszło z uwagami | wniosek 002 złożony; P4-16, O-10; P4-08 potwierdzone na formularzu wnioskodawcy |
| F nabór | przeszło z uwagami | O-11 |
| G ocena formalna | przeszło z uwagami | P4-17 |
| H eksperci | przeszło z uwagami | P4-18, O-12, O-13 |
| I wyniki | przeszło z uwagami | P4-19, O-15 |
| J umowa | przeszło z uwagami | P4-20, O-14 |
| K rezygnacja | przeszło z uwagami | O-16 |
| L sprawozdanie | przeszło z uwagami | O-17, O-18 |
| M terminy | przeszło z uwagami | O-19 |
| N izolacja | przeszło z uwagami | P4-21 |

---

## Karta wyniku

```
[x] 0  stos wstaje, health 200, pięć kont z create_test_users.py
[x] A  strony publiczne i przełącznik kontrastu
[x] B1 rejestracja z ekranu, obie zgody, ta sama odpowiedź dla zajętego adresu
[x] B2 potwierdzenie adresu, zepsuty link odmawia czytelnie
[x] B3 logowanie, wylogowanie, ochrona panelu
[x] B4 reset hasła, ta sama odpowiedź dla nieistniejącego adresu
[x] B5 blokada po pięciu nieudanych próbach
[x] B6 zmiana hasła i adresu z potwierdzeniem na nowy adres
[x] C1 kreator konkursu, siedem kroków, zapis w trakcie
[x] C2 import treści startowej, powtórzenie nic nie psuje
[x] C3 formularz w kreatorze i podgląd
[x] C4 wzór załącznika do pobrania u gościa
[x] C5 publikacja, lista braków, publiczna strona konkursu
[x] D1 wejście z publicznej strony i powrót po zalogowaniu
[x] D2 karta podmiotu
[x] D3 cztery części, autozapis, liczby liczone, limity blokują
[x] D4 załącznik, podmiana, odmowa złego formatu
[x] D5 lista braków z odnośnikami, jedno potwierdzenie, numer, PDF, mail
[x] E  grupa nieformalna: pola warunkowe i złożenie
[x] F1 lista wniosków, kolumny, sumy, eksporty, wyszarzone zakładki
[x] F2 zwrot do poprawy, tylko wskazane sekcje, dwie sumy kontrolne
[x] G  ocena formalna obu wniosków
[x] H1 ustawienia oceny
[x] H2 przypisanie dwóch ekspertów do obu wniosków
[x] H3 deklaracja przed treścią, dwie karty merytoryczne, zakończenie
[x] I1 zamknięcie naboru odcina składanie
[x] I2 ranking, kwota wprost na liście, stan puli, eksporty
[x] I3 zatwierdzenie wyników i maile, bez duplikatów
[x] I4 udostępnienie kart widoczne u wnioskodawcy
[x] I5 publiczne wyniki i archiwum
[x] J  wzór umowy, sporządzenie, PDF u wnioskodawcy, ZIP, data podpisania
[x] K  rezygnacja i przejście środków na listę rezerwową
[x] L  sprawozdanie: złożenie, zwrot, przyjęcie, rozliczenie
[x] M  odcięcie terminu i przypomnienie trzy dni przed końcem
[x] N  wszystkie próby dostępu zakończone odmową, log bez haseł i danych
```

---

## Ścieżka 0. Przygotowanie stosu i kont

**Stan: przeszło.**

Baza wyczyszczona celowo, bo scenariusz zakłada pusty stan w ścieżce A, a lokalna baza niosła dane z przebiegów 1 do 3 (17 kont, 5 konkursów, złożone wnioski, oceny, umowy). Usunięte zostały trzy wolumeny danych tego projektu; wolumeny cache budowania (`backend-bin`, `backend-obj`) zostawione, a wolumeny innych projektów na tej maszynie (`ocwip-ui_*`, `ocwip-gui_*`) nietknięte.

| Sprawdzenie | Wynik |
|---|---|
| `docker compose up -d` | db, backend i frontend wstały |
| `GET /health/db` | 200 |
| `GET /` na froncie | 200 |
| Migracje na pustej bazie | 29 tabel w schemacie `public` |
| Wiersze w `users`, `competitions`, `applications` | 0, 0, 0 przed założeniem kont |
| `create_test_users.py` | pięć kont założonych przez prawdziwy przepływ (rejestracja, link z logu, potwierdzenie, `grant-role`) |

Konta po skrypcie, wszystkie z potwierdzonym adresem i aktywne:

| Adres | Rola |
|---|---|
| `operator@example.org` | Operator |
| `wnioskodawca@example.org` | Applicant |
| `grupa@example.org` | Applicant |
| `recenzent@example.org` | Reviewer |
| `recenzent2@example.org` | Reviewer |

### Znalezisko P4-01: `scripts/seed.py` nie startuje na pustej bazie

Nie jest to część karty wyniku, ale wyszło przy przygotowaniu stosu i dotyczy dokładnie tego kroku.

- **Co zrobiono:** `python scripts/seed.py` na stosie ze świeżą bazą.
- **Co się stało:** skrypt kończy się kodem 1 i komunikatem `FAIL: the database has tables this script does not know about: report_versions. Add them to TABLES, otherwise they are never checked for emptiness`.
- **Co miało się stać:** baza miała dostać minimalny zestaw wierszy do pracy.
- **Przyczyna:** migracja dodała tabelę `report_versions`, a krotka `TABLES` w `scripts/seed.py:57` jej nie wymienia. Strażnik nieznanych tabel odpala się przed sprawdzeniem pustości, więc blokuje każde uruchomienie, nie tylko na niepustej bazie.
- **Skutek dla przebiegu:** żaden, Krok 0 scenariusza używa `create_test_users.py`, nie `seed.py`. Zgłoszone, bo `seed.py` jest w `README.md` jako droga na start pracy z repozytorium.

---

## Ścieżka A. Gość

**Stan: przeszło z uwagami.** Wszystkie strony bez konta, w konsoli zero błędów i ostrzeżeń (jedyny wpis to informacja React DevTools z trybu deweloperskiego).

| Krok | Wynik |
|---|---|
| A1 `/` | stoi, wyjaśnia, czym jest serwis, prowadzi do konkursów i archiwum; sekcja "Otwarte nabory" mówi "Nie ma teraz otwartego naboru" |
| A2 `/competitions` | pusty stan po polsku: "Nie ma teraz ogłoszonego konkursu" z wyjaśnieniem, kiedy się pojawi |
| A3 `/archive` | pusty stan: "Nie ma jeszcze rozstrzygniętych konkursów" |
| A4 strony informacyjne | `/regulamin`, `/klauzula-informacyjna`, `/kontakt`, `/deklaracja-dostepnosci` stoją; wartości robocze oznaczone jawnie ("do uzupełnienia przez OCWIP", "Wersja robocza, do zatwierdzenia") |
| A4 deklaracja, twierdzenia o faktach | link "Przejdź do treści" jest pierwszym elementem pod klawiszem Tab (sprawdzone); axe po każdym teście interfejsu jest prawdą (globalny `afterEach` w `frontend/vitest.setup.ts`), z zastrzeżeniem w P4-03 |
| A5 przełącznik kontrastu | wyłącza i włącza bez przeładowania (znacznik w `window` przetrwał), `aria-pressed` zgodny ze stanem; przeżywa nawigację kliencką i pełne ładowanie; skrypt w `<head>` z nonce CSP ustawia `data-contrast` przed malowaniem, więc nie ma mignięcia jasnego motywu |

Przeglądarka startowała w wysokim kontraście, bo profil Chrome miał `ocwip.contrast=true` w `localStorage` z wcześniejszych przebiegów. To zapamiętana preferencja, zachowanie zamierzone.

### P4-02: niespójne tytuły kart przeglądarki (drobne)

- **Gdzie:** `/`, `/competitions` kontra pozostałe strony publiczne.
- **Co jest:** `/` ma tytuł "Konkursy dotacyjne OCWIP", `/competitions` "Konkursy OCWIP", a `/archive`, `/regulamin`, `/kontakt` i reszta "<nazwa> | Generator konkursów OCWIP".
- **Co powinno być:** jeden wzór tytułu. Przy kilku otwartych kartach i w historii przeglądarki dwie strony bez przyrostka wyglądają jak inny serwis.

### P4-03: deklaracja dostępności nie mówi, że kontrast kolorów nie jest sprawdzany automatycznie (drobne)

- **Gdzie:** `/deklaracja-dostepnosci`, sekcja "Przygotowanie deklaracji dostępności".
- **Co jest:** "automatyczne sprawdzenie narzędziem axe po każdym teście interfejsu" w zdaniu o WCAG 2.1 AA. W `frontend/vitest.setup.ts` reguła `color-contrast` jest wyłączona (jsdom nie liczy kolorów), więc kryterium 1.4.3 nie jest sprawdzane automatycznie.
- **Co powinno być:** zdanie, że kontrast sprawdzano ręcznie albo innym narzędziem, albo zawężenie zakresu automatycznego sprawdzenia. Deklaracja jest dokumentem prawnym, a urzędnik czytający ją dosłownie uzna, że kontrast przeszedł axe.

---

## Ścieżka B. Konto

**Stan: przeszło z uwagami.** B6 wykonane po ścieżce C, bo konto `test.reczny@example.org` było zablokowane po B5.

### B1. Rejestracja

| Sprawdzenie | Wynik |
|---|---|
| Rozjechane powtórzenie adresu i hasła | oba pola wskazane z nazwy ("Adresy e-mail są różne. Sprawdź oba pola.", "Hasła są różne. Wpisz je jeszcze raz."), `aria-invalid` i komunikat w `aria-describedby`, podsumowanie w `role="alert"`; do serwera nic nie poszło |
| Bez zgód | odmowa wymieniająca brakujące dokumenty z nazwy ("Zaakceptuj: Regulamin serwisu, Klauzula informacyjna..."), oba checkboxy oznaczone; backend też odmawia (400), więc zgoda nie jest pilnowana tylko na froncie |
| Poprawne dane | "Sprawdź skrzynkę pocztową. Jeśli konto dla tego adresu można założyć, wysłaliśmy na niego link do potwierdzenia." |
| Ten sam adres drugi raz (inne imię i hasło) | tekst ekranu identyczny co do znaku, oba 202; konto nie nadpisane (w bazie dalej pierwsze imię i nazwisko), drugi mail weryfikacyjny nie wyszedł |

Pomiar czasu odpowiedzi `POST /register` opisany w P4-06. Zostawił w bazie przebiegu pięć niepotwierdzonych kont `rozgrzewka-p4@example.org` i `timing-p4-1..4@example.org`.

### B2. Potwierdzenie adresu

| Sprawdzenie | Wynik |
|---|---|
| Logowanie przed potwierdzeniem | odmowa: "Potwierdź swój adres e-mail, zanim się zalogujesz." z linkiem do ponownej wysyłki |
| Czy ta odmowa zdradza konto | nie: pokazuje się tylko przy poprawnym haśle; złe hasło na tym koncie i nieistniejący adres dają identyczne "Nieprawidłowy e-mail lub hasło." |
| Link z jedną zmienioną literą w tokenie (otwarty przed prawdziwym) | czytelna odmowa po polsku z drogą wyjścia (logowanie, formularz nowego linku), bez 500 |
| Prawdziwy link | "Adres e-mail jest potwierdzony. Możesz się teraz zalogować." |
| Prawdziwy link drugi raz | ta sama czytelna odmowa co przy zepsutym |

Drobiazg językowy w odmowie: "Link może być nieprawidłowy, wygasły, lub konto zostało już potwierdzone." ma przecinek przed "lub", którego w tym wyliczeniu nie powinno być.

### B3. Logowanie i wylogowanie

| Sprawdzenie | Wynik |
|---|---|
| Logowanie | przekierowanie na `/panel/applicant`, stan pusty "Nie masz jeszcze żadnego wniosku" z drogą do konkursów |
| "Wyloguj" z nagłówka | powrót na `/login` |
| `/panel/applicant` wpisane w adres po wylogowaniu | przekierowanie na `/login?returnUrl=%2Fpanel%2Fapplicant` |
| Przycisk Wstecz po wylogowaniu | zostaje na logowaniu, panel się nie odsłania |
| Sesja po stronie serwera | `GET /me` z przeglądarki 401; osobno curlem: stare ciasteczko po `POST /logout` dostaje 401, czyli serwer unieważnia sesję, a nie tylko przeglądarka gubi ciasteczko |

### B4. Reset hasła

| Sprawdzenie | Wynik |
|---|---|
| `/forgot-password` z istniejącym i nieistniejącym adresem | ekran identyczny co do znaku, oba 200 w kilka milisekund (mail idzie kolejką); mail "Resetowanie hasła" tylko do istniejącego konta |
| Za słabe nowe hasło | lista niespełnionych warunków ("co najmniej 12 znaków", "co najmniej jedną wielką literę") |
| Poprawne nowe hasło na tym samym linku | przyjęte (nieudana próba nie spaliła tokenu), "Hasło zostało zmienione. Wszystkie wcześniejsze sesje zostały wylogowane." |
| Logowanie starym hasłem | odmowa |
| Logowanie nowym | wpuszcza |
| Ten sam link resetu drugi raz | 400, "Link do resetowania hasła jest nieprawidłowy lub wygasł. Poproś o nowy." |

### B5. Blokada

| Sprawdzenie | Wynik |
|---|---|
| Złe hasło 1 do 4 | 401, "Nieprawidłowy e-mail lub hasło." (licznik w bazie rośnie) |
| Złe hasło 5 | 429, "Konto zostało tymczasowo zablokowane po kilku nieudanych próbach logowania. Spróbuj ponownie za około 15 min." |
| Próba 6 z dobrym hasłem | 429, ten sam komunikat; `lockout_end` w bazie 15 minut naprzód |
| Po 15 minutach | logowanie dobrym hasłem przechodzi (sprawdzone o 07:53, gdy `lockout_end` minął) |

Rozróżnienie 401 i 429 zdradza istnienie konta. To jest znana pozycja `S-02` w [`przeglad-bezpieczenstwa.md`](przeglad-bezpieczenstwa.md) (stan: otwarte), więc bez nowego wpisu.

Uwaga do metody, żeby następny przebieg się nie potknął: po odmowie formularz logowania czyści pole hasła, a pole ma `required`. Ponowne kliknięcie "Zaloguj się" bez wpisania hasła nie wysyła niczego, więc pięć kliknięć to nie pięć prób. Za pierwszym podejściem tak właśnie wyszło (do serwera doszła jedna próba, szósta wpuściła); powtórzone z hasłem wpisywanym za każdym razem.

### B6. Zmiana hasła i adresu po zalogowaniu

| Sprawdzenie | Wynik |
|---|---|
| "Zmień hasło" (stare plus nowe) | "Hasło zmienione. Inne urządzenia zostały wylogowane."; bieżąca sesja żyje (`GET /me` 200); stare hasło 401, nowe 200; mail "Hasło zostało zmienione" na adres konta |
| "Zmień adres" na `test.reczny.nowy@example.org` | "Jeśli ten adres może zostać użyty, wysłaliśmy na niego link. Adres zmieni się dopiero po jego otwarciu, do tego czasu logujesz się dotychczasowym." (nie zdradza zajętości adresu) |
| Maile | "Potwierdź nowy adres e-mail" na **nowy** adres, "Prośba o zmianę adresu e-mail" na stary |
| Przed potwierdzeniem | adres w bazie bez zmian, logowanie starym działa, nowym nie |
| Link `confirm-email-change` | strona z przyciskiem "Potwierdź zmianę adresu" (otwarcie linku samo niczego nie zmienia, więc skaner linków w poczcie nie zmieni adresu) |
| Po kliknięciu | "Adres konta został zmieniony..."; wszystkie sesje wylogowane (`/me` 401); mail "Adres e-mail został zmieniony" na stary adres |
| Logowanie z ekranu | stary adres: "Nieprawidłowy e-mail lub hasło."; nowy: panel wnioskodawcy |

### P4-10: zmiana hasła w "Moje konto" nie prosi o powtórzenie nowego hasła (drobne)

- **Gdzie:** `/panel/applicant/account`, "Zmiana hasła".
- **Co jest:** pola "Obecne hasło" i "Nowe hasło", bez "Powtórz nowe hasło". Rejestracja i `/reset-password` mają powtórzenie.
- **Dlaczego wpis:** to ten sam defekt co `B-GUI-07` z przebiegu 1, poprawiony wtedy tylko na `/reset-password` ("drugie pole, sprawdzane przy każdej zmianie"). Skutek ten sam: literówka w nowym haśle i jedyna droga powrotu to reset.

### P4-04: zła odmiana "godzin" w mailach weryfikacji i resetu

- **Gdzie:** mail "Potwierdzenie adresu e-mail" i mail "Resetowanie hasła" (log backendu).
- **Co jest:** "Link jest ważny przez 24 godzin." oraz "Link jest ważny przez 1 godzin."
- **Co powinno być:** "24 godziny" i "1 godzinę". Liczba idzie z konfiguracji, a polski ma trzy formy (1 godzinę, 2 do 4 godziny, 5 do 21 godzin, 22 do 24 godziny).
- **Źródło:** stały tekst `{TokenLifetimeHours()} godzin.` w `backend/src/Ocwip.Api/Services/EmailVerificationService.cs:104` i `backend/src/Ocwip.Api/Services/PasswordResetService.cs:87`. Backend nie ma funkcji liczby mnogiej; front ma `pluralForm` w `frontend/lib/amount-in-words.ts`.
- **Waga:** każdy nowy użytkownik dostaje ten mail jako pierwszy kontakt z systemem.

### P4-05: przecinek przed "lub" w odmowie potwierdzenia adresu (kosmetyka)

- **Gdzie:** `/verify-email` z nieprawidłowym albo zużytym linkiem.
- **Co jest:** "Link może być nieprawidłowy, wygasły, lub konto zostało już potwierdzone."
- **Co powinno być:** "Link może być nieprawidłowy lub wygasły albo konto zostało już potwierdzone." (albo bez przecinka przed "lub").

### P4-06: rejestracja nowego adresu jest stale o około 10 ms wolniejsza niż zajętego (niskie)

- **Gdzie:** `POST /register`.
- **Co zrobiono:** po rozgrzewce cztery pary przeplatane, nowy adres i zajęty (`wnioskodawca@example.org`), z przerwą 7 s, żeby nie wpaść w limit szybkości.
- **Co wyszło:** nowy 54, 56, 59, 54 ms; zajęty 49, 45, 45, 45 ms. Cztery na cztery w tym samym kierunku, oba zawsze 202 z tą samą treścią.
- **Dlaczego to jest wpis:** [`przeglad-bezpieczenstwa.md`](przeglad-bezpieczenstwa.md) w części o tym, co jest zrobione dobrze, pisze "wabik zużywa tyle samo czasu" przy logowaniu i rejestracji. Przy rejestracji to nie jest dosłownie prawdą.
- **Waga:** niska. Różnica jest mała i przez sieć zginie w szumie, a pomiar to cztery próbki na localhoście. Wystarczy poprawić zdanie w przeglądzie albo dorównać ścieżkę wabika. Pierwsza próba w przebiegu (226 ms kontra 54 ms) była zimnym startem po restarcie stosu, nie wyciekiem.

---

## Ścieżka C. Operator zakłada i publikuje konkurs

**Stan: przeszło z uwagami.** Konkurs `1/2026` "Kierunek NOWE FIO 2026 (próba)" opublikowany, identyfikator `e90a93a8-82d7-4d34-9809-3761914740eb`. Do prób powstały jeszcze szkic `P4/BRAKI` (odmowa publikacji) i kopia `1/2027` (dezaktywowana).

### C1. Kreator, siedem kroków

| Krok | Wpisane | Wynik |
|---|---|---|
| 1.1 | `1/2026`, tytuł ze scenariusza, nabór 07.10.2026 09:00 do 15.10.2026 16:00, informacja po złożeniu | pod datami zdanie "Nabór zamyka się 15 października 16:00. Wnioski złożone później nie wejdą." |
| 1.2 | opis, rezultaty, adres regulaminu | |
| 1.3 | bez papieru | |
| 1.4 | pula 20 000, maks. dotacja 7 000, przychód 50 000, koszty pośrednie 10 procent, realizacja 01.11 do 31.12.2026, dane osobowe do 31.12.2031 | kwoty słownie na bieżąco ("dwadzieścia tysięcy złotych") |
| 1.5 | `Statut`, wymagany, PDF | |
| 1.6 | Anna Operator, treść maila potwierdzającego | |
| 1.7 | | podsumowanie zgodne z wpisanym co do pola; wejście w ten krok samo zapisuje szkic (zamierzone: podgląd ma pochodzić z serwera, komentarz w `competition-wizard.tsx`) i przenosi na `/competitions/<id>/edit?krok=summary` |

- **Przejście dalej z niedokończonym krokiem:** dozwolone. Oznaczenie kroku pojawia się dopiero po błędzie pola z backendu; braki wykryte przed wysłaniem nie oznaczają kroku, patrz P4-07.
- **Zapis i powrót:** po przeładowaniu strony wszystkie pola wróciły, łącznie z treścią maila z 1.6, której podsumowanie nie pokazuje; `?krok=` otwiera właściwy krok.

### C2. Import treści startowej

Pierwsze uruchomienie: pięć linii "Published ... as version 1." (formularz, obie karty, sprawozdanie, wzór umowy). Drugie: pięć linii "... is already version 1. Nothing changed.", w bazie nic się nie zdublowało. Strona konkursu pokazuje wersje w mocy.

### C3. Formularz w kreatorze i podgląd

| Sprawdzenie | Wynik |
|---|---|
| Zaimportowany formularz | cztery części ze scenariusza |
| Dodanie sekcji testowej | dodana jako "Sekcja 5 z 5"; pasek "Szkic zapisany ... Zostaje po zamknięciu przeglądarki.", szkic w `localStorage` |
| Przesunięcie w górę | działa, fokus idzie za przyciskiem przesuniętej sekcji, przyciski mają opisowe etykiety ("Przesuń sekcję w górę: Sekcja testowa P4") |
| Usunięcie | działa bez potwierdzenia (szkic z cofaniem); fokus spada na `<body>`, patrz O-01 |
| "Cofnij ostatnią zmianę" | przywraca sekcję w przesuniętym miejscu |
| "Odrzuć szkic i zacznij od nowa" | czyści `localStorage`, wraca do wersji w mocy; jednym kliknięciem, patrz O-02 |
| Podgląd, "Wniosek składa" A | pojawiają się "Wnioskodawca jest", data wpisu do rejestru, roczny przychód |
| Podgląd, "Wniosek składa" C | pola organizacji znikają, pojawiają się nazwa grupy, tabela członków, rachunek lidera; tabela członków ma przesunięte nagłówki, patrz P4-08 |
| Nic nie opublikowane | wersja formularza dalej 1 |

### C4. Wzory załączników

| Sprawdzenie | Wynik |
|---|---|
| Wgranie PDF jako wzoru `Statut` | wgrany, link w panelu |
| Podmiana na tekst przebrany za PDF | "Niedozwolony format pliku. Dozwolone formaty: ...", stary wzór zostaje; patrz O-03 |
| Gość, `/competitions/<id>` | "Pobierz wzór: wzor-statutu.pdf"; pobranie bez ciasteczek 200, prawdziwy PDF, `Content-Disposition` z nazwą pliku, `nosniff` |
| "Wycofaj wzór" | znika z panelu i ze strony gościa, stary adres pliku 404 |

### C5. Publikacja

| Sprawdzenie | Wynik |
|---|---|
| Odmowa (szkic `P4/BRAKI` bez formularza i kart) | ramka "Przed publikacją brakuje" z dokładnie trzema tekstami ze scenariusza i linkiem do kreatora formularza; "Opublikuj konkurs" wyłączony; patrz O-04 |
| Publikacja 1/2026 | potwierdzenie w `<dialog>` w stronie ("Opublikowany konkurs jest widoczny publicznie... Opublikować?"), po "Tak" stan "Trwa nabór" |
| `/competitions` u gościa | konkurs na liście z maks. dotacją i pulą |
| `/competitions/<id>` | pełne ogłoszenie, licznik "Do zamknięcia naboru pozostało 7 dni i 8 godzin", terminy w czasie polskim, kwoty w złotych, wymagany załącznik, kontakt, regulamin w nowej karcie, data przetwarzania danych osobowych, "Wypełnij wniosek" z `returnUrl` na ten konkurs |

### C6. Kopia konkursu

| Sprawdzenie | Wynik |
|---|---|
| "Skopiuj karty i wzór" z 1/2026 do `P4/BRAKI` | "Skopiowano: karta oceny formalnej, karta oceny merytorycznej, wzór sprawozdania.", lista braków skurczyła się do formularza |
| "Skopiuj konkurs" 1/2026 jako `1/2027` | nowy szkic z limitami, kategoriami kosztów, kontaktem, wymogiem `Statut`, formularzem, kartami i wzorem umowy; bez dat realizacji i daty danych osobowych, zgodnie z opisem przy przycisku |
| "Dezaktywuj konkurs" na `1/2027` | potwierdzenie w `<dialog>`, potem "Roboczy · dezaktywowany", "Przywróć konkurs"; dane zostają |

### P4-07: braki przed zapisem konkursu nie oznaczają kroków, nie są ogłaszane i nie znikają po uzupełnieniu

- **Gdzie:** `/panel/operator/competitions/new`, pasek zapisu.
- **Co kliknięto:** numer i tytuł w 1.1, bez dat i bez maksymalnej dotacji, przejście do 1.2, "Zapisz".
- **Co się stało:** pojawia się "Żeby zapisać, uzupełnij najpierw: Rozpoczęcie naboru wniosków, Zakończenie naboru wniosków (albo zaznacz nabór ciągły), Maksymalna dotacja na jeden wniosek." Kroki 1.1 i 1.4, w których są te pola, nie dostają oznaczenia, komunikat nie mówi, w którym kroku szukać, a akapit (`frontend/app/panel/operator/competitions/new/save-bar.tsx:43`) nie ma `role="alert"` ani `aria-live`, choć błąd z serwera tuż pod nim ma; fokus zostaje na "Zapisz", więc czytnik ekranu nie mówi nic. Po uzupełnieniu dat i dotacji komunikat dalej wisi z tą samą listą, aż do następnego zapisu.
- **Co miało się stać:** scenariusz: "krok dostaje oznaczenie". Kropka "Ten krok ma błąd do poprawy" w `wizard-nav.tsx` zapala się tylko dla błędów z backendu; braki znalezione przed wysłaniem powinny ją zapalać tak samo, komunikat powinien być ogłaszany i znikać, gdy brak zniknie.

### P4-08: tabela o stałej liczbie wierszy ma nagłówki przesunięte o jedną kolumnę

- **Gdzie:** każdy formularz z tabelą `fixedTable`; widoczne w podglądzie kreatora na "Członkowie grupy nieformalnej" (wariant C), ten sam `FormRenderer` co u wnioskodawcy.
- **Co jest:** `<thead>` ma cztery komórki ("Imię i nazwisko", "Adres", "Telefon", "E-mail"), a każdy wiersz pięć: nazwę wiersza ("Lider grupy") i cztery pola. Nad nazwą wiersza stoi "Imię i nazwisko", nad polem imienia "Adres", nad polem adresu "Telefon", nad telefonem "E-mail", pole e-mail nie ma nagłówka. Pozycje kolumn zmierzone w przeglądarce potwierdzają przesunięcie.
- **Skutek:** osoba patrząca na ekran wpisuje dane w złe kolumny (adres pod "Adres" trafia do pola imienia). Czytnik ekranu dostaje poprawne `aria-label` na każdym polu ("Adres (wymagane), wiersz 1"), więc problem jest wizualny, ale dotyczy formularza, który wypełnia każda grupa nieformalna.
- **Przyczyna:** `frontend/components/form-renderer/table-field.tsx:52` renderuje pustą komórkę narożną tylko dla tabeli zmiennej (`{!isFixed ? <th scope="col" /> : null}`), a wiersz tabeli stałej też zaczyna się od `<th scope="row">` z nazwą wiersza (`:147`). Tabele budżetu (zmienne) są poprawne. Kod od pierwszej wersji renderera (`81439ba`).
- **Co miało się stać:** komórka narożna w nagłówku dla obu rodzajów tabel.

### P4-09: część IV w kreatorze to trzynaście identycznych wierszy "Oświadczenie"

- **Gdzie:** `/panel/operator/forms/<id>`, sekcja "Część IV. Oświadczenia".
- **Co jest:** wszystkie 13 pól typu `statement` w `backend/seed/forms/application-2026.json` ma etykietę "Oświadczenie", więc zwinięta lista to 13 jednakowych wierszy i nie da się trafić w właściwe bez rozwijania po kolei. To samo psuje komunikat blokady przy "Wniosek składa": "Tego pola używają: ..., Oświadczenie, Oświadczenie, Oświadczenie, Oświadczenie, Oświadczenie, Oświadczenie."
- **Co powinno być:** rozróżnialne podsumowanie: albo etykiety oświadczeń we wzorze (decyzja o danych, więc pytanie do zespołu), albo kreator pokazuje dla pola `statement` początek treści zamiast samej etykiety.
- **Ten sam skutek u wnioskodawcy (ścieżka D):** lista braków obok "Złóż wniosek" pokazuje jedenaście jednakowych pozycji "Część IV. Oświadczenia: Oświadczenie - To pole jest wymagane."; każda prowadzi do właściwego pola, ale z tekstu nie wiadomo, którego. Samo pole jest w porządku: ma dwie etykiety, a druga to pełna treść oświadczenia, więc czytnik ekranu czyta całe zdanie.

### Obserwacje drobne ze ścieżki C

- **O-01:** po "Usuń sekcję" w kreatorze formularza fokus spada na `<body>`; użytkownik klawiatury wraca na początek strony, przed ponad sto elementów.
- **O-02:** "Odrzuć szkic i zacznij od nowa" usuwa cały szkic formularza jednym kliknięciem, bez potwierdzenia i bez cofania, a publikacja obok ma potwierdzenie w linii.
- **O-03:** po odrzuconym pliku wzoru pole wyboru dalej pokazuje nazwę odrzuconego pliku ("udaje-pdf.pdf"), a komunikat "Niedozwolony format pliku" wisi jako `role="alert"` nawet po niezwiązanej publikacji konkursu.
- **O-04:** ramka braków mówi "Karty oceny ... wgra je administrator razem z treścią konkursu". Roli administratora w systemie nie ma (sam scenariusz to zaznacza), import robi się komendą na serwerze; operator nie wie, kogo prosić. Wyłączony "Opublikuj konkurs" nie ma `aria-describedby` do listy braków.
- **O-05:** drugie pole terminu realizacji w 1.4 ma etykietę samo "do", więc czytnik ekranu przeczyta tylko "do".

### Rozjazdy scenariusza z produktem (do poprawy w `przejscie-gui.md`, nie w produkcie)

- C1: "krok dostaje oznaczenie" przy przejściu dalej. Produkt oznacza krok dopiero po błędzie z backendu (część przypadku to P4-07).
- C1: "Zapisz. Po zapisie jesteś na stronie konkursu". Po zapisie jest się na `/competitions/<id>/edit`, a strona konkursu jest pod linkiem "Przejdź do strony konkursu"; identyfikator jest w adresie w obu przypadkach.
- C5: "przycisk zwraca listę braków". Lista stoi na stronie konkursu od razu, a przycisk jest wyłączony, dopóki braki są.

---

## Ścieżka D. Wnioskodawca: organizacja składa wniosek

**Stan: przeszło z uwagami.** Konto `wnioskodawca@example.org`, wniosek **001**, identyfikator `2dbc75ba-aabc-43e5-9a77-b0c657370727`, złożony 08.10.2026 08:12.

### D1. Wejście z publicznej strony

| Sprawdzenie | Wynik |
|---|---|
| Gość, "Wypełnij wniosek" | `/login?returnUrl=%2Fcompetitions%2F<id>` |
| Po zalogowaniu | powrót na ten sam konkurs; przycisk prowadzi teraz do `/panel/applicant/start/<id>` |
| "Co przygotować" | termin 15.10.2026 16:00, "Dotacja do 7000,00 zł", dane do zebrania, "Statut (wymagany, PDF)", limity 10 i 50 MB, link do regulaminu: zgodne z ustawieniami operatora |

### D2. Karta podmiotu

Wypełniona (nazwa, forma prawna, KRS, NIP i REGON z poprawnymi sumami kontrolnymi, adres, telefon, e-mail, rachunek, reprezentant), "Zapisz dane i przejdź do wniosku" zakłada szkic. Panel wniosku ma pasek czterech części ze stanem, "Zapisano o ...", licznik "7 dni i 8 godzin", wyłączony "Złóż wniosek" z listą 26 braków (pierwsze pięć, reszta pod "Pokaż wszystkie (26)") i blok techniczny (wersja formularza, suma kontrolna).

### D3. Cztery części

| Obietnica | Wynik |
|---|---|
| 1. Autozapis | przeszło w części: wpis w części II, "Zapisano o 07:58", zamknięcie karty, nowa karta, lista "Moje wnioski" z "zapisano 08.10.2026, 07:58" i "Wypełnij dalej"; **treść wróciła**, **miejsce nie**: wniosek otwiera się zawsze na części I, patrz P4-11 |
| 2. Liczby liczy komputer | przeszło: wartości wierszy, sumy A, B, C, wartość całkowita, wnioskowana kwota i oba procenty przeliczają się przy każdej zmianie; kolumny "Wartość całkowita" nie da się edytować |
| 3. Limit z konkursu blokuje | przeszło: przychód 60 000 zł "Przekroczono dopuszczalną wartość o 10 000,00 zł. Maksymalnie 50 000,00 zł."; dotacja 7700 zł "... o 700,00 zł. Maksymalnie 7000,00 zł."; koszty pośrednie 800 z 6000 zł (13,33 procent) "Razem C ... o 200,00 zł. Maksymalnie 600,00 zł."; podpowiedź przy procentach myli, patrz P4-13 |
| 4. Licznik znaków | przeszło: "104 z 3000", "23 z 8000"; minimum opisu pomysłu: "Wymagane co najmniej 1000 znaków (jest 23)." |
| 5. Przejście dalej z niedokończoną sekcją | dozwolone; sekcja z pustymi wymaganymi polami ma "w toku", a "są błędy" (z "!") dostaje dopiero przy wartości błędnej (za krótki opis, przekroczony limit), zgodnie z `frontend/lib/forms/validate.ts:203`; patrz rozjazdy scenariusza. Po "Dalej" widok i fokus zostają na dole następnej części, patrz P4-12 |
| 6. Jeden rodzaj komunikatu | przeszło: podpowiedź stoi pod polem cicho, błąd pojawia się z `aria-invalid` i w `aria-describedby` i znika po poprawce |

Część IV dla organizacji: dziesięć oświadczeń, klauzula RODO odsyła "do daty wskazanej w ogłoszeniu konkursu". Wszystkie cztery części "gotowa", jedynym brakiem został załącznik.

### D4. Załącznik

| Sprawdzenie | Wynik |
|---|---|
| PDF na kafelek `Statut` | przyjęty, "dodano", "Złóż wniosek" się włącza |
| Drugi PDF na ten sam kafelek | **dodaje drugi plik** zamiast podmienić, patrz P4-14 |
| "Zastąp" przy wierszu | podmienia ten wiersz; poprzedni w bazie `is_active = false`, nie skasowany (reguła 5) |
| "docx" na kafelek | "Załącznik "Statut" przyjmuje tylko: PDF." |
| Tekst przebrany za PDF przez "Zastąp" | ta sama odmowa, stary plik zostaje (format po bajtach, nie po rozszerzeniu) |
| Plik 11 MB | sprawdzony przez API w sesji wnioskodawcy, bo rozszerzenie nie wgra pliku powyżej 10 MB: 400 "Plik przekracza dopuszczalny rozmiar 10 MB." |

### D5. Podsumowanie i złożenie

| Sprawdzenie | Wynik |
|---|---|
| Ekran podsumowania | "Podsumowanie wniosku", cały wniosek do przeczytania, przy każdej części "Popraw"; części rozwinięte, nie zwinięte |
| Celowy brak (wyczyszczona gmina, bo załącznika nie da się usunąć) | "Złóż wniosek" widoczny i wyłączony, obok "Zanim złożysz wniosek, uzupełnij (1)" |
| Pozycja braku | kliknięta z części III przenosi do części I i ustawia fokus w polu gminy (widocznym, z `aria-invalid`) |
| Po uzupełnieniu | przycisk się włącza, panel "Wniosek jest kompletny. Sprawdź podsumowanie i złóż go." |
| "Złóż wniosek" | jedno okno (`<dialog>` z `showModal()`): "Po złożeniu wniosku nie będzie można go już edytować.", fokus domyślnie na "Wróć" |
| Po złożeniu | "Wniosek 001", "Wniosek został złożony 08.10.2026, 08:12. Treść jest zamrożona", zero pól edytowalnych, dane wnioskodawcy zamrożone na chwilę złożenia, numer, suma kontrolna |
| PDF | potwierdzenie 200 `application/pdf` (27 KB), cały wniosek 200 `application/pdf` (54 KB) |
| Mail | "Potwierdzenie złożenia oferty" z numerem 001, konkursem, datą w czasie polskim i treścią wpisaną przez operatora w kroku 1.6; przez cały szkic żadnego maila |

### P4-11: wniosek po powrocie otwiera się zawsze na części I

- **Gdzie:** `/panel/applicant/applications/<id>`.
- **Co zrobiono:** praca w części II, "Zapisano o 07:58", zamknięcie karty, powrót przez "Wypełnij dalej" i przez adres wprost.
- **Co się stało:** treść wróciła, ale wniosek otwiera się na części I, nie na części II.
- **Co miało się stać:** scenariusz D3 pkt 1: "Treść i miejsce mają wrócić."
- **Przyczyna:** `frontend/app/panel/applicant/applications/[applicationId]/draft-workspace.tsx:76` startuje od pierwszej sekcji (albo pierwszej sekcji zwrotu). Karta T-29 o miejscu nie mówi, więc to rozjazd scenariusza z produktem do rozstrzygnięcia: albo zapamiętywać ostatnią sekcję, albo poprawić scenariusz. Przy wniosku na 5 do 6 stron powrót na stronę pierwszą za każdym razem jest uciążliwy.

### P4-12: po "Dalej" widok i fokus zostają na dole nowej części

- **Gdzie:** formularz wniosku, przycisk "Dalej" pod częścią.
- **Co się stało:** po "Dalej" z części II nagłówek części III był 562 px nad ekranem, widać było jej koniec ("Razem C", "Dalej"), a fokus został na przycisku "Dalej" nowej części.
- **Co miało się stać:** nowa część od początku: przewinięcie do nagłówka i fokus na nim. Teraz osoba widząca przewija w górę po każdej części, a osoba z klawiaturą albo czytnikiem ekranu zaczyna nową część od jej ostatniego przycisku (WCAG 2.4.3).

### P4-13: podpowiedź "Maksymalnie" przy limicie procentowym nie jest prawdziwym maksimum

- **Gdzie:** część III, koszty pośrednie (limit "nie więcej niż 10 procent dotacji").
- **Co zrobiono:** A 5000 zł, B 200 zł, C 800 zł, potem C ustawione na podpowiedziane maksimum.
- **Co się stało:** przy C = 800 komunikat "Maksymalnie 600,00 zł". Po wpisaniu 600 zł dotacja spada do 5800 zł i komunikat zmienia się na "Przekroczono ... o 20,00 zł. Maksymalnie 580,00 zł."; po 580 będzie 578 i tak dalej.
- **Co miało się stać:** podpowiedź, po której wpisaniu błąd znika. Koszty pośrednie same wchodzą do dotacji, więc prawdziwe maksimum to C ≤ 0,1 x (A + B + C), czyli przy A + B = 5200 zł 577,77 zł.
- **Przyczyna:** `frontend/lib/forms/limits.ts:58` liczy dozwoloną kwotę jako procent bieżącej podstawy, a podstawa zawiera samo ograniczane pole. Samo sprawdzenie jest poprawne, myli tylko liczba w komunikacie (`frontend/lib/forms/validate.ts:112`).

### P4-14: dodanego przez pomyłkę załącznika nie da się wycofać

- **Gdzie:** sekcja "Załączniki" wniosku.
- **Co się stało:** drugi plik upuszczony na ten sam kafelek został dodany obok pierwszego. Przy plikach jest tylko "Zastąp"; żadnej drogi do wycofania pliku. Oba pliki weszły do złożonego wniosku 001 i do jego PDF-u.
- **Co miało się stać:** scenariusz D4: "Podmiana to przeciągnięcie nowego na to samo miejsce", a D5 zakłada "usuń załącznik". Produkt świadomie nie kasuje (`docs/architektura.md`, "podmiana nigdy nie kasuje", brak `DELETE` w `AttachmentEndpoints.cs`), ale wycofanie mogłoby być miękkie (`is_active = false`, jak przy podmianie), co reguła 5 dopuszcza. Bez tego pomyłkowo dodany dokument (na przykład z danymi innej osoby) trafia do ekspertów.

### P4-15: po utracie sesji autozapis milczy o tym, że nie zapisał

- **Gdzie:** formularz wniosku, wiersz "Zapisano o ..." w bocznej kolumnie.
- **Jak do tego doszło:** sesja przeglądarki zakończyła się, bo to samo konto wylogowało się gdzie indziej (wylogowanie kończy wszystkie sesje konta, celowo, `docs/architektura.md:57`). W realu: wylogowanie na telefonie przy otwartym wniosku na laptopie.
- **Co się stało:** następna zmiana dostała 401, a w miejscu "Zapisano o ..." pojawiło się wyszarzone "Zaloguj się, żeby zobaczyć tę stronę.": tekst o oglądaniu strony, którą człowiek właśnie edytuje, bez linku do logowania, bez `role="alert"`; blok techniczny niżej dalej pokazuje "Zapisano 08.10.2026, 08:06". Po ponownym zalogowaniu pole wróciło do wartości z serwera, zmiana przepadła bez słowa.
- **Co miało się stać:** jasne "Nie zapisano ostatnich zmian, zaloguj się ponownie" z linkiem (najlepiej w nowej karcie, żeby nie zgubić formularza), ogłoszone czytnikowi.
- **Przyczyna:** `frontend/app/panel/applicant/applications/[applicationId]/draft-workspace.tsx:151` ma dobry tekst zapasowy ("Nie udało się zapisać. Sprawdź połączenie: odpowiedzi zostają w formularzu."), ale `apiErrorMessage` woli szczegół z serwera, a dla 401 jest nim ogólne zdanie z `backend/src/Ocwip.Api/Configuration/AuthenticationConfiguration.cs:216`. Wiersz statusu to zwykły `<p>` (`draft-workspace.tsx:248`).

### Obserwacje drobne ze ścieżki D

- **O-06:** pole wyliczane "Wnioskowana kwota dotacji" (tylko do odczytu) przy przekroczonym limicie nie ma `aria-invalid` ani `aria-describedby` do komunikatu; pole przychodu w części I je ma.
- **O-07:** trzy przyciski "Dodaj wiersz" w budżecie i komórki "Nazwa kosztu (wymagane), wiersz 1" mają identyczne nazwy w tabelach A, B i C, a cztery przyciski "Popraw" w podsumowaniu nie mówią, której części dotyczą; na liście kontrolek czytnika nie da się ich odróżnić.
- **O-08:** rozmiar małego załącznika pokazany jako "(0 MB)".
- **O-09:** komunikaty o odrzuconych plikach ("Załącznik "Statut" przyjmuje tylko: PDF.") zostają na ekranie po udanej podmianie, ten sam wzór co O-03.

### Rozjazdy scenariusza z produktem (ścieżka D)

- D3 pkt 5: "sekcja dostaje stan 'są błędy'". Produkt rozróżnia "w toku" (puste wymagane) i "są błędy" (wartość błędna); rozróżnienie jest rozsądne, scenariusz do poprawy.
- D4: "Podmiana to przeciągnięcie nowego na to samo miejsce". Przeciągnięcie dodaje, podmiana to "Zastąp" przy wierszu (część problemu to P4-14).
- D5: "usuń załącznik" jako sposób na brak; usunąć się nie da. "Sekcje zwinięte"; są rozwinięte.
- D4: "plik za duży" nie da się sprawdzić rozszerzeniem Chrome (limit narzędzia 10 MB na wywołanie); sprawdzone przez API.

---

## Ścieżka E. Wnioskodawca: grupa nieformalna

**Stan: przeszło z uwagami.** Konto `grupa@example.org`, wniosek **002**, identyfikator `7db69455-667f-4f84-8861-71c3f66d1b71`, złożony 08.10.2026 08:18, w bazie `applicant_type = InformalGroup`.

| Sprawdzenie | Wynik |
|---|---|
| Karta podmiotu, "Grupa nieformalna" | z całej karty zostaje jedno pole "Nazwa grupy"; pola organizacji znikają |
| Część I, "C. Grupa nieformalna" | znikają "Wnioskodawca jest", data wpisu do rejestru i przychód; pojawiają się nazwa grupy, tabela członków (trzy stałe wiersze: lider, członek drugi, członek trzeci) i rachunek lidera |
| Tabela członków | przyjmuje trzy wiersze; nagłówki przesunięte o kolumnę (P4-08, zrzut w scratchpadzie przebiegu: imię pod "Adres", adres pod "Telefon", telefon pod "E-mail"); w podsumowaniu i po złożeniu tabela tylko do odczytu jest poprawna |
| Dane niewymagane od grupy | e-maile członków puste, część I "gotowa" |
| Część IV | osiem oświadczeń: bez trzech organizacyjnych (siedziba, podatki, składki), z grupowym "Wszyscy członkowie grupy nieformalnej są mieszkańcami województwa opolskiego." |
| Załącznik | wymóg `Statut` blokuje złożenie, patrz P4-16; do dokończenia ścieżki wgrany PDF |
| Złożenie | jedno okno potwierdzenia, "Wniosek 002 ... został złożony", zero pól edytowalnych, mail "Potwierdzenie złożenia oferty" z numerem 002 |

### P4-16: grupa nieformalna bez patrona nie złoży wniosku bez "statutu", którego nie ma (pytanie o regułę)

- **Gdzie:** konkurs skonfigurowany według scenariusza (C1, krok 1.5: jeden wymagany załącznik `Statut`), wniosek grupy C.
- **Co się stało:** po wypełnieniu wszystkich czterech części jedynym brakiem jest "Brakuje wymaganego załącznika: Statut.". Opis wymogu sam mówi "Aktualny statut organizacji albo patrona grupy nieformalnej", a grupa bez patrona nie ma ani jednego, ani drugiego. Prawdziwa grupa utknęłaby tu albo wgrała cokolwiek.
- **Dlaczego to nie jest tylko konfiguracja:** krok 1.5 kreatora konkursu oferuje "Wymagany", "Nieobowiązkowy" i "Wymagany od podmiotów spoza KRS". Grupa C jest spoza KRS, więc żaden wariant nie wyraża "wymagany od organizacji i patrona, nie od grupy C".
- **Co dalej:** to decyzja o regule (który rodzaj wnioskodawcy dołącza statut i czy wymóg zależy od rodzaju), więc według `AGENTS.md` pytanie do `docs/runbook/pytania.md`, nie samodzielna zmiana modelu. Scenariusz E zakłada, że "wniosek da się złożyć bez danych, których od grupy nie wymagamy", a przy konfiguracji z C1 to się nie udaje.

### Obserwacja ze ścieżki E

- **O-10:** karta podmiotu mówi już "grupa nieformalna", a pytanie "Wniosek składa" w części I jest puste i przyjmuje sprzeczną odpowiedź "A. organizacja" (szkic zapisuje się, pojawiają się pola rejestru i przychodu). Backend odrzuca to dopiero przy złożeniu, z dobrym komunikatem (`backend/src/Ocwip.Api/Models/Forms/ApplicantKinds.cs:47`). Wnioskodawca może wypełnić zbędne pola organizacji i dowiedzieć się o tym na samym końcu; pole mogłoby startować z wartością z karty albo ograniczać opcje.

---

## Ścieżka F. Operator prowadzi nabór

**Stan: przeszło z uwagami.**

### F1. Lista wniosków

| Sprawdzenie | Wynik |
|---|---|
| Zawartość | tylko złożone, oba, ponumerowane 001 i 002 w kolejności wpływu; kolumny: lp., numer, nazwa podmiotu, rodzaj, tytuł, całkowity koszt, wnioskowana kwota, status, ocena formalna, data złożenia |
| Sumy | "Suma wnioskowanych kwot 9200,00 zł", "Pozostało z puli konkursu 10 800,00 zł z 20 000,00 zł"; kafle: złożone 2, ocena formalna 0 z 2, zwrócone 0 |
| Sortowanie | po wnioskowanej kwocie w obie strony, `aria-sort` na nagłówku |
| Filtr rodzaju | "Grupa nieformalna" zostawia 002, suma przelicza się do widocznych wierszy (3500 zł) |
| CSV | 200, `text/csv; charset=utf-8`, BOM `ef bb bf`, separator `;`, polskie znaki, nazwa pliku `wnioski-1-2026.csv` |
| PDF listy | 200, `application/pdf` |
| PDF pojedynczego wniosku | polskie znaki poprawne ("Przyjaciół", "Małej", "zł"), sprawdzone `pdftotext` |
| Zakładki w stanie "trwa nabór" | "Edytuj ogłoszenie", "Formularz wniosku", "Wnioski", "Ocena i wyniki", "Publiczna strona konkursu": wszystkie aktywne, bo każda ma już treść; przypadku "wyszarzona z wyjaśnieniem" nie da się w tym stanie zobaczyć (tak samo w przebiegu 3) |

### F2. Zwrot do poprawy

| Sprawdzenie | Wynik |
|---|---|
| Pusty formularz zwrotu | 400 i komunikat "Wskaż co najmniej jedną sekcję do poprawy. Opisz, co trzeba poprawić. Podaj termin poprawy." |
| Zwrot 001: część II, opis, termin 13.10.2026 16:00 | status "Zwrócony do poprawy", panel "Wniosek czeka na poprawkę do 13.10.2026, 16:00. Sekcje: Część II.", wersja 1 z sumą kontrolną odłożona, wpis w historii |
| Mail | "Wniosek 001 zwrócony do poprawy" z powodem, listą sekcji i terminem w czasie polskim |
| U wnioskodawcy | "Wnioski zwrócone do poprawy: 1", "Popraw wniosek"; wniosek otwiera się od razu na części II; w bocznej kolumnie powód, "Zmienić możesz tylko sekcje: Część II... Pozostałe części wniosku są zablokowane.", termin poprawy i licznik do zamknięcia naboru; część I pokazana jako tekst bez żadnej kontrolki; patrz O-11 |
| Ponowne złożenie | ten sam numer 001, nowa suma `2122-1c29-bc75` (pierwotna `2112-8cd1-7ca6`), znowu mail z potwierdzeniem |
| U operatora | aktualna suma i "Wcześniejsze wersje: Wersja 1 ... suma kontrolna 2112-8cd1-7ca6, zwrócona 08.10.2026, 09:38"; historia: złożony, zwrócony, złożony |

- **O-11:** w poprawce pasek części wygląda tak samo dla części odblokowanej i zablokowanych (wszystkie "✓ gotowa"); scenariusz zapowiada "reszta szara". Blokada sama działa (część zablokowana nie ma pól), brakuje tylko wyróżnienia w pasku.

---

## Ścieżka G. Ocena formalna

**Stan: przeszło z uwagami.**

| Sprawdzenie | Wynik |
|---|---|
| "Rozpocznij ocenę formalną" na 001 | karta z ośmioma kryteriami tak/nie i polem uzasadnienia przy każdym: termin, uprawniony wnioskodawca, siedziba w województwie, działania w województwie, przychód do 50 000 zł, wpis w ciągu 60 miesięcy, działania w terminie, kwota do 7000 zł |
| Karta 002 (grupa) | sześć kryteriów, bez przychodu i wpisu do rejestru |
| Wynik na żywo | "spełnia wymogi formalne" przy samych "Tak", "nie spełnia wymogów formalnych" po jednym "Nie", z powrotem po zmianie |
| "Zakończ ocenę" | okno "Po zakończeniu oceny nie będzie można już zmienić karty.", potem karta tylko do odczytu |
| Lista wniosków | kolumna "Ocena formalna": "Pozytywna" przy obu, kafel "2 z 2" |

### P4-17: przy negatywnym wyniku oceny formalnej nie ma przycisku zwrotu do poprawy

- **Gdzie:** `/panel/operator/evaluation/<id>/<wniosek>`, karta oceny formalnej.
- **Co zrobiono:** na wniosku 002 jedno kryterium "Nie".
- **Co się stało:** wynik "nie spełnia wymogów formalnych", ale obok żadnej akcji. Zwrot do poprawy jest tylko na osobnej stronie wniosku w "Wnioskach" (F2), bez podpowiedzianych braków.
- **Co miało się stać:** `docs/runbook/M5-ocena.md:84`: "obok wyniku stoi przycisk zwrotu do poprawy z gotową listą braków z karty". Scenariusz G też mówi, że wynik negatywny nie zamyka sprawy. Dziś operator musi przejść na inny ekran i przepisać braki ręcznie.

---

## Ścieżka H. Eksperci i ocena merytoryczna

**Stan: przeszło z uwagami.** Wykonane po restarcie maszyny: stos podniesiony na tych samych wolumenach, baza w stanie z końca G.

### H1. Ustawienia oceny

2 ekspertów na wniosek, suma punktów, próg 50, strategiczne nie liczą się do progu, próg rozbieżności pusty. "Zapisano ustawienia oceny.", wartości przetrwały przeładowanie.

### H2. Powołanie i przypisanie

`/panel/operator/reviewers`: Ewa Recenzent i Piotr Recenzent na liście, zespół OCWIP tylko do odczytu z rolami. Na liście rankingowej "Zaznacz wszystkie wnioski", ekspert, "Przypisz": osobno dla obu; tabela ekspertów "Przypisane wnioski 2" przy każdym, deklaracja "Nie złożona"; zaznaczenie czyści się po udanym przypisaniu.

### H3. Deklaracja i karty

| Sprawdzenie | Wynik |
|---|---|
| Ekspert przed deklaracją, `/panel/reviewer` | "Masz przydzielonych wniosków do oceny: 2. Zobaczysz je po złożeniu deklaracji bezstronności.", treść oświadczenia, "Składam deklarację" i "Nie mogę jej złożyć"; ani jednego linku do wniosku |
| Przed deklaracją z adresu `/panel/reviewer/applications/<id>` | "Nie masz dostępu do tego wniosku."; `GET /applications/<id>` 403; start karty 403; treść nie wycieka; patrz O-12 |
| Po deklaracji | lista dwóch wniosków z kolumnami "Twoja karta" i "Rekomendowana", sumy "Wnioskowane razem", "Twoje rekomendacje razem", "Pula konkursu" |
| Karta merytoryczna | otwiera się od razu pod numerem wniosku, a pod kartą cały wniosek na jednym ekranie; trzy części: kryteria merytoryczne, proponowana kwota (z kwestionowanymi pozycjami, kwotą po ich uwzględnieniu i uzasadnieniem obniżenia), kryteria strategiczne |
| Granica punktów | 21 przy maksimum 20: "Wartość nie może być większa niż 20." |
| Suma | liczy się sama: 18 + 14 + 9 + 3 = 44, 12 + 10 + 6 + 2 = 30 |
| "Zakończ ocenę" | okno "Po zakończeniu oceny nie będzie można już zmienić karty.", potem karta tylko do odczytu |
| Niezależność ocen | drugi ekspert nie widzi karty pierwszego |
| Ekspert na `/panel/operator` | "403. Nie masz dostępu do panelu operatora ... Przejdź do swojego panelu"; API listy wniosków 403 |
| Wynik | Ewa i Piotr: 001 44 pkt (+1 strategiczny), 5700 zł; 002 30 pkt, 5700 zł |

### P4-18: kwota rekomendowana może przekroczyć wnioskowaną

- **Gdzie:** karta merytoryczna, "Proponowana kwota dotacji".
- **Co zrobiono:** grupa 002 wnioskuje o 3500 zł; scenariusz H3 pkt 4 każe dać jej 5700 zł.
- **Co się stało:** przyjęte bez ostrzeżenia, karta zakończona; panel eksperta pokazuje "Twoje rekomendacje razem 11 400,00 zł" przy "Wnioskowane razem 9200,00 zł".
- **Co miało się stać:** `docs/runbook/M5-ocena.md:85` opisuje kwotę rekomendowaną jako taką, która "może być niższa od wnioskowanej"; dotacja wyższa od wniosku nie ma sensu. Pole `proponowana_kwota` w `backend/seed/evaluation-cards/merit-2026.json:43` ma tylko `minValue: 0`. Skutek dla rozstrzygnięcia sprawdzony w I i K.
- **Też do poprawy:** scenariusz H3 pkt 4 (kwota 5700 dla grupy wnioskującej o 3500).

- **O-12:** przed deklaracją API odmawia słowami "Ten wniosek nie jest przypisany do Twojej oceny.", a wniosek jest przypisany, brakuje deklaracji. Ekspert z linkiem do wniosku nie dowie się, co ma zrobić.
- **O-13:** grupa nieformalna bez patrona dostaje w karcie merytorycznej kryterium strategiczne "Projekt złożony jest przez grupę nieformalną z Patronem", które jej z definicji nie dotyczy.

---

## Ścieżka I. Rozstrzygnięcie i wyniki

**Stan: przeszło z uwagami.** I5 (publiczne wyniki i archiwum) zrobione po K, J4 i L, zgodnie z radą scenariusza.

### I1. Zamknięcie naboru i start oceny

| Sprawdzenie | Wynik |
|---|---|
| "Zamknij nabór" | okno "Po zamknięciu naboru nikt nie złoży już wniosku, także przy naborze ciągłym. Zamknąć?", potem "Nabór zamknięty · Nabór został zamknięty. Wniosku nie można już złożyć." |
| Publiczna strona | bez "Wypełnij wniosek", zdanie o zamkniętym naborze |
| Nowy wniosek przez API kontem z kartą podmiotu | 409 "Nabór został zamknięty. Wniosku nie można już złożyć." |
| "Rozpocznij ocenę" | okno "Konkurs przejdzie do oceny. Rozpocząć?", stan "Trwa ocena" |

### I2. Lista rankingowa i kwoty

| Sprawdzenie | Wynik |
|---|---|
| Kolejność | według punktów: 001 88 + 2 strategiczne = 90, 002 60; oba "powyżej" progu; kolumny wnioskowanej, rekomendowanej i przyznanej kwoty, uwag, wyniku, ekspertów |
| Kwota przyznana 001 = 5700 (zapis po opuszczeniu pola) | "Przyznano 5700,00 zł z 20 000,00 zł, zostało 14 300,00 zł." |
| Przekroczenie puli (25 000 zł dla 002, potem cofnięte) | "Przyznano 30 700,00 zł z 20 000,00 zł: pula przekroczona o 10 700,00 zł." wyróżnione kolorem alarmowym (w wysokim kontraście żółtym, bo cały motyw jest czarno-żółty); przy okazji P4-19 |
| Grupa bez kwoty | po wyczyszczeniu pola zostaje bez kwoty |
| Eksporty | PDF do publikacji (200, `application/pdf`), XLSX (200, prawdziwy ZIP OOXML), CSV (200, BOM, `;`, polskie znaki, kolumna "Wynik: roboczo: dofinansowanie") |

### I3. Zatwierdzenie i wiadomości

| Sprawdzenie | Wynik |
|---|---|
| "Zatwierdź wyniki konkursu" | `<dialog>` z opisem skutków ("Wnioski z kwotą zostaną dofinansowane, pozostałe powyżej progu trafią na listę rezerwową... kwot nie będzie można już zmienić."), potem "Wyniki zatwierdzono 08.10.2026, 13:01. Kwot nie można już zmieniać."; 001 "Dofinansowany, umowa niepodpisana", 002 "Lista rezerwowa" |
| "Wyślij wiadomości o wynikach" | "Wysłano 2 z 2."; maile: organizacja "Wynik konkursu: wniosek dofinansowany" z numerem 001 i kwotą 5700,00 zł; grupa "Wynik konkursu: wniosek na liście rezerwowej" z numerem 002 |
| "Wyślij brakujące wiadomości" | wyłączony, gdy wszystko wyszło; wywołane wprost przez API zwraca `sent 2, pending 0`, w logu dalej dokładnie dwa maile o wynikach |

### I4. Udostępnienie kart

Okno "Udostępnić karty oceny wszystkim wnioskodawcom tego konkursu? Tej decyzji nie można cofnąć.", potem "Karty oceny udostępniono wnioskodawcom 08.10.2026, 13:02.". U wnioskodawcy 001: "Wynik konkursu: Dofinansowany, umowa niepodpisana, Przyznana kwota: 5700,00 zł", karta formalna i "Karta oceny merytorycznej 1" i "2" z punktacją; nigdzie imienia ani adresu eksperta.

### I5. Widok publiczny i archiwum

| Sprawdzenie | Wynik |
|---|---|
| Gość, `/competitions/<id>/results` przed archiwizacją | "Wyniki zatwierdzone 08.10.2026, 13:01 (czasu polskiego)", "Wnioski dofinansowane": 002, 60 pkt, 3500,00 zł; "Lista rezerwowa: Brak wniosków w tej części listy." (po K grupa przeszła z rezerwy do dofinansowanych, organizacji po rezygnacji nie ma na liście) |
| "Przenieś do archiwum" | okno "Konkurs zniknie z listy aktualnych konkursów, a jego strona zostanie pod tym samym adresem. Przenieść?", stan "Archiwalny" |
| `/competitions` | "Nie ma teraz ogłoszonego konkursu" |
| `/archive` | konkurs 1/2026 z datą zatwierdzenia i wynikami |
| `/competitions/<id>` i `/results` | 200 pod tym samym adresem, "Archiwalny", wyniki dalej widoczne |

### P4-19: kwota przyznana nie jest sprawdzana z kwotą wnioskowaną ani z limitem konkursu

- **Gdzie:** lista rankingowa, "Kwota przyznana" w wierszu.
- **Co zrobiono:** grupie 002 (wnioskowane 3500 zł, limit konkursu 7000 zł na wniosek) wpisano 25 000 zł.
- **Co się stało:** zapisane bez słowa przy wierszu; jedyny sygnał to przekroczenie całej puli. Przy puli 50 000 zł nie byłoby żadnego.
- **Co miało się stać:** ostrzeżenie (albo odmowa) przy kwocie wyższej niż wnioskowana i wyższej niż "maksymalna dotacja na wniosek" z kroku 1.4. Przekroczenie puli jest celowo bez blokady (`docs/runbook/M6-wyniki.md:32`), ale o limicie na wniosek dokumentacja nic takiego nie mówi.
- **Przyczyna:** `backend/src/Ocwip.Api/Services/Ranking/GrantDecisionService.cs:72` sprawdza tylko, że kwota jest dodatnia i ma najwyżej dwa miejsca po przecinku. Razem z P4-18 (rekomendacja ponad wnioskowaną) daje to łańcuch, w którym nigdzie nie pada pytanie, czy kwota ma sens.

- **O-15:** po zakończeniu kart i zatwierdzeniu wyników na liście rankingowej dalej stoi "Cofnij przypisanie eksperta" przy wnioskach. Nie klikałem, żeby nie ruszać zatwierdzonego wyniku; warto sprawdzić, co robi i czy w tym stanie powinien być dostępny.

---

## Ścieżka J. Umowa

**Stan: przeszło z uwagami.** J1 do J3 na wniosku 001; J4 (podpisanie) na wniosku 002 po K, bo umowa 001 musiała zostać niepodpisana do rezygnacji.

### J1. Wzór umowy

Wersja 1 z importu C2, edytor treści, objaśnienie fragmentów warunkowych (`{{#Organisation,PatronInformalGroup}} ... {{/}}`). Znaczniki systemowe: numer i data umowy, numer i data złożenia wniosku, nazwa, NIP, adres, tytuł, koszt, kwoty (także słownie), konkurs, członkowie grupy. Do wpisania przy każdej umowie: rejestr, numer w rejestrze, adres lidera grupy, reprezentant i funkcja, numer i data umowy z NIW, kontakt, terminy, numer rachunku, bank, źródło danych osobowych (patrz P4-20).

### J2. Sporządzenie i pola

| Sprawdzenie | Wynik |
|---|---|
| "Przygotuj umowę" na 001 | "Wzór w wersji 1. Jeszcze niepodpisana.", pola systemowe wypełnione ("pięć tysięcy siedemset złotych", data złożenia słownie), 14 pól do wpisania, "Data podpisania" |
| "Zapisz wartości" | "Zapisano wartości umowy." |
| PDF | 10 stron, polskie znaki poprawne (`pdftotext`), wartości wpisane trafiły na miejsce |
| U wnioskodawcy | "Umowa jest przygotowana do podpisu. Przeczytaj ją przed spotkaniem w OCWIP.", "Pobierz umowę (PDF)" pobiera prawdziwy PDF |

### J3. Hurtem

| Sprawdzenie | Wynik |
|---|---|
| ZIP z niewypełnionymi polami | archiwum z samym `braki.txt`: "Umowy bez kompletu pól do wpisania, których nie ma w tym pliku: 001 Stowarzyszenie ...: Rejestr, Numer w rejestrze, ... Źródło danych osobowych" |
| ZIP po uzupełnieniu | archiwum z `umowa-001.pdf`, bez `braki.txt` |

### J4. Data podpisania (wniosek 002, po K)

| Sprawdzenie | Wynik |
|---|---|
| Umowa grupy | bez rejestru i NIP-u; "Członkowie grupy nieformalnej (z wniosku): Anna Testowa, Jan Testowy, Ewa Testowa"; kwota "trzy tysiące pięćset złotych"; 12 pól do wpisania, zapisane |
| Data przyszła (20.10.2026) | po oknie potwierdzenia: "Data podpisania nie może być z przyszłości." |
| Data dzisiejsza | okno "Zapisać podpisanie umowy? Wniosek przejdzie w stan „umowa podpisana”, a wartości umowy nie będzie można już zmienić.", potem "Podpisana 08.10.2026.", data zawarcia słownie "8 października 2026 r." weszła do umowy, pola zablokowane |
| U grupy | "Wynik konkursu: Umowa podpisana", "Umowa podpisana 08.10.2026.", PDF umowy |

### P4-20: umowa każe wpisywać ręcznie dane, które system już ma

- **Gdzie:** umowa wniosku, pola "do wpisania przy każdej umowie".
- **Co jest:** rejestr, numer w rejestrze, reprezentant, funkcja reprezentanta i numer rachunku operator przepisuje ręcznie przy każdej umowie, choć karta podmiotu zamrożona przy złożeniu (widoczna na tej samej stronie w "Dane wnioskodawcy") ma dokładnie te dane; u grupy adres lidera i rachunek lidera są w odpowiedziach wniosku.
- **Dlaczego to wpis:** umowa to dokument podpisywany, a ręczne przepisywanie numeru rachunku, na który pójdą pieniądze, to najbardziej prawdopodobne miejsce literówki w całym procesie. Słownik pól systemowych (`backend/src/Ocwip.Api/Services/Documents/TemplatePlaceholders.cs`) zna NIP i adres z karty, ale nie te pola; w dokumentacji nie znalazłem decyzji, która by to uzasadniała.

- **O-14:** w umowie organizacji drukuje się "Członkowie grupy nieformalnej realizującej projekt, podpisujący umowę razem z Realizatorem: nie dotyczy", bo linia 6 wzoru `backend/seed/templates/contract-2026.txt` nie jest objęta `{{#InformalGroup}} ... {{/}}`, choć mechanizm do tego istnieje (zdanie wyżej z niego korzysta).

---

## Ścieżka K. Rezygnacja i lista rezerwowa

**Stan: przeszło z uwagami.** Rezygnacja na 001 (umowa przygotowana, niepodpisana), środki dla 002 z listy rezerwowej.

| Sprawdzenie | Wynik |
|---|---|
| Panel "Umowy, rezygnacje i lista rezerwowa" | termin podpisania umów 22.10.2026 13:01 (14 dni od wyników), wolne środki 14 300 zł, 001 z kwotą i "Potwierdź rezygnację 001", następny z rezerwy 002 z wnioskowaną kwotą 3500 zł; proponowana kwota w polu 3500 zł (wnioskowana, nie zawyżona rekomendacja 5700 zł z P4-18) |
| "Potwierdź rezygnację 001" | okno "Zapisać rezygnację wniosku 001? Wnioskodawca straci dofinansowanie, dostanie o tym wiadomość, a kwota wróci do puli dla listy rezerwowej.", wolne środki 20 000 zł, 001 "Rezygnacja", mail "Rezygnacja z dotacji: wniosek 001" |
| "Przyznaj dofinansowanie 002" (3500 zł) | 002 "Dofinansowany, umowa niepodpisana" z własnym terminem (22.10.2026 13:07), "Przyznano 3500,00 zł z 20 000,00 zł", mail "Dofinansowanie z listy rezerwowej: wniosek 002" z kwotą |
| Sprawozdanie po rezygnacji (API, konto organizacji) | 409 "Sprawozdanie składa się tylko z dofinansowanego projektu." |

- **O-16:** po rezygnacji jedynego dofinansowanego wniosku panel mówi "Wszystkie dofinansowane wnioski mają podpisaną umowę.", choć żadnej podpisanej umowy nie ma; zdanie prawdziwe tylko formalnie. Lepiej: "Nie ma dofinansowanych wniosków bez podpisanej umowy" albo osobny komunikat dla pustej listy.

---

## Ścieżka L. Sprawozdanie i rozliczenie

**Stan: przeszło z uwagami.** Na wniosku 002 z podpisaną umową.

| Sprawdzenie | Wynik |
|---|---|
| "Przejdź do sprawozdania" | `/panel/applicant/reports/<id>`, "Stan: W przygotowaniu.", cztery części; "Wartości z wniosku są pokazane jako tekst i nie da się ich zmienić. Obok wpisz, jak było naprawdę." |
| Część I | data "do" i gmina przyszły z wniosku, data "od" pusta, dane lidera do wpisania od nowa (patrz O-17) |
| Część II | opisy, liczba uczestników osiągnięta, rezultaty z wniosku z polem na osiągnięty poziom; opis działań wymaga co najmniej 1000 znaków ("Wymagane co najmniej 1000 znaków (jest 70)." na liście braków) |
| Część III | wydatki per pozycja budżetu z numerem dokumentu, wartością i kwotą z dotacji; sumy i udziały liczą się same (3500 zł, koszty pośrednie 8,57 procent) |
| "Złóż sprawozdanie" | okno "Po złożeniu sprawozdania nie zmienisz go, chyba że operator zwróci je do poprawy.", stan "Złożone, czeka na sprawdzenie", wstępne rozliczenie; maila z potwierdzeniem brak (O-18) |
| Operator, ocena kosztów | 100 zł z plakatów nieuznane z powodem: "Koszty nieuznane 100,00 zł, Koszty uznane 3400,00 zł, Kwota do zwrotu 100,00 zł", "Ocena kosztów zapisana." |
| "Zwróć do poprawy" bez powodu | przycisk wyłączony |
| Z powodem | okno, stan "Zwrócone do poprawy" z powodem, mail "Sprawozdanie z wniosku 002 zwrócone do poprawy" z treścią powodu |
| U wnioskodawcy | powód zwrotu i nieuznane koszty z uzasadnieniem; poprawka i ponowne złożenie |
| "Przyjmij sprawozdanie" | okno "Przyjąć sprawozdanie? ... wniosek zostanie rozliczony z zapisaną oceną kosztów.", stan "Przyjęte", wniosek `Settled`, mail "Sprawozdanie z wniosku 002 przyjęte. Do zwrotu zostaje 100,00 zł." |

- **O-17:** w sprawozdaniu grupy data "od" jest pusta, choć system zna datę podpisania umowy, a dane lidera (imię, adres, telefon) trzeba wpisać od nowa, choć wniosek ma je w tabeli członków; e-mail lidera jest tu wymagany, a we wniosku był nieobowiązkowy.
- **O-18:** złożenie sprawozdania (i ponowne złożenie) nie wysyła wnioskodawcy maila z potwierdzeniem, w przeciwieństwie do złożenia wniosku.
- **Pytanie do zespołu:** organizacja z przyznaną dotacją widzi "Przejdź do sprawozdania" przed podpisaniem umowy, a rozliczenie przyjmuje sprawozdanie także ze stanu "dofinansowany" (`backend/src/Ocwip.Api/Services/Reports/ReportService.Settlement.cs:57`). Scenariusz L zakłada sprawozdanie po podpisaniu; reguła nie jest nigdzie zapisana.

---

## Ścieżka M. Terminy i zadania w tle

Zadania w tle włączone (`BackgroundJobs__Enabled=true`, co 60 s). Dwa konkursy próbne skopiowane z 1/2026 (formularz, karty, wymóg `Statut`): **M1/2026** z naborem do 08.10.2026 13:35 (w oknie trzech dni i z bliskim odcięciem, więc obsługuje oba punkty M) oraz **M2/2026** z końcem 11.10.2026 13:48 (ponad trzy dni, granica okna przypomnienia).

### M2. Przypomnienie trzy dni przed końcem

| Sprawdzenie | Wynik |
|---|---|
| Szkic w M1 (`wnioskodawca@example.org`, 13:16; przy drugim wniosku karta podmiotu wypełniona, "Dane są aktualne" / "Popraw", co domyka też obietnicę z D2) | odpowiedzi przeniesione przez API autozapisu z wniosku 001, `Statut` przez API (pliki testowe zniknęły z `/tmp` przy restarcie maszyny, a rozszerzenie nie przyjmuje plików spoza katalogów sesji); wniosek kompletny, "Złóż wniosek" aktywny |
| Pierwszy przebieg zadania | mail tylko do `wnioskodawca@example.org`: "Przypomnienie: nabór "Próba terminów M (przebieg 4)" kończy się 08.10.2026 o godzinie 13:35 czasu polskiego" |
| Po trzech kolejnych przebiegach | dalej jeden mail dla M1 (raz) |
| Szkic grupy w M2 (koniec za ponad 3 dni) | zero przypomnień dla M2 i zero maili do grupy |
| Konta bez szkicu | żadnego przypomnienia |

### M1. Twarde odcięcie terminu naboru

| Sprawdzenie | Wynik |
|---|---|
| Strona szkicu otwarta przed 13:35, przycisk "Złóż wniosek" aktywny | po 13:35 strona dalej mówi "Nabór trwa..." i ma aktywny przycisk (licznik minut zniknął), patrz O-19 |
| "Złóż wniosek" o 13:35:54 (podsumowanie, okno, potwierdzenie) | `POST /submit` 409, w oknie "Nabór został zamknięty 08.10.2026 o godzinie 13:35 czasu polskiego. Wniosku nie można już złożyć." |
| Wersja robocza | zostaje (`Draft`, aktywna) |
| Autozapis po terminie | 409 z tym samym komunikatem |

- **O-19:** strona szkicu otwarta przed końcem naboru nie zauważa jego upływu: dalej "Nabór trwa" i aktywny "Złóż wniosek"; po odmowie okno potwierdzenia zostaje otwarte z aktywnym "Złóż wniosek" pod komunikatem o zamkniętym naborze. Odmowa jest poprawna i czytelna, tylko ekran zostaje w starym stanie.

---

## Ścieżka N. Izolacja danych

**Stan: przeszło z uwagami.** Próby przez API (w sesji danego konta) zrobione w czasie oczekiwania na odcięcie M1, próby z ekranu na końcu.

### Z ekranu

| Próba | Wynik |
|---|---|
| `grupa` otwiera `/panel/applicant/applications/<id wniosku 001>` | "Nie ma takiego wniosku. Ten adres nie prowadzi do żadnego z Twoich wniosków...", treść nie wycieka |
| wnioskodawca otwiera `/panel/operator` i `/panel/operator/competitions/<id>` | "403. Nie masz dostępu do panelu operatora" z drogą do własnego panelu, bez treści konkursu |
| wnioskodawca otwiera `/panel/reviewer` | "403. Nie masz dostępu do panelu recenzenta" |
| ekspert otwiera nieprzypisany wniosek | "Ten wniosek nie jest przypisany do Twojej oceny.", bez treści |
| ekspert w panelu | widzi tylko konkurs 1/2026, do którego jest powołany |
| wylogowany otwiera wniosek, ocenę operatora, panel eksperta | `/login?returnUrl=...` za każdym razem |
| wylogowany otwiera `/competitions/<id szkicu P4/BRAKI>` | "Nie ma takiej strony", bez nazwy konkursu |

### Przez API

| Próba | Wynik |
|---|---|
| `grupa` otwiera wniosek 001, jego PDF i załącznik | 403, 403, 403 |
| wnioskodawca pobiera listę wniosków operatora, wzór umowy, ranking CSV | 403, 403, 403 |
| wnioskodawca pyta `/reviewer/applications` | 403 |
| ekspert otwiera nieprzypisany wniosek (szkic w M1) i zaczyna jego kartę | 403, 403 |
| ekspert w konkursie, do którego nie jest powołany: ranking, złożenie deklaracji | 403, 404; odczyt deklaracji 200, patrz P4-21 |
| anonim: `/me`, wniosek, załącznik | 401, 401, 401 |
| anonim: publiczna strona szkicu `P4/BRAKI` | 404 |
| log backendu | brak haseł (żadnego z używanych w przebiegu), NIP-u, numerów rachunków, adresów i treści wniosków; "password" pada tylko jako nazwa kolumny w zapytaniach SQL, bez wartości |

### P4-21: ekspert odczytuje deklarację w konkursie, do którego nie jest powołany (niskie)

- **Gdzie:** `GET /reviewer/competitions/{id}/declaration`.
- **Co się stało:** `recenzent@example.org`, bez żadnego przypisania w M1/2026 ani w szkicu `P4/BRAKI`, dostaje 200 z treścią oświadczenia i stanem "NotDecided"; dla nieistniejącego identyfikatora 404 "Nie ma takiego konkursu.". Ekspert może więc sprawdzić istnienie konkursu, także szkicu, który gościowi odpowiada 404. Treści wniosków to nie odsłania; złożenie deklaracji w takim konkursie dostaje 404.
- **Co miało się stać:** reguła 1 z `AGENTS.md` ("brak reguły oznacza brak dostępu"): odczyt tylko dla eksperta przypisanego w tym konkursie, inaczej ta sama odpowiedź co dla nieistniejącego.
- **Przyczyna:** `backend/src/Ocwip.Api/Services/Ranking/DeclarationService.cs:36` sprawdza tylko istnienie konkursu (`:38`), nie przypisanie.

---

## Podsumowanie przebiegu

Cały scenariusz, ścieżki 0 do N, wykonany od nowa; wszystkie 35 pozycji karty wyniku przeszły, większość z uwagami. Pełny cykl na jednej bazie: konkurs 1/2026 opublikowany, dwa wnioski złożone (001 organizacji, 002 grupy), zwrot do poprawy i ponowne złożenie, ocena formalna i merytoryczna (dwóch ekspertów), wyniki zatwierdzone i rozesłane, karty udostępnione, umowa przygotowana, rezygnacja 001 i dofinansowanie 002 z listy rezerwowej, umowa 002 podpisana, sprawozdanie zwrócone, poprawione i przyjęte z rozliczeniem, konkurs zarchiwizowany. Odcięcie terminu i przypomnienie sprawdzone na konkursach próbnych, próby dostępu zakończone odmową.

### Według wagi

| Waga | Wpisy | Dlaczego |
|---|---|---|
| Do poprawy przed wdrożeniem | P4-08, P4-15, P4-16, P4-19 | P4-08 prowadzi każdą grupę do wpisania danych członków w złe kolumny; P4-15 gubi pracę wnioskodawcy po cichu; P4-16 blokuje grupę bez patrona przy konfiguracji ze scenariusza (wymaga decyzji); P4-19 pozwala przyznać dowolną kwotę ponad wniosek i limit konkursu bez słowa ostrzeżenia |
| Ważne | P4-04, P4-07, P4-12, P4-13, P4-14, P4-17, P4-18, P4-20 | błędna polszczyzna w pierwszym mailu; kreator konkursu nie mówi, gdzie są braki; nawigacja po wniosku zostawia człowieka na dole sekcji; podpowiedź limitu prowadzi w pętlę; pomyłkowego załącznika nie da się wycofać; brak zwrotu do poprawy przy negatywnej ocenie formalnej; rekomendacja ponad wnioskowaną; ręczne przepisywanie danych do umowy, w tym numeru rachunku |
| Drobne | P4-01, P4-02, P4-03, P4-05, P4-06, P4-09, P4-10, P4-11, P4-21 | spójność, kosmetyka, rozjazdy z obietnicami scenariusza, drobny wyciek istnienia konkursu do eksperta |
| Obserwacje | O-01 do O-19 | dostępność, komunikaty, treść wzoru umowy, stan ekranu po zdarzeniach |
| Pytania do zespołu | P4-16 (statut grupy bez patrona), L (sprawozdanie przed podpisaniem umowy), O-15 ("Cofnij przypisanie" po zatwierdzeniu wyników) | reguły, których dokumentacja nie rozstrzyga |

### Scenariusz do poprawy

Rozjazdy opisane przy ścieżkach C, D, F, H (oznaczenie kroku kreatora, miejsce po zapisie, lista braków przy publikacji, stan "są błędy", podmiana i usuwanie załącznika, sekcje podsumowania, "reszta szara" w poprawce, kwota 5700 zł dla grupy wnioskującej o 3500 zł w H3) oraz F1 (zakładek "wyszarzonych z wyjaśnieniem" w stanie "trwa nabór" nie da się zobaczyć, bo każda ma treść). Według zasady z [`przejscie-gui.md`](przejscie-gui.md) poprawia się scenariusz, gdy opisuje produkt niezgodnie z tym, co produkt robi; tu w kilku miejscach trzeba najpierw zdecydować, czy zmienić produkt (P4-11, P4-14), czy opis.

### Uwagi do metody

- Rozszerzenie Claude in Chrome działa tylko w sesji Claude Code uruchomionej z `--chrome`. Limit `file_upload` to 10 MB na wywołanie, więc próba za dużego pliku idzie przez API.
- Wylogowanie kończy wszystkie sesje konta (celowo). Logowanie i wylogowanie tym samym kontem z boku (curl) zrywa sesję w przeglądarce; tak wyszło P4-15.
- Formularz logowania czyści hasło po odmowie, więc próby blokady trzeba wpisywać od nowa (opis przy B5).
- Kliknięcia przez referencje narzędzia bywają zawodne (jedno "Wypełnij dalej" nie zadziałało, kliknięcie we współrzędne tak); przed zgłoszeniem "przycisk nie działa" warto powtórzyć klik inaczej.
- Konta i dane z przebiegu: pięć z `create_test_users.py`, `test.reczny.nowy@example.org` (hasło zmienione w B6), pięć niepotwierdzonych `*-p4@example.org` z pomiaru czasu, szkic konkursu `P4/BRAKI`, dezaktywowana kopia `1/2027`, konkursy próbne `M1/2026` (nabór zamknięty, niezłożony szkic organizacji) i `M2/2026` (nabór do 11.10, szkic grupy). Konkurs 1/2026 jest archiwalny: 001 po rezygnacji, 002 rozliczony.

---

## Znaleziska

Numeracja `P4-xx`, jeden wpis na jedną porażkę: adres strony, co kliknięto, co się stało, co miało się stać.

Wpisy stoją przy ścieżkach, na których wyszły: P4-01 (krok 0), P4-02 i P4-03 (A), P4-04 do P4-06 i P4-10 (B), P4-07 do P4-09 i obserwacje O-01 do O-05 (C), P4-11 do P4-15 i O-06 do O-09 (D), P4-16 i O-10 (E), O-11 (F), P4-17 (G), P4-18, O-12, O-13 (H), P4-19, O-15 (I), P4-20, O-14 (J), O-16 (K), O-17, O-18 (L), O-19 (M), P4-21 (N). Pełna lista według wagi w podsumowaniu wyżej.

---

## Stan poprawek

Stan na 2026-10-09, jedna gałąź i jeden pull request na cały przebieg (`fix/gui-pass-4`). Zapis wyżej zostaje bez zmian, jako dowód, co produkt robił w dniu przejścia; tutaj jest tylko, co z każdym wpisem zrobiono. Każda poprawka ma test, który bez niej nie przechodzi.

| Wpis | Stan | Co zmieniono |
|---|---|---|
| P4-01 | poprawione | `report_versions` w krotce `TABLES` w `scripts/seed.py` |
| P4-02 | poprawione | każda strona publiczna i strona konta ma tytuł "<nazwa> \| Generator konkursów OCWIP" |
| P4-03 | poprawione | deklaracja mówi, że kontrast (1.4.3) sprawdzano tylko ręcznie |
| P4-04 | poprawione | `PolishPlural.Hours`: "24 godziny", "1 godzinę" |
| P4-05 | poprawione | "nieprawidłowy lub wygasły albo konto zostało już potwierdzone" |
| P4-06 | poprawione w dokumentacji | zdanie w `przeglad-bezpieczenstwa.md` mówi o różnicy około 10 ms przy rejestracji zamiast "tyle samo czasu" |
| P4-07 | poprawione | braki przed zapisem oznaczają swoje kroki, są ogłaszane (`role="alert"`) i znikają po uzupełnieniu |
| P4-08 | poprawione | komórka narożna nagłówka dla obu rodzajów tabel |
| P4-09 | poprawione | oświadczenie z samą etykietą pokazuje początek treści w kreatorze, w odwołaniach i na liście braków; dane wzoru bez zmian |
| P4-10 | poprawione | "Powtórz nowe hasło" w "Moje konto" |
| P4-11 | poprawione | wniosek otwiera się na ostatniej części (pamięć przeglądarki); zwrot do poprawy dalej otwiera pierwszą część do poprawy |
| P4-12 | poprawione | po "Dalej" i "Wstecz" fokus i widok na nagłówku nowej części |
| P4-13 | poprawione | przy limicie procentowym od podstawy, która zawiera samo pole, podpowiedź podaje rozwiązane maksimum (577,77 zł w przykładzie) |
| P4-14 | poprawione | "Wycofaj" przy pliku, z potwierdzeniem; wiersz nieaktywny, nic nie skasowane; złożony wniosek odmawia |
| P4-15 | poprawione | własny komunikat o zakończonej sesji, logowanie w nowej karcie i "zapisz zmiany" bez utraty wpisanych odpowiedzi |
| P4-16 | pytanie | [`runbook/pytania.md`](runbook/pytania.md), P25 |
| P4-17 | poprawione | pod zakończoną negatywną kartą formalną formularz zwrotu z opisem wypełnionym kryteriami na "Nie" i ich uzasadnieniami |
| P4-18 | poprawione | zakończenie karty merytorycznej odmawia kwoty wyższej od wnioskowanej |
| P4-19 | poprawione | decyzja na liście rankingowej odmawia kwoty wyższej od wnioskowanej; przekroczenie puli dalej bez blokady |
| P4-20 | poprawione | nowa umowa bierze rejestr (KRS), numer w rejestrze, reprezentanta z funkcją i rachunek z zamrożonej karty, do zmiany przez operatora; adres lidera grupy czeka na P21 |
| P4-21 | poprawione | odczyt deklaracji tylko z przypisaniem albo własną decyzją, poza tym 404 jak dla nieznanego konkursu |
| O-01 | poprawione | po usunięciu sekcji fokus na sekcji, która zajęła jej miejsce |
| O-02 | poprawione | "Odrzuć szkic i zacznij od nowa" pyta o potwierdzenie |
| O-03 | bez zmiany | nazwa odrzuconego pliku w polu wyboru i komunikat zostają do następnej akcji w tym samym miejscu; do wzięcia razem z O-09 |
| O-04 | poprawione | ramka braków mówi, kto wgrywa pierwsze karty; wyłączony "Opublikuj konkurs" ma `aria-describedby` do listy braków |
| O-05 | poprawione | czytnik ekranu słyszy "Termin realizacji zadań do" |
| O-06 | poprawione | pole wyliczane jest tylko do odczytu, a nie wyłączone, i ma `aria-invalid` oraz `aria-describedby` do komunikatu limitu |
| O-07 | poprawione | "Dodaj wiersz: <tabela>" i "Popraw: <część>" w nazwie dostępnej; komórki budżetu bez zmiany |
| O-08 | poprawione | mały plik w KB, nie "(0 MB)" |
| O-09 | bez zmiany | jak O-03: komunikat odmowy na kafelku zostaje po udanej podmianie w wierszu obok |
| O-10 | bez zmiany | rodzaj wnioskodawcy w części I nie startuje z karty; backend odmawia sprzeczności przy złożeniu |
| O-11 | poprawione | w poprawce części zablokowane są szare i podpisane "zablokowana" |
| O-12 | poprawione | odmowa na stronie wniosku eksperta podpowiada deklarację bezstronności, tymi samymi słowami dla wniosku przypisanego i nieprzypisanego |
| O-13 | pytanie | P27 |
| O-14 | poprawione | linia o członkach grupy we wzorze umowy tylko dla grup |
| O-15 | poprawione | po zatwierdzeniu wyników lista rankingowa nie pokazuje "Cofnij" ani przypisania (serwer i tak odmawiał) |
| O-16 | poprawione | "Nie ma dofinansowanych wniosków bez podpisanej umowy." |
| O-17 | bez zmiany | podpowiedzi sprawozdania z wniosku i z umowy to osobna praca nad wzorem sprawozdania |
| O-18 | bez zmiany | mail potwierdzający złożenie sprawozdania to nowa wiadomość, poza zakresem tej poprawki |
| O-19 | poprawione w części | po odmowie 409 okno nie proponuje drugiej próby, a panel mówi, że nabór się zamknął; sam upływ terminu na otwartej stronie bez odmowy dalej niczego nie zmienia |
| Pytanie z L | pytanie | P26 |
| Scenariusz | poprawione | `przejscie-gui.md`: miejsce po zapisie, oznaczenie kroku, lista braków przy publikacji, "w toku" i "są błędy", "Zastąp" i "Wycofaj", sekcje rozwinięte, kwota grupy w H3, odmowa ponad wnioskowaną w I2 zamiast czerwonej puli |
