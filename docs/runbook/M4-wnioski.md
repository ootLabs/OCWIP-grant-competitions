# M4 · Składanie wniosków

Rdzeń produktu. Osiem kart, przez które przechodzi najwięcej ludzi: pracownik OCWIP nauczy się każdego narzędzia, bo pracuje w nim codziennie, a prezes małego stowarzyszenia siada do wniosku raz i wszystko, czego nie zrozumie od razu, kończy się telefonem do OCWIP albo porzuconym wnioskiem.

Kamień domyka `T-36`, czyli testy izolacji danych, bez których aplikacja nie wychodzi poza lokalną maszynę.

Przebieg ścieżki i dziewięć zasad projektowych: [`proces.md`](proces.md), ścieżka 3. Pola wniosku część po części: [`pola.md`](pola.md).

---

## T-29 [P0 / Backend] Wersja robocza wniosku i autozapis

Karta: <https://trello.com/c/zw48liiX>

**Kontekst.** Reguła twarda: wersje robocze i autozapis to wymóg, nie udogodnienie. Wniosek ma 5 do 6 stron i nikt nie wypełnia go za jednym posiedzeniem. Utrata pracy na takim formularzu oznacza, że organizacja nie złoży wniosku w ogóle.

**Zakres.** Zapis i odczyt wersji roboczej, autozapis w tle, informacja o czasie ostatniego zapisu.

**Dlaczego wnioskodawca musi widzieć, że zapisano.** Autozapis, którego nie widać, nie buduje zaufania. Ludzie i tak będą kopiować treść do Worda na wszelki wypadek, jeśli nie zobaczą potwierdzenia.

**Odcięcie terminu.** Autozapis po zamknięciu naboru jest odrzucany. Wersja robocza zostaje w bazie i zostaje widoczna, ale nie da się jej już edytować ani złożyć.

**Retencja.** Wersji roboczych nie kasujemy twardo, nawet po zamknięciu konkursu. Decyzja D10: usunięcie draftu przez wnioskodawcę to oznaczenie jako nieaktywny, nie DELETE, a komunikat w UI **nie obiecuje nieodwracalności**: mówi, że wniosek zniknie z listy, a przywrócenie jest możliwe przez kontakt z OCWIP do końca naboru.

**Zależności.** Blokuje nas: T-11.4, T-25, T-21. Blokujemy: T-33, T-34.

**Kryteria akceptacji.** Checklista pusta, wpisujesz na kartę tę:

- [ ] Zapis wersji roboczej po każdym wypełnionym polu, nie co pięć minut
- [ ] Odczyt wersji roboczej wraca dokładnie w to miejsce, w którym wnioskodawca przerwał
- [ ] Odpowiedź niesie czas ostatniego zapisu, żeby front mógł pokazać "zapisano o 14:32"
- [ ] Autozapis po zamknięciu naboru odrzucony przez regułę z T-21, a nie przez własne liczenie dat
- [ ] Wersja robocza zostaje przy swojej wersji definicji formularza, także po opublikowaniu nowej
- [ ] Usunięcie wersji roboczej to oznaczenie jako nieaktywna, nigdy DELETE
- [ ] Zapis wersji roboczej dopuszcza braki, w odróżnieniu od złożenia (T-30, dwa poziomy surowości)
- [ ] Test: zapis minutę po zamknięciu naboru zwraca jednoznaczny błąd

**Uzupełnienie z raportu: suma kontrolna (decyzja D15).** Suma kontrolna powstaje **już dla niezłożonej wersji roboczej**, jest wersjonowana razem z wnioskiem i widoczna dla wnioskodawcy przed złożeniem. Format: trzy grupy po cztery znaki szesnastkowe rozdzielone dywizami, na przykład `0a55-22c2-b414`. Ekran wniosku pokazuje numer, numer wersji, datę ostatniego zapisu i sumę kontrolną, wzorem bloku "Informacje techniczne". Dwanaście znaków w trzech grupach przepisuje się z wydruku bez pomyłki, a przy telefonie do wsparcia jednoznacznie identyfikuje, o której wersji rozmawiamy. Suma zmienia się z każdą zapisaną wersją. **To nie ma osobnej karty na Trello**, pozycja `R-13`.

