# Spis pól

Kompletny spis pól wyprowadzony z dokumentu `RAPORT-proces-i-pola.docx` i z trzech wzorów wniosku na 2026. To jest **jedyne miejsce**, w którym te listy żyją: powtórzenie ich w karcie zadania tworzy drugą listę, która z czasem się rozjedzie.

Trzy zastrzeżenia, zanim zaczniesz z tego korzystać:

1. **To nie jest dokument zatwierdzony przez zamawiającego.** Raport jest propozycją z pytaniami. Pola oznaczone jako "do potwierdzenia" mogą wypaść.
2. **Wzór karty oceny i wzór sprawozdania nie dotarły.** Odpowiednie sekcje są w tym pliku ubogie i to jest stan faktyczny, a nie przeoczenie. Patrz [`blokery.md`](blokery.md).
3. **Część pól kłóci się z dzisiejszym modelem danych.** Każda taka pozycja ma odnośnik do [`rozbieznosci.md`](rozbieznosci.md).

---

## Rodzaje pól, z których buduje się formularz

Lista nie została wymyślona: powstała z rozebrania trzech wzorów wniosku na części. To jest komplet, który w nich występuje.

| Rodzaj pola | Gdzie występuje |
|---|---|
| Tekst krótki | tytuł projektu, nazwa wnioskodawcy |
| Tekst długi, z limitem znaków | opis pomysłu, cel główny, uzasadnienie kosztów |
| Liczba | liczba uczestników projektu |
| Kwota | pozycje budżetu, średni roczny przychód |
| Procent | limity kosztów pośrednich i rozwoju instytucjonalnego |
| Data | zakończenie projektu |
| Data i godzina | terminy naboru w ustawieniach konkursu |
| Tak albo nie | wymóg wersji papierowej, nabór ciągły |
| Wybór jednej opcji | forma prawna, próg miejscowości |
| Wybór wielu opcji | dopuszczalne formaty załącznika |
| Tabela o zmiennej liczbie wierszy | osoby uprawnione do reprezentowania, rezultaty, pozycje budżetu |
| Tabela o stałej liczbie wierszy | trzech członków grupy nieformalnej |
| Plik | załączniki |
| Oświadczenie | osiem oświadczeń z części IV |
| Pole wyliczane | sumy, procenty, wartość pozycji budżetu; wnioskodawca go nie wpisuje |

## Co definicja przechowuje przy każdym polu

Minimum, bez którego formularza nie da się odtworzyć: **nazwa, podpowiedź dla wnioskodawcy, rodzaj, wymagalność, limit znaków, warunek widoczności**. Do tego dwie rzeczy z decyzji projektowych:

- **flaga wydruku** (D14): czy pole trafia na wydruk oferty. Pola techniczne są widoczne w interfejsie, ale nie na wydruku.
- **źródło obliczenia** przy polu wyliczanym: z czego się liczy. Definicja, w której pole wyliczane jest oznaczone jako drukowane, ale nie ma z czego się policzyć, jest odrzucana przez walidację schematu.

## Cztery mechanizmy, bez których tego formularza nie da się zrobić

1. **Pole warunkowe.** Wybór "inna forma prawna" odsłania pole tekstowe. Wybór rejestru innego niż KRS odsłania inne pole numeru.
2. **Tabela, do której wnioskodawca dodaje wiersze**, z usuwaniem i zmianą kolejności.
3. **Pole wyliczane.** Wartość pozycji budżetu to liczba jednostek razy cena. Procent kosztów pośrednich to suma tabeli C podzielona przez dotację.
4. **Powiązanie między sekcjami.** Pozycje budżetu odnoszą się do działań opisanych wcześniej; zmiana w jednym miejscu przelicza drugie.

---

# Ustawienia konkursu

Kreator ogłoszenia, siedem kroków. Karty: `T-20` (API), `T-22` (interfejs).

## Krok 1.1. Dane konkursu

