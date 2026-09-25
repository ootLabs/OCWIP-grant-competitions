# Proces krok po kroku

Pełny cykl życia konkursu, tak jak opisuje go `RAPORT-proces-i-pola.docx`. Pola każdego kroku są w [`pola.md`](pola.md), tutaj jest **zachowanie**: co się z czego bierze, co odsłania, co zmienia dalej.

Schemat każdego kroku: wchodzisz z czego, uzupełniasz co, decyzja jaka, **zmienia dalej co**, idziesz do czego. Linia "zmienia dalej" jest najważniejsza, bo pokazuje, że konkurs nie jest zbiorem niezależnych ekranów: to, co operator wpisze w kroku 1.4, decyduje o tym, co zablokuje się wnioskodawcy w kroku 3.5.

---

## Role

Ustalone raz, w całym dokumencie nie używamy innych nazw.

| Rola | Kto to jest | Co może | Kto ją nadaje |
|---|---|---|---|
| Gość | ktokolwiek, bez konta | czyta ogłoszenia opublikowanych konkursów, pobiera wzory załączników, przegląda archiwum wyników | nikt, jest domyślna |
| Wnioskodawca | osoba z kontem, działająca w imieniu organizacji albo grupy nieformalnej | wszystko co gość, plus karta swojej organizacji, wnioski tej organizacji, sprawozdania | rejestruje się sam |
| Ekspert | osoba powołana do komisji w danym konkursie | czyta wyłącznie wnioski przypisane mu w tym jednym konkursie, wypełnia karty oceny | tylko operator, imiennie, na konkurs |
| Operator | pracownik OCWIP obsługujący konkursy | zakłada i publikuje konkursy, układa formularze, prowadzi nabór, powołuje ekspertów, wykonuje ocenę formalną, rozstrzyga, generuje umowy | administrator |
| Administrator | jedna, najwyżej dwie osoby w OCWIP | wszystko co operator, plus dodaje i odbiera dostęp operatorom, zatwierdza awaryjnie dostęp do karty organizacji, uruchamia usunięcie danych po terminie retencji | ustalane przy wdrożeniu, poza aplikacją |

**Uwaga.** Repozytorium i Trello znają trzy role: operator, wnioskodawca, recenzent. Raport opisuje pięć, z czego gość nie jest rolą w bazie, a administrator jest nowy. Rozjazd i jego konsekwencje: `R-02` w [`rozbieznosci.md`](rozbieznosci.md). Nazwa "ekspert" jest w raporcie nazwą domyślną, a sposób nazywania oceniającego w dokumentach jest **ustawieniem konkursu** (krok 5.0).

Jedna osoba może mieć kilka ról: prezes fundacji składa wniosek w jednym konkursie, a w innym ocenia cudze jako osoba prywatna. Operator dodaje ją do komisji po adresie e-mail.

**Granica publiczne kontra zalogowane, jedno zdanie:** czytanie jest publiczne, pisanie wymaga konta.

## Kto widzi rzeczy niedokończone

Jedna reguła na wszystkie przypadki: każdy widzi tylko swoje rzeczy w toku albo te, do których został przypisany.

| Co jest niedokończone | Kto to widzi |
|---|---|
| Konkurs roboczy, nieopublikowany | operator i administrator; taki konkurs nie ma publicznego adresu |
| Wniosek roboczy, rozpoczęty i niezłożony | wyłącznie osoby z dostępem do karty tej organizacji; przy grupie nieformalnej bez patrona wyłącznie osoba, która go rozpoczęła |
| Karta oceny w toku, niezatwierdzona | ten ekspert, który ją wypełnia, i operator |
| Lista rankingowa przed rozstrzygnięciem | operator i administrator, do momentu ogłoszenia wyników |

---

# Ścieżka 1: ogłoszenie konkursu

Pracownik OCWIP. Siedem kroków w tej samej kolejności i pod tymi samymi nazwami, co dzisiaj, plus krok 0 przed kreatorem.

| Krok | Co się w nim ustawia |
|---|---|
| 0 | pusty formularz albo kopia konkursu z poprzedniego roku |
| 1.1 Dane konkursu | numer, tytuł, terminy publikacji i naboru |
| 1.2 Opis konkursu | treść ogłoszenia, cel, zakładane rezultaty |
| 1.3 Forma dostarczenia | czy poza wersją elektroniczną wymagany jest papier |
| 1.4 Limity | pieniądze, procenty, ramy czasowe projektu |
| 1.5 Załączniki do oferty | jakie pliki dołącza wnioskodawca i kiedy są wymagane |
| 1.6 Osoby kontaktowe | kto odpowiada na pytania w tym konkursie |
| 1.7 Podsumowanie | wszystko na jednym ekranie przed publikacją |

