# Pytania do zamawiającego

**Jedno miejsce na wszystko, o co pytamy OCWIP.** Nowe pytanie dopisujesz tutaj, a nie w pliku, przy którym się pojawiło: wcześniej te same pytania stały w czterech miejscach (`plan-v1.md`, `zalozenia-robocze.md`, `blokery.md`, `decyzje.md`) i nikt nie wiedział, która lista jest pełna.

Dwie serie identyfikatorów, bo mówią do dwóch różnych odbiorców, i obie zostają bez przenumerowania: numery krążą już po kartach Trello i po pakiecie wysłanym do klientki.

- **`PK-A` do `PK-P`: paczka dla klientki.** Jedno pytanie na jedną decyzję, każde z odpowiedzią domyślną, którą przyjmujemy przy ciszy. To ta lista idzie w mailu albo na spotkanie.
- **`P1` do `P27`: pytania szczegółowe**, powstałe przy konkretnych kartach. Kilka z nich mieści się w jednym pytaniu z paczki, co mówi kolumna "W paczce".

Pytanie bez numeru `P`, które wyszło z założenia roboczego, jest tu pod identyfikatorem tego założenia (`ZR-xx`).

Czym to nie jest:

- [`decyzje.md`](decyzje.md) to decyzje **już podjęte** (`D`, `RD`, `DZ`), także te, które przedstawiamy do zatwierdzenia;
- [`blokery.md`](blokery.md) to **dokumenty i warunki**, bez których czegoś nie da się zrobić;
- [`zalozenia-robocze.md`](zalozenia-robocze.md) to **co zbudowaliśmy bez odpowiedzi**, z odnośnikiem do pytania tutaj;
- [`rozbieznosci.md`](rozbieznosci.md) to miejsca, gdzie **dwa źródła mówią co innego**.

---

## Paczka dla klientki (seria PK)

Wysyłamy jeden dokument, pogrupowany według tego, co odpowiedź odblokowuje. **Każde pytanie ma odpowiedź domyślną**, którą przyjmujemy po terminie (G0, 2026-10-16). Kolumna "kiedy za późno" mówi, od kiedy zmiana zdania kosztuje migrację na danych.

| ID | Pytanie | Domyślnie | Odblokowuje | Kiedy za późno |
|---|---|---|---|---|
| PK-A | ~~Czy organizację reprezentuje jedna osoba (jedno konto), czy kilka, z prośbą o dostęp (RD7, R-01, B-09)?~~ **Rozstrzygnięte 2026-09-21** akceptacją raportu: kilka osób, prośba o dostęp (RD7). Zbudowane w T-93a | kilka osób przy organizacji | T-93a (zrobione), T-47b (retencja karty, R-15) | G1 |
| PK-B | Czy dodawanie operatorów i usuwanie danych po terminie ma robić osobna rola administratora (R-02)? | nie, operator i komenda wdrożeniowa | T-104 | po v1, bez kosztu |
| PK-C | Hosting: na czyim koncie, kto płaci w roku czwartym, czy dane muszą leżeć w Polsce? Czy OCWIP ma grant Azure (Microsoft for Nonprofits, 2000 USD rocznie przez TechSoup)? | VPS w UE (około 10 do 15 EUR miesięcznie z kopiami), na koncie OCWIP | T-48, T-114 | G4 |
| PK-D | Kiedy rusza najbliższy nabór i jaki to konkurs? Czy OCWIP będzie operatorem NOWEFIO w 2027? | marzec 2027, Kierunek NOWE FIO 2027 na wzorach 2026 | cały harmonogram | G3 |
| PK-E | Kontakt do IOD. Klauzule, także dla osób trzecich (R-16). Umowa powierzenia z nami i z hostingiem. Okres retencji z umowy z NIW. Zgoda na szyfrowanie po stronie aplikacji | klauzule robocze do podmiany, retencja 5 lat od końca roku realizacji | T-107, T-121, T-47b, G4 | G4 (bez klauzul nie ma startu) |
| PK-F | Domena systemu i adres nadawcy maili (np. `wnioski.ocwip.pl`, `powiadomienia@...`). Dostęp do DNS dla rekordów SPF, DKIM i DMARC | subdomena `ocwip.pl` | T-48, RY10 | G4 |
| PK-G | Czy wzór umowy 2026 to wersja od prawnika? Czy OCWIP ma własną numerację umów? Czy wnioskodawca ma widzieć projekt umowy przed spotkaniem (P15 do P17, P21, ZR-13, ZR-17, ZR-19)? | wzór 2026; numer umowy to numer wniosku (tak działa T-45) | T-45b | po v1, bez kosztu (nowa wersja wzoru) |
| PK-H | Zwrot do poprawy na obu etapach, czy tylko w naborze (RD10)? | na obu | T-103 | G1 |
| PK-I | Organizacja młoda i lokalna: rozróżnienie trwałe czy tylko w 2026 (R-36)? | pole we wniosku, nie nowy typ podmiotu | T-94 | G1 |
| PK-J | Publikacja konkursu klikana czy z zaplanowaną datą (R-27)? | klikana | T-97 | po v1, bez kosztu |
| PK-K | Czy lista publiczna ma tylko dofinansowane i rezerwę, czy wszystkie wnioski z punktami (ZR-10, P13)? Czy publikacja idzie razem z zatwierdzeniem? | tylko dofinansowane i rezerwa, razem z zatwierdzeniem | T-108 | po v1, bez kosztu |
| PK-L | Pytania z ocen: P2, P8 do P12 i P14 (ZR-02, ZR-05 do ZR-09, ZR-11) oraz ZR-01 (bez numeru P) | jak w [`zalozenia-robocze.md`](zalozenia-robocze.md) | drobne poprawki | po v1, bez kosztu |
| PK-M | Sprawozdanie: wydatek spoza budżetu, termin (P18, P19, ZR-12, ZR-16) | jak w ZR-12 | T-50c | przed pierwszym sprawozdaniem |
| PK-N | Kreator formularzy: cztery pytania z B-10 | kopia z poprawkami wystarcza w v1 | T-26a | bez kosztu |
| PK-O | Czy dane z Witkaca trzeba przenieść (B-07)? | nie; po odpowiedzi zapisać w [`../zakres.md`](../zakres.md) jako decyzję | nic w v1 | bez kosztu |
| PK-P | Kto z OCWIP przejdzie próbę generalną i kiedy (T-120)? | operator, pierwsza połowa grudnia 2026 | G3 | G3 |