| Pole | Rodzaj | Wymagane | Uwagi |
|---|---|---|---|
| Numer konkursu | tekst | tak | format typu 1/2026 |
| Tytuł konkursu | tekst | tak | |
| Data publikacji konkursu | data i godzina | tak | kiedy konkurs staje się widoczny publicznie |
| Rozpoczęcie naboru wniosków | data i godzina | tak | |
| Zakończenie naboru wniosków | data i godzina | tak, chyba że nabór ciągły | tnie co do minuty |
| Nabór ciągły | tak albo nie | nie | wyłącza datę zakończenia |
| Informacja pokazywana po złożeniu wniosku | tekst z formatowaniem | nie | osobna od treści e-maila z kroku 1.6 |

## Krok 1.2. Opis konkursu

| Pole | Rodzaj | Wymagane |
|---|---|---|
| Opis konkursu: cel, kto może startować, na co | tekst z formatowaniem | tak |
| Zakładane rezultaty konkursu | tekst z formatowaniem | nie |
| Adres strony z regulaminem | odnośnik | nie |

## Krok 1.3. Forma dostarczenia

| Pole | Rodzaj | Uwagi |
|---|---|---|
| Czy wymagane jest złożenie dokumentów w wersji papierowej | tak albo nie | domyślnie nie |
| Termin składania wersji papierowej | data i godzina | widoczne tylko po zaznaczeniu wyżej |
| Adres do złożenia wersji papierowej | tekst do 500 znaków | jak wyżej |

Wersja papierowa jest przełącznikiem konkursu, domyślnie wyłączonym. **Nie budujemy pod papier osobnego obiegu:** nie porównujemy automatycznie obu wersji i nie blokujemy oceny do czasu wpływu koperty.

## Krok 1.4. Limity

Najważniejszy krok w całym kreatorze, bo z niego wynikają wszystkie blokady w budżecie wniosku.

| Pole | Rodzaj | Wymagane | Uwagi |
|---|---|---|---|
| Termin realizacji zadań od, do | data | tak | ramy, w których musi zmieścić się projekt |
| Całkowita kwota na realizację zadań | kwota | tak | pula konkursu, informacyjnie dla wnioskodawców |
| Minimalna dotacja na jeden wniosek | kwota | nie | |
| Maksymalna dotacja na jeden wniosek | kwota | tak | limit pilnowany w budżecie wniosku |
| Maksymalny procent kosztów pośrednich z dotacji | procent | tak | we wszystkich trzech wzorach na 2026 jest to 10% kwoty dotacji |
| Maksymalny procent kosztów rozwoju instytucjonalnego | procent | nie | we wzorze na 2026: 50% dla organizacji pozarządowej, 30% dla obu wariantów grupy nieformalnej, przy czym w obu wzorach dla grup jest to oznaczone do usunięcia |
| Maksymalny średni roczny przychód organizacji z trzech ostatnich zamkniętych lat | kwota | nie | próg odcinający duże organizacje, liczony tą samą metodą co pole we wniosku |
| Data, do której przetwarzane będą dane osobowe | data | tak | wchodzi do klauzuli RODO; **system nie przyjmuje daty wcześniejszej niż pięć lat od zamknięcia naboru** |

Cztery reguły, które z tego kroku wynikają:

- **Podstawa liczenia procentu jest ustawieniem konkursu**, do wyboru: kwota dotacji albo całkowita wartość projektu. Domyślnie kwota dotacji, bo tak pisze wzór na 2026, ale przełącznik zostaje.
- **Próg przychodu** wynosi we wzorze 200 000 zł i występuje wyłącznie w formularzu dla organizacji pozarządowej. Pole musi przyjmować wartość 0, a przekroczenie progu jest **twardą blokadą** z komunikatem "przekroczono limit zgodny z Regulaminem. Podmiot nieuprawniony".
- **Pod każdym polem kwotowym dopisujemy kwotę słownie**, bo wchodzi potem do umowy.
- **Kategorie kosztów są ustawieniem konkursu**, nie stałą w systemie. Domyślnie trzy: koszty bezpośrednie, rozwój instytucjonalny, koszty pośrednie. Wyłączenie kategorii chowa nie tylko jej tabelę w budżecie, ale i odpowiadającą jej sekcję opisową w części o projekcie.

