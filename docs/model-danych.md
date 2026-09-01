# Model danych

Stan: **wszystkie sześć tabel pierwszego podejścia (`users`, `entities`, `competitions`, `form_definitions`, `applications`, `attachments`) są w `AppDbContext` i w migracjach.** Ten dokument opisuje kierunek i, co ważniejsze, jawnie oddziela ustalenia od założeń.

Kto przyjdzie do projektu za miesiąc, musi umieć odróżnić jedno od drugiego.

## Encje, których świadomie NIE budujemy

**Ocena, Umowa, Sprawozdanie.**

Powód jest konkretny: nie mamy jeszcze realnych wzorów dokumentów. Nie widzieliśmy karty oceny, nie wiemy, ilu recenzentów ocenia jeden wniosek ani czy liczy się suma czy średnia punktów. Nie mamy wzoru umowy ani wzoru sprawozdania.

Modelowanie tego teraz byłoby zgadywaniem, a zgadywanie w modelu danych kosztuje najwięcej. Dokumenty dostaniemy od zamawiającego.

## Encje planowane w pierwszym podejściu

### Użytkownik (`users`)

Konto do logowania. Ma rolę: operator, wnioskodawca albo recenzent.

Istnieje. Rola trzymana jako tekst, tak samo jak status konkursu i z tego samego powodu. E-mail unikalny indeksem w bazie, nie sprawdzeniem w kodzie: `SELECT` przed `INSERT` przegrywa wyścig z drugą rejestracją w tej samej chwili, a dwa konta na jeden adres psują reset hasła. `Pesel` jest nullable, bo pojawia się dopiero na etapie umowy, a wymagana kolumna kazałaby każdemu wcześniejszemu kontu nosić wartość zastępczą, która przechodzi każdą walidację. Relacja do podmiotu jest opcjonalna, bo operator i recenzent pracują dla OCWIP i nie składają wniosków. Znaczniki czasu to `DateTimeOffset`, nie `DateTime`: `DateTime` zmapowany na `timestamptz` tylko przenosi problem na `DateTimeKind`, patrz [`architektura.md`](architektura.md).

### Podmiot (`entities`)

Ten, kto składa wniosek. **To nie jest to samo co użytkownik.**

Jedna encja z polem typu, nie trzy tabele. Trzy typy: grupa nieformalna, grupa nieformalna pod patronatem organizacji, organizacja. NIP i adres są wymagane tylko dla organizacji, więc walidacja jest zależna od typu, a nie wymuszona przez NOT NULL na wszystkich kolumnach. Podmiot bez NIP-u to nie błąd danych, to grupa nieformalna.

Istnieje. Wymagalność zależna od typu **nie jest** też check constraintem, i to jest decyzja, nie przeoczenie: nie wiemy, czy grupa pod patronatem organizacji podaje NIP patrona, więc schemat by tu zgadywał. Ta walidacja siedzi na brzegu API. Typ trzymany jako tekst w kolumnie na 30 znaków, bo `PatronInformalGroup` ma już 19 i przyjęte dla pozostałych enumów 20 nie zostawiałoby miejsca na zmianę nazwy.

### Konkurs (`competitions`)

Data i godzina startu oraz zamknięcia (UTC, pełne minuty), maksymalna kwota dotacji, wymagane załączniki, status.

Istnieje. Status trzymany jako tekst, nie jako ordynał enuma: wstawienie albo przestawienie wartości w `CompetitionStatus` przeinterpretowałoby po cichu wszystkie istniejące wiersze. Baza pilnuje dwóch rzeczy, których komentarz nie utrzyma: `start_date < end_date` (inaczej powstaje konkurs zamknięty przed otwarciem, do którego nigdy nie da się złożyć oferty) oraz `max_grant_amount > 0`. Okno konkursu przechowywane w pełnych minutach: settery `StartDate` i `EndDate` ucinają sekundy, a dwa check constraints pilnują tego samego w bazie, żeby insert omijający encję nie wpisał `12:00:30`. Ucinanie celowo nie jest konwerterem, powód w [`architektura.md`](architektura.md). Znaczniki audytowe (`created_at`, `updated_at`) zachowują pełną precyzję, bo odpowiadają na inne pytanie. Indeks na `(status, end_date)`, bo po tej parze filtruje się publiczna lista konkursów. Wymagane załączniki jeszcze nie istnieją, wchodzą razem z encją załącznika.

### Definicja formularza (`form_definitions`)

Struktura formularza jako dokument JSONB plus numer wersji. Wersjonowana, bo operator może edytować formularz w trakcie życia konkursu.

Zawartość tego JSON-a, czyli jak wyglądają sekcje, pola i walidacje, jest osobnym, dużym tematem. Kontrakt tej kolumny powstaje w osobnej karcie.

