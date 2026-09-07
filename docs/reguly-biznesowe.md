# Role, typy podmiotów i twarde reguły biznesowe

Reguły ustalone na etapie analizy wymagań. Są wiążące i wracają w wielu kartach. Zmiana którejkolwiek z nich wymaga uzgodnienia z zamawiającym i wpisu w [`log.md`](log.md).

## Role w systemie

Trzy role widzą trzy różne systemy. Rola jest cechą użytkownika zapisaną w bazie, a nie czymś wywnioskowanym w widoku.

| Rola | Co widzi |
|---|---|
| **Operator** (pracownik OCWIP) | Wszystko: wszystkie konkursy, wszystkie wnioski, wszystkie umowy, na bieżąco w trakcie naboru |
| **Wnioskodawca** | Wyłącznie swoje konto i swoje wnioski |
| **Recenzent** | Wyłącznie wnioski przypisane mu przez operatora |

Model dopuszcza wielu operatorów: konkursy po stronie OCWIP obsługuje więcej niż jedna osoba. Na kolumnie roli nie ma żadnego ograniczenia unikalności i pilnuje tego test dowodzący nieobecności, a nie komentarz.

**Roli operatora nie nadaje się z interfejsu.** Operator widzi dane osobowe wszystkich organizacji. Jeśli w aplikacji istnieje ekran nadający tę rolę, istnieje też droga, żeby ją zdobyć przez błąd w uprawnieniach. Nadajemy ją komendą administracyjną albo wprost w bazie:

```bash
docker compose exec backend dotnet run --project src/Ocwip.Api/Ocwip.Api.csproj \
  --no-launch-profile -- grant-role --email adres@example.org --role Operator
```

Komenda działa w obie strony (`--role Applicant` odbiera rolę), nie zakłada konta i nie rusza konta dezaktywowanego. Uzasadnienie i szczegóły: [`architektura.md`](architektura.md).

## Typy podmiotów

1. Grupa nieformalna: trzy osoby fizyczne, samodzielnie.
2. Grupa nieformalna pod patronatem organizacji.
3. Organizacja (NIP, adres).

**Ważne:** z perspektywy konta różnica jest kosmetyczna, bo podmiot i tak podaje albo adres organizacyjny, albo prywatny. Różnice siedzą w DANYCH WE WNIOSKU, nie w modelu konta.

Wniosek projektowy: nie budujemy trzech ścieżek rejestracji ani trzech tabel. Jedna encja Podmiot z polem typu, a walidacja NIP-u i adresu jest zależna od typu, a nie wymuszona przez NOT NULL na wszystkich kolumnach. Podmiot bez NIP-u to nie jest błąd danych, to grupa nieformalna.

## Twarde reguły

1. **Platforma pokazuje wyłącznie konkursy OCWIP.** To główna wada obecnego rozwiązania i główny powód, dla którego ten projekt istnieje.
2. **Wnioskodawca nigdy nie widzi cudzego wniosku.** Reguła bez testu automatycznego to życzenie, nie reguła. Test podmiany identyfikatora w adresie jest obowiązkowy.
3. **Termin zamknięcia konkursu tnie co do minuty.** Wejście minutę po terminie oznacza brak możliwości złożenia wniosku. Czas trzymamy w UTC, konwersja na czas lokalny dzieje się na brzegach.
4. **Jeden podmiot może złożyć kilka ofert w jednym konkursie.** Niczego tu nie blokujemy. Żadnego ograniczenia unikalności na parze podmiot plus konkurs.
5. **Wersje robocze i autozapis to wymóg, nie udogodnienie.** Wniosek ma 5 do 6 stron i nie jest wypełniany za jednym posiedzeniem.
6. **OCWIP musi móc samodzielnie tworzyć i modyfikować formularze, bez programisty.** To determinuje całą architekturę kreatora: struktura formularza jest DANYMI w bazie, a nie klasami w kodzie.
7. **W danych są PESEL-e** (pojawiają się przy umowach). Podnosi to poprzeczkę bezpieczeństwa i nie da się tego dokleić na końcu.
8. **Retencja minimum 5 lat.** Wyklucza twarde kasowanie danych. Operator "usuwa" konkurs tylko w sensie oznaczenia go jako nieaktywny.
9. **Limit kwoty dotacji jest pilnowany przy budżecie wniosku.** Przekroczenie limitu ma dać komunikat wskazujący konkretną pozycję budżetu, a nie ogólny błąd formularza.
10. **Dostępność na poziomie WCAG AA.** OCWIP jest organizacją wydającą środki publiczne, a ich własna strona ma tryb wysokiego kontrastu. To prawdopodobnie wymóg formalny, nie nasza dobra wola.
11. **Responsywność nie jest opcjonalna.** Wśród wnioskodawców są grupy nieformalne bez biura i firmowego sprzętu. Dla części z nich telefon będzie jedynym urządzeniem.

## Reguły wynikające z bezpieczeństwa

Pełna lista w [`AGENTS.md`](../AGENTS.md), sekcja "Bezpieczeństwo i dane osobowe". Skrót:

- Brak reguły autoryzacji oznacza brak dostępu.
- Odmowa dostępu to 403, nie 500 i nie pusta strona.
- Nie ujawniamy, czy konto o danym adresie istnieje.
- Nie logujemy haseł ani danych wrażliwych.