Pole "ograniczenie terytorialne dla siedziby wnioskodawcy" świadomie **nie wchodzi**: wszystkie konkursy dotyczą Opolszczyzny, a sam wymóg siedziby obsługuje oświadczenie nr 4 z części IV.

## Krok 1.5. Załączniki do oferty

Dla każdego załącznika osobno:

| Pole | Rodzaj |
|---|---|
| Tytuł załącznika | tekst |
| Opis, czyli co dokładnie ma być dołączone | tekst |
| Wymagalność | wybór jednej opcji |
| Wzór pliku do pobrania przez wnioskodawcę | plik, do 10 MB |
| Dopuszczalne formaty pliku | zaznaczenie wielu: pdf, doc, docx, xls, xlsx, jpg, odt, ods |

**Wymagalność ma trzy warianty**, nie sześć jak dziś: wymagany, niewymagalny, wymagany warunkowo gdy wnioskodawca jest w rejestrze innym niż KRS. Dodanie kolejnego wariantu jest proste.

Limity: **10 MB na plik, 50 MB na cały wniosek**, oba jako ustawienie.

Załączniki konkursu na 2026: odpis z rejestru lub wyciąg z ewidencji, jeśli nie jest ogólnodostępny, oraz CIT albo sprawozdanie finansowe za lata 2025, 2024 i 2023.

## Krok 1.6. Osoby kontaktowe

| Pole | Rodzaj | Uwagi |
|---|---|---|
| Osoby do kontaktu w konkursie | wybór z listy pracowników | widoczne dla wnioskodawców na stronie konkursu, także dla gościa bez konta |
| Treść wiadomości e-mail wysyłanej po złożeniu wniosku | tekst | osobne pole od informacji na ekranie po złożeniu |

## Krok 5.0. Ustawienia oceny

Osiem parametrów ustawianych raz przy konkursie. Szczegóły i konsekwencje: [`M5-ocena.md`](M5-ocena.md).

| Ustawienie | Co robi |
|---|---|
| Liczba ekspertów na wniosek | ilu recenzentów ocenia jeden wniosek, domyślnie dwóch |
| Czy do systemu wchodzi tylko ocena zbiorcza komisji | tryb: każdy osobno albo jedna ocena wprowadzana przez przewodniczącego |
| Lista osób oceniających zbiorczo | tekst: imię i nazwisko, miejsce pracy, stanowisko |
| Sposób liczenia wyniku | suma albo średnia punktów |
| Próg rozbieżności ocen | domyślnie 30% skali |
| Czy publikować punktację | czy wnioskodawca widzi liczby, czy tylko wynik |
| Karta oceny widoczna dla wnioskodawcy | czy karty są udostępniane |
| Nazwa oceniającego | jak go nazywać w dokumentach, na przykład "Członek komisji konkursowej" |

## Sekcja "Dane do dokumentów"

Dane urzędowe potrzebne wzorom dokumentów, a niepochodzące ani z wniosku, ani z limitów: organ wydający zarządzenia, numer i data zarządzenia o ogłoszeniu konkursu, numer i data zarządzenia o powołaniu komisji, numer, rok i data uchwały o programie współpracy, paragraf i klasyfikacja budżetowa. Jeśli są zbędne, zostają puste.

---

# Konto i karta organizacji

## Krok 2.1. Rejestracja

| Pole | Rodzaj | Wymagane | Sprawdzenie |
|---|---|---|---|
| Adres e-mail | e-mail | tak | poprawność formatu, adres służy do logowania |
| Powtórz adres e-mail | e-mail | tak | musi się zgadzać z powyższym |
| Hasło | hasło | tak | minimalna długość |
| Powtórz hasło | hasło | tak | musi się zgadzać |
| Imię | tekst | tak | |
| Nazwisko | tekst | tak | |
| Telefon kontaktowy | tekst | tak | |
| Zgoda na regulamin | zaznaczenie | tak | |
| Zgoda na przetwarzanie danych | zaznaczenie | tak | |