**Uzupełnienie z raportu: przypomnienie przed końcem naboru.** Jedno przypomnienie e-mailem trzy dni przed końcem naboru, wyłącznie do osób z **rozpoczętym i niezłożonym** wnioskiem. Treść ustawiana przy konkursie. To ta karta jest miejscem, w którym wiadomo, kto ma rozpoczęty wniosek. Brak karty na Trello, pozycja `R-09`.

---

## T-30 [P0 / Backend] Walidacja odpowiedzi względem definicji formularza

Karta: <https://trello.com/c/m5SPETCx>

**Kontekst.** Formularz jest dynamiczny, więc reguły walidacji też są danymi, nie kodem. Walidacja po stronie frontu jest wygodą dla użytkownika, ale prawda jest po stronie backendu: pominięcie tej karty oznacza, że każdy może wysłać dowolny JSON prosto do API.

**Zakres.** Sprawdzenie odpowiedzi wniosku względem konkretnej wersji definicji formularza: pola wymagane, typy, zakresy, pola zależne. Zwrot błędów w formacie, który front umie przypiąć do konkretnego pola (ProblemDetails, ustalone w T-17, po stronie frontu `ApiError.fieldErrors`).

**Dlaczego walidujemy względem wersji, a nie aktualnej definicji.** Bo wniosek wypełniany według wersji 3 nie może nagle przestać przechodzić walidacji, kiedy operator opublikuje wersję 4.

**Dwa poziomy surowości.** Zapis wersji roboczej dopuszcza braki, bo formularz jest wypełniany przez kilka tygodni. Złożenie oferty wymaga kompletu.

**Zależności.** Blokuje nas: T-24, T-25. Blokujemy: T-33.

**Kryteria akceptacji.** Checklista pusta, wpisujesz na kartę tę:

- [x] Walidacja czyta definicję ze wskazanej wersji, nie z najnowszej
- [x] Dwa poziomy: szkic dopuszcza braki, złożenie wymaga kompletu
- [x] Błędy wracają jako ProblemDetails z kluczem pola, front przypina je bez zgadywania
- [x] Pole niewidoczne z powodu warunku nie jest wymagane
- [x] Komunikat limitu podaje wyliczoną wartość graniczną, nie samą regułę (D12)
- [x] Odpowiedzi spoza definicji są odrzucane, a nie po cichu ignorowane
- [x] Test: żądanie z dowolnym JSON-em prosto do API nie przechodzi

**Uzupełnienie z raportu: silnik walidacji odwraca reguły.** To jest architektoniczna konsekwencja decyzji D12 i musi być w kontrakcie walidatora **od pierwszej wersji**: każda reguła limitu dostarcza nie tylko predykat, ale i wartość graniczną dla bieżącego stanu wniosku. Dorobienie tego później oznacza przepisanie wszystkich reguł. Przy budżecie, w którym dotacja sama jest wyliczana (D11), policzenie progu w głowie jest realnie trudne, więc to nie jest uprzejmość, tylko funkcja.

---

## T-31 [P0 / Backend] Limit kwoty dotacji przy budżecie wniosku

Karta: <https://trello.com/c/dWHtvzvX>

**Kontekst.** Reguła twarda, podana na konkretnym przykładzie: przy limicie dziewięciu tysięcy ktoś wpisuje dziesięć i system ma zareagować. Cytat: "generator mu krzyczy halo, za dużo wpisałeś, bo jest tylko dziewięć".

**Zakres.** Sumowanie pozycji budżetu i porównanie z limitem konkursu, wraz z komunikatem błędu.

**Czego wymagamy od komunikatu.** Ma wskazywać konkretną pozycję budżetu, a nie zwracać ogólny błąd formularza. Wnioskodawca ma 5 stron wniosku, więc "przekroczono limit" bez wskazania miejsca oznacza szukanie po omacku i telefon do OCWIP.

**Czego nie robimy tutaj.** Nie zaokrąglamy ani nie poprawiamy kwot za wnioskodawcę. Sygnalizujemy problem, decyzję zostawiamy człowiekowi.