**Walidacja nie blokuje przechodzenia między krokami.** Konkurs układa się w kilku podejściach, więc niekompletny krok tylko dostaje oznaczenie. Kompletność sprawdzamy przy publikacji.

**Krok 0.** Kopia konkursu z poprzedniego roku przenosi ustawienia, formularz wniosku, karty oceny i wzory dokumentów. Na koncie zamawiającego są dziś dwie procedury: "Kierunek Nowe FIO 2021" i "Procedura grantowa Opolska MOC Społeczności". Na tym opieramy zakres: system ma dobrze obsłużyć dwa powtarzalne typy konkursu.

**Co zmienia dalej krok 1.1.** Data publikacji tworzy publiczny adres konkursu. Koniec naboru zamyka nabór sam, co do minuty. Oba terminy wchodzą na ekran "co przygotować" u wnioskodawcy.

**Co zmienia dalej krok 1.3.** Przy "tak" w kroku 1.5 dochodzi wariant "wymagany papierowo", a na liście wniosków kolumna z datą wpływu.

**Co zmienia dalej krok 1.4.** Maksymalna dotacja i procenty stają się blokadami w budżecie wniosku (krok 3.5). Te same liczby, przeliczone na złotówki, wnioskodawca widzi jako wskazówkę na wejściu do budżetu. Data usunięcia danych wchodzi do klauzuli RODO w formularzu.

**Co zmienia dalej krok 1.5.** Lista wymaganych plików blokuje złożenie wniosku w kroku 3.7 i sama wpisuje się na ekran "co przygotować".

**Istotna różnica wobec dzisiaj.** Limity wpisane w parametrach konkursu nie pilnują się dziś same w formularzu: blokady wpisuje ręcznie programista po stronie dostawcy narzędzia. U nas parametr konkursu jest jedynym źródłem prawdy, a formularz go czyta i liczy na bieżąco.

## Stany konkursu

```
roboczy -> opublikowany -> trwa nabór -> nabór zamknięty -> trwa ocena -> rozstrzygnięty -> archiwalny
```

Kto przestawia: nikt. Przejścia dzieją się same, z terminów z kroku 1.1, poza rozstrzygnięciem i archiwizacją.

Roboczy nie ma publicznego adresu. Od stanu opublikowany konkurs jest widoczny dla gościa, ale przycisk "Wypełnij wniosek" działa dopiero od stanu trwa nabór.

Edycja konkursu, do którego wpłynęły już wnioski, pokazuje ostrzeżenie. Formularz jest wersjonowany, więc wnioski w toku się nie psują, ale zmiana limitów kwotowych wpływa na już wpisane budżety.

---

# Ścieżka 2: konto i karta organizacji

Wnioskodawca. Zasada: na starcie nie pytamy o nic, co dotyczy organizacji.

**Krok 2.1, rejestracja.** Wchodzisz z przycisku "Wypełnij wniosek" na publicznej stronie konkursu. Po rejestracji idzie e-mail z odnośnikiem aktywacyjnym; bez kliknięcia konto nie działa. Wracasz **na stronę tego samego konkursu**, z której przyszedłeś.

Odzyskanie dostępu: zapomniane hasło resetuje się odnośnikiem na adres konta, a zmiana adresu e-mail wymaga potwierdzenia z nowego adresu, bo adres służy do logowania. **Obie rzeczy wchodzą do MVP i żadna nie wymaga kontaktu z OCWIP.**

**Krok 2.2, karta organizacji.** Wchodzisz z pierwszego wniosku: część I pierwszego wniosku jest formularzem karty.

Decyzja systemu: czy podany NIP już istnieje.

- **Nie istnieje:** powstaje nowa karta, a Ty jesteś jej pierwszą osobą.
- **Istnieje:** nie powstaje druga karta. Widzisz "ta organizacja jest już zarejestrowana" i wysyłasz prośbę o dostęp. Zatwierdza ją osoba, która kartę założyła, awaryjnie administrator OCWIP.

