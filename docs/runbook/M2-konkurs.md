# M2 · Konkurs i publikacja

Ogłoszenie naboru i publiczna strona konkursu. Pierwszy kamień, który zamawiający zobaczy jako działający produkt, i pierwszy, w którym raport `RAPORT-proces-i-pola.docx` dokłada konkretne pola, których na Trello nie ma.

Pełny spis pól kreatora: [`pola.md`](pola.md), sekcja "Ogłoszenie konkursu". Przebieg kroków: [`proces.md`](proces.md), ścieżka 1.

---

## T-20 [P0 / Backend] Konkurs: tworzenie, statusy i publikacja

Karta: <https://trello.com/c/dcj9E3qW>

**Kontekst.** Pierwszy etap cyklu życia konkursu: OCWIP ogłasza nabór. Bez tego nie ma czego składać ani czego oceniać, więc M2 startuje zaraz po fundamencie z M1.

**Zakres.** Operacje na konkursie po stronie API: utworzenie, edycja, publikacja, zamknięcie, oznaczenie jako nieaktywny. Statusy: szkic, opublikowany, zamknięty, nieaktywny. Parametry konkursu: terminy startu i zamknięcia w UTC, maksymalna kwota dotacji, wymagane załączniki, wskazanie wersji definicji formularza.

**Retencja.** Nie ma twardego kasowania. Usunięcie konkursu to oznaczenie go jako nieaktywnego, bo dokumentację trzymamy minimum 5 lat.

**Czego nie robimy tutaj.** Interfejsu (T-22, T-23) ani logiki odcięcia terminu (T-21). Nie definiujemy zawartości definicji formularza, to M3.

**Zależności.** Blokuje nas: T-11.3, T-13.2, T-17. Blokujemy: T-21, T-22, T-23 i cały M4.

**Kryteria akceptacji (checklista z karty):**

- [ ] Operator tworzy, edytuje i publikuje konkurs przez API
- [ ] Statusy zmieniają się wyłącznie po dozwolonych przejściach, reszta odrzucona
- [ ] Konkurs nieaktywny zostaje w bazie, znika tylko z widoku publicznego
- [ ] Test negatywny: wnioskodawca i recenzent dostają 403 przy próbie publikacji
- [ ] Terminy zapisane w UTC, potwierdzone testem

**Co już jest w schemacie.** Tabela `competitions` istnieje od T-11.3: okno konkursu w pełnych minutach (settery ucinają sekundy, dwa check constrainty pilnują tego samego w bazie), `start_date < end_date`, `max_grant_amount > 0`, status jako tekst (nie ordynał enuma), indeks na `(status, end_date)` pod publiczną listę. Wymagane załączniki jeszcze nie istnieją jako kolumna.

**Uzupełnienie z raportu, ważne.** Raport podaje **siedmiostopniowy ciąg stanów**, a nie czterostopniowy z karty:

```
roboczy -> opublikowany -> trwa nabór -> nabór zamknięty -> trwa ocena -> rozstrzygnięty -> archiwalny
```

Kto je przestawia: nikt. Przejścia dzieją się same, z terminów z kroku 1.1, poza rozstrzygnięciem i archiwizacją. Konsekwencje, które trzeba wpisać już tutaj, bo później są migracją:

- **roboczy nie ma publicznego adresu.** Nie chodzi o ukrycie odnośnika, tylko o to, że taki adres nie istnieje, dopóki konkurs nie zostanie opublikowany.
- Od stanu **opublikowany** konkurs jest widoczny dla gościa, ale przycisk "Wypełnij wniosek" działa dopiero od stanu **trwa nabór**. To są dwa różne momenty, bo data publikacji jest osobnym polem od początku naboru.
- Edycja konkursu, do którego wpłynęły już wnioski, pokazuje ostrzeżenie. Formularz jest wersjonowany, więc wnioski w toku się nie psują, ale zmiana limitów kwotowych wpływa na już wpisane budżety.