Pytania oznaczone G1 (PK-H, PK-I; PK-A rozstrzygnięte) są **jedynymi, przy których cisza jest droga**. Przy wysyłce warto je wyróżnić.

Bramki G0 do G4 i to, co każda z nich zamyka, są w [`plan-v1.md`](plan-v1.md), sekcja 3.

---

## Pytania szczegółowe (seria P)

Jedno pytanie na wiersz, w kolejności numerów. "Dotyczy" mówi, które założenie, bloker albo rozbieżność wisi na odpowiedzi; "W paczce" mówi, z którym pytaniem `PK` idzie do klientki. Treść `P1` do `P8` powstała przy kartach i żyje w komentarzach blokerów na Trello: tutaj jest to, co o nich wiemy z repozytorium.

| ID | Pytanie | Dotyczy | W paczce | Stan |
|---|---|---|---|---|
| P1 do P7 | Pytania z wczesnych kart M1 do M3, treść w komentarzach karty B-02 na Trello | B-02 | PK-L | otwarte |
| P2 | Czy skalą ostrzeżenia o rozbieżności jest jedna karta (50 punktów), czy suma obu (100); czy ostrzeżenie w ogóle obowiązuje | ZR-02 | PK-L | otwarte |
| P8 | Treść deklaracji bezstronności od OCWIP (załącznik 1 regulaminu komisji, którego nie ma w opublikowanych dokumentach) | ZR-05, B-02 | PK-L | otwarte |
| P9 | Czy ekspert komisji bywa też oceniającym formalnie; czym jest "dostęp podglądowy" z raportu (osoba widząca wnioski bez oceniania?) | ZR-06, B-02 | PK-L | otwarte |
| P10 | Czy OCWIP chce automatycznego rozdziału wniosków między ekspertów (na przykład po równo), zamiast przypisania ręcznego i grupowego | ZR-07, B-02 | PK-L | otwarte |
| P11 | Czy blokować udostępnienie kart przed zakończeniem wszystkich; czy kwota proponowana przez eksperta ma być widoczna dla wnioskodawcy (może rozmijać się z przyznaną) | ZR-08, B-02 | PK-L | otwarte |
| P12 | Czy "lista rezerwowa" to osobny wynik ogłaszany wnioskodawcom, czy tylko porządek na liście; czy wniosek z negatywną oceną formalną może po odwołaniu wrócić do oceny przed zatwierdzeniem | ZR-09, B-02 | PK-L | otwarte |
| P13 | Czy publiczna lista wyników ma zawierać wszystkie wnioski z punktami (także odrzucone); czy publikacja ma być osobną decyzją po zatwierdzeniu | ZR-10, B-02 | PK-K | otwarte |
| P14 | Czy RTF albo DOCX jest komuś potrzebny do edycji wydruków, czy PDF wystarcza | ZR-11, B-02 | PK-L | otwarte |
| P15 | Czy wzór umowy 2026 to wersja od prawnika; czy OCWIP ma własną numerację umów; czy wnioskodawca ma widzieć projekt umowy przed spotkaniem | ZR-13, ZR-17, B-03 | PK-G | otwarte |
| P16 | Pytanie o wzór umowy z karty B-03, treść w komentarzu karty | B-03 | PK-G | otwarte |
| P17 | Kto podpisuje umowę przy grupie nieformalnej z patronem i bez patrona; czy członkowie grupy mają osobne miejsca na podpis | ZR-17, B-03 | PK-G | otwarte |
| P18 | Czy grupa nieformalna bez patrona rozlicza promocję w części A sprawozdania; czy sprawozdanie elektroniczne wymaga podpisu; czy termin realizacji brać z umowy | ZR-16, B-04 | PK-M | otwarte |
| P19 | Czy wnioskodawca może wykazać w sprawozdaniu wydatek spoza budżetu wniosku; od kiedy liczy się termin sprawozdania | ZR-12, B-04 | PK-M | otwarte |
| P20 | Czy kwota do zwrotu liczy się też od niewykorzystanej części dotacji i od udziału własnego (proporcja z umowy), czy dochodzą odsetki; czy "rozliczony" wymaga potwierdzenia zwrotu środków | ZR-14, B-04 | PK-M | otwarte |
| P21 | Czy adres grupy nieformalnej ma trafiać do umowy automatycznie z wniosku, a jeśli tak, to z osobnego pola "adres lidera", czy z kolumny tabeli członków | ZR-19, B-03 | PK-G | otwarte |
| P22 | Czy wniosek ma zbierać wkład własny i inne źródła finansowania, a budżet czwartą tabelę ze źródłami? Bez nich dotacja równa się całkowitej wartości projektu i udział dotacji zawsze wynosi 100%, a decyzja D11 zakłada, że wkłady istnieją. Przy okazji: czy umowa potrzebuje REGON-u | B-01, D11, [`pola.md`](pola.md) część III | PK-L | otwarte |
| P23 | Czy po zatwierdzeniu wyników operator ma móc poprawić kartę oceny, na przykład przy omyłce w punktacji? Domyślnie nie: ocena jest po zatwierdzeniu zablokowana bez wyjątku. Jeśli tak, potrzebny jest jawny sposób (np. cofnięcie zatwierdzenia przez operatora), a nie wyjątek w samej blokadzie | karta Trello "Zablokować zmiany kart oceny po zatwierdzeniu wyników" | PK-L | otwarte |
| P24 | Czy uzasadnienia w karcie oceny formalnej mają nieść dane osób (imiona, nazwiska i funkcje członków grupy nieformalnej oraz organów patrona)? Kryterium o funkcjach u patrona wprost o to prosi, a pole uzasadnienia ma 2000 znaków. Jeśli tak, oznaczamy te pola we wzorze jako wrażliwe, tak jak pola osobowe wniosku i sprawozdania; sam mechanizm szyfrowania karty już działa (S-34) | `formal-2026.json`, przegląd bezpieczeństwa S-34 | PK-L | otwarte |
| P25 | Kto dołącza statut? Grupa nieformalna bez patrona nie ma ani statutu, ani patrona, a krok 1.5 kreatora zna tylko "wymagany", "nieobowiązkowy" i "wymagany od podmiotów spoza KRS", więc przy wymaganym "Statucie" grupa nie złoży wniosku. Czy wymóg ma zależeć od rodzaju wnioskodawcy (organizacja, grupa z patronem, grupa bez patrona)? Domyślnie: dodajemy wariant "wymagany od organizacji i grup z patronem" | przejście GUI, przebieg 4, `P4-16` | PK-L | otwarte |
| P26 | Czy sprawozdanie można złożyć i rozliczyć przed podpisaniem umowy? Dziś organizacja z przyznaną dotacją widzi "Przejdź do sprawozdania" od razu, a przyjęcie sprawozdania rozlicza także wniosek w stanie "dofinansowany". Domyślnie: sprawozdanie dopiero po podpisaniu umowy | przejście GUI, przebieg 4, ścieżka L; `ReportService.Settlement.cs` | PK-M | otwarte |
| P27 | Czy kryterium strategiczne "Projekt złożony jest przez grupę nieformalną z Patronem" ma się pokazywać w karcie wniosku organizacji i grupy bez patrona? Dziś ekspert widzi je przy każdym wniosku i odpowiada na pytanie, które z definicji wniosku nie dotyczy. Domyślnie: tylko przy grupie z patronem (`appliesTo` we wzorze karty) | przejście GUI, przebieg 4, `O-13`; `merit-2026.json` | PK-L | otwarte |
| ZR-01 | Czy kwota rekomendowana na liście rankingowej to średnia kwot od ekspertów, niższa z dwóch, czy obie pokazywane osobno | ZR-01 | PK-L | otwarte |
| ZR-03 | Czy próg punktowy i próg rozbieżności mają mieć wartości domyślne dla konkursu utworzonego przed ustawieniami oceny | ZR-03 | PK-L | otwarte, bez kosztu |
| ZR-15 | Treść regulaminu serwisu i obu klauzul informacyjnych od IOD; czy konta założone przed uruchomieniem mają zaakceptować dokumenty przy pierwszym logowaniu | ZR-15 | PK-E | otwarte |
| ZR-18 | Kto jest koordynatorem dostępności i jaki ma telefon; opis dostępności siedziby przy Damrota 4; data publikacji serwisu; czy OCWIP chce audytu zewnętrznego | ZR-18 | PK-E | otwarte |