**Gdy założyciel karty nie odpowiada.** To nie jest przypadek brzegowy: w organizacjach zmieniają się zarządy, a konta zostają po ludziach, którzy odeszli. Prośba bez odpowiedzi przez siedem dni trafia na listę u administratora OCWIP, razem z danymi osoby proszącej i wskazaniem, kto założył kartę. Administrator zatwierdza ręcznie, po weryfikacji poza systemem. Każde takie zatwierdzenie zostaje w historii, z nazwiskiem administratora i datą. Siedmiodniowy próg jest propozycją.

**Co zmienia dalej.** Przy każdym następnym wniosku część I jest już wypełniona. Zmiana adresu albo rachunku dzieje się tu, raz, i obowiązuje od razu we wszystkich kolejnych wnioskach.

**Jedno konto, kilka organizacji.** Osoba może mieć dostęp do więcej niż jednej karty, a organizacja do więcej niż jednej osoby. Przy rozpoczynaniu wniosku pojawia się wtedy pytanie, w imieniu którego podmiotu jest składany. Rozpoznawanie po NIP-ie zapobiega duplikatom.

**Nad jednym wnioskiem pracuje czasem kilka osób.** U nas rozwiązuje się to samo z siebie: kto ma dostęp do karty organizacji, widzi ten sam wniosek roboczy i może go dokończyć. Udostępnianie pojedynczego wniosku osobie spoza organizacji jest poza MVP.

**Cała ta ścieżka nie ma dziś reprezentacji w modelu danych**, bo schemat wiąże użytkownika z podmiotem jeden do jednego, a dane podmiotu nie są osobnym rekordem z listą osób. Patrz `R-01` w [`rozbieznosci.md`](rozbieznosci.md).

---

# Ścieżka 3: składanie wniosku

Wnioskodawca. Najważniejsza ścieżka, bo przechodzi przez nią najwięcej ludzi.

## Dziewięć zasad, według których to projektujemy

Każda zasada zamyka jeden konkretny sposób, w który wnioskodawca dziś się gubi.

| Zasada | Co to znaczy w praktyce |
|---|---|
| 1. Zero okienek | wszystko robimy na miejscu, w formularzu; jedyny wyjątek to jedno potwierdzenie przy złożeniu wniosku, bo tego się nie odkręca |
| 2. Przycisk nigdy nie znika | "Złóż wniosek" jest widoczny od pierwszej sekundy; dopóki czegoś brakuje, jest wyłączony i wypisuje, czego, a każdy brak jest odnośnikiem prowadzącym prosto do tego pola |
| 3. Liczby liczy komputer | nigdy nie prosimy o liczbę, którą system może policzyć: sumy, procenty, wartość pozycji budżetu, udział kosztów pośrednich w dotacji |
| 4. Jeden rodzaj komunikatu | podpowiedź stoi pod polem zawsze i cicho; błąd pojawia się tylko wtedy, gdy naprawdę jest, i znika, gdy zniknie przyczyna; żadnych ostrzeżeń włączonych na stałe |
| 5. Jedna kolumna, jedna rzecz naraz | jedna sekcja na ekran, czytana od góry do dołu; bez ośmiu zakładek obok siebie |
| 6. Wniosek do przeczytania przed złożeniem | jak podsumowanie zamówienia w sklepie: cała treść na jednym ekranie, z "popraw" przy każdej sekcji |
| 7. Załącznik to jeden ruch | plik przeciąga się myszką, podmiana to przeciągnięcie nowego na to samo miejsce; obok zawsze zwykły przycisk wyboru pliku |
| 8. Nic się nie gubi | zapis po każdym wypełnionym polu i widoczne "zapisano o 14:32"; po zamknięciu przeglądarki wraca się na to samo miejsce |
| 9. Wnioskodawca wie, gdzie jest | na każdym ekranie widać, która to sekcja z czterech, co zostało i ile czasu do zamknięcia naboru |

## Krok 3.1. Start

Wchodzisz z publicznej strony konkursu, bez konta. Decyzja: "Wypełnij wniosek" albo mniejszy odnośnik "zacznij od wniosku z 2025 roku", widoczny tylko wtedy, gdy ta organizacja składała wniosek wcześniej. Dwa wyjścia, oba od razu widoczne, zero okien.

Na wejściu jedno pole: **rodzaj wnioskodawcy**. Od niego zależy cała pierwsza sekcja, więc lepiej zapytać raz na początku niż odsłaniać i chować pola w trakcie.