Na starcie **nie pytamy o nic, co dotyczy organizacji**. Konto zakłada osoba w minutę, może się rozejrzeć, a dane podmiotu podaje raz, przy pierwszym wniosku.

Województwo, powiat i gmina, o które pyta dzisiejsza rejestracja, **wypadają**: służą do filtrowania konkursów z całej Polski, a my pokazujemy tylko konkursy OCWIP.

Uwaga: dzisiejszy `RegisterRequest` w kodzie ma cztery pola (adres, hasło, imię, nazwisko). Telefon, powtórzenia i dwie zgody to różnica do domknięcia, patrz `R-19` w [`rozbieznosci.md`](rozbieznosci.md).

## Krok 2.2. Karta organizacji, typ 1: organizacja pozarządowa

Kolumna "gdzie" mówi, czy pole siedzi na stałe w karcie organizacji, czy wypełnia się je w każdym wniosku od nowa. **Karta organizacji jako osobny rekord nie istnieje dziś w modelu**, patrz `R-01`.

| Pole | Rodzaj | Wymagane | Sprawdzenie | Gdzie |
|---|---|---|---|---|
| Pełna nazwa | tekst | tak | | karta |
| Forma prawna | wybór: stowarzyszenie, fundacja, klub sportowy, koło gospodyń wiejskich, inna | tak | "inna" odsłania pole tekstowe | karta |
| Rejestr | wybór: KRS albo inny | tak | odsłania właściwe pole numeru | karta |
| Numer w rejestrze | tekst | tak | KRS: 10 cyfr | karta |
| NIP | tekst | tak | suma kontrolna | karta |
| REGON | tekst | nie | 9 albo 14 cyfr | karta; **do potwierdzenia**, wzór wniosku REGON-u nie zbiera |
| Adres siedziby | tekst | tak | | karta |
| Adres korespondencyjny | tekst | tylko jeśli inny | odsłaniany zaznaczeniem | karta |
| Telefon | tekst | tak | | karta |
| E-mail | e-mail | tak | format | karta |
| Numer rachunku bankowego | tekst | tak | suma kontrolna | karta |
| Osoby uprawnione do reprezentowania | tabela: imię, nazwisko, funkcja | tak | co najmniej jeden wiersz | karta |
| Średni roczny przychód z trzech ostatnich zamkniętych lat | kwota | tak | porównywany z progiem z kroku 1.4 | wniosek |
| Rodzaj wnioskodawcy | wybór jednej z trzech opcji | tak | decyduje o widoczności pól | wniosek |
| Próg miejscowości siedziby | wybór jednego z sześciu progów | tak | | wniosek |
| Gmina realizacji projektu | tekst, jedna gmina | tak | | wniosek |

Progi miejscowości: wieś, miasto poniżej 5 000, od 5 000 do 9 999, od 10 000 do 19 999, od 20 000 do 49 999, od 50 000 do 99 999 mieszkańców.

## Typ 2: grupa nieformalna z patronem

Dane rejestrowe dotyczą patrona, więc karta jest kartą patrona i wygląda jak wyżej. Dochodzą pola grupy, wypełniane w każdym wniosku, bo grupa nie jest trwałym podmiotem.

| Pole | Rodzaj | Wymagane | Gdzie |
|---|---|---|---|
| Wszystkie pola z typu 1, dotyczące patrona | | | karta patrona |
| Nazwa grupy nieformalnej | tekst | tak | wniosek |
| Członkowie grupy: imię i nazwisko, adres, telefon, e-mail | tabela, dokładnie trzy wiersze; pierwsza osoba oznaczona jako lider | tak | wniosek |
| Krótka charakterystyka grupy | tekst długi | tak | wniosek |

Ta sama fundacja może w jednym konkursie firmować grupę, a w innym startować sama. Dlatego **rodzaj wnioskodawcy jest polem wniosku, nie karty**.

## Typ 3: grupa nieformalna bez patrona

Bez danych rejestrowych, więc bez karty organizacji. Osoba składająca taki wniosek nigdy nie zobaczy pytania o NIP.

