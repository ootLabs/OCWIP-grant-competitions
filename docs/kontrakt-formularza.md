# Kontrakt definicji formularza

Dokument JSON siedzący w kolumnie `form_definitions.definition` (typ `jsonb`). Opisuje **strukturę** formularza: sekcje, pola, warunki i obliczenia. Odpowiedzi wnioskodawcy są osobnym dokumentem w `applications.answers`, a ich kształt opisuje sekcja [Odpowiedzi wnioskodawcy](#odpowiedzi-wnioskodawcy-t-30) na końcu.

Kontrakt ma dwie strony: kreator z `T-26` go zapisuje, renderer z `T-28` go czyta, a walidacja odpowiedzi z `T-30` czyta go z trzeciej strony. Dlatego stoi tutaj jako dokument, a nie wyłącznie jako klasy w kodzie: dwie strony budowane równolegle bez spisanego kontraktu rozjeżdżają się w tydzień.

Kod kontraktu: `backend/src/Ocwip.Api/Models/Forms/`, wejście przez `FormSchemaValidator`. Reguła jest jedna: **do bazy nie trafia definicja, której renderer nie umie wyświetlić**.

---

## Korzeń

```json
{
  "schemaVersion": 1,
  "sections": [ ... ]
}
```

Check constraint w bazie dopuszcza obiekt albo tablicę. Kontrakt wybiera **obiekt**, bo dokument musi nieść własną wersję kontraktu: tablica sekcji nie ma gdzie jej zapisać, a pierwsza zmiana kontraktu kazałaby zgadywać po kształcie.

`schemaVersion` to wersja **kontraktu**, nie formularza. Wersja formularza jest kolumną (`T-25`) i odpowiada na inne pytanie: która wersja pól została pokazana wnioskodawcy. Definicja z inną wersją kontraktu jest odrzucana, nie czytana optymistycznie.

## Sekcja

```json
{
  "key": "budzet",
  "title": "Budżet projektu",
  "description": "Opcjonalny akapit wstępu",
  "visibleWhen": { "field": "rodzaj_wnioskodawcy", "equalsAnyOf": ["ngo"] },
  "fields": [ ... ]
}
```

Sekcja to jeden ekran wniosku (zasada "jedna kolumna, jedna rzecz naraz" z [`runbook/proces.md`](runbook/proces.md)). Cała sekcja może być warunkowa, bo rozwój instytucjonalny wyłącza się razem ze swoją kategorią kosztów.

## Pole

```json
{
  "key": "opis_pomyslu",
  "type": "longText",
  "label": "Opis pomysłu na projekt",
  "help": "Napisz, co konkretnie się wydarzy i dla kogo",
  "required": true,
  "printed": true,
  "maxLength": 5000,
  "minLength": 1000,
  "visibleWhen": null
}
```

Minimum, bez którego formularza nie da się odtworzyć: `key`, `type`, `label`, `help`, `required`, `maxLength` przy polach tekstowych, `visibleWhen` oraz `printed`.

- `key`: małe litery, cyfry i podkreślenia, pierwszy znak literą, do 64 znaków. Unikalny w całym dokumencie, bo pod tym kluczem stoi odpowiedź, kolumna raportu i znacznik we wzorze umowy.
- `printed` (**decyzja D14**): czy pole trafia na wydruk oferty. Pola techniczne, czyli takie, które istnieją tylko po to, żeby coś policzyć albo spiąć dwie sekcje, są widoczne w interfejsie i nieobecne na wydruku. Flaga dołożona później oznaczałaby przejście po wszystkich istniejących definicjach i zgadywanie.
- `maxLength` jest **wymagane** przy `shortText` i `longText`: bez niego renderer nie ma czego pokazać w liczniku znaków, a kreator nie ma czego ustawić. Przy pozostałych rodzajach jest odrzucane, bo nic nie znaczy.
- `minValue` i `maxValue` tylko przy rodzajach liczbowych.
- `role` (opcjonalne, `T-35`, karty oceny w sekcji "Karta oceny" niżej): które pole wypełnia kolumnę listy wniosków operatora. `projectTitle` (tytuł projektu) tylko na `shortText`, `totalCost` (całkowity koszt zadania) i `requestedGrant` (wnioskowana kwota) tylko na `amount` albo na `calculated`, które nie jest ilorazem, bo iloraz to procent. Każda rola najwyżej raz w dokumencie i nigdy na kolumnie tabeli: lista pokazuje jedną wartość na wniosek. Znacznik, a nie umówiony klucz, bo klucze wybiera operator, a literówka w kluczu zostawiłaby kolumnę po cichu pustą, a nieznana rola jest odrzucana przy publikacji. Definicja bez ról jest poprawna: lista pokazuje wtedy puste komórki. `schemaVersion` bez zmian, bo pole jest opcjonalne, a dotychczasowe definicje go nie mają.

## Piętnaście rodzajów pól

Lista pochodzi z [`runbook/pola.md`](runbook/pola.md) i została wyprowadzona z trzech wzorów wniosku na 2026.

| `type` | Rodzaj | Co dodatkowo niesie |
|---|---|---|
| `shortText` | tekst krótki | `maxLength` |
| `longText` | tekst długi | `maxLength`, `minLength` |
| `number` | liczba | `minValue`, `maxValue` |
| `amount` | kwota | jak wyżej, plus `limits` |
| `percent` | procent | jak wyżej |
| `date` | data | |
| `dateTime` | data i godzina | |
| `yesNo` | tak albo nie | |
| `singleChoice` | wybór jednej opcji | `options` |
| `multipleChoice` | wybór wielu opcji | `options` |
| `repeatableTable` | tabela o zmiennej liczbie wierszy | `table.columns`, `table.minRows`, `table.maxRows` |
| `fixedTable` | tabela o stałej liczbie wierszy | `table.columns`, `table.rows` |
| `file` | plik | `file.allowedFormats`, `file.maxSizeMegabytes` |
| `statement` | oświadczenie | `statementText` |
| `calculated` | pole wyliczane | `calculation`, `limits` |

**Nie ma rodzaju "tabela budżetu".** Budżet to `repeatableTable`, którego kolumna wartości jest kolumną `calculated`. To jest test całego schematu: gdyby budżet potrzebował własnego rodzaju pola, schemat byłby zbudowany pod coś innego, a budżet doklejony z boku.

## Warunek widoczności

```json
"visibleWhen": { "field": "forma_prawna", "equalsAnyOf": ["inna"] }
```

Wybór pary "pole plus wartość", nie wyrażenie. Powód jest w `T-26`: operator ma ustawiać warunek klikaniem, a wyrażenie trzeba wpisać.

Odrzucane jest:

- wskazanie pola, którego w formularzu nie ma,
- wskazanie pola stojącego **niżej** w formularzu, bo odsłonić może tylko odpowiedź już udzielona,
- wskazanie pola, którego odpowiedzi nie da się porównać z listą wartości (tylko `singleChoice`, `multipleChoice` i `yesNo`),
- wartość, której nie ma na liście opcji, bo taki warunek nigdy się nie spełni,
- wskazanie kolumny cudzej tabeli, bo kolumna ma tyle wartości, ile wierszy.

## Tabela

```json
{
  "key": "budzet_a",
  "type": "repeatableTable",
  "table": {
    "minRows": 1,
    "columns": [ { "key": "liczba", "type": "number", ... } ]
  }
}
```

Kolumna jest zwykłym polem, z własną etykietą, wymagalnością i flagą wydruku. W kolumnie nie wolno postawić tabeli ani załącznika: obu renderer nie ma jak narysować w wierszu.

Tabela o stałej liczbie wierszy (`fixedTable`, na przykład trzej członkowie grupy nieformalnej) wypisuje wiersze z góry w `table.rows` i nie przyjmuje widełek liczby wierszy. Tabela zmienna jest odwrotnie: widełki tak, wypisane wiersze nie.

Wewnątrz tabeli odwołania są **krótkie**: kolumna `wartosc` pisze `"operands": ["liczba", "cena"]` o własnym wierszu. Poza tabelą ta sama kolumna nazywa się `budzet_a.wartosc`.

## Pole wyliczane

```json
"calculation": { "kind": "product", "operands": ["liczba", "cena"] }
```

| `kind` | Składniki | Znaczenie |
|---|---|---|
| `sum` | co najmniej jeden: kolumna tabeli albo pole poza tabelą | suma w dół kolumny, a przy kilku składnikach ich suma, na przykład koszty z trzech tabel budżetu (`T-31`) |
| `product` | co najmniej dwa | iloczyn, na przykład liczba jednostek razy cena |
| `ratio` | dokładnie dwa | licznik przez mianownik, jako procent |
| `difference` | co najmniej dwa | pierwszy minus reszta |

Deklaratywnie, nie wzorem w tekście. Powód jest podwójny: kreator ma pozwolić wskazać składniki zamiast pisać wzór, a **decyzja D11** każe liczyć kwotę dotacji jako różnicę i wtedy regułę trzeba czytać w obie strony.

Odrzucane jest: pole wyliczane bez `calculation`, składnik nieistniejący, składnik, który nie jest liczbą, pole liczące się samo z siebie, cykl obliczeń oraz sięganie po kolumnę cudzej tabeli inaczej niż przez `sum`.

## Limity

```json
"limits": [
  { "kind": "maxAmount", "basis": "competition.maxGrantAmount" },
  { "kind": "maxPercentOf", "percent": 10, "basis": "dotacja" }
]
```

**Decyzja D12**: limit jest zapisany jako reguła plus to, względem czego jest mierzony, żeby silnik umiał go **odwrócić**. Komunikat ma powiedzieć "możesz wpisać jeszcze 900 zł", a nie "maksymalnie 10%", a tego nie da się wyprowadzić z odpowiedzi prawda albo fałsz.

Limit procentowy podaje procent na jeden z dwóch sposobów, nigdy oba naraz: liczbą w `percent` albo nazwą ustawienia konkursu w `percentFrom` (`competition.maxIndirectCostPercent` albo `competition.maxInstitutionalDevelopmentPercent`, `T-31`). Próg tabeli kosztów B i C jest ustawieniem konkursu, więc formularz się do niego odwołuje, zamiast go przepisywać. Ustawienie, którego operator nie wypełnił, oznacza, że kategoria nie ma w tym konkursie progu, i limit nie jest sprawdzany.

```json
{ "kind": "maxPercentOf", "percentFrom": "competition.maxIndirectCostPercent", "basis": "dotacja" }
```

`basis` to klucz pola formularza albo ustawienie konkursu pisane z przedrostkiem `competition.`. Dopuszczalne ustawienia: `maxGrantAmount`, `minGrantAmount`, `totalPoolAmount`, `maxIndirectCostPercent`, `maxInstitutionalDevelopmentPercent`, `maxAverageAnnualRevenue`. Liczb z konkursu **nie kopiujemy do definicji**: ten sam formularz służy konkursom o różnych limitach, a skopiowana kwota jest tą, która za rok będzie nieprawdziwa.

Samo liczenie i odwracanie limitu robi walidacja odpowiedzi (`T-30`, niżej). Tutaj pilnujemy wyłącznie tego, żeby dało się je wykonać.

## Odmowa

`FormSchemaValidator` zwraca **komplet** powodów naraz, każdy jako para: ścieżka JSON do miejsca w dokumencie (`$.sections[1].fields[3].calculation`) i komunikat po polsku nazywający pole. Pierwsze jest dla kreatora, żeby ustawił operatora na właściwym polu, drugie dla człowieka. Odmowa przy pierwszym błędzie oznaczałaby cztery podejścia do zapisu formularza, który ma cztery usterki.

Sprawdzanie ma dwie fazy i druga rusza dopiero wtedy, gdy pierwsza nie miała zastrzeżeń. Najpierw czytany jest szkielet i każde pole z osobna, potem odwołania między polami. Powód jest praktyczny: w dokumencie, z którego wypadło nieczytelne pole, pozostałe pola stoją pod innymi numerami, więc ścieżka wskazywałaby operatorowi nie to pole, a odwołanie do pola nieczytelnego produkowałoby drugi komunikat o tej samej usterce.

## Wersja kontraktu a wersja formularza

To są dwie różne liczby i mylenie ich kosztuje. `schemaVersion` w dokumencie mówi, **jak czytać ten dokument**, i zmienia się wtedy, gdy zmienia się kontrakt. `form_definitions.version_number` mówi, **które pola widział wnioskodawca**, i rośnie o jeden przy każdej publikacji (`T-25`).

Publikacja dokłada wiersz i nigdy nie nadpisuje poprzedniego, bo wniosek wskazuje na wersję, a nie na konkurs: dokument podmieniony pod wnioskiem oznacza, że złożonej oferty nie da się odtworzyć w postaci, w jakiej ją pokazano. Nowa wersja staje się tą, którą dostaje następny wnioskodawca; wnioski już rozpoczęte zostają przy swojej. Szczegóły i odrzucone warianty w [`architektura.md`](architektura.md).

## Odpowiedzi wnioskodawcy (T-30)

`applications.answers` to **obiekt** z kluczami pól najwyższego poziomu. Lista na korzeniu jest odrzucana, choć kolumna by ją przyjęła, bo lista nie ma kluczy, które dałoby się sprawdzić z formularzem.

| Rodzaj pola | Odpowiedź | Puste |
|---|---|---|
| `shortText`, `longText` | tekst | `""` albo `null` |
| `number`, `amount`, `percent` | liczba JSON | `null` |
| `date` | `"2026-10-25"` | `""` |
| `dateTime` | `"2026-10-25T12:00"`, sekundy dozwolone | `""` |
| `yesNo` | `true` albo `false` | `null` |
| `statement` | `true`; `false` znaczy "nie zaznaczono" | `null` |
| `singleChoice` | wartość opcji | `""` |
| `multipleChoice` | lista wartości opcji, bez powtórzeń | `[]` |
| `file` | `{ "name": "odpis.pdf", "sizeBytes": 204800 }` do czasu `T-32` | `null` |
| `repeatableTable`, `fixedTable` | lista wierszy, wiersz to obiekt z kluczami kolumn, `null` to wiersz jeszcze nieruszony | `null` |
| `calculated` | **brak**: wartość zawsze się wylicza (D11) | |

Walidator (`AnswerValidator`) sprawdza odpowiedzi względem **wersji formularza, na której wniosek rozpoczęto**, nigdy względem najnowszej. Ma dwa poziomy surowości:

- **Szkic** (każdy autozapis) dopuszcza braki i wartości poza zakresem, bo formularz wypełnia się tygodniami, a budżet przekracza limit w połowie wpisywania. Odrzuca to, czego renderer nie mógłby wysłać: klucz spoza formularza, klucz podany dwa razy, wartość złego rodzaju, opcję spoza listy, tekst dłuższy niż `maxLength` (pole samo nie pozwala wpisać więcej), odpowiedź w polu wyliczanym, więcej wierszy niż tabela ma wypisanych albo więcej niż 500 w tabeli zmiennej (chyba że definicja pozwala na więcej przez `maxRows`).
- **Złożenie** (`T-33`) dokłada pola wymagane, `minLength`, `minValue` i `maxValue`, widełki liczby wierszy oraz limity. Pole ukryte warunkiem albo stojące w ukrytej sekcji nie jest wymagane, a to, co zostało w nim sprzed ukrycia, nie jest oceniane.

Odmowa to `ValidationProblemDetails` z kompletem powodów, jeden komunikat na klucz, w kolejności renderera: kształt, potem wymagalność, potem zakres, na końcu limit. Klucz jest dokładnie tym, pod którym renderer trzyma pole: klucz pola albo `tabela[wiersz].kolumna` dla komórki (`cellKey` w `renderer-context.tsx`). Dzięki temu front przypina komunikat do pola bez tłumaczenia kluczy.

Limit na sumie tabeli (pole `sum` z jednym składnikiem `tabela.kolumna`) nazywa tabelę i dostaje drugi komunikat na pozycji, od której suma przekracza granicę, pod kluczem jej komórki (`T-31`). Wnioskodawca ma pięć stron wniosku i musi wiedzieć, gdzie szukać.

Limit podaje wyliczoną granicę, nie regułę: "Przekroczono dopuszczalną wartość o 1000,00 zł. Maksymalnie 9000,00 zł." Kwoty są liczone na pełnej precyzji i zaokrąglane dopiero w komunikacie (D13). Limit względem ustawienia konkursu, którego operator nie wypełnił, nie jest sprawdzany: nie ma granicy do przekroczenia.

## Karta oceny (T-38)

Karta oceny formalnej i merytorycznej to dokument tego samego kontraktu (D16). O tym, czym jest dokument, decyduje **przeznaczenie** wersji (`purpose` na `form_definitions`: `Application`, `FormalEvaluation`, `MeritEvaluation`, od T-50a także `Report`), a nie coś w samym JSON-ie: tę samą strukturę publikuje się inną trasą (`/competitions/{id}/evaluation-cards/{formal|merit}`). `schemaVersion` bez zmian, bo wszystko niżej jest opcjonalne.

| Właściwość | Gdzie | Znaczenie |
|---|---|---|
| `appliesTo` | pole poza tabelą, tylko na karcie | lista rodzajów wnioskodawcy (`Organisation`, `InformalGroup`, `PatronInformalGroup`), którym pole jest zadawane. Brak znaczy "wszystkim". Pole niezadane jest ukryte, niewymagane i nic nie punktuje. Pisownia jak `EntityType` w API, z rozróżnianiem wielkości liter (druga pisownia tego samego enuma to rozjazd typu `R-34`) |
| `points` | `yesNo` poza tabelą, tylko na karcie | ile punktów daje "tak". Takie pole może być składnikiem `sum` (i tylko `sum`) |
| `role: "formalCriterion"` | `yesNo`, tylko karta formalna | kryterium oceny formalnej. Jedyna rola, która może wystąpić wiele razy. Karta jest pozytywna, gdy każde kryterium zadane temu wnioskodawcy ma "tak" |
| `role: "meritScore"` | `calculated` z `sum`, tylko karta merytoryczna | suma punktów kryteriów merytorycznych, obowiązkowa na karcie merytorycznej |
| `role: "strategicScore"` | `calculated` z `sum`, tylko karta merytoryczna | suma kryteriów strategicznych, osobno od merytorycznej, bo próg 2026 jej nie liczy |
| `role: "recommendedGrant"` | `amount`, tylko karta merytoryczna | proponowana kwota dotacji |

Odmowa przy publikacji: `appliesTo`, `points` albo rola oceny na formularzu wniosku (renderer wnioskodawcy ich nie zna, dopóki T-40 nie nauczy silnika frontu), rola wniosku na karcie, kryterium formalne na karcie merytorycznej i odwrotnie, karta formalna bez żadnego kryterium, karta merytoryczna bez `meritScore`.

**Kryteria formalne to pola, nie wiersze tabeli.** Propozycja z T-38.0 miała tabelę o stałej liczbie wierszy, ale warunek "to kryterium dotyczy tylko organizacji" na wierszu tabeli byłby nowym pojęciem w kontrakcie, a na polu już istnieje (`visibleWhen` i teraz `appliesTo`). Uzasadnienie przy kryterium to zwykłe pole `longText` obok.

Odpowiedzi karty mają ten sam kształt co odpowiedzi wniosku (sekcja wyżej) i przechodzą przez ten sam walidator na dwóch poziomach: szkic przy każdym zapisie, całość przy "zapisz i zakończ etap".

## Wzór sprawozdania (T-50a)

Przeznaczenie `Report`, publikowane trasą `/competitions/{id}/report-form`. Wzór sprawozdania to ten sam kontrakt co wniosek, z dwiema właściwościami, które mają sens tylko tam, gdzie jest wniosek, z którego da się coś przepisać: zasada "było i jest" z raportu.

| Właściwość | Gdzie | Znaczenie |
|---|---|---|
| `prefillFrom` | pole, tabela, kolumna tabeli | klucz pola wniosku, z którego wartość przepisuje się raz, przy założeniu sprawozdania. Na tabeli: klucz tabeli wniosku (przepisuje wiersze); na kolumnie: klucz kolumny tej tabeli wniosku. Pole wyliczane wniosku przepisuje się jako liczba, policzona jak we wniosku |
| `readOnly` | pole poza tabelą, kolumna | wnioskodawca widzi wartość jako tekst i nie może jej zmienić; serwer przywraca ją przy każdym zapisie. Usunięty wiersz z wniosku wraca, w wierszu dopisanym komórki `readOnly` są puste |
| `appliesTo` | pole poza tabelą | jak na karcie oceny: trzy warianty sprawozdania (4a, 4b, 4c) w jednym dokumencie |

Odmowa przy publikacji: `readOnly` albo `prefillFrom` poza wzorem sprawozdania; pole `readOnly` bez `prefillFrom` (nie miałoby skąd wziąć wartości) albo wymagane (wnioskodawca nie uzupełni go sam, gdy wniosek zostawił je puste); cała tabela `readOnly` (tylko do odczytu bywają kolumny); kolumna z `prefillFrom` w tabeli bez `prefillFrom`; pole wyliczane z którąkolwiek z tych właściwości; rola albo punkty we wzorze sprawozdania. Klucz z `prefillFrom` nie jest sprawdzany wobec formularza wniosku: formularz może mieć już inną wersję, a klucza, którego nie ma, po prostu nie da się przepisać.

## Czego kontrakt świadomie nie ma

- **Wyrażeń.** Ani w warunku, ani w obliczeniu. Wszystko jest wyborem z listy, bo kreator ma obsłużyć osobę, która mówi o sobie, że nie zna się na technikaliach.
- **Stylów i układu.** Kolejność pól wynika z kolejności w dokumencie, reszta należy do renderera.
- **Treści konkursu.** Kwoty, procenty i terminy są ustawieniami konkursu, a formularz odwołuje się do nich po nazwie.
- **Powiązania między sekcjami po stronie wartości** (pozycja budżetu wskazująca działanie z części II). Mechanizm 4 z [`runbook/pola.md`](runbook/pola.md) nie wszedł do `T-30`: liczenie między sekcjami już działa, bo pole w jednej sekcji czyta pole z innej po kluczu, ale wskazanie wiersza cudzej tabeli wymaga listy wyboru zasilanej wierszami, czyli zmiany kontraktu, a nie reguły walidacji. Kryteria karty tego nie wymieniają.
