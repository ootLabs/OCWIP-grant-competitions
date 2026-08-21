# Model danych

Stan: **infrastruktura migracji gotowa, encji jeszcze nie ma.** Ten dokument opisuje kierunek i, co ważniejsze, jawnie oddziela ustalenia od założeń.

Kto przyjdzie do projektu za miesiąc, musi umieć odróżnić jedno od drugiego.

## Encje, których świadomie NIE budujemy

**Ocena, Umowa, Sprawozdanie.**

Powód jest konkretny: nie mamy jeszcze realnych wzorów dokumentów. Nie widzieliśmy karty oceny, nie wiemy, ilu recenzentów ocenia jeden wniosek ani czy liczy się suma czy średnia punktów. Nie mamy wzoru umowy ani wzoru sprawozdania.

Modelowanie tego teraz byłoby zgadywaniem, a zgadywanie w modelu danych kosztuje najwięcej. Dokumenty dostaniemy od zamawiającego.

## Encje planowane w pierwszym podejściu

### Użytkownik (`users`)

Konto do logowania. Ma rolę: operator, wnioskodawca albo recenzent.

### Podmiot (`entities`)

Ten, kto składa wniosek. **To nie jest to samo co użytkownik.**

Jedna encja z polem typu, nie trzy tabele. Trzy typy: grupa nieformalna, grupa nieformalna pod patronatem organizacji, organizacja. NIP i adres są wymagane tylko dla organizacji, więc walidacja jest zależna od typu, a nie wymuszona przez NOT NULL na wszystkich kolumnach. Podmiot bez NIP-u to nie błąd danych, to grupa nieformalna.

### Konkurs (`competitions`)

Data i godzina startu oraz zamknięcia (UTC, odcięcie co do minuty), maksymalna kwota dotacji, wymagane załączniki, status.

### Definicja formularza (`form_definitions`)

Struktura formularza jako dokument JSONB plus numer wersji. Wersjonowana, bo operator może edytować formularz w trakcie życia konkursu.

Zawartość tego JSON-a, czyli jak wyglądają sekcje, pola i walidacje, jest osobnym, dużym tematem. Kontrakt tej kolumny powstaje w osobnej karcie.

### Wniosek (`applications`)

Odpowiedzi jako JSONB. Wskazuje na Podmiot oraz **na konkretną wersję definicji formularza, nie na konkurs.** Uzasadnienie w [`architektura.md`](architektura.md).

Brak ograniczenia unikalności na parze podmiot plus konkurs: jeden podmiot może złożyć kilka ofert w jednym konkursie.

### Załącznik (`attachments`)

Na tym etapie tylko metadane pliku i powiązanie z wnioskiem. Fizyczne przechowywanie plików to osobny temat.

## Jawne założenia do potwierdzenia

Są to **założenia**, nie ustalenia. Potwierdzić z zamawiającym.

| Założenie | Skąd się wzięło | Co się stanie, jeśli jest błędne |
|---|---|---|
| Użytkownik do Podmiotu jeden do jednego | Nie wiemy, czy w organizacji wniosek może składać kilka osób z osobnych kont | Trzeba dodać tabelę pośredniczącą i przemyśleć uprawnienia w obrębie podmiotu |
| Jedna rola na użytkownika | Na spotkaniu nie padło nic o osobie, która jest jednocześnie operatorem i recenzentem | Rola przestaje być kolumną, staje się relacją |
| Sprawozdanie jest jedno na wniosek | Standard w małych dotacjach, ale nie ustalone | Relacja jeden do wielu, plus statusy sprawozdań cząstkowych |
| Brak aneksów do umów | Na spotkaniu nie padło ani słowo | Umowa zyskuje wersjonowanie, podobnie jak definicja formularza |

## Reguły, które model musi respektować

1. Zero `ON DELETE CASCADE`. Retencja minimum 5 lat wyklucza twarde kasowanie.
2. Wszystkie znaczniki czasu w UTC.
3. Klucze główne jako UUID (`gen_random_uuid()`), nie sekwencje. Identyfikator wniosku pojawia się w adresie URL, a sekwencja mówi konkurentowi, ile wniosków wpłynęło i pozwala zgadywać cudze.
4. Każde pole trzymające dane wrażliwe (PESEL, NIP, adres osoby fizycznej) oznaczone komentarzem w kodzie.

## Dane testowe

Minimum, potrzebne do testów uprawnień, a nie do ozdoby: jeden operator, dwóch wnioskodawców, jeden konkurs i dwa wnioski, z czego jeden roboczy i jeden złożony.