Istnieje. Numer wersji jest unikalny w obrębie konkursu, nie globalnie: wersja 1 musi móc istnieć w każdym konkursie, a dwa wiersze z tą samą wersją w jednym konkursie odbierałyby możliwość stwierdzenia, przeciw której wersji formularza wypełniono ofertę. Numer wersji musi być dodatni, a korzeń JSON-a musi być obiektem albo tablicą, oba pilnowane check constraintem: bez tego kolumna przyjmuje `-7` jako wersję i `123` jako definicję formularza. Który z dwóch korzeni wybierze kontrakt, decyduje T-20, więc constraint tego nie przesądza. JSON siedzi w kolumnie jako `JsonElement`, nie `JsonDocument`: EF nigdy nie zwalnia zmaterializowanych instancji, a `JsonDocument` jest `IDisposable` i oparty o `ArrayPool`, więc zapytanie listujące alokowałoby jedną na wiersz.

### Wniosek (`applications`)

Odpowiedzi jako JSONB. Wskazuje na Podmiot oraz **na konkretną wersję definicji formularza, nie na konkurs.** Uzasadnienie w [`architektura.md`](architektura.md).

Brak ograniczenia unikalności na parze podmiot plus konkurs: jeden podmiot może złożyć kilka ofert w jednym konkursie.

Istnieje. Ta nieobecność jest wymogiem, nie luką, więc ma **test dowodzący, że constraintu nie ma**. Komentarz by nie przeżył pierwszej osoby, która dostrzeże "oczywisty brakujący constraint".

Wniosek trzyma obok siebie `competition_id` i `form_definition_id`, choć wersja formularza sama należy do konkursu. Dwa zwykłe klucze obce pozwoliłyby tej parze się rozjechać, więc klucz obcy na definicję formularza jest **złożony** i wskazuje na klucz alternatywny `(competition_id, id)`. Powód i alternatywy w [`architektura.md`](architektura.md).

Status jest jednym z dwóch: `Draft` albo `Submitted`. Dalsze stany, czyli wszystko, co dzieje się na liście rankingowej, należą do encji oceny, której świadomie nie budujemy. Data złożenia i numer wniosku są sparowane ze statusem osobnymi check constraintami: złożonej oferty, której nikt nie potrafi zadatować, nie da się użyć w sporze o termin, a wersja robocza z datą złożenia czyta się jednocześnie jako niewysłana i wysłana. Numer nadawany jest przy złożeniu, więc wersja robocza go nie ma i nie zużywa, bo rejestr z lukami po nigdy niezłożonych wersjach roboczych jest rejestrem, którego operator nie umie wyjaśnić wnioskodawcy. Korzeń JSON-a z odpowiedziami musi być obiektem albo tablicą, tym samym constraintem co przy definicji formularza.

### Załącznik (`attachments`)

Na tym etapie tylko metadane pliku i powiązanie z wnioskiem. Fizyczne przechowywanie plików to osobny temat.

Istnieje: nazwa pliku, typ MIME, rozmiar, ścieżka w storage. Typ MIME jest **zadeklarowany przez klienta, nie sprawdzony**, i kolumna mówi to wprost, bo inaczej następny czytający uzna ją za wiarygodną. Rozmiar musi być dodatni, bo załącznik zerobajtowy to nieudany upload, nie dokument. Ścieżka w storage jest unikalna: dwa wiersze wskazujące na jeden plik zamieniają usunięcie pliku w sposób psucia cudzego załącznika. Ścieżka nie może dać się zgadnąć, a pobranie musi przechodzić tę samą kontrolę uprawnień co sam wniosek, bo załącznik to dokument cudzej organizacji. Jedno i drugie realizuje T-32, tutaj jest tylko zapisane przy kolumnie.

## Jawne założenia do potwierdzenia

Są to **założenia**, nie ustalenia. Potwierdzić z zamawiającym.