### P21 do P24 szerzej

Te cztery powstały po wysłaniu paczki, więc idą dopiskiem przy najbliższej rozmowie.

**P21, adres grupy nieformalnej w umowie.** Dziś operator wpisuje adres w pole `{{adres_lidera}}` wzoru umowy, przepisując go z tabeli członków we wniosku (ZR-19). Karta podmiotu grupy nieformalnej ma tylko nazwę, a kolumn tabeli nic nie oznacza rolą, więc system nie ma skąd wziąć siedziby strony umowy. Odpowiedź "z osobnego pola" znaczy nowe pole z rolą we wzorze wniosku i nowy znacznik systemowy, czyli jedna rola i jeden wiersz w słowniku znaczników. Odpowiedź "z kolumny tabeli" wymaga najpierw zmiany kontraktu formularza, który dziś roli na kolumnie nie przyjmuje. Odpowiedź "niech wpisuje operator" zostawia wszystko jak jest. Skąd się wzięło: przejście przedprodukcyjne 2026-10-02, znalezisko 11 w [`../przejscie-gui-bledy.md`](../przejscie-gui-bledy.md).

**P22, wkład własny i czwarta tabela budżetu.** Opis pola po polu jest w [`pola.md`](pola.md), część III. To pytanie i D11 (dotacja jako pole wyliczane z wkładów) trzeba rozstrzygnąć razem: dziś wzory 2026 nie zbierają wkładu własnego, więc pole "udział dotacji" wychodzi 100% z definicji, a we wzorze dla grupy nieformalnej jest wpisane na sztywno.

