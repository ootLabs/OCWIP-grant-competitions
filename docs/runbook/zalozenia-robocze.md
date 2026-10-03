# Założenia robocze

Miejsce na wszystko, co zbudowaliśmy **bez potwierdzenia w dokumentach**, żeby praca nie stała: interpretacje, wartości przyjęte na próbę i prototypy idące dalej niż to, co mówią raport, regulamin albo karty. Decyzja użytkownika z 2026-09-25: zamiast zatrzymywać pętlę przy każdej takiej luce, budujemy według najrozsądniejszej interpretacji i zapisujemy ją tutaj.

Różnica wobec pozostałych plików w `runbook/`:

- [`rozbieznosci.md`](rozbieznosci.md) to miejsca, gdzie **dwa źródła mówią co innego**;
- [`blokery.md`](blokery.md) to rzeczy, których **nie da się zrobić** bez dokumentu;
- ten plik to rzeczy, które **zrobiliśmy mimo braku odpowiedzi**, i które trzeba potwierdzić albo cofnąć.

Każda pozycja mówi, gdzie siedzi w kodzie, bo cofnięcie założenia to konkretna zmiana, a nie dyskusja. Ostatnia kolumna niesie **numer pytania**, nie jego treść: wszystkie pytania do zamawiającego, z pełnym brzmieniem i odpowiedzią domyślną, stoją w [`pytania.md`](pytania.md). Wcześniej to samo pytanie bywało opisane tutaj, w `blokery.md` i w paczce dla klientki, każde trochę inaczej.

