# Kontekst projektu

Podsumowanie analizy wymagań. Przeczytaj przed wzięciem pierwszej karty.

## Zamawiający

Opolskie Centrum Wspierania Inicjatyw Pozarządowych (OCWIP), organizacja pozarządowa działająca na terenie województwa opolskiego. Pozyskuje środki zewnętrzne i redystrybuuje je w formie małych dotacji.

**OCWIP jest ORGANIZATOREM konkursu, nie wnioskodawcą.** To najczęstsze nieporozumienie w tym projekcie. Wnioskodawcami są inne organizacje i grupy nieformalne, które startują po dotację.

Dane kontaktowe i ustalenia organizacyjne trzymamy poza repozytorium.

## Problem

Cały cykl konkursowy obsługuje dziś zewnętrzna platforma Witkac.pl. Dwa powody, dla których ten projekt powstaje:

1. **Koszt utrzymania zewnętrznej platformy** ogranicza liczbę źródeł finansowania, po które organizacja może startować.
2. **Witkac jest ogólnopolski.** Wnioskodawca po zalogowaniu widzi konkursy z całej Polski, w większości takie, w których i tak nie może startować.

## Co budujemy

Własną platformę obsługującą pełny cykl życia konkursu dotacyjnego, pokazującą wyłącznie konkursy OCWIP.

## Cykl życia konkursu (to jest cały produkt)

1. **Ogłoszenie.** OCWIP tworzy konkurs, ustawia parametry (terminy, limit kwoty, wymagane załączniki) i publikuje.
2. **Wnioski.** Podmiot zakłada konto, wypełnia formularz, zapisuje wersje robocze, składa ofertę.
3. **Ocena.** Operator przypisuje wnioski recenzentom, ci wypełniają karty oceny, system układa listę rankingową według punktów.
4. **Umowy.** Operator ręcznie przypisuje kwoty dotacji i jednym kliknięciem generuje umowy ze wzoru. Umowa zaciąga dane z wniosku. Podpis odbywa się poza systemem.
5. **Sprawozdanie.** Po realizacji projektu podmiot składa sprawozdanie w systemie.
6. **Archiwum.** Wszystko przechowywane minimum 5 lat, eksport PDF i DOCX.

## Skala

Dwa aktywne konkursy z tego samego źródła finansowania, różniące się logotypami i kilkoma wytycznymi. Rzędu 120 ofert na konkurs. Formularz wniosku ma 5 do 6 stron.

Te liczby są małe i to jest dobra wiadomość: nie potrzebujemy tu skalowania, potrzebujemy poprawności i bezpieczeństwa danych.

## Punkt docelowy

Na stronie ocwip.pl w menu wisi pozycja "Generator wniosków", która dziś prowadzi na witkac.pl. Docelowo ma prowadzić do naszej aplikacji, a wnioskodawca ma kliknąć i nie poczuć, że wylądował gdzie indziej.

## Jak rozmawiamy z zamawiającym

Po stronie zamawiającego pracują osoby nietechniczne, opisujące system językiem obecnego narzędzia. Trzy praktyczne wnioski:

- Rozmowa uda się na obrazkach, nie na diagramach klas ani na schemacie bazy.
- Używamy ich słownictwa, nie swojego. Zobacz [`slownik.md`](slownik.md).
- Prostota panelu operatora jest wymaganiem funkcjonalnym, nie kwestią gustu.

## Czego nam brakuje

Nie mamy jeszcze realnych dokumentów: wzoru wniosku, karty oceny, wzoru umowy ani wzoru sprawozdania.

Dopóki tych dokumentów nie ma, **nie modelujemy encji Ocena, Umowa ani Sprawozdanie.** Zgadywanie w modelu danych kosztuje najwięcej. Zobacz [`model-danych.md`](model-danych.md).

## Źródła

Zadania i bieżące ustalenia: tablica Trello "OCWIP | Generator konkursów". Notatki ze spotkań i materiały od zamawiającego trzymamy poza repozytorium.
