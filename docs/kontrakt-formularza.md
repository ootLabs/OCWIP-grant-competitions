# Kontrakt definicji formularza

Dokument JSON siedzący w kolumnie `form_definitions.definition` (typ `jsonb`). Opisuje **strukturę** formularza: sekcje, pola, warunki i obliczenia. Nie opisuje odpowiedzi wnioskodawcy, bo te są osobnym dokumentem w `applications.answers`.

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
| `sum` | dokładnie jeden, kolumna tabeli | suma w dół kolumny |
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

`basis` to klucz pola formularza albo ustawienie konkursu pisane z przedrostkiem `competition.`. Dopuszczalne ustawienia: `maxGrantAmount`, `minGrantAmount`, `totalPoolAmount`, `maxIndirectCostPercent`, `maxInstitutionalDevelopmentPercent`, `maxAverageAnnualRevenue`. Liczb z konkursu **nie kopiujemy do definicji**: ten sam formularz służy konkursom o różnych limitach, a skopiowana kwota jest tą, która za rok będzie nieprawdziwa.

Samo liczenie i odwracanie limitu to `T-30`. Tutaj pilnujemy wyłącznie tego, żeby dało się je wykonać.

## Odmowa

`FormSchemaValidator` zwraca **komplet** powodów naraz, każdy jako para: ścieżka JSON do miejsca w dokumencie (`$.sections[1].fields[3].calculation`) i komunikat po polsku nazywający pole. Pierwsze jest dla kreatora, żeby ustawił operatora na właściwym polu, drugie dla człowieka. Odmowa przy pierwszym błędzie oznaczałaby cztery podejścia do zapisu formularza, który ma cztery usterki.

## Czego kontrakt świadomie nie ma

- **Wyrażeń.** Ani w warunku, ani w obliczeniu. Wszystko jest wyborem z listy, bo kreator ma obsłużyć osobę, która mówi o sobie, że nie zna się na technikaliach.
- **Stylów i układu.** Kolejność pól wynika z kolejności w dokumencie, reszta należy do renderera.
- **Treści konkursu.** Kwoty, procenty i terminy są ustawieniami konkursu, a formularz odwołuje się do nich po nazwie.
- **Powiązania między sekcjami po stronie wartości** (pozycja budżetu wskazująca działanie z części II). Mechanizm 4 z [`runbook/pola.md`](runbook/pola.md) czeka na kartę `T-30`, bo jest regułą liczenia, nie strukturą. Kontrakt już to udźwignie: pole w jednej sekcji czyta pole z innej po kluczu.