**Zależności.** Blokuje nas: T-30, T-20. Blokujemy: T-33.

**Kryteria akceptacji.** Checklista pusta, wpisujesz na kartę tę:

- [x] Suma całego budżetu nie przekracza maksymalnej kwoty dotacji z ustawień konkursu
- [x] Suma tabeli kosztów rozwoju instytucjonalnego nie przekracza swojego progu procentowego
- [x] Suma tabeli kosztów pośrednich nie przekracza swojego progu procentowego
- [x] W każdym wierszu wartość całkowita zgadza się z iloczynem liczby jednostek i ceny
- [x] Komunikat wskazuje konkretną tabelę i konkretną pozycję
- [x] Komunikat podaje kwotę, o jaką przekroczono limit, i kwotę dopuszczalną (D12)
- [x] Kwoty liczone na pełnej precyzji, zaokrąglane dopiero przy wyświetleniu (D13)
- [x] Testy graniczne: dokładnie na limicie, złotówkę pod, złotówkę nad

**Uzupełnienie z raportu: system sprawdza cztery rzeczy naraz.** Powyższa lista czterech pierwszych pozycji jest wprost z raportu i nie jest przypadkowa: to są cztery niezależne reguły, a nie jedna suma. Trzy tabele kosztów:

| Tabela | Nazwa | Limit |
|---|---|---|
| A | Koszty wynikające ze specyfiki projektu, czyli bezpośrednie | bez limitu procentowego |
| B | Koszty związane z rozwojem instytucjonalnym | maksimum 50% kwoty dotacji dla organizacji pozarządowej; kategorię można w konkursie wyłączyć |
| C | Koszty pośrednie | maksimum 10% kwoty dotacji |

**Podstawa liczenia procentu jest ustawieniem konkursu**, do wyboru kwota dotacji albo całkowita wartość projektu. Domyślnie kwota dotacji (decyzja 5 z raportu, D-raport), ale przełącznik zostaje.

**Próg przychodu organizacji.** We wzorze na 2026 wynosi 200 000 zł i występuje wyłącznie w formularzu dla organizacji pozarządowej. Pole musi przyjmować wartość 0, a przekroczenie progu jest **twardą blokadą** z komunikatem "przekroczono limit zgodny z Regulaminem. Podmiot nieuprawniony", a nie miękkim ostrzeżeniem.

**Kwota dotacji jest wyliczana (D11).** Walidacja limitu działa na wartości wyliczonej z wkładów własnych, nie na wpisanej przez wnioskodawcę.

---

## T-32 [P0 / Backend] Załączniki: przesyłanie, limity, przechowywanie

Karta: <https://trello.com/c/K4ouKUD6>

**Kontekst.** Do oferty dołącza się pliki, a ich wymagalność zależy od konfiguracji konkursu. T-11.4 zamodelowała tylko metadane, tutaj dochodzi realne przechowywanie plików.

**Zakres.** Przesyłanie pliku, limit rozmiaru, biała lista formatów, przechowywanie, pobieranie z kontrolą uprawnień.

**Dlaczego biała lista, a nie czarna.** Czarna lista formatów zawsze czegoś nie obejmie. Dopuszczamy wprost to, co ma sens dla dokumentów konkursowych, resztę odrzucamy.

**Dlaczego pliki nie mogą leżeć pod przewidywalnym adresem.** Załącznik do wniosku to dokument cudzej organizacji. Adres pliku nie może dać się zgadnąć, a pobranie musi przechodzić przez tę samą kontrolę uprawnień co sam wniosek. Plik dostępny, bo ktoś zna link, to wyciek.

**Zależności.** Blokuje nas: T-11.4, T-13.2. Blokujemy: T-33.

**Kryteria akceptacji.** Checklista pusta, wpisujesz na kartę tę:

- [ ] Biała lista formatów, reszta odrzucana z czytelnym komunikatem
- [ ] Limit rozmiaru pojedynczego pliku i limit na cały wniosek, oba jako ustawienie
- [ ] Ścieżka w storage nie daje się zgadnąć i jest unikalna
- [ ] Pobranie przechodzi tę samą kontrolę uprawnień co sam wniosek
- [ ] Zadeklarowany typ MIME jest traktowany jako deklaracja klienta, nie jako fakt
- [ ] Podmiana pliku zastępuje poprzedni, a poprzedni nie znika twardo
- [ ] Test: pobranie cudzego załącznika po zgadniętym identyfikatorze daje 403