**Czego nie pytamy na wejściu, choć obecne narzędzie pyta:** który podmiot składa wniosek (pytamy tylko przy dostępie do kilku kart) oraz czy oferta jest pojedyncza czy wspólna (ofert wspólnych nie ma we wzorach). Nie przenosimy opcji "wgraj ofertę z pliku .xml".

**Ekran "co przygotować".** Jeden krótki ekran przed pierwszym polem, generowany z ustawień konkursu:

> Zanim zaczniesz, przygotuj:
> - numer KRS albo innego rejestru i NIP organizacji (tylko przy pierwszym wniosku)
> - numer rachunku bankowego
> - sprawozdanie finansowe albo CIT za lata 2025, 2024 i 2023, w pliku PDF
> - pomysł na projekt: co robicie, dla kogo, po czym poznacie, że się udało
> - kosztorys, choćby z grubsza
>
> Możesz przerwać w dowolnym momencie, wniosek zapisuje się sam.
> Nabór kończy się 17 września o 23:59. Zostało 16 dni.

Załączniki bierzemy z kroku 1.5, terminy z 1.1, a dwa zdania od siebie dopisuje OCWIP w ustawieniach formularza.

## Krok 3.2. Wypełnianie

Zawsze widzisz: pasek czterech sekcji ze stanem każdej (gotowa, w toku, są błędy), "zapisano o 14:32", licznik dni do końca naboru.

Przejście dalej z niedokończoną sekcją jest dozwolone; sekcja dostaje stan "są błędy" i tyle.

Prowadzenie za rękę, pięć rzeczy konkretnie:

1. Wprowadzenie do sekcji, dwa lub trzy zdania, pisane przez OCWIP.
2. **Wskazówki przeliczone z limitów konkursu.** Na wejściu do budżetu: "możesz wnioskować o maksymalnie 9 000 zł; koszty pośrednie najwyżej 10% dotacji, czyli 900 zł; rozwój instytucjonalny najwyżej 50%, czyli 4 500 zł".
3. Potwierdzenie po każdej sekcji zamiast ciszy: "Sekcja o projekcie gotowa. Zostały dwie sekcje i załączniki."
4. **Licznik dni bez sekundnika.** "Zostało 6 dni", a w ostatniej dobie "zostało 5 godzin" na czerwono.
5. **Jedno przypomnienie e-mailem trzy dni przed końcem naboru**, tylko do osób z rozpoczętym i niezłożonym wnioskiem.

Pole z limitem pokazuje licznik znaków, na przykład 176 z 500.

## Kroki 3.3 do 3.6

Zawartość czterech części wniosku jest w [`pola.md`](pola.md). Tutaj tylko to, co dotyczy zachowania:

- **Część I** dla drugiego i kolejnego wniosku pokazuje blok już wypełniony, z datą ostatniej aktualizacji karty i dwoma przyciskami: "dane są aktualne" oraz "popraw". Poprawka trafia do karty.
- **Część II** jest niemal identyczna dla wszystkich trzech rodzajów podmiotu. Różnice obsługuje pole warunkowe.
- **Część III**: tabela rezultatów i działania z części II są podstawą pozycji budżetu. Każda liczba, którą można policzyć, jest policzona.
- **Część IV**: lista oświadczeń jest warunkowa, tak jak część I, a po niej klauzula RODO z datą retencji z kroku 1.4.

## Krok 3.7. Podsumowanie i złożenie

To ekran, na którym w obecnym narzędziu ginie najwięcej ludzi, bo załączniki stoją poza formularzem, a przycisk "Złóż ofertę" nie pojawia się, dopóki brakuje pliku.

Widzisz: cały wniosek do przeczytania, sekcje zwinięte do nagłówków z "popraw" przy każdej; kafelki załączników; przycisk "Złóż wniosek".

**Załączniki jako kafelki:** każdy z nazwą, opisem od OCWIP i wzorem pliku do pobrania, jeśli został dodany. Plik przeciąga się myszką albo dodaje jednym kliknięciem. Nieobowiązkowe stoją niżej, wyraźnie oddzielone.

**Przycisk jest zawsze widoczny.** Dopóki czegoś brakuje, jest wyłączony, a obok stoi lista braków, w której każda pozycja jest odnośnikiem:

> Zostały trzy rzeczy do uzupełnienia:
> - Sekcja "Projekt": pole "Opis pomysłu na projekt" ma 640 znaków, wymagane jest minimum 1000. -> popraw
> - Sekcja "Budżet": koszty pośrednie to 12% dotacji, dopuszczalne jest 10%. -> popraw
> - Załącznik "Sprawozdanie finansowe za 2023" nie został dodany. -> dodaj plik

To jedyna rzecz na tym ekranie, którą naprawdę trzeba dobrze zrobić, bo od niej zależy, czy zamiast telefonu do OCWIP człowiek poradzi sobie sam.

Kliknięcie "Złóż wniosek" pokazuje **jedno okno potwierdzenia, jedyne w całej tej ścieżce**: "Po złożeniu wniosku nie będzie można go już edytować."

**Po złożeniu:** treść zamrożona, wniosek dostaje numer, idzie e-mail, można pobrać PDF potwierdzenia. Historia wersji i suma kontrolna pokazują, czy wniosek był poprawiany po złożeniu.

E-mail idzie **wyłącznie po złożeniu**, żeby nie dało się pomylić wersji roboczej ze złożoną.

Jeden podmiot może złożyć kilka wniosków w jednym konkursie. Nie blokujemy tego niczym.

---

# Ścieżka 4: prowadzenie naboru

Pracownik OCWIP.

**Krok 4.1, lista wniosków.** Widzisz wnioski złożone; rozpoczęte i niezłożone nie pojawiają się tu wcale. Kolumny: liczba porządkowa, numer wniosku, nazwa podmiotu, tytuł projektu, całkowity koszt zadania, wnioskowana kwota, status, wynik oceny formalnej. Na dole suma kwot wnioskowanych i ile zostało z puli. Sortowanie, filtrowanie po rodzaju wnioskodawcy, statusie i wyniku oceny formalnej, eksport do arkusza albo PDF.

Konkurs u operatora zachowuje dzisiejszy podział na zakładki: Informacje o konkursie, Nabór, Ocena, Wyniki, Zmiany, Umowy, Aneksy, Sprawozdania. **Zakładki puste w danym stanie konkursu są wyszarzone z wyjaśnieniem, a nie ukryte:** "Umowy" w trwającym naborze mówi "pojawią się po rozstrzygnięciu".

**Krok 4.2, zwrot wniosku do poprawy.** Mechanizm z obecnego narzędzia, który uznajemy za bardzo dobry i powtarzamy w całości.

Dostępny na obu etapach: w trakcie naboru i po ocenie formalnej. Operator wskazuje, **które konkretnie sekcje** wnioskodawca może edytować, co ma poprawić i do kiedy. Wniosek wraca do stanu roboczego z odblokowanymi tylko wskazanymi sekcjami, resztą szarą; idzie e-mail; wnioskodawca widzi licznik dni i powód przy każdej odblokowanej sekcji.

**Poprawka bez ponownego złożenia nie liczy się.** Powtórne złożenie tworzy nową wersję z nowym znacznikiem czasu i nową sumą kontrolną. Obok siebie widać dwie sumy: pierwotną i ostatnią, więc jednym spojrzeniem wiadomo, czy wniosek był poprawiany.

Zwrot do poprawy **nie ma karty na Trello**, pozycja `R-03`.

## Stany wniosku

```
roboczy -> złożony -> (zwrócony do poprawy -> złożony ponownie)
        -> po ocenie formalnej pozytywnej albo negatywnej
        -> oceniony merytorycznie
        -> dofinansowany albo odrzucony
        -> dofinansowany, umowa niepodpisana -> umowa podpisana -> w realizacji -> rozliczony
```

Wnioskodawca przestawia tylko roboczy na złożony i z powrotem po zwrocie do poprawy. Resztę przestawia operator albo dzieje się to samo, z terminów konkursu.

Od stanu złożony treść wniosku jest zamrożona i wniosek pojawia się na liście u operatora. Stan dofinansowany odblokowuje generowanie umowy. Rozliczony zamyka projekt i uruchamia liczenie retencji.

**Stan "dofinansowany, umowa niepodpisana" wygląda na zbędny, a nie jest:** umowę podpisuje się ręcznie, poza systemem, więc między przyznaniem dotacji a podpisem jest realny odstęp, w którym pieniądze są już zarezerwowane, a zobowiązania jeszcze nie ma.