| Pole | Rodzaj | Wymagane | Sprawdzenie | Gdzie |
|---|---|---|---|---|
| Nazwa grupy nieformalnej | tekst | tak | | wniosek |
| Członkowie grupy: imię i nazwisko, adres, telefon, e-mail | tabela, dokładnie trzy wiersze, pierwsza osoba lider | tak | | wniosek |
| Krótka charakterystyka grupy | tekst długi | tak | | wniosek |
| Numer rachunku bankowego lidera | tekst | tak | suma kontrolna | wniosek |
| PESEL lidera | tekst | tak | suma kontrolna | **umowa, nie wniosek** |

Dwa ostatnie wiersze zostały dopisane przez nas, bo dotacja musi gdzieś trafić, a umowę trzeba z kimś podpisać. Zakładamy umowę z liderem jako osobą fizyczną. **PESEL zbieramy dopiero przy umowie, nie od wszystkich składających wniosek.**

## Czego w karcie świadomie nie ma

| Zostaje we wniosku | Dlaczego |
|---|---|
| Średni roczny przychód z trzech ostatnich zamkniętych lat | kwota zmienia się co roku i służy do sprawdzenia progu w danym konkursie |
| Rodzaj wnioskodawcy | ta sama fundacja raz startuje sama, raz jako patron grupy |
| Próg miejscowości i gmina realizacji projektu | to dane o projekcie, nie o organizacji |

---

# Wniosek

## Część I, dane wnioskodawcy

| Kiedy | Co widzi wnioskodawca |
|---|---|
| Pierwszy wniosek | pusty formularz z pełną listą pól; po zapisaniu te dane stają się kartą organizacji |
| Każdy następny | blok już wypełniony, z datą ostatniej aktualizacji karty i dwoma przyciskami: "dane są aktualne" oraz "popraw"; poprawka trafia do karty |

Pola wypełniane w każdym wniosku od nowa:

| Pole | Rodzaj | Wymagane | Sprawdzenie |
|---|---|---|---|
| Rodzaj wnioskodawcy | wybór jednej z trzech opcji | tak | decyduje, które pola niżej się odsłonią |
| Średni roczny przychód z trzech ostatnich zamkniętych lat | kwota | tak | długa podpowiedź o sposobie liczenia; porównywane z progiem z kroku 1.4 |
| Siedziba według progu miejscowości | wybór jednego z sześciu progów | tak | |
| Gmina realizacji projektu | tekst, jedna gmina | tak | |

**Kopia danych w złożonym wniosku.** Wniosek złożony zachowuje kopię danych organizacji z chwili złożenia. Karta idzie dalej swoją drogą, więc późniejsza zmiana adresu nie zmienia treści czegoś, co jest już u zamawiającego na biurku.

## Część II, informacje o projekcie

| Nr | Pole | Rodzaj | Sprawdzenie |
|---|---|---|---|
| 1 | Tytuł projektu | tekst | |
| 2 | Czas trwania projektu | okres | początek: "od dnia podpisania umowy", koniec: data |
| 3 | Krótka charakterystyka projektu | tekst długi | maksimum 500 znaków |
| 4 | Cel główny projektu | tekst długi | |
| 5 | Opis pomysłu na projekt | tekst długi | minimum 1000 znaków |
| 6a | Działania merytoryczne | tekst długi | |
| 6b | Rozwój instytucjonalny | tekst długi | sekcja włączana w ustawieniach konkursu, razem z kategorią kosztów B; dla grup nieformalnych domyślnie wyłączona |
| 7 | Promocja projektu | tekst długi | |
| 8a | Opis zakładanych rezultatów | tekst długi | |
| 8b | Tabela rezultatów | tabela, dowolna liczba wierszy | kolumny: rezultat, wartość docelowa, sposób monitorowania i źródło informacji |
| 9 | Liczba uczestników projektu | liczba | we wzorze jest to pierwszy, z góry wpisany wiersz tabeli rezultatów, a nie osobne pole; robimy tak samo |
| 10 | Dostępność dla osób ze szczególnymi potrzebami | trzy pola tekstowe | architektoniczna, cyfrowa, informacyjno-komunikacyjna |