| ID | Założenie | Gdzie w kodzie | Skąd wątpliwość | Pytanie |
|---|---|---|---|---|
| ZR-01 | Kwota rekomendowana na liście rankingowej to **średnia** kwot proponowanych przez ekspertów, zaokrąglona do grosza | `Services/Ranking/RankingCalculator.cs`, `RankingRow.RecommendedGrant` (T-39) | Regulamin 2026 mówi o "proponowanej kwocie" na każdej karcie, nie mówi, jak łączyć dwie. Raport mówi, że decyduje operator (T-42) | ZR-01 ([`pytania.md`](pytania.md)) |
| ZR-02 | Skala ostrzeżenia o rozbieżności to suma `maxValue` pól składających się na sumę merytoryczną karty (50 w 2026); karta bez tych widełek nie ostrzega wcale | `RankingCalculator.MeritScale` (T-39) | Raport: "próg rozbieżności domyślnie 30% skali", bez definicji skali; regulamin 2026 progu nie zna (P2) | P2 |
| ZR-03 | Próg punktowy i ostrzeżenie o rozbieżności nie mają wartości domyślnej dla istniejących konkursów (`null`); liczba ekspertów (2) i suma mają | migracja `AddEvaluationSettings` (T-39) | Regulamin 2026 podaje próg 50, ale konkursy utworzone wcześniej mogą mieć inny regulamin | ZR-03 |
| ZR-04 | ~~Panel recenzenta bez bramy deklaracji bezstronności~~ **Zamknięte w T-40a**: brama jest w warstwie autoryzacji | - | - | - |
| ZR-05 | Tekst deklaracji bezstronności jest **roboczy**: mówi to, czego wymaga § 2 regulaminu komisji 2026, bo załącznika 1 (samej deklaracji) nie ma w opublikowanych dokumentach. Każda decyzja zapisuje tekst, który ekspert widział | `Models/ImpartialityDeclaration.cs` (T-40a) | Brak załącznika 1 | P8 |
| ZR-06 | Tabela ekspertów w ocenie konkursu ma kolumny: imię i nazwisko, adres, liczba przypisanych wniosków, deklaracja i powód odmowy. **Bez** liczników oceny formalnej i "dostępu podglądowego" z raportu (krok 5.1) | `app/panel/operator/evaluation/[competitionId]/experts-table.tsx` (T-41) | Ocenę formalną robi u nas operator, nie ekspert (regulamin 2026), więc licznik formalny eksperta byłby zawsze zerem; raport nie mówi, czym jest dostęp podglądowy | P9 |
| ZR-07 | Przypisanie grupowe przypisuje jednego eksperta do zaznaczonych wniosków po kolei, wniosek, który ekspert już ma, pomija; pierwsza odmowa zatrzymuje serię, a to, co przeszło, zostaje | `ranking-table.tsx`, `page.tsx` w `evaluation/[competitionId]` (T-41) | Raport wymaga działania na grupie, nie mówi o losowaniu ani równym podziale | P10 |
| ZR-08 | Udostępnienie kart nie czeka na koniec oceny: operator może je zrobić w każdej chwili, a wnioskodawca widzi tylko karty **zakończone**; karta zakończona po udostępnieniu pojawi się u niego od razu. Wnioskodawca widzi też proponowaną kwotę z karty merytorycznej | `Services/Ranking/CardSharingService.cs`, `CardSharingEndpoints.cs` (T-41b) | Raport mówi o jednej nieodwracalnej decyzji, nie mówi, czy wymaga kompletu kart ani które pola karty są jawne | P11 |
| ZR-09 | Wynik zatwierdzenia: kwota przyznana daje `Funded`, miejsce na liście powyżej progu bez kwoty daje `Reserve` (lista rezerwowa), reszta `Rejected`. Zatwierdzenie odmawia, dopóki któryś wniosek nie ma zakończonej oceny formalnej albo kompletu kart merytorycznych | `Services/Ranking/GrantDecisionService.cs`, `ApplicationStatus` (T-42) | Regulamin 2026 zna rezygnację i przejście środków na kolejny wniosek "spełniający próg", ale nie nazywa listy rezerwowej; raport nie mówi, co z wnioskiem ocenionym pozytywnie bez środków | P12 |
| ZR-10 | Publiczna lista wyników pokazuje tylko wnioski dofinansowane (z kwotą) i listę rezerwową (bez kwoty), z nazwą wnioskodawcy, tytułem i punktami; odrzuconych nie ma. Publikacja następuje razem z zatwierdzeniem wyników, bez osobnego kroku | `Services/Ranking/RankingPublication.cs`, `app/competitions/[id]/results/page.tsx` (T-42a) | Raport mówi tylko, że listę da się opublikować bez przepisywania; regulamin 2026 nie mówi, co zawiera lista publikowana | P13 |
| ZR-11 | ~~PDF bez polskich znaków~~ **Polskie znaki zamknięte w T-45a** (osadzona czcionka Noto). Otwarte zostaje tylko: bez wersji RTF do edycji, o której mówi raport | `Services/Pdf/SimplePdfDocument.cs` | RTF nie ma dziś odbiorcy w procesie | P14 |
| ZR-12 | Sprawozdanie zakłada się dopiero przy wniosku `Funded`, bez terminu i bez załączników; wnioskodawca może dopisywać wiersze do tabel przepisanych z wniosku (np. wydatek, którego nie planował), a wierszy z wniosku nie usunie | `Services/Reports/ReportService.cs`, `ReportPrefill.cs` (T-50a) | Termin wynika z umowy (T-45), której nie ma; wzory 4a do 4c nie mówią, czy wolno dodać pozycję spoza budżetu | P19 |
| ZR-13 | Numer umowy to numer wniosku; każda nazwa znacznika spoza słownika systemu jest polem do wpisania przez operatora; wzór w mocy to najwyższa wersja; podpisanie wymaga kompletu pól i daty nie z przyszłości; wnioskodawca widzi umowę już przed podpisaniem | `Services/Documents/ContractService.cs`, `TemplatePlaceholders.cs` (T-45) | Wzór 2026 ma "umowa nr ……", bez reguły numeracji; B-03 pyta o wersję od prawnika | P15 |
| ZR-14 | Rozliczenie liczy tylko kolumnę "sfinansowane z dotacji" (`grantSpent`) budżetu sprawozdania; kwota do zwrotu = dotacja przyznana minus uznane wydatki z dotacji, nie mniej niż zero; operator nie uznaje kosztu kwotą z powodem, pozycja po pozycji, tylko w złożonym sprawozdaniu; ocena zapisana osobno od odpowiedzi; przyjęcie sprawozdania oznacza "rozliczony" | `Services/Reports/ReportSettlement.cs`, `ReportService.Settlement.cs`, `Models/Forms/FormFieldRole.cs` (T-50b) | Raport mówi o uznawaniu kosztów i kwocie do zwrotu, bez wzoru liczenia; propozycja w `model-danych.md` miała kolumny operatora w tabeli wnioskodawcy | P20 |
| ZR-15 | Regulamin serwisu i klauzula informacyjna przy rejestracji oraz klauzula dla osób trzecich we wniosku (`o_rodo_osoby_trzecie`) to **teksty robocze** napisane przez nas, każdy oznaczony jako wersja robocza; konta sprzed T-107 (także z `seed.py`) nie mają zapisanej akceptacji i nie są o nią proszone | `seed/consents/terms.md`, `seed/consents/privacy.md`, `seed/forms/application-2026.json`, `Services/Consents/ConsentCatalog.cs` (T-107) | Treść prawną daje OCWIP i jego IOD (PK-E, RY3); nie piszemy jej za klientkę | ZR-15 |
| ZR-16 | Wzór sprawozdania 2026 (4a, 4b, 4c w jednym dokumencie): tabela "B. Promocja projektu" i kolumna "Wkład własny" (liczona jako wartość wydatku minus część z dotacji) są przy każdym rodzaju wnioskodawcy, choć wzór 4c ich nie ma; bez pól "Sprawozdanie składa" i "Pełna nazwa wnioskodawcy" (wariant i nazwa wynikają z wniosku i karty podmiotu); bez części V (załączniki i podpisy); data końca realizacji przepisana z wniosku, do poprawienia | `seed/forms/report-2026.json`, `Services/Reports/ReportSettlement.cs` (T-95) | Formularz wniosku 2026 pyta o koszty promocji każdego wnioskodawcę, więc sprawozdanie bez tej tabeli nie rozliczyłoby wydanej dotacji i zawyżyło zwrot; załączniki sprawozdania to T-50c (B-04); podpis zastępuje złożenie z konta | P18 |
| ZR-17 | Wzór umowy 2026 to publiczny załącznik 5 regulaminu, rozpisany na znaczniki: dane wnioskodawcy, tytuł, daty i kwoty wypełnia system, rejestr, reprezentant, kontakt, terminy, rachunek, bank, umowa z NIW i źródło danych osobowych wpisuje operator przy umowie; członkowie grupy dopisani pod stronami umowy ("nie dotyczy" przy organizacji); myślniki typograficzne zamienione na dywiz; umowy hurtem obejmują tylko umowy z kompletem pól; od 2026-10-03 klauzula o rejestrze, numerze w rejestrze, NIP-ie i funkcji reprezentanta stoi we fragmencie `{{#Organisation,PatronInformalGroup}} ... {{/}}`, więc grupa nieformalna nie jest o nie pytana (B-GUI-17 i znalezisko 11 przejścia przedprodukcyjnego) | `seed/templates/contract-2026.txt`, `Services/Documents/ContractBundleService.cs`, `GroupMembersValue.cs`, `TemplatePlaceholders.cs` (T-45b) | P15 pyta o wersję od prawnika, P17 o podpisujących przy grupie z patronem; numer umowy z NIW jest wspólny dla konkursu, a wzór konkursu można go zawierać wprost | P15, P17 |
| ZR-18 | Deklaracja dostępności ma stan "częściowo zgodna" z jedną niedostępną treścią (PDF-y bez struktury znaczników); daty publikacji, aktualizacji i sporządzenia to 2026-09-29; osoba do kontaktu, telefon oraz opisy dostępności architektonicznej i komunikacyjnej to wartości robocze "do uzupełnienia przez OCWIP"; podmiot, adres i e-mail to publiczne dane OCWIP | `frontend/lib/accessibility-statement.ts` (T-121) | Dane kontaktowe, opis siedziby i data publikacji serwisu pochodzą od OCWIP (PK-E); stan zgodności wynika z samooceny T-46, nie z audytu zewnętrznego | ZR-18 |
| ZR-19 | Adres grupy nieformalnej w umowie wpisuje operator w pole `{{adres_lidera}}`, przepisując go z tabeli członków we wniosku. Karta podmiotu grupy nieformalnej ma tylko nazwę (reguła z `reguly-biznesowe.md`), a kolumn tabeli członków nic nie oznacza rolą, więc system nie ma skąd wziąć adresu strony umowy | `seed/templates/contract-2026.txt`, `Services/Documents/ContractService.cs` (T-45) | Wzór umowy musi podać siedzibę strony, a RD8 mówi, że przy grupie bez patrona stroną jest lider jako osoba fizyczna; adres lidera jest we wniosku, ale jako zwykła kolumna tabeli, nie jako pole z rolą | P21 |

