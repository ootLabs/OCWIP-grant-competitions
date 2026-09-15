# Decyzje

Dwa zestawy decyzji, z dwóch źródeł. **Nie łącz ich w jedną numerację**, bo obie są cytowane w innych dokumentach pod swoimi numerami.

- `D1` do `D15`: lista "Decyzje projektowe" na Trello. Decyzje zespołu, podejmowane w trakcie prac.
- `RD1` do `RD14`: decyzje, które raport `RAPORT-proces-i-pola.docx` podjął sam i przedstawił zamawiającemu do zatwierdzenia. Raport numeruje je po prostu 1 do 14; prefiks `RD` jest nasz, żeby nie myliły się z `D`.

Decyzja jest wiążąca do czasu, aż zastąpi ją nowa karta. Jeśli zmiana, którą robisz, odwraca którąś z nich, to jest [powód do zapytania człowieka](../../runbook.md#kiedy-naprawdę-pytasz), a nie do cichego obejścia.

---

## Decyzje zespołu (Trello)

| Nr | Decyzja | Konsekwencje w kodzie | Karta |
|---|---|---|---|
| D1 | Natywny kreator formularzy, nie parsowanie Worda | T-24, T-26, T-27 | [lvu3meHB](https://trello.com/c/lvu3meHB) |
| D2 | **Samoobsługowość jest wymogiem twardym.** Operator zmienia formularz bez programisty | struktura formularza jako JSONB, wersjonowanie, renderer generyczny; najważniejsza konsekwencja architektoniczna w projekcie | [wHRs5sds](https://trello.com/c/wHRs5sds) |
| D3 | Parsowanie Worda ewentualnie później, nie w MVP | nic; odpowiedź na "a może przy okazji" | [V9OVWXH0](https://trello.com/c/V9OVWXH0) |
| D4 | Podpis umowy odbywa się poza systemem | zdejmuje integrację z e-podpisem; wymaga rejestru umów u operatora | [oR85sQHM](https://trello.com/c/oR85sQHM) |
| D5 | Treści merytoryczne dostarcza OCWIP, my robimy mechanizm | granica odpowiedzialności; źródło B-03 i B-04 | [EG4rrryK](https://trello.com/c/EG4rrryK) |
| D6 | Publiczna witryna pokazuje wyłącznie konkursy OCWIP | T-23; brak filtrowania po województwie to cała funkcja, nie brak funkcji | [GKWLAlhL](https://trello.com/c/GKWLAlhL) |
| D7 | **Twarde odcięcie terminu co do minuty** | T-21; czas w UTC, test na przejściu czasu letniego na zimowy | [GR8foD6n](https://trello.com/c/GR8foD6n) |
| D8 | RODO po stronie OCWIP, ale w danych są PESEL-e | T-47 w Sprincie 1, nie na końcu; klucz szyfrujący poza repozytorium; brak twardego kasowania | [0hQQCdoi](https://trello.com/c/0hQQCdoi) |
| D9 | Witkac to punkt odniesienia, ale nie odtwarzamy jego skali | lista świadomych cięć w [`../zakres.md`](../zakres.md) | [ypPtjfzT](https://trello.com/c/ypPtjfzT) |
| D10 | **Nie kasujemy twardo nawet niezłożonej wersji roboczej** | T-29: usunięcie draftu to zmiana flagi; UI nie obiecuje nieodwracalności | [8r4smIg5](https://trello.com/c/8r4smIg5) |
| D11 | **Wnioskowana dotacja jest polem wyliczanym, nie wpisywanym** | T-22, T-26, T-28: kolumna dotacji tylko do odczytu; walidacja widełek działa na wartości wyliczonej | [wNZYjElk](https://trello.com/c/wNZYjElk) |
| D12 | **Komunikat walidacji podaje wyliczoną wartość graniczną**, nie samą regułę | silnik walidacji musi umieć ODWRÓCIĆ regułę; zmienia kontrakt walidatora i musi w nim być od początku | [9h0GBRTb](https://trello.com/c/9h0GBRTb) |
| D13 | Kwoty trzymamy na czterech miejscach, wyświetlamy dwa | typ pieniężny w migracjach; sumy i procenty na pełnej precyzji, zaokrąglanie przy wyświetlaniu | [avvr9m9F](https://trello.com/c/avvr9m9F) |
| D14 | **Definicja formularza rozróżnia pola techniczne od drukowanych** | właściwość pola w schemacie JSONB od pierwszej wersji; renderer i generowanie PDF czytają tę samą flagę | [e5iQ5tEI](https://trello.com/c/e5iQ5tEI) |
| D15 | **Suma kontrolna powstaje już dla wersji roboczej** | T-29: suma zmienia się z każdą zapisaną wersją; format `0a55-22c2-b414`; wydruk niesie tę samą sumę na każdej stronie | [ubMWQeSC](https://trello.com/c/ubMWQeSC) |

### Cztery, które najłatwiej przeoczyć

**D11 i D12 razem** zmieniają budżet w coś, czego nie da się dorobić później: dotacja jest wyliczana z wkładów własnych, a komunikat limitu podaje kwotę graniczną przy bieżącym stanie wniosku. Silnik walidacji, który umie tylko odpowiedzieć prawda albo fałsz, trzeba będzie przepisać w całości.

**D13** to typ kolumny w migracji. Po wejściu danych produkcyjnych zmiana precyzji jest migracją na danych, a nie poprawką.

**D14** to właściwość w schemacie JSONB. Dołożona później znaczy przejście po wszystkich istniejących definicjach formularzy i zgadywanie, które pola były techniczne.

---

## Decyzje raportu, przedstawione zamawiającemu do zatwierdzenia

Raport podjął je sam, tam gdzie materiały nie dawały odpowiedzi. Kolumna "jeśli jest inaczej" mówi, co się zmienia, gdy zamawiający odpowie inaczej. **Żadna z nich nie jest potwierdzona**, więc każda jest kandydatem na kartę w liście "Decyzje projektowe" albo na bloker.

| Nr | Zdecydowaliśmy tak | Jeśli jest inaczej |
|---|---|---|
| RD1 | Publiczna strona konkursu bez logowania, ze stałym odnośnikiem do udostępniania i archiwum wyników | wszystko za logowaniem, jak dziś |
| RD2 | Liczba wniosków złożonych w trwającym naborze **nie jest pokazywana publicznie**; po zamknięciu naboru już tak | pokazujemy ją na bieżąco |
| RD3 | W wynikach publikujemy nazwę grupy nieformalnej, **bez imion i nazwisk** jej członków | regulamin każe publikować inaczej |
| RD4 | Wersja papierowa jako przełącznik konkursu, domyślnie wyłączony; bez osobnego obiegu dla papieru | papier zawsze wymagany albo pełna obsługa z porównywaniem wersji |
| RD5 | Limit procentowy kosztów liczony **od kwoty dotacji**; sama podstawa jest przełącznikiem konkursu | na pokazie padło "procent całej wartości", więc trzeba potwierdzić podstawę |
| RD6 | **Jeden formularz warunkowy zamiast trzech osobnych wzorów** | trzy osobne formularze, droższe w utrzymaniu |
| RD7 | **Karta organizacji jako osobny rekord**, do którego wniosek się odwołuje; jedna osoba może mieć dostęp do kilku organizacji | zostajemy przy kopiowaniu danych do wniosku przy jego tworzeniu, jak dziś |
| RD8 | Grupa bez patrona: umowa z liderem jako osobą fizyczną, jego rachunek, PESEL dopiero przy umowie | jeśli OCWIP robi zakupy zamiast wypłacać dotację, to inny model |
| RD9 | Jeden podmiot może złożyć kilka wniosków w jednym konkursie | dokładamy blokadę |
| RD10 | **Zwrot wniosku do poprawy** w naborze i po ocenie formalnej; poprawka wymaga ponownego złożenia | wyłączamy jeden z etapów |
| RD11 | Oświadczenie o konflikcie interesów w systemie; bez podpisu ekspert nie widzi wniosków | prowadzą je poza systemem, wtedy tylko odnotowujemy fakt |
| RD12 | **Dwóch recenzentów na wniosek** jako ustawienie konkursu; przy rozbieżności powyżej 30% skali decyduje operator | inna liczba recenzentów albo inna reguła przy rozbieżności |
| RD13 | Ocena formalna: **jedna osoba**; wynik negatywny nie zamyka sprawy, obok stoi "zwróć do poprawy" | więcej osób albo twarde odrzucenie bez możliwości poprawy |
| RD14 | Każdy ekspert ocenia osobno w systemie, ale **model dopuszcza ocenę zbiorczą** wprowadzoną przez operatora | od razu włączamy tryb zbiorczy |

### Gdzie te dwie listy się spotykają

| Para | Co z tego wynika |
|---|---|
| D9 i RD9 | to samo ustalenie z dwóch źródeł, zgodne |
| D4 i RD8 | podpis poza systemem, a przy grupie bez patrona umowa z osobą fizyczną, stąd PESEL w T-45 |
| D6 i RD1 | D6 mówi "tylko konkursy OCWIP", RD1 dokłada "i widoczne bez logowania"; RD1 jest szersza i **nie jest potwierdzona** |
| D2 i RD6 | jeden formularz warunkowy jest praktycznym wymogiem dla samoobsługowego kreatora, bo trzy wzory znaczą trzy razy więcej klikania przy każdej zmianie |
| brak odpowiednika D | RD7 (karta organizacji) jest największą niepotwierdzoną zmianą modelu danych w całym raporcie, patrz `R-01` w [`rozbieznosci.md`](rozbieznosci.md) |

### Decyzje raportu, które trzeba potwierdzić najpilniej

1. **RD7, karta organizacji.** Zmienia relację użytkownik do podmiotu, czyli jedno z czterech założeń już wypalonych w schemacie. Migracja dziś jest bezkosztowa, bo baza jest pusta.
2. **RD5, podstawa liczenia procentu.** Wchodzi do ustawień konkursu i do silnika walidacji. Zła podstawa znaczy źle policzone limity w każdym budżecie.
3. **RD12 i RD13**, bo odpowiadają na trzy z czterech pytań otwartych w T-37 i pozwalają ruszyć połowę M5 mimo B-02.