**Uzupełnienie z raportu, i zdejmuje pytanie otwarte z karty.** Karta mówi, że limity i formaty nie zostały ustalone. Raport je podaje:

- **Dopuszczalne formaty:** pdf, doc, docx, xls, xlsx, jpg, odt, ods. Lista jest ustawieniem załącznika w konkursie, wybieranym z zaznaczenia wielu.
- **Maksymalny rozmiar pliku: 10 MB. Na cały wniosek: 50 MB.** Jedno i drugie jest ustawieniem.
- **Wymagalność ma trzy warianty**, nie sześć: wymagany, niewymagalny, wymagany warunkowo przy rejestrze innym niż KRS. Dzisiejsze narzędzie ma sześć wykluczających się wariantów i świadomie tego nie kopiujemy.
- Każdy załącznik ma tytuł, opis (co dokładnie ma być dołączone) i opcjonalnie **wzór pliku do pobrania przez wnioskodawcę**, do 10 MB.
- W konkursie na 2026 rok załączniki to: odpis z rejestru lub wyciąg z ewidencji, jeśli nie jest ogólnodostępny, oraz CIT albo sprawozdanie finansowe za lata 2025, 2024 i 2023.

**Konsekwencja, o której karta nie mówi:** lista wymaganych plików blokuje złożenie wniosku w kroku 3.7 **i sama wpisuje się na ekran "co przygotować"** przed pierwszym polem. Dodanie załącznika w ustawieniach konkursu zmienia tę listę bez niczyjej ingerencji.

**B-08.** Karta B-08 na Trello odnotowuje, gdzie na dokumentach pojawiają się logotypy źródeł finansowania, i wskazuje, że wchodzi w T-32. Sprawdź ją przed startem.

---

## T-33 [P0 / Backend] Złożenie oferty i historia zmian statusu

Karta: <https://trello.com/c/uG9aepGO>

**Kontekst.** Moment, w którym wersja robocza staje się złożoną ofertą. Nieodwracalny i twardo związany z terminem. To jest najważniejsza pojedyncza operacja w całym systemie.

**Zakres.** Operacja złożenia: pełna walidacja, sprawdzenie terminu, zamrożenie odpowiedzi, nadanie numeru wniosku, zapis wpisu w historii statusów, wysyłka potwierdzenia mailem.

**Dlaczego odpowiedzi są zamrażane.** Złożona oferta jest dokumentem. Jeśli po złożeniu dałoby się ją po cichu edytować, ocena straciłaby sens, a przy retencji 5 lat nie dałoby się odtworzyć, co komisja faktycznie oceniała.

**Historia statusów.** Każda zmiana statusu wniosku zapisuje kto, kiedy i z czego na co. Nie nadpisujemy poprzedniego statusu.

**Zależności.** Blokuje nas: T-29, T-30, T-31, T-32, T-21. Blokujemy: cały M5.

**Kryteria akceptacji.** Checklista pusta, wpisujesz na kartę tę:

- [ ] Złożenie wymaga kompletu danych, a lista braków wskazuje konkretne pola
- [ ] Sprawdzenie terminu idzie przez regułę z T-21
- [ ] Odpowiedzi po złożeniu są zamrożone, próba edycji odrzucona
- [ ] Wniosek dostaje numer dopiero przy złożeniu, wersja robocza go nie zużywa
- [ ] Wpis w historii statusów: kto, kiedy, z czego na co, bez nadpisywania poprzedniego
- [ ] Mail z potwierdzeniem idzie wyłącznie po złożeniu, nigdy przy zapisie szkicu
- [ ] Wnioskodawca może pobrać PDF potwierdzenia
- [ ] Jeden podmiot może złożyć kilka wniosków w jednym konkursie, nic tego nie blokuje (D9)
- [ ] Test: dwie jednoczesne próby złożenia nie dają dwóch wniosków z tym samym numerem