## Poza planem, zapisane gdzie indziej

Tabela wyżej to założenia co do treści. To, co wyszło poza plan z Trello i z runbooka, ale ma swoje właściwe miejsce w innym pliku, jest tu wymienione, żeby jedna lista prowadziła do wszystkiego. Dopisuj tu nową pozycję razem z nową kartą albo decyzją, która nie wynikała z planu.

**Karty dopisane w trakcie pracy** (zakres w `M5-ocena.md` i `M6-wyniki.md`, stan w [`kolejka.md`](kolejka.md)):

| Karta | Skąd się wzięła | Gdzie opis |
|---|---|---|
| T-41a Karta formalna operatora i wgląd w pojedyncze oceny | wydzielona z T-41, żeby karta nie rosła bez końca | `M5-ocena.md` |
| T-41b Udostępnienie kart oceny wnioskodawcom | krok 5.5 raportu, wydzielony z T-41 | `M5-ocena.md` |
| T-42a Eksport i publikacja listy rankingowej | wydzielona z T-42 | `M6-wyniki.md` |
| T-43a Prawdziwa wysyłka maili (SMTP) | rozbieżność R-18, bez karty na Trello do 2026-09-26 | `M6-wyniki.md`, [`rozbieznosci.md`](rozbieznosci.md) |
| T-50a Sprawozdanie: formularz, wypełnianie, złożenie, przyjęcie albo zwrot | zbudowane na propozycji T-50.0 po akceptacji użytkownika (2026-09-26) | `M7-wdrozenie.md` |
| T-50b Rozliczenie: uznawanie kosztów, kwota do zwrotu, stan "rozliczony" | wydzielone z T-50, zbudowane 2026-09-27 (ZR-14) | `M7-wdrozenie.md` |
| T-50c Sprawozdanie: termin, sprawozdanie częściowe, załączniki, historia projektu | wydzielone z T-50b; wzór 2026 przeszedł do T-95 (DZ-5) | `M7-wdrozenie.md` |
| T-45 Generowanie umowy ze wzoru | zbudowane na propozycji T-45.0 po akceptacji użytkownika (2026-09-26) | `M6-wyniki.md` |
| T-45b Umowy hurtem i wzór umowy 2026 | wydzielone z T-45, odblokowane na założeniu 2026-09-27 (DZ-4) | `M6-wyniki.md` |
| T-90 do T-121, T-100a, T-100b, T-47a, T-47b: droga do pierwszej wersji | przejście całego procesu po kodzie i audyt produkcji, 2026-09-27; decyzje zespołu DZ-1 do DZ-6 | [`plan-v1.md`](plan-v1.md) |
| T-45a Polskie znaki w PDF: osadzona czcionka | warunek umowy, decyzja użytkownika z 2026-09-26 | `M6-wyniki.md` |
| T-45.0 / T-50.0 Umowa i sprawozdanie jako dane: propozycja | wzory NOWE FIO 2026 zamiast czekania na B-03 i B-04, na wzór T-38.0 | [`../model-danych.md`](../model-danych.md), sekcja "Umowa i sprawozdanie jako dane" |

