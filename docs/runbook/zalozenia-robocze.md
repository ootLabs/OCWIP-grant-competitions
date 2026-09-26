# Założenia robocze

Miejsce na wszystko, co zbudowaliśmy **bez potwierdzenia w dokumentach**, żeby praca nie stała: interpretacje, wartości przyjęte na próbę i prototypy idące dalej niż to, co mówią raport, regulamin albo karty. Decyzja użytkownika z 2026-09-25: zamiast zatrzymywać pętlę przy każdej takiej luce, budujemy według najrozsądniejszej interpretacji i zapisujemy ją tutaj.

Różnica wobec pozostałych plików w `runbook/`:

- [`rozbieznosci.md`](rozbieznosci.md) to miejsca, gdzie **dwa źródła mówią co innego**;
- [`blokery.md`](blokery.md) to rzeczy, których **nie da się zrobić** bez dokumentu;
- ten plik to rzeczy, które **zrobiliśmy mimo braku odpowiedzi**, i które trzeba potwierdzić albo cofnąć.

Każda pozycja mówi, gdzie siedzi w kodzie, bo cofnięcie założenia to konkretna zmiana, a nie dyskusja. Pytanie do klientki, jeśli jest, trafia też na kartę blokera na Trello.

| ID | Założenie | Gdzie w kodzie | Skąd wątpliwość | Co potwierdzić |
|---|---|---|---|---|
| ZR-01 | Kwota rekomendowana na liście rankingowej to **średnia** kwot proponowanych przez ekspertów, zaokrąglona do grosza | `Services/Ranking/RankingCalculator.cs`, `RankingRow.RecommendedGrant` (T-39) | Regulamin 2026 mówi o "proponowanej kwocie" na każdej karcie, nie mówi, jak łączyć dwie. Raport mówi, że decyduje operator (T-42) | Czy operator widzi średnią, niższą z dwóch, czy obie osobno. Do czasu odpowiedzi T-41 może pokazać obie kwoty obok średniej |
| ZR-02 | Skala ostrzeżenia o rozbieżności to suma `maxValue` pól składających się na sumę merytoryczną karty (50 w 2026); karta bez tych widełek nie ostrzega wcale | `RankingCalculator.MeritScale` (T-39) | Raport: "próg rozbieżności domyślnie 30% skali", bez definicji skali; regulamin 2026 progu nie zna (P2 na B-02) | Czy skalą jest jedna karta (50), czy suma obu (100); czy ostrzeżenie w ogóle obowiązuje |
| ZR-03 | Próg punktowy i ostrzeżenie o rozbieżności nie mają wartości domyślnej dla istniejących konkursów (`null`); liczba ekspertów (2) i suma mają | migracja `AddEvaluationSettings` (T-39) | Regulamin 2026 podaje próg 50, ale konkursy utworzone wcześniej mogą mieć inny regulamin | Nic, dopóki nie ma drugiego konkursu; operator ustawia próg trasą ustawień oceny |
| ZR-04 | ~~Panel recenzenta bez bramy deklaracji bezstronności~~ **Zamknięte w T-40a**: brama jest w warstwie autoryzacji | - | - | - |
| ZR-05 | Tekst deklaracji bezstronności jest **roboczy**: mówi to, czego wymaga § 2 regulaminu komisji 2026, bo załącznika 1 (samej deklaracji) nie ma w opublikowanych dokumentach. Każda decyzja zapisuje tekst, który ekspert widział | `Models/ImpartialityDeclaration.cs` (T-40a) | Brak załącznika 1 | Treść deklaracji od OCWIP (P8 na B-02); podmiana to zmiana stałej, a zapisane decyzje zachowują swój tekst |
| ZR-06 | Tabela ekspertów w ocenie konkursu ma kolumny: imię i nazwisko, adres, liczba przypisanych wniosków, deklaracja i powód odmowy. **Bez** liczników oceny formalnej i "dostępu podglądowego" z raportu (krok 5.1) | `app/panel/operator/evaluation/[competitionId]/experts-table.tsx` (T-41) | Ocenę formalną robi u nas operator, nie ekspert (regulamin 2026), więc licznik formalny eksperta byłby zawsze zerem; raport nie mówi, czym jest dostęp podglądowy | Czy ekspert komisji bywa też oceniającym formalnie; czym jest dostęp podglądowy (osoba widząca wnioski bez oceniania?). P9 na B-02 |
| ZR-07 | Przypisanie grupowe przypisuje jednego eksperta do zaznaczonych wniosków po kolei, wniosek, który ekspert już ma, pomija; pierwsza odmowa zatrzymuje serię, a to, co przeszło, zostaje | `ranking-table.tsx`, `page.tsx` w `evaluation/[competitionId]` (T-41) | Raport wymaga działania na grupie, nie mówi o losowaniu ani równym podziale | Czy OCWIP chce automatycznego rozdziału (na przykład po równo między ekspertów); to byłaby osobna karta. P10 na B-02 |
| ZR-08 | Udostępnienie kart nie czeka na koniec oceny: operator może je zrobić w każdej chwili, a wnioskodawca widzi tylko karty **zakończone**; karta zakończona po udostępnieniu pojawi się u niego od razu. Wnioskodawca widzi też proponowaną kwotę z karty merytorycznej | `Services/Ranking/CardSharingService.cs`, `CardSharingEndpoints.cs` (T-41b) | Raport mówi o jednej nieodwracalnej decyzji, nie mówi, czy wymaga kompletu kart ani które pola karty są jawne | Czy blokować udostępnienie przed zakończeniem wszystkich kart; czy kwota proponowana przez eksperta ma być widoczna dla wnioskodawcy (może rozmijać się z przyznaną w T-42). P11 na B-02 |
| ZR-09 | Wynik zatwierdzenia: kwota przyznana daje `Funded`, miejsce na liście powyżej progu bez kwoty daje `Reserve` (lista rezerwowa), reszta `Rejected`. Zatwierdzenie odmawia, dopóki któryś wniosek nie ma zakończonej oceny formalnej albo kompletu kart merytorycznych | `Services/Ranking/GrantDecisionService.cs`, `ApplicationStatus` (T-42) | Regulamin 2026 zna rezygnację i przejście środków na kolejny wniosek "spełniający próg", ale nie nazywa listy rezerwowej; raport nie mówi, co z wnioskiem ocenionym pozytywnie bez środków | Czy "lista rezerwowa" to osobny wynik ogłaszany wnioskodawcom, czy tylko porządek na liście; czy wniosek z negatywną oceną formalną po odwołaniu (3 dni) może wrócić do oceny przed zatwierdzeniem. P12 na B-02 |
| ZR-10 | Publiczna lista wyników pokazuje tylko wnioski dofinansowane (z kwotą) i listę rezerwową (bez kwoty), z nazwą wnioskodawcy, tytułem i punktami; odrzuconych nie ma. Publikacja następuje razem z zatwierdzeniem wyników, bez osobnego kroku | `Services/Ranking/RankingPublication.cs`, `app/competitions/[id]/results/page.tsx` (T-42a) | Raport mówi tylko, że listę da się opublikować bez przepisywania; regulamin 2026 nie mówi, co zawiera lista publikowana | Czy lista publiczna ma zawierać wszystkie wnioski z punktami (także odrzucone); czy publikacja ma być osobną decyzją po zatwierdzeniu. P13 na B-02 |
| ZR-11 | PDF wniosku jest bez polskich znaków (transliteracja, jak potwierdzenie i lista wniosków: `SimplePdfDocument` używa czcionek Base14 bez osadzania) i bez wersji RTF do edycji, o której mówi raport | `Services/Pdf/ApplicationPdfBuilder.cs`, `SimplePdfDocument.cs` (T-44) | Osadzenie czcionki z polskimi znakami to własny generator czcionek albo biblioteka PDF; RTF nie ma dziś odbiorcy w procesie | Czy wydruk bez polskich znaków wystarczy do teczki konkursu (jeśli nie, osobna karta na osadzenie czcionki); czy RTF albo DOCX jest komuś potrzebny do edycji. P14 na B-02 |

