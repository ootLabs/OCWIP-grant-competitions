# M3 · Kreator formularzy

Najtrudniejszy technicznie kamień i jednocześnie główny argument sprzedażowy całego systemu: OCWIP układa formularze bez programisty (decyzja D2). Kolejność jest wymuszona: najpierw kontrakt, potem obie strony tego kontraktu, na końcu publikacja wersji.

**Reguła, od której nie ma odstępstwa:** struktura formularza wniosku, karty oceny i sprawozdania jest **danymi w bazie, nie kodem**, od pierwszego dnia prac. Ekran do zarządzania tymi danymi dochodzi później, ale sama możliwość jest wbudowana od początku. Gdyby formularz był zapisany w kodzie, późniejsze dorobienie kreatora znaczyłoby przepisanie połowy systemu.

Rodzaje pól, mechanizmy i pełny spis pól realnego wniosku: [`pola.md`](pola.md).

---

## T-24 [P0 / Backend] Kontrakt JSON definicji formularza

Karta: <https://trello.com/c/gcslfR97>

**Kontekst.** Najtrudniejszy technicznie element całego projektu i jednocześnie wymóg twardy: OCWIP musi móc samodzielnie układać formularze, bez programisty (decyzja D2). Struktura formularza jest DANYMI w bazie, nie klasami w kodzie. Karta T-11.3 świadomie zostawiła zawartość tej kolumny nieokreśloną, tutaj ją definiujemy.

**Zakres.** Schemat dokumentu JSONB: sekcje, pola, typy pól, walidacje, pola zależne. Walidacja samego schematu, żeby do bazy nie trafiła definicja, której renderer nie umie wyświetlić.

**Minimalny zestaw typów pól z karty.** Tekst krótki i długi, liczba, data, lista wyboru, wielokrotny wybór, załącznik oraz tabela budżetu. Tabela budżetu jest najtrudniejsza i decyduje o kształcie całego schematu, więc projektujemy schemat pod nią, a nie doklejamy ją później.

**Dlaczego to osobna karta przed kreatorem.** Kreator (T-26) i renderer (T-28) to dwie strony tego samego kontraktu. Jeśli powstaną równolegle bez spisanego schematu, rozjadą się w tydzień.

**Zależności.** Blokuje nas: T-11.3. Blokujemy: T-25, T-26, T-28, T-30.

**Kryteria akceptacji.** Checklista na karcie jest pusta. Bierzesz tę listę i wpisujesz ją na kartę:

- [ ] Schemat opisany w `docs/` jako dokument, nie tylko jako kod: sekcje, pola, walidacje, pola zależne
- [ ] Wszystkie piętnaście rodzajów pól z [`pola.md`](pola.md) ma reprezentację w schemacie
- [ ] Każde pole niesie co najmniej: nazwę, podpowiedź, rodzaj, wymagalność, limit znaków, warunek widoczności, flagę wydruku
- [ ] Walidacja schematu odrzuca definicję, której renderer nie umie wyświetlić, z komunikatem wskazującym pole
- [ ] Tabela budżetu przechodzi przez schemat bez wyjątków szytych pod nią
- [ ] Test: definicja z nieznanym typem pola jest odrzucana
- [ ] Test: definicja z polem wyliczanym bez źródła obliczenia jest odrzucana
- [ ] Korzeń dokumentu jest obiektem albo tablicą, zgodnie z check constraintem, który już jest w bazie

**Co już jest w bazie.** Kolumna `form_definitions.definition` jako JSONB z check constraintem pilnującym, że korzeń jest obiektem albo tablicą, oraz numer wersji unikalny w obrębie konkursu i wymagany dodatni. JSON siedzi jako `JsonElement`, nie `JsonDocument`. Którą z dwóch postaci korzenia wybiera kontrakt, rozstrzyga ta karta.

**Uzupełnienie z raportu, i jest kluczowe.** Raport wyprowadził pełną listę rodzajów pól z trzech realnych wzorów wniosku na 2026. Karta wymienia osiem, raport piętnaście, a różnica nie jest kosmetyczna:

tekst krótki, tekst długi z limitem znaków, liczba, **kwota**, **procent**, data, **data i godzina**, tak albo nie, wybór jednej opcji, wybór wielu opcji, **tabela o zmiennej liczbie wierszy**, **tabela o stałej liczbie wierszy**, plik, **oświadczenie**, **pole wyliczane**.

Cztery mechanizmy, bez których tego formularza nie da się zbudować, wszystkie występujące w realnych wzorach:

1. **Pole warunkowe.** Wybór "inna forma prawna" odsłania pole tekstowe. Wybór rejestru innego niż KRS odsłania inne pole numeru.
2. **Tabela, do której wnioskodawca dodaje wiersze**, z usuwaniem i zmianą kolejności.
3. **Pole wyliczane.** Wartość pozycji budżetu to liczba jednostek razy cena. Procent kosztów pośrednich to suma tabeli C podzielona przez dotację. Człowiek tego nie wpisuje.
4. **Powiązanie między sekcjami.** Pozycje budżetu odnoszą się do działań opisanych wcześniej, a zmiana w jednym miejscu przelicza drugie.

Trzy decyzje, które muszą wejść do schematu **teraz**, bo dorobienie ich później oznacza przepisanie wszystkich reguł:

- **D14: każde pole ma jawną właściwość, czy trafia na wydruk oferty.** Pola techniczne istnieją tylko po to, żeby coś policzyć albo spiąć dwie sekcje: są widoczne w interfejsie, ale nie na wydruku. Bez tej flagi wydruk będzie albo uboższy od formularza, albo zaśmiecony wierszami pomocniczymi.
- **D12: silnik walidacji musi umieć ODWRÓCIĆ regułę, nie tylko ją sprawdzić.** Każda reguła limitu dostarcza nie tylko odpowiedź prawda albo fałsz, ale i wartość graniczną dla bieżącego stanu wniosku, bo komunikat ma powiedzieć "możesz wpisać jeszcze 900 zł", a nie "maksymalnie 10%". To zmienia kontrakt walidatora i musi w nim być od początku.
- **D11: kwota dotacji jest polem wyliczanym**, nie wpisywanym. Wnioskodawca deklaruje wkłady własne przy pozycjach kosztowych, a dotacja wychodzi jako różnica. Kolumna dotacji jest tylko do odczytu.

Jedna decyzja, której ta karta **nie** podejmuje: w jakiej formie OCWIP będzie edytować formularz (wypełnianie parametrów, kopia z poprawkami, pełny edytor). Raport stawia na kopię z poprawkami i zadaje to pytanie zamawiającemu. Odpowiedź zmienia kształt T-26, nie T-24: schemat ma być na tyle ogólny, żeby obsłużyć każdy z trzech wariantów.

---

## T-25 [P0 / Backend] Wersjonowanie definicji formularza

Karta: <https://trello.com/c/bl59xg1v>

**Kontekst.** Operator może edytować formularz w trakcie życia konkursu. Bez wersjonowania każda taka edycja psuje wnioski już wypełnione według starej struktury, bo ich odpowiedzi przestają pasować do nowych pól.

**Zakres.** Numer wersji na definicji formularza, publikowanie nowej wersji zamiast nadpisywania starej, oraz reguła: wniosek wskazuje na konkretną WERSJĘ definicji, nie na konkurs.

**Dlaczego nie nadpisujemy.** Retencja 5 lat oznacza, że za trzy lata ktoś może chcieć zobaczyć wniosek dokładnie tak, jak został złożony. Bez zachowanej wersji formularza nie da się go poprawnie odtworzyć.

**Co z wnioskami w trakcie wypełniania.** Wersja robocza zostaje przy swojej wersji formularza. Podnoszenie jej do nowej wersji w locie oznaczałoby, że wnioskodawcom nagle znikają wypełnione pola.

**Zależności.** Blokuje nas: T-24. Blokujemy: T-27, T-29.

**Kryteria akceptacji.** Checklista pusta, wpisujesz na kartę tę:

- [ ] Publikacja nowej wersji tworzy nowy wiersz, nie nadpisuje poprzedniego
- [ ] Wniosek wskazuje na wersję definicji, a nie na konkurs, i złożony wniosek renderuje się według swojej wersji
- [ ] Wersja robocza zostaje przy swojej wersji formularza także po opublikowaniu nowej
- [ ] Numer wersji unikalny w obrębie konkursu, potwierdzone testem
- [ ] Test: zmiana formularza w trakcie naboru nie psuje wniosku wypełnianego wcześniej

**Co już jest w bazie.** Unikalność `(competition_id, version)`, wymóg dodatniego numeru, złożony klucz obcy z `applications` na klucz alternatywny `(competition_id, id)`, żeby para konkurs plus wersja formularza nie mogła się rozjechać. Uwaga z T-11.4: ten klucz broni pary w bazie, ale **nie przy zapisie przez nawigacje EF**, bo EF wyrównuje `CompetitionId` do konkursu definicji zamiast odrzucić rozjazd. Sprawdzenie pary należy do brzegu API w T-29 i T-33.

**Uzupełnienie z raportu.** Wersjonowanie ma jeszcze jeden powód, o którym karta nie mówi, a który jest argumentem prawnym: **równe traktowanie wnioskodawców**. Zmiana warunków po ogłoszeniu może być podważona. Wersjonowanie formularza załatwia literówki i drobne poprawki, ale zmiana limitów albo terminu to już zasada organizacyjna, nie techniczna: zmiana istotna wymaga ponownego ogłoszenia i zwykle wydłużenia naboru. Ta druga część nie jest funkcją systemu i nie próbuj jej wbudować.