Pole 2 nie jest datą z kalendarza po stronie początku: wzór zapisuje "od dnia podpisania umowy" i robimy dokładnie tak samo.

## Część III, budżet

Trzy tabele kosztów, w każdym wierszu: nazwa (tekst), jednostka miary (tekst), liczba jednostek (liczba), cena jednostkowa (kwota).

**Pola wyliczane, wyszarzone, nie do wpisania:** wartość pozycji, sumy tabel, udział kosztów rozwoju instytucjonalnego, udział kosztów administrowania, udział dotacji w całkowitej wartości projektu.

| Tabela | Nazwa | Limit |
|---|---|---|
| A | Koszty wynikające ze specyfiki projektu, czyli bezpośrednie | bez limitu procentowego |
| B | Koszty związane z rozwojem instytucjonalnym | maksimum 50% kwoty dotacji dla organizacji pozarządowej; kategorię można w konkursie wyłączyć |
| C | Koszty pośrednie | maksimum 10% kwoty dotacji |

Na końcu jedno pole tekstowe: uzasadnienie zaplanowanych kosztów.

**System sprawdza cztery rzeczy naraz:** suma całego budżetu nie przekracza maksymalnej kwoty dotacji; suma tabeli B nie przekracza swojego progu; suma tabeli C nie przekracza swojego progu; w każdym wierszu wartość całkowita zgadza się z iloczynem liczby jednostek i ceny. Komunikat błędu wskazuje **konkretną tabelę i konkretną pozycję**.

**Pytanie otwarte do rozstrzygnięcia z zamawiającym.** Wzór nie zbiera ani wkładu własnego, ani innych źródeł finansowania, więc pole wyliczane "wysokość dotacji w stosunku do całkowitej wartości projektu" zawsze wyjdzie 100%, a we wzorze dla grupy nieformalnej jest wręcz wpisane na sztywno jako 100,00%. Od odpowiedzi zależy, czy w budżecie potrzebna jest czwarta tabela ze źródłami finansowania. Decyzja D11 (dotacja jako pole wyliczane z wkładów) zakłada, że wkłady w ogóle istnieją, więc **te dwie rzeczy trzeba rozstrzygnąć razem**.

Drobiazg z wzoru: dla grupy z patronem i dla grupy nieformalnej trzecia tabela budżetu jest oznaczona tą samą literą "B" co druga. U nas są to A, B i C.

## Część IV, oświadczenia

Liczba i treść zależy od rodzaju wnioskodawcy, więc lista jest warunkowa, tak jak część I. Po każdym oświadczeniu klauzula RODO z datą retencji z kroku 1.4. Treść edytowalna przez zamawiającego, tym samym mechanizmem co formularz.

| Rodzaj wnioskodawcy | Oświadczenia |
|---|---|
| Organizacja pozarządowa: osiem | związanie wnioskiem do dnia podpisania umowy; zgodność informacji ze stanem prawnym i faktycznym; brak skazania prawomocnym wyrokiem; siedziba na terenie województwa opolskiego; realizacja wyłącznie w zakresie działalności pożytku publicznego; zapoznanie się z regulaminem; brak zaległości podatkowych; brak zaległości w składkach na ubezpieczenie społeczne |
| Grupa nieformalna: sześć | pierwsze trzy jak wyżej; wszyscy członkowie grupy są mieszkańcami województwa opolskiego; realizacja wyłącznie w zakresie działalności pożytku publicznego; zapoznanie się z regulaminem. Bez oświadczeń podatkowych i składkowych |
| Grupa z patronem: siedem wspólnych plus trzy patrona | jak przy grupie nieformalnej, plus brak powiązań między członkami grupy a patronem; osobno patron oświadcza o braku zaległości podatkowych, braku zaległości składkowych i o zgodności danych z części I z rejestrem |

## Różnice między trzema wzorami, pole po polu

Wszystkie są tego rodzaju, który obsługuje pole warunkowe. Dlatego budujemy **jeden formularz warunkowy, nie trzy formularze** (decyzja 6 z raportu).