**Decyzje techniczne spoza planu** (uzasadnienia w [`../architektura.md`](../architektura.md), sekcje z numerem karty w tytule):

| Decyzja | Karta |
|---|---|
| Przypisanie grupowe jako seria istniejących przypisań, bez nowej trasy | T-41 |
| Anonimowość kart w osobnym kontrakcie odpowiedzi API, nie w ekranie | T-41b |
| Decyzje i statusy pisane z pominięciem `UpdatedAt`, żeby nie zmieniała się suma kontrolna wniosku (D15) | T-42 |
| XLSX pisany ręcznie, bez biblioteki | T-42a |
| Kolejka maili w transakcji wyników, wysyłka na żądanie operatora zamiast w tle | T-43 |
| SMTP przez klienta z frameworka, bez MailKit; odmowa startu przy konfiguracji w połowie i na porcie 465 | T-43a |
| Typografia z edytora (cudzysłowy, półpauzy) zamieniana na zwykłe znaki we wszystkich PDF | T-44 |
| Wartości z wniosku w sprawozdaniu przywracane przez serwer przy każdym zapisie, nie tylko blokowane w ekranie | T-50a |
| Wspólny `ConfirmDialog` dla nowych okien potwierdzenia | T-50a |
| Czcionka Noto osadzana w całości (bez podzbioru) przez własny generator, bez biblioteki PDF | T-45a |
| Tekst umowy nie jest zapisywany, tylko składany przy każdym druku z wersji wzoru i wartości | T-45 |

**Treść pytań** z tych prac jest w [`pytania.md`](pytania.md), razem z paczką dla klientki; numery `P1` do `P8` mają dodatkowo komentarze na kartach B-02, B-03 i B-04 na Trello.
