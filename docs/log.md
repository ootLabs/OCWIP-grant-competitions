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

## 2026-10-04 - maile kont przez kolejkę, czas odpowiedzi nie zdradza konta
**Zrobione:** Weryfikacja i reset hasła oddają mail do kolejki w pamięci (`IAccountMailQueue`), więc `/register`, `/forgot-password` i `/resend-verification` odpowiadają tak samo szybko dla znanego i nieznanego adresu; wcześniej znany adres czekał na przekaźnik SMTP.
**Decyzje:** Błąd przekaźnika nie wraca już jako 500 (kolejka próbuje trzy razy, potem loguje temat), więc człowiek prosi o mail jeszcze raz. Uzasadnienie w [`architektura.md`](architektura.md), sekcja o rejestracji.
**Uwaga:** Nowy test `Forgot_password_does_not_wait_for_the_mail_relay` potrzebuje PostgreSQL, bez bazy jest pomijany. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-10-03 - dokumentacja znormalizowana, jedna lista w jednym pliku
**Zrobione:** Pytania do zamawiającego z czterech miejsc zebrane w [`runbook/pytania.md`](runbook/pytania.md) (paczka `PK-A` do `PK-P` i pytania `P1` do `P22`, każde z odnośnikiem do założenia i blokera). Scenariusz przejścia ręcznego istniał w dwóch identycznych kopiach (`testGUI.md` i `preproduction-test.md`), został jeden: [`przejscie-gui.md`](przejscie-gui.md). Dwa dzienniki błędów z przejść połączone w [`przejscie-gui-bledy.md`](przejscie-gui-bledy.md), przebieg po przebiegu, bez przenumerowania. Warunki ukończenia zostały tylko w `AGENTS.md`; runbook, `CONTRIBUTING.md` i szablon PR odsyłają tam.
**Decyzje:** Identyfikatorów nie przenumerowujemy (`PK`, `P`, `B-GUI`, znaleziska), bo krążą po kartach Trello i po komentarzach w kodzie. Nowa reguła w `AGENTS.md`: jedna lista w jednym pliku, a brakujące rzeczy dopisujemy w pliku kanonicznym, nie obok.
**Uwaga:** Poprawione przy okazji: 97 zepsutych odnośników w `log-archiwum/2026.md` i `map/backend.md` (plik przeniesiony o katalog niżej, linki zostały), liczba stanów wniosku w `proces.md` (dziewięć, nie siedem) i "administrator" w `zakres.md`, którego w kodzie nie ma. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-10-03 - dziennik przejścia GUI zamknięty, pytanie P21 zapisane
**Zrobione:** Dwa ostatnie otwarte znaleziska z [`przejscie-gui-bledy.md`](przejscie-gui-bledy.md) poprawione: etykiety pól umowy mają polską pisownię (`BlankLabels`, nazwa spoza słownika nadal generuje etykietę), a sprawozdanie czeka z przyciskiem na komplet, z listą braków prowadzącą kursorem do pola jak we wniosku. Pozostałe szesnaście przejrzane w kodzie i opisane stanem w tabeli znalezisk.
**Decyzje:** Fokus po kliknięciu braku wydzielony do wspólnego `useFieldFocus`, bo wniosek i sprawozdanie potrzebują tego samego. Adres grupy nieformalnej w umowie zostaje pytaniem P21 do zamawiającego ([`runbook/decyzje.md`](runbook/decyzje.md), założenie ZR-19), a nie wymyślonym polem.
**Uwaga:** Seria `P` (pytania do zamawiającego) ma teraz dwa miejsca: P9 do P20 w komentarzach blokerów na Trello, P21 i następne w `runbook/decyzje.md`. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-10-03 - znaleziska z przejścia przedprodukcyjnego poprawione
**Zrobione:** Dwanaście znalezisk z [`przejscie-gui-bledy.md`](przejscie-gui-bledy.md) i cztery drobne obserwacje poprawione, każde z testem: komunikat o niezgodnych powtórzeniach znika po poprawieniu pola, lista wniosków i jej eksporty mają kolumnę oceny formalnej, ekran pokazuje komunikat walidacyjny backendu, umowa grupy nieformalnej nie żąda rejestru i NIP-u, zwrot i przyjęcie sprawozdania wysyłają mail, adres wychodzi z linku potwierdzającego zmianę e-maila, a kwoty w zdaniach dla ludzi są grupowane jak na ekranach.
**Decyzje:** Fragment wzoru umowy dla wybranych rodzajów wnioskodawcy (`{{#Organisation,...}} ... {{/}}`), reguła wyniku formalnej w jednym `FormalStandingReader`, adres oczekujący w `users.pending_email` zamiast parametru w linku, dwa formaty kwoty (zdanie kontra arkusz). Uzasadnienia w [`architektura.md`](architektura.md). Obserwacje 3 i 5 świadomie bez zmiany, 6 i 7 były błędem scenariusza, nie produktu.
**Uwaga:** Wzór umowy 2026 zmienił się w pliku startowym, więc konkurs z już zaimportowanym wzorem potrzebuje nowej wersji (ekran wzoru albo `import-content --contract`). Adres grupy nieformalnej w umowie zostaje pytaniem do zamawiającego: na razie operator wpisuje `{{adres_lidera}}` ręcznie. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-30 - przejście ręczne GUI przed wystawieniem na serwer
**Zrobione:** Cały cykl konkursu wyklikany w przeglądarce w jednym przebiegu na jednej bazie, od pustego systemu do rozliczonej dotacji. Scenariusz siedzi w [`przejscie-gui.md`](przejscie-gui.md), dziennik porażek w [`przejscie-gui-bledy.md`](przejscie-gui-bledy.md). Żadna ścieżka nie została zablokowana.
**Decyzje:** Z osiemnastu znalezisk dwanaście poprawionych na tej gałęzi (B-GUI-02 do B-GUI-09, 11, 12, 15, 16), B-GUI-01 był warunkiem środowiska, nie usterką produktu. Pięć zostaje otwartych: B-GUI-10, 13, 14, 17, 18.
**Uwaga:** Przejście zostawiło dane w bazie (cztery konkursy, sześć kont), więc powtórka chce świeżego wolumenu. Na Windows import treści startowej wymaga `MSYS_NO_PATHCONV=1`, inaczej Git Bash przepisuje `/src/seed/...` na ścieżkę Windows.