---

## T-26 [P0 / Frontend] Kreator formularzy: sekcje, pola, walidacje

Karta: <https://trello.com/c/Xw5EirNk>

**Kontekst.** Narzędzie, w którym OCWIP samodzielnie układa formularz wniosku. Dziś wysyłają plik Worda do firmy zewnętrznej i czekają. Ta karta likwiduje tę zależność, co jest głównym argumentem sprzedażowym całego systemu.

**Zakres.** Interfejs budowania formularza: dodawanie sekcji, dodawanie pól, ustawianie etykiet i pomocy kontekstowej, oznaczanie pól wymaganych, konfiguracja walidacji, zmiana kolejności.

**Dla kogo to projektujemy.** Dla osoby, która sama mówi, że nie zna się na technikaliach. Żadnego żargonu w etykietach, żadnego wpisywania JSON-a ręcznie, żadnych regexów wystawionych użytkownikowi. Jeśli operator musi zrozumieć strukturę danych, żeby dodać pole, ta karta jest niezrobiona.

**Tabela budżetu.** Najtrudniejszy typ pola. Operator ustawia kolumny i to, która kolumna sumuje się do kwoty dotacji pilnowanej limitem.

**Czego nie robimy tutaj.** Renderowania formularza dla wnioskodawcy, to T-28. Podglądu i publikacji wersji, to T-27.

**Zależności.** Blokuje nas: T-24, T-15.3. Blokujemy: T-27.

**Kryteria akceptacji.** Checklista pusta, wpisujesz na kartę tę:

- [ ] Operator dodaje sekcję i pole bez wpisywania czegokolwiek technicznego
- [ ] Wszystkie rodzaje pól ze schematu da się dodać z interfejsu
- [ ] Zmiana kolejności sekcji i pól działa i jest odwracalna przed zapisem
- [ ] Warunek widoczności pola ustawia się przez wybór pola i wartości, nie przez wpisanie wyrażenia
- [ ] Pole wyliczane ustawia się przez wskazanie składników, nie przez wpisanie wzoru tekstem
- [ ] Tabela budżetu: operator ustawia kolumny i wskazuje, która sumuje się do kwoty pilnowanej limitem
- [ ] Zero żargonu w etykietach, zero JSON-a, zero regexów wystawionych użytkownikowi
- [ ] Praca kreatora zapisuje się jako szkic i przeżywa zamknięcie przeglądarki

**Zanim zaczniesz, przeczytaj cztery pytania z raportu.** Raport nie wie, jak duży edytor jest realnie potrzebny, i zadaje zamawiającemu cztery pytania: co realnie zmienia się między konkursami, jak duża jest zmiana, czy kolejność tego, co OCWIP chce zmieniać sam, jest dobrze odczytana, i w jakiej formie edycja jest dla nich naturalna. Trzy warianty, od najprostszego: wypełnianie pól ustawień, kopia z poprawkami, pełny edytor. **Typ raportu to wariant drugi.**

Jeśli odpowiedzi jeszcze nie ma, gdy bierzesz tę kartę, buduj **wariant drugi plus minimum wariantu trzeciego**: kopiowanie konkursu z formularzem, edycja etykiet, podpowiedzi, limitów znaków i wymagalności, oraz dodawanie i usuwanie pól w istniejących sekcjach. Przestawianie sekcji i budowanie formularza od zera zostaw na później i zapisz to jako świadome zawężenie w `docs/log.md`. Pełny edytor zbudowany pod zły sposób pracy jest gorszy od jego braku, bo zajmuje miejsce i nikt go nie używa.

---

## T-28 [P0 / Frontend] Renderer formularza z definicji JSON

Karta: <https://trello.com/c/EJZABgdX>

**Kontekst.** Druga strona kontraktu z T-24. Komponent, który dostaje definicję formularza jako dane i buduje z niej działający formularz. Bez niego kreator produkuje strukturę, której nikt nie umie wyświetlić.

**Zakres.** Renderowanie sekcji i wszystkich typów pól ze schematu, obsługa pól zależnych, prezentacja błędów walidacji przy konkretnych polach, nawigacja między stronami formularza.

**Dlaczego nawigacja między stronami.** Wniosek ma 5 do 6 stron. Jedna nieskończona strona oznacza, że wnioskodawca gubi się w tym, ile jeszcze przed nim i co zostało do uzupełnienia.