**Dzisiejszy schemat zna pięć stanów wniosku:** `Draft`, `Submitted` i od T-42 trzy wyniki zapisywane naraz przy zatwierdzeniu wyników konkursu: `Funded` ("dofinansowany, umowa niepodpisana"), `Reserve` (lista rezerwowa, ZR-09) i `Rejected`. Stany umowy, realizacji i rozliczenia przyjdą z T-43 i dalej; stany pośrednie oceny (po ocenie formalnej, oceniony merytorycznie) nie są stanami wniosku, tylko wynikiem kart, liczonym przy odczycie (T-39). Reszta rozjazdu to pozycja `R-17`.

---

# Ścieżka 5: ocena

Szczegóły i podział na to, co blokuje B-02, a co nie: [`M5-ocena.md`](M5-ocena.md).

Skrót przebiegu: krok 5.0 ustawienia oceny w konkursie, 5.1 powołanie komisji z oświadczeniem o konflikcie interesów, 5.2 przypisanie wniosków przez losowanie albo ręcznie, 5.3 ocena formalna (jedna osoba, pytania tak albo nie, wynik negatywny nie zamyka sprawy), 5.4 ocena merytoryczna (punkty, kwota rekomendowana, uzasadnienie obniżenia), 5.5 udostępnienie kart wnioskodawcom (jedna decyzja na cały konkurs, nieodwracalna), 5.6 dokumenty komisji.

**Dokumenty, które komisja wytwarza**, wszystkie z tego samego mechanizmu wzorów co umowa:

| Dokument | Format dziś |
|---|---|
| Lista kontaktowa ekspertów | edytowalny |
| Lista obecności komisji | edytowalny, tabela: liczba porządkowa, imię i nazwisko z miejscem pracy, kolumna na podpis |
| Protokół z posiedzenia komisji konkursowej | edytowalny |
| Wyniki oceny merytorycznej | PDF |
| Wyniki oceny merytorycznej | edytowalny |

Do tego dokument z oświadczeniami o konflikcie interesów (tabela: liczba porządkowa, nazwisko i imię, oświadczenie zaakceptowane albo nie, uzasadnienie odmowy) oraz **wykaz błędów formalnych dla całego konkursu** jako jedna tabela: podmiot, nazwa zadania, stwierdzone braki. Przy dwudziestu wnioskach taka tabela zastępuje dwadzieścia ręcznie pisanych e-maili.

---

# Ścieżka 6: rozstrzygnięcie i umowa

Szczegóły: [`M6-wyniki.md`](M6-wyniki.md).

**Krok 6.1, lista rankingowa.** Wnioski według punktów, z kwotą wnioskowaną i rekomendowaną, kwota przyznana jako pole edytowalne wprost na liście, kolumna uwag. Zawsze widoczne: "przyznano 84 000 zł z 100 000 zł, zostało 16 000 zł", na czerwono po przekroczeniu. Wpisanie kwoty oznacza, że wniosek dostał dofinansowanie. Eksport: PDF do publikacji, XLSX i CSV do liczenia.

**Krok 6.2, wzory dokumentów.** Dokument to treść z osadzonymi znacznikami, a znacznik jest nazwanym odwołaniem do danej z systemu. Wzór ustawia się raz, nie przy każdej umowie. Lista znaczników: [`pola.md`](pola.md).

**Krok 6.3, generowanie.** Jeden dokument dla jednego wniosku albo hurtem dla całego konkursu, wtedy wynik przychodzi w jednym pliku zip. Format: PDF do odczytu i RTF do dalszej edycji. Podpis ręczny, poza systemem.

Po podpisaniu operator wpisuje datę podpisania umowy. **To jedno pole robi trzy rzeczy naraz:** przestawia wniosek ze stanu "dofinansowany, umowa niepodpisana" w "umowa podpisana", zasila znacznik daty podpisania i wyznacza początek realizacji projektu, bo wzór liczy czas trwania "od dnia podpisania umowy".

**Krok 6.4, aneksy i transze.** Poza MVP. Model umowy dopuszcza wiele wypłat, interfejs na start pokazuje jedną. Dorobienie tabeli transz później jest tanie; rozbicie pojedynczej kwoty na wiele wypłat po wdrożeniu, gdy w bazie leżą podpisane umowy, nie jest.

---

# Ścieżka 7: sprawozdanie

Poza MVP, zablokowane przez B-04. Zasada "było i jest", rozliczenie i uznawanie kosztów pozycja po pozycji: [`M7-wdrozenie.md`](M7-wdrozenie.md).

---

# Wiadomości, które system wysyła