**Otwarty punkt implementacyjny, który ugryzie właśnie tutaj.** Schemat wymaga numeru dokładnie w tej samej instrukcji, która ustawia status na `Submitted`, a para `(competition_id, number)` jest unikalna. **Nic w schemacie tego numeru nie przydziela:** nie ma sekwencji, wartości domyślnej ani blokady. Dwóch wnioskodawców klikających "Złóż" w tej samej sekundzie odczyta ten sam `MAX(number)` i jeden dostanie 23505 przy próbie złożenia, która może być sekundy od odcięcia co do minuty. **Strategię przydziału wybiera ta karta**: sekwencja per konkurs, blokada doradcza albo ponowienie. Opis w [`../model-danych.md`](../model-danych.md).

**Drugi otwarty punkt.** Złożony klucz obcy broni pary konkurs plus definicja formularza w bazie, ale nie przy zapisie przez nawigacje EF: EF wyrównuje `CompetitionId` do konkursu definicji zamiast odrzucić rozjazd. Sprawdzenie pary należy do brzegu API w T-29 i w tej karcie.

**Uzupełnienie z raportu.** Po złożeniu: treść zamrożona, wniosek dostaje numer, idzie e-mail, można pobrać PDF potwierdzenia. **Historia wersji i suma kontrolna pokazują, czy wniosek był poprawiany po złożeniu** i obok siebie widać dwie sumy: pierwotną i ostatnią. To jest potrzebne dopiero przy zwrocie do poprawy (`R-03`), ale zaprojektuj historię tak, żeby to udźwignęła.

Wymóg, który raport nazywa wprost: **dowód, kto i kiedy złożył wniosek**. Odcięcie naboru co do minuty jest do obrony tylko wtedy, gdy da się je udowodnić. Środki: znacznik czasu z serwera, numer wersji, suma kontrolna, niezmienialny zapis zdarzeń, e-mail i PDF potwierdzenia dla wnioskodawcy.

Drugi wymóg: **wersja robocza nie może wyglądać jak złożona.** Najczęstszy realny spór to ktoś, kto wypełnił i nie kliknął "Złóż". Środki: przycisk widoczny od początku z listą braków, jednoznaczny status, e-mail wysyłany tylko po złożeniu.

**Pytanie otwarte z karty jest w raporcie rozstrzygnięte inaczej.** Karta pisze, że nie ustalono, czy wniosek można poprawić po złożeniu, i do czasu odpowiedzi złożenie jest nieodwracalne. Raport opisuje pełny mechanizm zwrotu do poprawy (krok 4.2, decyzja 10) jako coś, co **powtarzamy w całości** z obecnego narzędzia. To jest `R-03` w [`rozbieznosci.md`](rozbieznosci.md) i osobna karta, nie doklejka do tej. Tutaj buduj złożenie jako nieodwracalne, ale zostaw miejsce w historii statusów na powrót do stanu roboczego.

---

## T-35 [P0 / Frontend] Lista wniosków i statusów dla operatora

Karta: <https://trello.com/c/GCyfm14r>

**Kontekst.** Zamawiający śledzi spływające wnioski na bieżąco w trakcie naboru. To jest ekran, na którym pracownik OCWIP spędzi najwięcej czasu po zamknięciu konkursu.

**Zakres.** Tabela wniosków w konkursie: podmiot, numer, status, data złożenia, kwota. Filtrowanie po statusie, sortowanie, wejście w pojedynczy wniosek, podgląd złożonej oferty wraz z załącznikami.

**Skala, pod którą projektujemy.** Około 120 ofert w jednym konkursie. Tabela, która rozjeżdża się przy stu wierszach, nie działa dokładnie w tym momencie, w którym jest najbardziej potrzebna. Sprawdzamy na danych testowych w tej skali, nie na trzech wierszach.

**Czego nie robimy tutaj.** Przypisywania recenzentów ani oceny, to M5.

**Zależności.** Blokuje nas: T-33, T-15.3. Blokujemy: T-41.

**Kryteria akceptacji.** Checklista pusta, wpisujesz na kartę tę:

- [ ] Kolumny: liczba porządkowa, numer wniosku, nazwa podmiotu, tytuł projektu, całkowity koszt zadania, wnioskowana kwota, status
- [ ] Na dole suma kwot wnioskowanych i informacja, ile zostało z puli konkursu
- [ ] Sortowanie i filtrowanie po statusie oraz po rodzaju wnioskodawcy
- [ ] Wejście w pojedynczy wniosek pokazuje złożoną ofertę wraz z załącznikami
- [ ] Wersje robocze nie pojawiają się na tej liście wcale
- [ ] Tabela ze 120 wierszami nie rozjeżdża się i nie skacze przy ładowaniu
- [ ] Eksport listy do arkusza i do PDF
- [ ] Test negatywny: wnioskodawca i recenzent nie wchodzą na ten ekran

**Uzupełnienie z raportu.** Raport dokłada dwie kolumny (wynik oceny formalnej, a przy konkursie z wymogiem papieru także data wpływu koperty) i wprost pisze, że **rozpoczęte i niezłożone wnioski nie pojawiają się tu wcale**. Eksport do arkusza albo PDF jest w raporcie częścią tego ekranu, nie osobną funkcją.

**Zwrot wniosku do poprawy** (krok 4.2) siada na tym ekranie, ale jest osobnym zakresem bez karty na Trello, pozycja `R-03`. Nie dopisuj go tutaj po cichu.

---

## T-34 [P0 / Frontend] Ścieżka wnioskodawcy: robocze, złożenie, potwierdzenie

Karta: <https://trello.com/c/0OWDa8wR>

**Kontekst.** Domknięcie ścieżki, dla której powstaje ten system. Od kliknięcia w konkurs do potwierdzenia złożenia oferty.

**Zakres.** Lista własnych wniosków ze statusami, wejście w wersję roboczą, wypełnianie formularza, dołączanie załączników, ekran przed złożeniem podsumowujący komplet danych, świadome potwierdzenie złożenia, ekran potwierdzenia z numerem wniosku.

**Dlaczego ekran podsumowania przed złożeniem.** Bo złożenie jest nieodwracalne i tnie termin. Wnioskodawca musi zobaczyć w jednym miejscu, co dokładnie wysyła i czego brakuje, zanim kliknie.

**Jeden podmiot, kilka ofert.** Jedna organizacja może złożyć kilka ofert w jednym konkursie. Lista i nawigacja muszą to znieść bez mylenia wniosków.

**Zależności.** Blokuje nas: T-28, T-29, T-33, T-23, T-15.2, T-15.4. Blokujemy: demo pełnej ścieżki.

**Kryteria akceptacji.** Checklista pusta, wpisujesz na kartę tę:

- [ ] Przycisk "Złóż wniosek" jest widoczny od pierwszej sekundy, a dopóki czegoś brakuje, jest wyłączony
- [ ] Obok wyłączonego przycisku stoi lista braków, a każda pozycja jest odnośnikiem prowadzącym prosto do tego pola
- [ ] Ekran podsumowania pokazuje cały wniosek z "popraw" przy każdej sekcji
- [ ] Załączniki jako kafelki: nazwa, opis, wzór do pobrania, przeciąganie pliku ORAZ zwykły przycisk wyboru pliku
- [ ] Załączniki nieobowiązkowe stoją niżej, wyraźnie oddzielone
- [ ] Widoczne "zapisano o 14:32" i licznik dni do końca naboru, w ostatniej dobie w godzinach
- [ ] Jedno okno potwierdzenia przy złożeniu, jedyne w całej ścieżce
- [ ] Ekran potwierdzenia z numerem wniosku i pobraniem PDF
- [ ] Lista własnych wniosków znosi kilka ofert tego samego podmiotu w jednym konkursie
- [ ] Cała ścieżka przechodzi się klawiaturą, przeciąganie myszką nigdy nie jest jedyną drogą

**Uzupełnienie z raportu: dziewięć zasad i ekran "co przygotować".** Ta karta jest miejscem, w którym dziewięć zasad z [`proces.md`](proces.md) przestaje być deklaracją. Dwie z nich mają tu konkretną postać:

- **Zero okienek.** Wszystko robimy na miejscu, w formularzu. Jedyny wyjątek to jedno potwierdzenie przy złożeniu wniosku, bo tego się nie odkręca: "Po złożeniu wniosku nie będzie można go już edytować."
- **Przycisk nigdy nie znika.** To jest jedyna rzecz na ekranie podsumowania, którą naprawdę trzeba dobrze zrobić, bo od niej zależy, czy zamiast telefonu do OCWIP człowiek poradzi sobie sam. Przykład listy braków z raportu:

  > Zostały trzy rzeczy do uzupełnienia:
  > - Sekcja "Projekt": pole "Opis pomysłu na projekt" ma 640 znaków, wymagane jest minimum 1000. -> popraw
  > - Sekcja "Budżet": koszty pośrednie to 12% dotacji, dopuszczalne jest 10%. -> popraw
  > - Załącznik "Sprawozdanie finansowe za 2023" nie został dodany. -> dodaj plik

**Ekran "co przygotować"** stoi przed pierwszym polem i jest generowany: załączniki bierze z ustawień konkursu (krok 1.5), terminy z kroku 1.1, a dwa zdania od siebie dopisuje OCWIP w ustawieniach formularza. Zmiana załącznika w konkursie zmienia tę listę sama. Brak karty na Trello, pozycja `R-10`.

**Rodzaj wnioskodawcy pytamy raz, na wejściu** (krok 3.1), bo od niego zależy cała pierwsza sekcja. Lepiej zapytać raz na początku niż odsłaniać i chować pola w trakcie.

---

## T-36 [P0 / Backend] Testy izolacji danych wnioskodawcy

Karta: <https://trello.com/c/eKXNBKtF>

**Kontekst.** Rozszerzenie T-13.3 na realne dane. Tam testowaliśmy uprawnienia na danych testowych, tutaj na kompletnej ścieżce z prawdziwymi wnioskami, wersjami roboczymi i załącznikami. Reguła bez testu automatycznego to życzenie, nie reguła.

**Zakres.** Testy potwierdzające, że wnioskodawca nie sięgnie po cudzy wniosek, cudzą wersję roboczą ani cudzy załącznik, niezależnie od tego, jak przekręci identyfikator w adresie.

**Co testujemy konkretnie.** Podmiana identyfikatora wniosku, podmiana identyfikatora załącznika, próba edycji cudzej wersji roboczej, próba złożenia cudzego wniosku, dostęp do wniosku z konkursu, w którym podmiot nie startował. Każdy przypadek kończy się odmową 403, a nie błędem 500 ani pustą stroną.

**Dlaczego osobna karta, a nie dopisek do każdej poprzedniej.** Bo to jest warunek, na jakim ten system w ogóle wychodzi poza lokalną maszynę. Rozproszony po dziesięciu kartach zniknąłby przy pierwszym pośpiechu.

**Zależności.** Blokuje nas: T-33, T-32, T-13.3. Blokujemy: wystawienie aplikacji pod publicznym adresem.

**Kryteria akceptacji.** Checklista pusta, wpisujesz na kartę listę z sekcji "Co testujemy konkretnie", plus:

- [x] Każdy przypadek kończy się 403, nigdy 500 i nigdy pustą odpowiedzią
- [x] Testy wpięte w CI i blokują merge przy niepowodzeniu
- [x] Test na pobranie załącznika po adresie pliku z pominięciem API

**Uzupełnienie z raportu, i jest to rozszerzenie zakresu.** Raport dokłada regułę odwrotną, której dziś nie da się przetestować, bo model jej nie ma: **wewnątrz jednej organizacji kto ma dostęp do karty organizacji, widzi wszystkie jej wnioski, także robocze. Dostęp idzie za organizacją, nie za osobą, która kliknęła "nowy wniosek".** Dziś schemat wiąże użytkownika z podmiotem jeden do jednego, więc ta reguła nie ma reprezentacji. Pozycja `R-01` w [`rozbieznosci.md`](rozbieznosci.md). W tej karcie testujesz to, co jest, i dopisujesz test oczekujący dla reguły organizacyjnej dopiero po decyzji.