## 2026-09-30 - zapis do wniosku pod blokadą i tylko dla wnioskodawcy
**Zrobione:** Autozapis i załączniki biorą blokadę wiersza na czas sprawdzeń, złożenie odmawia (409), gdy odpowiedzi zmieniły się po walidacji, a zapis do wniosku wymaga roli wnioskodawcy. Ekspert nie zostaje przypisany do szkicu, a podmiana załącznika trzyma się formatów wymogu.
**Decyzje:** Przy rozjeździe odpowiedzi 409 zamiast cichego zamrożenia: zamrożonej wersji wnioskodawca już nie poprawi. Uzasadnienie w [`architektura.md`](architektura.md).
**Uwaga:** Czystego wyścigu dwóch równoległych żądań nie ma w testach, bo byłby niestabilny; sprawdzana jest bramka, nie splot. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-30 - rejestracja pyta o adres i hasło dwa razy (T-124, R-19)
**Zrobione:** Formularz rejestracji ma powtórzenie adresu i hasła, sprawdzane przy wysłaniu; niezgodność zatrzymuje żądanie i mówi to przy właściwym polu. R-19 zostaje otwarte wyłącznie na telefonie kontaktowym.
**Decyzje:** Powtórzenia zostają na froncie, `RegisterRequest` bez zmian: to pytanie "czy na pewno to wpisałeś", a nie dana o człowieku. Adres porównywany bez rozróżniania wielkości liter (jeden indeks, jedno konto), hasło dosłownie. Uzasadnienie w [`architektura.md`](architektura.md) (T-124).
**Uwaga:** `getByLabel` w Playwright dopasowuje po fragmencie i bez wielkości liter, więc "Adres e-mail" łapie też "Powtórz adres e-mail". Scenariusze e2e pytają teraz o etykietę dokładną; następna etykieta zaczynająca się od istniejącej zepsuje je tak samo.

## 2026-09-30 - nieczytelne ciało JSON kończy się 400, nie 500 (T-123, R-43)
**Zrobione:** Ucięty JSON, pole o złym typie i bajty spoza UTF-8 na dowolnej trasie z ciałem dają 400 jako ProblemDetails z polskim komunikatem, a w logu nie ma wpisu `Error`, więc monitoring (T-116) ani test obciążenia (T-118) nie liczą cudzego skanera jako awarii.
**Decyzje:** `IExceptionHandler` oddaje status z samego wyjątku zamiast stałego 400, więc ciało ponad limit Kestrela też przestaje być 500 (413). Komunikat stały, bo wyjątek potrafi zacytować ciało. Uzasadnienie w [`architektura.md`](architektura.md) (T-111).