Rozjazd czterech statusów z karty wobec siedmiu z raportu jest pozycją `R-17` w [`rozbieznosci.md`](rozbieznosci.md). Buduj przejścia jako tabelę dozwolonych par w jednym miejscu, nie jako `switch` rozsypany po serwisie, bo dołożenie trzech stanów ma być wtedy dopisaniem wierszy.

**Parametry konkursu, których karta nie wymienia, a raport tak** (pełna lista w [`pola.md`](pola.md)): numer konkursu, data publikacji, nabór ciągły (wyłącza datę zakończenia), pula konkursu, minimalna dotacja, maksymalny procent kosztów pośrednich, maksymalny procent kosztów rozwoju instytucjonalnego, próg średniego rocznego przychodu, data usunięcia danych osobowych, podstawa liczenia procentu (kwota dotacji albo całkowita wartość projektu), lista kategorii kosztów, wymóg wersji papierowej wraz z terminem i adresem, osoby kontaktowe, treść e-maila po złożeniu, treść informacji na ekranie po złożeniu.

Dwie z nich mają twarde reguły: **data usunięcia danych osobowych nie może być wcześniejsza niż pięć lat od zamknięcia naboru**, a **kategorie kosztów są ustawieniem konkursu, nie stałą w systemie** (wyłączenie kategorii chowa zarówno jej tabelę w budżecie, jak i odpowiadającą jej sekcję opisową w części o projekcie).

---

## T-21 [P0 / Backend] Twarde odcięcie terminu naboru

Karta: <https://trello.com/c/jU68qLLX>

**Kontekst.** Postawione jednoznacznie: kto wejdzie o 12:05 przy zamknięciu o 12:00, ten już nie złoży wniosku. To reguła twarda, opisana w decyzji D7.

**Zakres.** Jedno miejsce w kodzie, które odpowiada na pytanie "czy ten konkurs przyjmuje jeszcze wnioski". Wszystkie ścieżki (składanie, edycja wersji roboczej, upload załącznika) pytają je, a nie liczą dat samodzielnie.

**Czas.** Trzymamy w UTC, konwersja na czas lokalny wyłącznie na brzegach. Zmiana czasu w październiku trafia w środek sezonu konkursowego, więc test na przejściu czasu letniego na zimowy jest obowiązkowy.

**Dlaczego to osobna karta.** Bo warunek rozsypany po trzech endpointach rozjedzie się przy pierwszej zmianie. Jedna reguła, jedno miejsce, komplet testów granicznych: minuta przed, dokładnie w minucie zamknięcia, minuta po.

**Czego nie robimy tutaj.** Komunikatu w interfejsie. Front pokazuje to, co zwróci ta reguła.

**Zależności.** Blokuje nas: T-20. Blokujemy: T-33, T-29.

**Kryteria akceptacji (checklista z karty):**

- [ ] Jedna reguła w kodzie odpowiada, czy konkurs przyjmuje jeszcze wnioski
- [ ] Testy graniczne: minuta przed, dokładnie w minucie zamknięcia, minuta po
- [ ] Test na przejściu czasu letniego na zimowy przechodzi
- [ ] Złożenie, autozapis i upload załącznika pytają tę regułę, a nie liczą dat same
- [ ] Próba po terminie zwraca jednoznaczny błąd, nie 500 i nie pustą odpowiedź

**Uzupełnienie z raportu.** Nabór ciągły (pole z kroku 1.1) wyłącza datę zakończenia, więc reguła musi mieć jawną gałąź dla konkursu bez terminu, a nie traktować pustą datę jak przeszłość. To jest dokładnie ten przypadek, w którym warunek rozsypany po endpointach daje "nabór zamknięty" na konkursie, który nigdy się nie zamyka.

**Uwaga o D12.** Decyzja D12 mówi, że komunikat walidacji podaje wyliczoną wartość graniczną, nie samą regułę. Przy odcięciu terminu znaczy to: komunikat podaje **datę i godzinę zamknięcia w czasie lokalnym wnioskodawcy**, a nie zdanie "nabór zamknięty".

---

## T-23 [P0 / Frontend] Publiczna lista konkursów i strona konkursu

Karta: <https://trello.com/c/7PRbbgV5>