| Miejsce we wzorze | Organizacja pozarządowa | Grupa z patronem | Grupa nieformalna |
|---|---|---|---|
| Cz. I, średni roczny przychód | jest | nie ma | nie ma |
| Cz. II pkt 6b, rozwój instytucjonalny | opis organizacji | opis grupy, zakaz zakupu sprzętu | to samo, ale w tabeli |
| Cz. II pkt 7, promocja | ze wzmianką o oznaczeniu zakupionego wyposażenia | tak samo | bez tej wzmianki |
| Cz. II pkt 8, wiersz tabeli rezultatów | "Liczba uczestników/odbiorców" | "Liczba uczestników" | "Liczba uczestników" |
| Cz. III, tabela B | maksimum 50% kwoty dotacji | maksimum 30% | maksimum 30% |
| Cz. III, załączniki | dwa: rejestr oraz CIT albo sprawozdanie finansowe | jeden: rejestr, dotyczy patrona | sekcji nie ma wcale |
| Cz. IV, oświadczenia | osiem | siedem wspólnych plus trzy patrona | sześć |
| Podpisy | reprezentanci podmiotu | reprezentanci patrona i członkowie grupy | członkowie grupy |

Po naniesieniu komentarzy zamawiającego część tych różnic znika: rozwój instytucjonalny i limit 30% są w obu wzorach dla grup oznaczone do usunięcia.

---

# Sprawozdanie

Wzoru nie mamy (B-04), więc lista jest szczątkowa i to jest stan faktyczny.

| Pole | Uwaga |
|---|---|
| Data złożenia sprawozdania | wersja elektroniczna |
| Koszt całkowity według sprawozdania | ile projekt kosztował w rzeczywistości |
| Koszty ogółem, czyli kwota dotacji wykorzystana | |
| Kwota do zwrotu | niewykorzystana część dotacji, liczona przez system |
| Wykaz braków w sprawozdaniach częściowych | tabela ze spisem sprawozdań częściowych i błędami w każdym |

Do tego wykonanie rzeczowe i finansowe (te same rodzaje pól co we wniosku), wyjaśnienia różnic (tekst długi) i załączniki ustawiane tak samo jak przy wniosku, zwykle skany faktur albo lista uczestników. Zasada "było i jest": [`M7-wdrozenie.md`](M7-wdrozenie.md).

---

# Znaczniki wzorów dokumentów

Spis znaczników, które mają pokrycie we wzorze wniosku na 2026. **To nie jest opis umowy zamawiającego**, bo wzoru umowy nie mamy (B-03). To są dane, które da się wstawić.

| Grupa | Pola |
|---|---|
| Strony umowy | nazwa zleceniobiorcy, NIP, adres, osoby upoważnione do reprezentowania |
| Rachunek | numer konta |
| Sama umowa | numer w formacie 1/2026, data podpisania w formacie słownym, wartość dofinansowania |
| Kwoty z wniosku | całkowita wartość zadania (suma budżetu), kwota wnioskowana, kwota przyznana i ta sama kwota słownie |
| Tabele z wniosku | kosztorys (tabele A, B i C), tabela rezultatów |
| Terminy | koniec realizacji zadania z wniosku, terminy z konkursu |
| Dane urzędowe | organ wydający zarządzenia, numer i data zarządzenia o ogłoszeniu konkursu i o powołaniu komisji, numer, rok i data uchwały o programie współpracy, paragraf i klasyfikacja budżetowa |
| Wynik oceny | status oceny formalnej, wynik punktowy, suma kontrolna wersji wniosku |
| Transze | data wypłaty, kwota, kwota słownie, numer konta |

Znaczniki, które istnieją w obecnym narzędziu, ale **nie mają czego zaciągnąć** ze wzoru na 2026: wkład własny finansowy i osobowy, całkowity wkład własny, źródła finansowania, harmonogram działań jako tabela, kosztorys w podziale na części I, II i III z ustawowego wzoru, REGON i nazwa banku. Dopiero wzór umowy powie, czy któregoś z nich potrzeba, i wtedy wraca ono **razem z polem we wniosku**, bo bez pola znacznik nie ma czego wstawić.