## Poza planem, zapisane gdzie indziej

Tabela wyżej to założenia co do treści. To, co wyszło poza plan z Trello i z runbooka, ale ma swoje właściwe miejsce w innym pliku, jest tu wymienione, żeby jedna lista prowadziła do wszystkiego. Dopisuj tu nową pozycję razem z nową kartą albo decyzją, która nie wynikała z planu.

**Karty dopisane w trakcie pracy** (zakres w `M5-ocena.md` i `M6-wyniki.md`, stan w [`kolejka.md`](kolejka.md)):

| Karta | Skąd się wzięła | Gdzie opis |
|---|---|---|
| T-41a Karta formalna operatora i wgląd w pojedyncze oceny | wydzielona z T-41, żeby karta nie rosła bez końca | `M5-ocena.md` |
| T-41b Udostępnienie kart oceny wnioskodawcom | krok 5.5 raportu, wydzielony z T-41 | `M5-ocena.md` |
| T-42a Eksport i publikacja listy rankingowej | wydzielona z T-42 | `M6-wyniki.md` |
| T-43a Prawdziwa wysyłka maili (SMTP) | rozbieżność R-18, bez karty na Trello do 2026-09-26 | `M6-wyniki.md`, [`rozbieznosci.md`](rozbieznosci.md) |
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

**Pytania do klientki** z tych prac (P9 do P19) są w komentarzach na kartach B-02, B-03 i B-04 na Trello.