**Kontekst.** To jest powód, dla którego ten projekt w ogóle istnieje. W obecnym narzędziu wnioskodawca po zalogowaniu widzi konkursy z całej Polski, w większości takie, w których nie może startować. U nas widzi wyłącznie konkursy OCWIP. Decyzja D6.

**Zakres.** Publiczna, dostępna bez logowania lista ogłoszonych konkursów oraz strona pojedynczego konkursu: opis, terminy, limit kwoty, wymagane załączniki, przycisk prowadzący do złożenia wniosku.

**Dostępność i responsywność.** Ta strona ma największy ruch z zewnątrz i trafi na nią telefon grupy nieformalnej bez firmowego sprzętu. WCAG AA i działanie na telefonie sprawdzamy tutaj, a nie na końcu projektu.

**Widoczny licznik do zamknięcia.** Skoro odcięcie tnie co do minuty, wnioskodawca musi widzieć bez liczenia w głowie, ile czasu zostało i w jakiej strefie czasowej.

**Czego nie robimy tutaj.** Wypełniania wniosku, to M4.

**Zależności.** Blokuje nas: T-20, T-15.1. Blokujemy: T-34.

**Kryteria akceptacji.** Checklista na karcie jest pusta. Do odhaczenia bierzesz tę listę, wyprowadzoną z opisu karty i z raportu, i **wpisujesz ją na kartę Trello**, zanim zaczniesz:

- [ ] Lista opublikowanych konkursów widoczna bez konta
- [ ] Strona pojedynczego konkursu widoczna bez konta: opis, cel, terminy, pula, limit kwoty, wymagane załączniki, osoby kontaktowe
- [ ] Wzory załączników do pobrania bez logowania
- [ ] Stały odnośnik do konkursu, do skopiowania i wklejenia w mediach społecznościowych
- [ ] Znaczniki Open Graph: tytuł konkursu, kwota i termin widoczne w podglądzie wklejonego odnośnika
- [ ] Licznik czasu do zamknięcia naboru z jawną strefą czasową, bez sekundnika
- [ ] Konkurs w stanie roboczym nie ma publicznego adresu, a wejście na zgadnięty adres daje 404
- [ ] Przycisk "Wypełnij wniosek" prowadzi do rejestracji albo logowania i wraca na ten sam konkurs
- [ ] WCAG AA na tej stronie: kontrast, focus, nawigacja klawiaturą, struktura nagłówków
- [ ] Działa na telefonie

**Uzupełnienie z raportu, dwie rzeczy do zrobienia i dwie do niezrobienia.**

Do zrobienia: **archiwum wyników** dostępne publicznie (nazwa organizacji, tytuł projektu, kwota) jako wizytówka i jako to, czego zwykle wymaga przejrzystość wydatkowania środków publicznych. Nie ma karty na Trello, pozycja `R-14`. Przy grupach nieformalnych publikujemy nazwę grupy, **bez imion i nazwisk jej członków**.

Do niezrobienia, świadomie: **nie pokazujemy liczby wniosków złożonych w trwającym naborze**. Technicznie łatwe, w skutkach złe: informacja "złożono już 40 wniosków" przy 20 dotacjach zniechęca właśnie te organizacje, które wahają się najbardziej. Po zamknięciu naboru pokazać to można. To decyzja 2 z raportu.

---

## T-22 [P0 / Frontend] Kreator ogłoszenia konkursu (operator)

Karta: <https://trello.com/c/3S8truZC>

**Kolejność.** Karta stoi w kolejce **za M3**, choć jej sekcja ZALEŻNOŚCI wymienia tylko T-20, T-15.3 i T-15.1. Powód jest w karcie T-27: kreator ogłoszenia wybiera opublikowaną wersję formularza, a ta powstaje dopiero tam. Wzięcie T-22 wcześniej znaczy zbudowanie listy wyboru, która nie ma z czego wybierać.

**Kontekst.** Ekran, na którym pracownik OCWIP ogłasza nabór. Osoba, która będzie go używać, mówi o sobie, że nie zna się na technikaliach, więc prostota jest tu wymaganiem funkcjonalnym, a nie kwestią gustu. To jest też pierwszy ekran, który realnie pokażemy jej na demie.