**Tabela budżetu.** Najtrudniejszy komponent całego frontu: dodawanie i usuwanie wierszy, sumy, walidacja limitu kwoty. Warto zrobić ją pierwszą, bo to ona zweryfikuje, czy schemat z T-24 jest dobry.

**Dostępność.** Każde pole ma powiązaną etykietę, błędy są odczytywalne przez czytnik ekranu, formularz działa z klawiatury. Tutaj to nie kosmetyka, bo to jest główny ekran całego produktu.

**Zależności.** Blokuje nas: T-24, T-15.2. Blokujemy: T-27, T-34.

**Kryteria akceptacji.** Checklista pusta, wpisujesz na kartę tę:

- [ ] Każdy rodzaj pola ze schematu renderuje się i da się wypełnić
- [ ] Pole warunkowe odsłania się i chowa bez przeładowania i bez gubienia wpisanej wartości
- [ ] Pole wyliczane jest wyszarzone i przelicza się na bieżąco
- [ ] Tabela o zmiennej liczbie wierszy: dodawanie, usuwanie, zmiana kolejności
- [ ] Tabela o stałej liczbie wierszy nie pozwala dodać ani usunąć wiersza
- [ ] Błąd stoi przy konkretnym polu i znika, gdy zniknie przyczyna
- [ ] Pole z limitem znaków pokazuje licznik, na przykład 176 z 500
- [ ] Nawigacja między sekcjami pokazuje stan każdej: gotowa, w toku, są błędy
- [ ] Każde pole ma widoczną etykietę, nie tylko podpowiedź w środku pola
- [ ] Błędy odczytywalne przez czytnik ekranu, cały formularz przechodzi się klawiaturą

**Uzupełnienie z raportu: dziewięć zasad, według których to projektujemy.** Każda zamyka jeden konkretny sposób, w który wnioskodawca dziś się gubi. Te zasady rządzą tym komponentem i kartą T-34, i są w [`proces.md`](proces.md) w całości. Trzy najważniejsze dla renderera:

- **Liczby liczy komputer.** Nigdy nie prosimy o liczbę, którą system może policzyć: sumy, procenty, wartość pozycji budżetu, udział kosztów pośrednich w dotacji.
- **Jeden rodzaj komunikatu.** Podpowiedź stoi pod polem zawsze i cicho. Błąd pojawia się tylko wtedy, gdy naprawdę jest. Żadnych ostrzeżeń włączonych na stałe.
- **Jedna kolumna, jedna rzecz naraz.** Jedna sekcja na ekran, czytana od góry do dołu. Bez ośmiu zakładek obok siebie.

**Wskazówki przeliczone z limitów.** Na wejściu do budżetu renderer pokazuje nie regułę, tylko kwotę: "możesz wnioskować o maksymalnie 9 000 zł; koszty pośrednie najwyżej 10% dotacji, czyli 900 zł; rozwój instytucjonalny najwyżej 50%, czyli 4 500 zł". Te liczby są w parametrach konkursu z kroku 1.4 i to jest praktyczne zastosowanie decyzji D12.

---

## T-27 [P0 / Frontend] Podgląd formularza i publikacja wersji

Karta: <https://trello.com/c/fYqlfSoR>

**Kontekst.** Operator układa formularz w kreatorze, ale wnioskodawca widzi coś innego: gotowy formularz do wypełnienia. Bez podglądu operator publikuje w ciemno i dowiaduje się o błędzie od organizacji, która już zaczęła wypełniać.

**Zakres.** Podgląd formularza dokładnie w takiej formie, w jakiej zobaczy go wnioskodawca, oraz publikacja wersji definicji.

**Dlaczego podgląd musi używać tego samego renderera.** Osobny komponent podglądu rozjedzie się z prawdziwym formularzem i podgląd zacznie kłamać. Podgląd woła renderer z T-28 na tych samych danych.

**Publikacja.** Jawna, świadoma akcja z informacją, co się stanie z konkursami korzystającymi z poprzedniej wersji.

**Zależności.** Blokuje nas: T-25, T-26, T-28. Blokujemy: T-22.

**Kryteria akceptacji.** Checklista pusta, wpisujesz na kartę tę:

- [ ] Podgląd woła ten sam komponent co formularz wnioskodawcy, nie własną kopię
- [ ] Podgląd da się przejść do końca bez zapisywania czegokolwiek jako wniosek
- [ ] Publikacja jest jawną akcją z potwierdzeniem i mówi, co stanie się z poprzednią wersją
- [ ] Publikacja nie zmienia wersji formularza we wnioskach już rozpoczętych
- [ ] Opublikowana wersja jest do wyboru w kreatorze ogłoszenia konkursu (T-22)
- [ ] Test: publikacja wersji 2 w trakcie naboru zostawia wersję roboczą wniosku na wersji 1