**P24, dane osób w uzasadnieniach karty formalnej.** Kolumna `evaluations.answers` szyfruje od teraz każde pole, które karta oznaczy jako wrażliwe, tak samo jak wniosek i sprawozdanie. Żadne pole wzoru `formal-2026.json` takiego oznaczenia dziś nie ma, a kryterium o tym, czy członek grupy pełni funkcję w organach patrona, naturalnie zbiera imiona, nazwiska i funkcje konkretnych osób. Odpowiedź "tak" to dopisanie `"sensitive": true` przy uzasadnieniach i przebieg `reencrypt-data`, bez zmian w kodzie. Odpowiedź "nie" zostawia wzór jak jest i warto wtedy dopisać w pomocy pola, żeby nie wpisywać tam danych osób. Skąd się wzięło: przegląd bezpieczeństwa 2026-10-05, znalezisko S-34.

**P23, zmiana karty oceny po zatwierdzeniu wyników.** Dziś serwis oceny nie sprawdza zatwierdzenia wyników (`ResultsApprovedAt`), a publiczny ranking liczy punkty i miejsca z kart przy każdym zapytaniu, więc przypisany ekspert mógłby po publikacji zmienić to, co widać publicznie. Blokada jest drobną poprawką (odmowa rozpoczęcia, zapisu i zakończenia karty), a odpowiedź decyduje tylko o tym, czy zostawić ją bez wyjątku. Odpowiedź "nie" zostawia blokadę jak jest. Odpowiedź "tak" wymaga osobnego, jawnego kroku operatora, który cofa zatwierdzenie i zostawia ślad, bo cofnięcie wyniku po publikacji dotyka list rezerwowych, rezygnacji i umów. Skąd się wzięło: przegląd bezpieczeństwa całego repozytorium 2026-10-04, obserwacja poniżej progu zgłoszenia.

---

## Co zrobić z odpowiedzią

1. Wpisz ją w kolumnie "Stan" razem z datą, a treść odpowiedzi dopisz pod tabelą, jeśli jest dłuższa niż zdanie.
2. Jeśli odpowiedź zamyka założenie, skreśl wiersz w [`zalozenia-robocze.md`](zalozenia-robocze.md) i napisz, co z tego wynikło (wzór: ZR-04).
3. Jeśli odpowiedź coś odblokowuje, popraw [`blokery.md`](blokery.md) i, gdy pojawia się zadanie, [`kolejka.md`](kolejka.md).
4. Jeśli odpowiedź odwraca decyzję, nowa decyzja idzie do [`decyzje.md`](decyzje.md); stara zostaje z adnotacją, bo numery są cytowane w kodzie i w dokumentacji.