**Zakres.** Formularz konkursu w panelu operatora: nazwa, opis, terminy, limit kwoty, wymagane załączniki, wybór formularza wniosku. Podgląd przed publikacją oraz jawne, świadome potwierdzenie publikacji.

**Dlaczego publikacja wymaga potwierdzenia.** Opublikowany konkurs jest widoczny publicznie i zaczyna przyjmować wnioski. Cofnięcie tego jest kosztowne wizerunkowo wobec organizacji, które już zaczęły wypełniać.

**Terminy w UI.** Operator wpisuje czas lokalny, ekran pokazuje wprost, co to znaczy w praktyce: "nabór zamyka się 12 września o 12:00, wnioski złożone później nie wejdą".

**Czego nie robimy tutaj.** Kreatora formularzy, to M3. Tutaj wybieramy gotowy formularz z listy.

**Zależności.** Blokuje nas: T-20, T-15.3, T-15.1, T-27. Blokujemy: demo dla zamawiającego.

**Kryteria akceptacji (checklista z karty):**

- [ ] Operator przechodzi cały formularz i publikuje konkurs bez pomocy programisty
- [ ] Podgląd przed publikacją pokazuje dokładnie to, co zobaczy wnioskodawca
- [ ] Publikacja wymaga świadomego potwierdzenia, nie dzieje się jednym kliknięciem
- [ ] Ekran mówi wprost, co oznacza wpisany termin zamknięcia

**Uzupełnienie z raportu, i jest go tu najwięcej w całym projekcie.** Kreator ma **siedem kroków w tej samej kolejności i pod tymi samymi nazwami, co dzisiaj**, plus krok 0 przed nimi. Zmieniamy to, co dzieje się w środku kroków, nie ich układ:

| Krok | Co się w nim ustawia |
|---|---|
| 0 | Pusty formularz albo **kopia konkursu z poprzedniego roku** (przenosi ustawienia, formularz wniosku, karty oceny i wzory dokumentów) |
| 1.1 Dane konkursu | numer, tytuł, terminy publikacji i naboru, nabór ciągły, informacja po złożeniu |
| 1.2 Opis konkursu | treść ogłoszenia, cel, zakładane rezultaty, odnośnik do regulaminu |
| 1.3 Forma dostarczenia | czy poza wersją elektroniczną wymagany jest papier |
| 1.4 Limity | pieniądze, procenty, ramy czasowe projektu, data usunięcia danych |
| 1.5 Załączniki do oferty | jakie pliki dołącza wnioskodawca i kiedy są wymagane |
| 1.6 Osoby kontaktowe | kto odpowiada na pytania, treść e-maila po złożeniu |
| 1.7 Podsumowanie | wszystko na jednym ekranie przed publikacją, z "popraw" przy każdej sekcji |

Trzy reguły zachowania, które trzeba wpisać w ten ekran:

1. **Walidacja nie blokuje przechodzenia między krokami.** Konkurs układa się w kilku podejściach, więc niekompletny krok tylko dostaje oznaczenie. Kompletność sprawdzamy dopiero przy publikacji.
2. **Krok 1.4 jest najważniejszy**, bo z niego wynikają wszystkie blokady w budżecie wniosku. Parametr konkursu jest jedynym źródłem prawdy, a formularz go czyta i liczy na bieżąco. Dziś jest odwrotnie: blokady wpisuje ręcznie programista po stronie dostawcy narzędzia.
3. **Pod każdym polem kwotowym dopisujemy kwotę słownie**, bo wchodzi potem do umowy.

Pełne tabele pól dla każdego z siedmiu kroków, z rodzajem pola i wymagalnością, są w [`pola.md`](pola.md). Nie przepisuj ich tutaj, bo powstaną dwie listy, które się rozjadą.

Kopia konkursu z poprzedniego roku (krok 0) **nie ma karty na Trello**, a jest podstawowym sposobem pracy zakładanym przez raport, patrz `R-11` w [`rozbieznosci.md`](rozbieznosci.md). Bez niej operator przy drugim naborze przepisuje wszystko ręcznie.