Zebrane w jednym miejscu, bo treści i tak będą do zatwierdzenia, a brakująca wiadomość jest znacznie tańsza do dodania teraz niż po wdrożeniu.

| Kiedy | Do kogo | Treść ustawiana przez OCWIP |
|---|---|---|
| Rejestracja konta: odnośnik aktywacyjny | osoba zakładająca konto | nie, tekst systemowy |
| Reset zapomnianego hasła | osoba z kontem | nie, tekst systemowy |
| Potwierdzenie zmiany adresu e-mail, na nowy adres | osoba z kontem | nie, tekst systemowy |
| Prośba o dostęp do istniejącej karty organizacji | osoba, która kartę założyła | nie, tekst systemowy |
| Prośba o dostęp bez odpowiedzi przez siedem dni | administrator OCWIP | nie, tekst systemowy |
| Przypomnienie trzy dni przed końcem naboru | wnioskodawca z rozpoczętym i niezłożonym wnioskiem | tak, treść przy konkursie |
| Potwierdzenie złożenia wniosku | wnioskodawca | tak, krok 1.6 |
| Zwrot wniosku do poprawy, z powodem i terminem | wnioskodawca | tak, operator pisze przy zwrocie |
| Wynik oceny, także odmowa, razem z kartą oceny, jeśli jest udostępniana | wnioskodawca | tak, treść przy konkursie |
| Rozstrzygnięcie konkursu i przyznana kwota | wnioskodawca | tak, treść przy konkursie |
| Sprawozdanie do wypełnienia, z terminem z umowy | wnioskodawca | tak, treść przy konkursie |
| Zwrot sprawozdania do poprawy | wnioskodawca | tak, operator pisze przy zwrocie |
| Powołanie do komisji i prośba o oświadczenie | ekspert | tak, treść przy konkursie |

Treści oznaczone jako edytowalne działają tym samym mechanizmem co wzory dokumentów, czyli bez zgłaszania czegokolwiek komukolwiek.

---

# Uwarunkowania prawne, o których pamiętamy

Nie jest to opinia prawna. To, co realnie wpływa na budowę systemu.

**Założenie, na którym stoi cały pomysł kreatora formularza:** konkursy OCWIP to regranting, czyli rozdzielanie dalej środków otrzymanych od kogoś innego, na podstawie własnego regulaminu, a nie otwarty konkurs ofert w trybie art. 13 ustawy o działalności pożytku publicznego i o wolontariacie. Gdyby którykolwiek konkurs był prowadzony w trybie art. 13, obowiązywałby ustawowy wzór oferty z rozporządzenia z 24 października 2018 r. (Dz. U. 2018 poz. 2057), którego nie wolno uprościć ani przestawić. Wnioskujemy tak z materiałów: wnioskodawcą może być grupa nieformalna bez osobowości prawnej, co w trybie ustawowym jest niemożliwe.

| Wymóg | Skąd wynika | Jak to robimy |
|---|---|---|
| Dowód, kto i kiedy złożył wniosek | odcięcie naboru co do minuty jest do obrony tylko wtedy, gdy da się je udowodnić | znacznik czasu z serwera, numer wersji, suma kontrolna, niezmienialny zapis zdarzeń, e-mail i PDF potwierdzenia |
| Wersja robocza nie może wyglądać jak złożona | najczęstszy realny spór: ktoś wypełnił i nie kliknął "Złóż" | przycisk widoczny od początku z listą braków, jednoznaczny status, e-mail tylko po złożeniu |
| Równe traktowanie wnioskodawców | zmiana warunków po ogłoszeniu może być podważona | wersjonowanie formularza; zmiana istotna wymaga ponownego ogłoszenia, i to jest zasada organizacyjna, nie techniczna |
| Udokumentowana ocena | procedury grantowe wymagają przejrzystych kryteriów i śladu po ocenie | karty oceny w systemie, lista rankingowa, karty udostępniane wnioskodawcom |
| Ścieżka odwoławcza albo zastrzeżeń do oceny | zwykle wymagana przez regulamin lub grantodawcę | **nieopisane, brakuje regulaminu** |
| Przechowywanie dokumentacji, minimum 5 lat | wymóg twardy od pierwszego dnia | wniosków złożonych nie da się usunąć, usunięcie konkursu to oznaczenie jako nieaktywny; usunięcie po retencji dotyczy danych osobowych, nie samego wniosku |
