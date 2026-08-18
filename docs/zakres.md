# Zakres MVP i świadome cięcia

Zamawiający nie oczekuje systemu tak dużego jak obecna platforma, ale będzie jej używać jako punktu odniesienia. Dlatego ta lista istnieje.

## Zakres MVP

1. Rejestracja użytkownika i organizacji.
2. Tworzenie konkursów przez administratora.
3. Konfigurowalny formularz wniosku i wersje robocze.
4. Elektroniczne złożenie wniosku i walidacja pól.
5. Lista wniosków i statusów.
6. Przydzielanie wniosków oceniającym i karta oceny.
7. Informowanie o wynikach, eksport do PDF.
8. Repozytorium załączników i historia zmian statusu.

## Główne ryzyko

Projekt jest bardzo złożony i ma naturalną skłonność do rozrostu w pełny system grantowy. Trudne są zwłaszcza: uniwersalny kreator formularzy, wiele ról z wyjątkami oraz wymagania archiwizacji i bezpieczeństwa dokumentów.

Sugerowana kolejność cięcia, jeśli zabraknie czasu: pierwsza wypada sprawozdawczość, potem generowanie umowy. Publikacja, składanie i ocena to rdzeń, bez którego system nie ma sensu.

## Czego świadomie NIE robimy

Jeśli zaczynasz to budować, znaczy że wyszedłeś poza zakres. Przerwij i zapytaj.

**1. Parsowanie dokumentów Word.**
Zamawiający przygotowuje dziś formularze w Wordzie z adnotacjami, które pola uzupełnia organizacja. Automatyczne odczytywanie takich plików odrzucone: zbyt duże ryzyko przy tym budżecie czasowym. Zamiast tego budujemy natywny kreator formularzy, co zostało uzgodnione. Temat może wrócić, jeśli zostanie czas.

**2. Podpis elektroniczny umów.**
Umowa jest podpisywana ręcznie, poza systemem. Nie integrujemy się z żadnym dostawcą podpisu.

**3. Wielu organizatorów konkursów.**
Obecna platforma obsługuje ministerstwa, urzędy marszałkowskie i setki organizacji. My obsługujemy jedną: OCWIP. Nie budujemy wielotenantowości.

**4. Aplikacja mobilna.**
Aplikacja webowa ma działać na telefonie w przeglądarce. Nie budujemy natywnej apki.

**5. Moduł odwołań od wyniku.**
Nie wiemy, czy taka procedura istnieje. Pytanie zadane, czeka na odpowiedź.

**6. Aneksy do umów i zmiany budżetu w trakcie realizacji.**
W dotacjach to standard, ale nie zostało zgłoszone jako wymaganie. Pytanie zadane, czeka na odpowiedź.

**7. Migracja danych historycznych z obecnej platformy.**
Retencja 5 lat oznacza, że migracja może być potrzebna. Nie wiemy, czy obecna platforma pozwala na eksport. Jeśli wejdzie, będzie osobnym projektem.

**8. Statystyki i raporty zarządcze.**
Nie są priorytetem dla zamawiającego. Wchodzą po MVP.

## Pytania otwarte

Zadane, czekają na odpowiedź. Do czasu odpowiedzi nie projektujemy tych obszarów.

- Czy w organizacji wniosek może składać kilka osób z osobnych kont? (Przyjęliśmy relację Użytkownik do Podmiotu jeden do jednego.)
- Ilu recenzentów ocenia jeden wniosek i co przy rozbieżnych ocenach?
- Czy ocena jest anonimowa i jak wykluczamy recenzenta przy konflikcie interesów?
- Czy istnieje procedura odwoławcza?
- Czy wniosek można poprawić po złożeniu?
- Limity i dopuszczalne formaty załączników.
- Kto odpowiada za hosting po zakończeniu prac i na czyim koncie stoi system.