## 2026-09-29 - kreator buduje formularz od zera i przestawia sekcje (T-26a)
**Zrobione:** Operator dodaje, usuwa i przestawia sekcje, zaczyna formularz od pustej sekcji zamiast kopiować konkurs, i nadaje wiersze tabeli o stałej liczbie wierszy, której kreator dotąd potrafił tylko dodać i nigdy naprawić. Ruch i usunięcie sekcji są zablokowane zdaniem o tym, co by się zepsuło, zamiast ścieżką JSON przy publikacji.
**Decyzje:** Blokada ruchu liczy dokument PO ruchu i odejmuje naruszenia, które dokument miał wcześniej, zamiast zakazywać ruchu sekcji z warunkiem: formularz zepsuty wcześniej nie zamraża się w miejscu. Uzasadnienie w [`architektura.md`](architektura.md) (T-26a).
**Uwaga:** Karta ruszona bez odpowiedzi na cztery pytania z B-10, decyzją Piotra. B-10 zostaje otwarty i zawęzi ten kreator, a nie przestawi.

## 2026-09-29 - umowa drukuje stronę ze złożenia, PDF nie pada na wcięciu
**Zrobione:** Nazwa, NIP i adres na umowie pochodzą z kopii karty zapisanej przy złożeniu, więc podpisana umowa drukuje się tak samo po zmianie karty. Wcięcie szersze niż pół linii nie wywraca już PDF-u wniosku, umowy ani pakietu umów. Zapis wartości umowy bez `values` to 400, rezygnacja wycofuje nieprzyjęte sprawozdanie, a podpisania nie da się zapisać na wycofanej umowie ani przy wniosku, który nie jest już dofinansowany.
**Uwaga:** Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-29 - testy PDF, pakietu umów i CSP mówią to, co obiecują
**Zrobione:** Test podzbioru czcionki sprawdza każdą składową glifu złożonego, test pakietu umów także brak wniosku odrzuconego, a naruszenie CSP w e2e oblewa test, który je spowodował. Nieaktualne komentarze o czcionce w całości poprawione.
**Decyzje:** Limit kosztów pośrednich w sprawozdaniu (od czego liczony, czy nadwyżka jest nieuznana sama) to pytanie do OCWIP, zapisane jako R-41; do odpowiedzi nadwyżkę odmawia operator.
**Uwaga:** Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-29 - przeszyfrowanie i log odczytów bez cichych luk
**Zrobione:** `reencrypt-data` czyta flagi `sensitive` z zapisanej definicji, gdy wersja formularza nie przechodzi dzisiejszego kontraktu, zamiast przepisać wrażliwe odpowiedzi jawnym tekstem. Log odczytów sprawdza wartość trasy przy starcie, nie liczy Forbid i przekierowań jako odczytu, a test trzyma listę ośmiu logowanych endpointów. Compose produkcyjne ma drugi klucz na czas rotacji.
**Decyzje:** Identyfikator wiersza poza danymi powiązanymi szyfrowania: podmiana między wierszami wymaga zapisu do bazy, a z nim atakujący zmienia i tak jawne kolumny. Uzasadnienie w [`architektura.md`](architektura.md) (T-47a).
**Uwaga:** Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-29 - konta zespołu, zmiana adresu, kopia i wzory bez luk
**Zrobione:** `reactivate-account` cofa wyłączenie konta, a ostatniego aktywnego operatora `deactivate-account` nie wyłączy. Zmiana adresu zapisuje adres i nazwę konta jednym zapisem, a adres zajęty dostaje powiadomienie bez linku, więc oba przypadki trwają tyle samo. Kopia konkursu pomija kontaktowego, który nie jest już operatorem, a dwie podmiany wzoru naraz idą po kolei.
**Decyzje:** Ponowna akceptacja regulaminu po jego zmianie to pytanie do OCWIP i IOD, zapisane jako R-40, nie poprawka.
**Uwaga:** Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-29 - terminy umów, maile po zapisie i odporny harmonogram
**Zrobione:** Awans z listy rezerwowej ma własne 14 dni i własne przypomnienie, a zadanie patrzy tylko na terminy z ostatniego tygodnia. Odmowa maila po rezygnacji albo awansie zostawia zmianę i mówi `mailSent: false` zamiast 500; mail o rezygnacji podaje prawdziwą przyczynę. Wysyłka SMTP ma limit czasu, pętlę zadań kończy tylko zatrzymanie hosta, a temat przypomnienia o naborze podaje chwilę końca.
**Decyzje:** Termin umowy od ostatniego przejścia na `Funded` w historii: dla awansowanego wynikiem jest awans. Założenie do potwierdzenia z OCWIP, opisane w [`architektura.md`](architektura.md) (T-109).
**Uwaga:** Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-29 - wdrożenie wraca po nieudanym starcie, staging przechodzi
**Zrobione:** `deploy.sh` wraca do poprzedniego commita także wtedy, gdy samo `up` się nie uda (migracja, usługa bez zdrowia). Workflow wdrożenia przyjmuje tylko commit zmergowany i pyta staging z hasłem (`SITE_BASIC_AUTH`). API ufa nagłówkom przekazanym tylko od stałego adresu Caddy. CI ma domyślnie `contents: read` i akcje przypięte do commita. `monitor.py --backup-max-age` alarmuje, gdy kopia nocna nie powstaje, a `restore.sh` nie restartuje działającej bazy.
**Decyzje:** Staging dostaje hasło w workflow, a nie wyjątek w Caddy dla `/api`: zostaje w całości prywatny. Wiek kopii sprawdza monitor poza serwerem, bo alarm z serwera zamilknie razem z nim.
**Uwaga:** Administrator: "Deployment branches" w obu środowiskach i sekret `SITE_BASIC_AUTH` na stagingu ([`wdrozenie.md`](wdrozenie.md)). Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-29 - zwrot do poprawy nie blokuje konkursu
**Zrobione:** Wniosek zwrócony i niezłożony do terminu poprawy zatwierdzenie wyników odrzuca, zamiast stać na nim w nieskończoność. Zwrot czyści roboczą decyzję o kwocie, ponowne złożenie zostawia kopię karty podmiotu z pierwszego złożenia, a otwarcie karty oceny blokuje wiersz wniosku przed równoczesnym zwrotem.
**Decyzje:** Po terminie odrzucenie, a nie przedłużenie ani ręczna decyzja operatora: okno poprawy już się nie otworzy, a nieuzupełniony wniosek i tak nie przeszedłby oceny formalnej. Uzasadnienie w [`architektura.md`](architektura.md), sekcja T-103.
**Uwaga:** Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-29 - przegląd wszystkich tras bez sesji
**Zrobione:** `AnonymousRouteSweepTests` czyta tabelę endpointów aplikacji: 18 tras publicznych to przejrzana lista z powodami, każda inna odpowiada 401 bez sesji. Pod strażą `PermissionSuiteCiGuardTests`.
**Decyzje:** Lista w teście, nie w kodzie produkcyjnym: nowa trasa publiczna jest widoczna w review jako zmiana testu. Sprawdzone mutacją (`AllowAnonymous` na `/me`, polityka przepuszczająca każdego na `GET /applications`).
**Uwaga:** Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-29 - CI pilnuje wygenerowanego klienta API (R-32)
**Zrobione:** Job smoke generuje `frontend/lib/api-schema.ts` z dokumentu OpenAPI i odmawia przy różnicy z zacommitowanym. `npm run api:generate` w kontenerze działa, R-32 zamknięte.
**Uwaga:** Po zmianie sygnatury endpointu nadal `docker compose restart backend` przed generowaniem (README). Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-29 - jedna pisownia nazw w kontrakcie formularza (R-34)
**Zrobione:** Sposób obliczenia, rodzaj limitu i format pliku w definicji formularza są czytane z rozróżnianiem wielkości liter; `"Ratio"` jest odrzucane przy zapisie ze ścieżką pola.
**Decyzje:** Ustąpił backend, nie front: jedno miejsce zamiast siedmiu i jedna pisownia w zapisanym JSON-ie. Uzasadnienie w [`architektura.md`](architektura.md), sekcja T-24.
**Uwaga:** Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-29 - dokumentacja dogania kod (T-92)
**Zrobione:** "Stan repozytorium" w `AGENTS.md` i "Czego tu jeszcze nie ma" w `architektura.md` opisują stan faktyczny i odsyłają do `kolejka.md`; README i `seed.py` mówią, jak zalogować się na konto z seeda (reset hasła, mail w Mailpit); D16 w `decyzje.md`; `npm ci` w mapie infra.
**Decyzje:** Rozjazd "recenzent" kontra "ekspert" zapisany jako R-39, bez zmiany UI: to nazewnictwo do potwierdzenia z OCWIP.
**Uwaga:** Reset hasła na koncie z seeda sprawdzony na lokalnym stosie (mail, reset, logowanie). Log przekroczył limit, najstarszy wpis w archiwum.