| Założenie | Skąd się wzięło | Co się stanie, jeśli jest błędne |
|---|---|---|
| Użytkownik do Podmiotu jeden do jednego | Nie wiemy, czy w organizacji wniosek może składać kilka osób z osobnych kont | Trzeba dodać tabelę pośredniczącą i przemyśleć uprawnienia w obrębie podmiotu |
| Jedna rola na użytkownika | Na spotkaniu nie padło nic o osobie, która jest jednocześnie operatorem i recenzentem | Rola przestaje być kolumną, staje się relacją |
| Sprawozdanie jest jedno na wniosek | Standard w małych dotacjach, ale nie ustalone | Relacja jeden do wielu, plus statusy sprawozdań cząstkowych |
| Brak aneksów do umów | Na spotkaniu nie padło ani słowo | Umowa zyskuje wersjonowanie, podobnie jak definicja formularza |
| Numer wniosku nadawany przy złożeniu, nie przy utworzeniu wersji roboczej | Wersja robocza, której nikt nie złożył, zużywałaby numer i zostawiała lukę w rejestrze | Numer staje się kolumną wymaganą od utworzenia, a check constraint parujący go ze statusem znika |
| Numer wniosku unikalny w obrębie konkursu, nie globalnie | Nie znamy schematu numeracji OCWIP. Unikalność globalna odrzuciłaby numer "001" w drugim konkursie, czyli poprawne dane | Nic. Ten zakres nie odrzuca niczego, co wyprodukowałby schemat globalny, bo numer globalnie unikalny jest też unikalny w konkursie |
| Numer wniosku raz nadany nie wraca do puli, nawet gdy wniosek zostanie wycofany | Indeks unikalny na `(competition_id, number)` obejmuje wszystkie wiersze, a reguła 1 zabrania twardego kasowania | Indeks staje się częściowy (`WHERE is_active`), a numer zwolniony przez wycofany wniosek może trafić do kolejnego wnioskodawcy |
| Unikalność e-maila wrażliwa na wielkość liter | Normalizacja adresu, czyli rozstrzygnięcie, czy "Adam@x.pl" i "adam@x.pl" to jedno konto, należy do rejestracji (T-12.1) | Indeks unikalny wchodzi na wyrażenie albo adres jest normalizowany przy zapisie, plus migracja czyszcząca istniejące duplikaty |
| Dezaktywowane konto zachowuje swój e-mail i swój podmiot na zawsze, więc drogą powrotną jest reaktywacja, a nie ponowna rejestracja | Indeksy unikalne na `email` i `entity_id` obejmują wszystkie wiersze, a reguła 1 zabrania twardego kasowania, więc wiersz nigdy nie zwalnia adresu | Oba indeksy stają się częściowe (`WHERE is_active`), a rejestracja przestaje być jedyną drogą wejścia dla wcześniej dezaktywowanego konta |

## Otwarte punkty implementacyjne

Nie założenia o domenie, tylko rzeczy, których schemat świadomie nie rozstrzyga, a które ugryzą kartę wdrażającą ścieżkę zapisu.

- **Nadawanie numeru wniosku.** Schemat wymaga numeru dokładnie w tej samej instrukcji, która ustawia status na `Submitted`, a para `(competition_id, number)` jest unikalna. Nic w schemacie tego numeru nie przydziela: nie ma sekwencji, wartości domyślnej ani blokady. Dwóch wnioskodawców klikających "Złóż" w tej samej sekundzie odczyta ten sam `MAX(number)` i jeden dostanie 23505 przy próbie złożenia, która może być sekundy od odcięcia co do minuty. Strategię przydziału (sekwencja per konkurs, blokada doradcza albo ponowienie) wybiera karta domykająca składanie wniosku.
- **Reaktywacja konta.** Patrz ostatni wiersz tabeli powyżej. Dezaktywowane konto blokuje swój e-mail i swój podmiot, więc T-12.1 musi mieć ścieżkę reaktywacji, bo sama rejestracja nie da się pogodzić z regułą "nie ujawniamy, czy konto istnieje".
- **Szyfrowanie odpowiedzi wniosku.** T-80 nie może zaszyfrować całej kolumny `answers`: szyfrogram nie jest ani obiektem, ani tablicą, więc padłby check constraint, a razem z kolumną jsonb zniknęłaby wyszukiwalność, po którą jsonb został wybrany. Szyfrowane są pola WEWNĄTRZ dokumentu, nie dokument.
- **Szerokości kolumn wrażliwych.** `nip` na 10 znaków i `pesel` na 11 mieszczą dokładnie tekst jawny i zero szyfrogramu. T-80 musi te kolumny poszerzyć, inaczej pierwszy zaszyfrowany zapis wywali 22001.

## Reguły, które model musi respektować

1. Zero `ON DELETE CASCADE`. Retencja minimum 5 lat wyklucza twarde kasowanie. Soft delete to `IsActive` plus nullable `DeactivatedAt`, sparowane check constraintem, patrz [`architektura.md`](architektura.md).
2. Wszystkie znaczniki czasu w UTC. Wymusza to `UtcDateTimeOffsetConverter` na każdej właściwości `DateTimeOffset`, patrz [`architektura.md`](architektura.md).
3. Klucze główne jako UUID (`gen_random_uuid()`), nie sekwencje. Identyfikator wniosku pojawia się w adresie URL, a sekwencja mówi konkurentowi, ile wniosków wpłynęło i pozwala zgadywać cudze.
4. Każde pole trzymające dane wrażliwe (PESEL, NIP, adres osoby fizycznej) oznaczone komentarzem w kodzie.

## Dane testowe

Minimum, potrzebne do testów uprawnień, a nie do ozdoby: jeden operator, dwóch wnioskodawców, jeden konkurs i dwa wnioski, z czego jeden roboczy i jeden złożony.

Skryptu zasilającego jeszcze nie ma, wchodzi razem z diagramem ERD w T-11.5. Testy bazodanowe zasiewają swój własny łańcuch (konkurs, wersja definicji formularza, podmiot) przez `TestApplicationChain`, żeby nie zależeć od stanu przygotowanego ręcznie.
