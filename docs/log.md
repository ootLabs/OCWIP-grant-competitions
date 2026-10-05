# Log projektu

Krótki, gęsty zapis tego, co się wydarzyło i dlaczego. Najnowsze na górze.

**Czytaj 3 do 5 górnych wpisów**, gdy bierzesz pracę, a nie cały plik. To jest log, nie podręcznik: trwała wiedza należy do [`architektura.md`](architektura.md) (decyzje), [`map/`](map/README.md) (gdzie co leży) albo [`konwencje.md`](konwencje.md) (jak piszemy). Jeśli fakt będzie ważny za trzy miesiące, ląduje tam, a tutaj zostaje wskaźnik.

## Format, trzymaj się ściśle

```
## RRRR-MM-DD - krótki tytuł
**Zrobione:** co teraz działa, jedna linia.
**Decyzje:** co wybrano i dlaczego, po jednej linii. Tylko nieoczywiste.
**Uwaga:** co ugryzie następnym razem. Pomiń linię, jeśli nic nie ugryzie.
```

**Limit: 20 wpisów.** Dodajesz dwudziesty pierwszy? Przenieś najstarszy do `docs/log-archiwum/<rok>.md` w tym samym commicie. Limit jest sensem tego pliku: nieograniczony dziennik to plik, którego nikt nie czyta, a każda sesja za niego płaci.

Każdy wpis maksymalnie 5 linii. Nie opowiadaj procesu, nie wypisuj zmienionych plików (git wie), nie powtarzaj tego, co już mówi mapa.

---

## 2026-10-06 - sprawozdanie ma wersje, ocena kosztów nie znika
**Zrobione:** Tabela `report_versions` zapisywana przy zwrocie do poprawy w tej samej transakcji co zmiana statusu, z odpowiedziami, wartościami z wniosku i oceną kosztów tej wersji (S-35). `SubmitAsync` nie przepisuje już `cost_review`.
**Decyzje:** Nowa tabela ma osobne purposes szyfrowania dla `answers` i `prefill`, inaczej niż `reports`, gdzie dzielą jeden (uwaga z części C8 przeglądu). Wpisy oceny kosztów zostają, a o ich aktualności decyduje rozliczenie przy odczycie.
**Uwaga:** `EncryptionAtRestTests` pokrywa od razu nową tabelę, żeby nie powtórzyć luki, przez którą S-08 przetrwał zielone CI: kolumna z konwerterem bez testu w spoczynku.

## 2026-10-05 - zatwierdzone wyniki zamykają ocenę, rezerwa jest kolejką
**Zrobione:** Po zatwierdzeniu wyników odmawiają ustawienia oceny, założenie nowej karty, zapis i zakończenie karty oraz dopisanie i odpięcie aktywnego eksperta (S-05); kartę już otwartą ta sama trasa nadal oddaje do odczytu. Zapis i zakończenie karty idą pod blokadą wiersza (S-07). Promocja z rezerwy bierze następnego w kolejności i nie przyjmuje kwoty ponad wnioskowaną (S-33).
**Decyzje:** Promocja poza kolejnością to 409 (`OutOfOrder`), nie 400: to stan listy, nie zły kształt żądania, a wniosek staje się promowalny, gdy te przed nim zostaną rozstrzygnięte. Wariant domyślny z P23 (blokada bez wyjątku) zrealizowany, pytanie zostaje otwarte.
**Uwaga:** Test współbieżny zapisu i zakończenia przechodzi też bez blokady, bo żądania nie wchodzą sobie w drogę w tym środowisku; blokada broni się konstrukcyjnie, a test łapie tylko grubszą regresję. Dwa istniejące testy wymagały dostosowania do kolejki rezerwy.

## 2026-10-05 - pierwsze złożenie czyta zwalidowaną kartę, nie wiersz
**Zrobione:** Wymagalność załączników i rodzaj wnioskodawcy przy pierwszym złożeniu liczą się z karty po walidacji, czyli z tej, która za chwilę zostaje zamrożona, a nie z wiersza podmiotu (dokończenie S-32). Druga ścieżka S-32, czyli pole formularza z rolą `applicantType` w sekcji odblokowanej zwrotem, ma własny test.
**Uwaga:** Walidator karty zrzuca pola, których dany rodzaj podmiotu nie ma, więc wiersz z zabłąkanym numerem KRS u grupy nieformalnej dawał "jest w KRS" przy pierwszym złożeniu, a poprawka, czytająca już kopię, żądała odpisu z rejestru. Jedno i drugie przypięte testem.

## 2026-10-05 - tożsamość wnioskodawcy zamrożona przy złożeniu
**Zrobione:** Nazwa wnioskodawcy we wszystkich dziewięciu miejscach idzie przez `EntitySnapshots.NameOf`, czyli z kopii karty, a nie z wiersza, który wnioskodawca zmienia w dowolnej chwili (S-06). Rodzaj wnioskodawcy i rejestr przy poprawce też czytają kopię, a poprawka zmieniająca rodzaj jest odrzucana (S-32). Przypisany ekspert nie czyta już uwag zwrotu ani wcześniejszych wersji (druga połowa S-22).
**Decyzje:** Rodzaj wnioskodawcy zamrożony, nie odświeżany: karty oceny wybierają po nim kryteria, więc poprawka nie może przestawić oceny już wykonanej. Czy zwrot ma w ogóle móc zmienić rodzaj, to pytanie do zamawiającego, bo wymaga odświeżenia całej kopii karty.
**Uwaga:** `ResultsArchiveTests` robił z podmiotu grupę nieformalną zmianą wiersza po złożeniu, czyli opisywał dokładnie to, co S-06 odcina; setup ustawia teraz także kopię, asercje bez zmian.

## 2026-10-05 - dziennik odczytów po typie odpowiedzi, zastąpiony załącznik nie wychodzi
**Zrobione:** Filtr `personal_data_reads` na ośmiu trasach, które oddawały wniosek, załącznik, umowę albo sprawozdanie i nie były logowane, bo nie były GET-ami (S-36). Pobranie załącznika serwuje tylko wersję obowiązującą (S-22).
**Decyzje:** Kryterium dla nowej trasy to typ odpowiedzi, nie czasownik HTTP: sporządzenie umowy, która już istnieje, oddaje jej wartości bez zmiany wiersza, więc było cichym odczytem. Zastąpiony plik zostaje w bazie i na dysku (reguła 5), ale API go nie wydaje: kryterium T-32 mówi o miękkim usunięciu, nie o serwowaniu.
**Uwaga:** Test `An_applicant_uploads_downloads_and_replaces_their_own_attachment` przypinał poprzednie zachowanie i został przepisany; retencji pilnuje teraz mocniej, bo sprawdza plik na wolumenie, a nie odpowiedź trasy.

## 2026-10-05 - dane wrażliwe: karta oceny, sprawozdanie, rotacja klucza
**Zrobione:** `evaluations.answers` szyfruje pola oznaczone na karcie jako wrażliwe (S-34, czwarta i ostatnia kolumna z odpowiedziami bez tej ścieżki), wzór sprawozdania oznacza cztery pola osobowe (S-08), a `reencrypt-data` przepisuje też karty oceny i nie przerywa się na sprawozdaniu ze wzorem spoza dzisiejszego kontraktu (S-37).
**Decyzje:** Oznaczeń we wzorze karty formalnej nie dopisujemy sami, bo to treść zamawiającego: pytanie P24 w [`runbook/pytania.md`](runbook/pytania.md). Mechanizm działa niezależnie od odpowiedzi.
**Uwaga:** Rotacja kończyła się dotąd z resztą bazy pod nowym kluczem i sprawozdaniami pod starym, więc po wdrożeniu tej zmiany uruchom `reencrypt-data` jeszcze raz, zanim wycofasz klucz. `EncryptionAtRestTests` pokrywa teraz wszystkie kolumny z konwerterem, nie pięć z dziewięciu.

## 2026-10-04 - maile kont przez kolejkę, czas odpowiedzi nie zdradza konta
**Zrobione:** Weryfikacja i reset hasła oddają mail do kolejki w pamięci (`IAccountMailQueue`), więc `/register`, `/forgot-password` i `/resend-verification` odpowiadają tak samo szybko dla znanego i nieznanego adresu; wcześniej znany adres czekał na przekaźnik SMTP.
**Decyzje:** Błąd przekaźnika nie wraca już jako 500 (kolejka próbuje trzy razy, potem loguje temat), więc człowiek prosi o mail jeszcze raz. Uzasadnienie w [`architektura.md`](architektura.md), sekcja o rejestracji.
**Uwaga:** Nowy test `Forgot_password_does_not_wait_for_the_mail_relay` potrzebuje PostgreSQL, bez bazy jest pomijany.

## 2026-10-03 - dokumentacja znormalizowana, jedna lista w jednym pliku
**Zrobione:** Pytania do zamawiającego z czterech miejsc zebrane w [`runbook/pytania.md`](runbook/pytania.md) (paczka `PK-A` do `PK-P` i pytania `P1` do `P22`, każde z odnośnikiem do założenia i blokera). Scenariusz przejścia ręcznego istniał w dwóch identycznych kopiach (`testGUI.md` i `preproduction-test.md`), został jeden: [`przejscie-gui.md`](przejscie-gui.md). Dwa dzienniki błędów z przejść połączone w [`przejscie-gui-bledy.md`](przejscie-gui-bledy.md), przebieg po przebiegu, bez przenumerowania. Warunki ukończenia zostały tylko w `AGENTS.md`; runbook, `CONTRIBUTING.md` i szablon PR odsyłają tam.
**Decyzje:** Identyfikatorów nie przenumerowujemy (`PK`, `P`, `B-GUI`, znaleziska), bo krążą po kartach Trello i po komentarzach w kodzie. Nowa reguła w `AGENTS.md`: jedna lista w jednym pliku, a brakujące rzeczy dopisujemy w pliku kanonicznym, nie obok.
**Uwaga:** Poprawione przy okazji: 97 zepsutych odnośników w `log-archiwum/2026.md` i `map/backend.md` (plik przeniesiony o katalog niżej, linki zostały), liczba stanów wniosku w `proces.md` (dziewięć, nie siedem) i "administrator" w `zakres.md`, którego w kodzie nie ma. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-10-03 - dziennik przejścia GUI zamknięty, pytanie P21 zapisane
**Zrobione:** Dwa ostatnie otwarte znaleziska z [`przejscie-gui-bledy.md`](przejscie-gui-bledy.md) poprawione: etykiety pól umowy mają polską pisownię (`BlankLabels`, nazwa spoza słownika nadal generuje etykietę), a sprawozdanie czeka z przyciskiem na komplet, z listą braków prowadzącą kursorem do pola jak we wniosku. Pozostałe szesnaście przejrzane w kodzie i opisane stanem w tabeli znalezisk.
**Decyzje:** Fokus po kliknięciu braku wydzielony do wspólnego `useFieldFocus`, bo wniosek i sprawozdanie potrzebują tego samego. Adres grupy nieformalnej w umowie zostaje pytaniem P21 do zamawiającego ([`runbook/decyzje.md`](runbook/decyzje.md), założenie ZR-19), a nie wymyślonym polem.
**Uwaga:** Seria `P` (pytania do zamawiającego) ma teraz dwa miejsca: P9 do P20 w komentarzach blokerów na Trello, P21 i następne w `runbook/decyzje.md`. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-10-03 - znaleziska z przejścia przedprodukcyjnego poprawione
**Zrobione:** Dwanaście znalezisk z [`przejscie-gui-bledy.md`](przejscie-gui-bledy.md) i cztery drobne obserwacje poprawione, każde z testem: komunikat o niezgodnych powtórzeniach znika po poprawieniu pola, lista wniosków i jej eksporty mają kolumnę oceny formalnej, ekran pokazuje komunikat walidacyjny backendu, umowa grupy nieformalnej nie żąda rejestru i NIP-u, zwrot i przyjęcie sprawozdania wysyłają mail, adres wychodzi z linku potwierdzającego zmianę e-maila, a kwoty w zdaniach dla ludzi są grupowane jak na ekranach.
**Decyzje:** Fragment wzoru umowy dla wybranych rodzajów wnioskodawcy (`{{#Organisation,...}} ... {{/}}`), reguła wyniku formalnej w jednym `FormalStandingReader`, adres oczekujący w `users.pending_email` zamiast parametru w linku, dwa formaty kwoty (zdanie kontra arkusz). Uzasadnienia w [`architektura.md`](architektura.md). Obserwacje 3 i 5 świadomie bez zmiany, 6 i 7 były błędem scenariusza, nie produktu.
**Uwaga:** Wzór umowy 2026 zmienił się w pliku startowym, więc konkurs z już zaimportowanym wzorem potrzebuje nowej wersji (ekran wzoru albo `import-content --contract`). Adres grupy nieformalnej w umowie zostaje pytaniem do zamawiającego: na razie operator wpisuje `{{adres_lidera}}` ręcznie. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-30 - przejście ręczne GUI przed wystawieniem na serwer
**Zrobione:** Cały cykl konkursu wyklikany w przeglądarce w jednym przebiegu na jednej bazie, od pustego systemu do rozliczonej dotacji. Scenariusz siedzi w [`przejscie-gui.md`](przejscie-gui.md), dziennik porażek w [`przejscie-gui-bledy.md`](przejscie-gui-bledy.md). Żadna ścieżka nie została zablokowana.
**Decyzje:** Z osiemnastu znalezisk dwanaście poprawionych na tej gałęzi (B-GUI-02 do B-GUI-09, 11, 12, 15, 16), B-GUI-01 był warunkiem środowiska, nie usterką produktu. Pięć zostaje otwartych: B-GUI-10, 13, 14, 17, 18.
**Uwaga:** Przejście zostawiło dane w bazie (cztery konkursy, sześć kont), więc powtórka chce świeżego wolumenu. Na Windows import treści startowej wymaga `MSYS_NO_PATHCONV=1`, inaczej Git Bash przepisuje `/src/seed/...` na ścieżkę Windows.

## 2026-09-30 - zapis do wniosku pod blokadą i tylko dla wnioskodawcy
**Zrobione:** Autozapis i załączniki biorą blokadę wiersza na czas sprawdzeń, złożenie odmawia (409), gdy odpowiedzi zmieniły się po walidacji, a zapis do wniosku wymaga roli wnioskodawcy. Ekspert nie zostaje przypisany do szkicu, a podmiana załącznika trzyma się formatów wymogu.
**Decyzje:** Przy rozjeździe odpowiedzi 409 zamiast cichego zamrożenia: zamrożonej wersji wnioskodawca już nie poprawi. Uzasadnienie w [`architektura.md`](architektura.md).
**Uwaga:** Czystego wyścigu dwóch równoległych żądań nie ma w testach, bo byłby niestabilny; sprawdzana jest bramka, nie splot. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-30 - rejestracja pyta o adres i hasło dwa razy (T-124, R-19)
**Zrobione:** Formularz rejestracji ma powtórzenie adresu i hasła, sprawdzane przy wysłaniu; niezgodność zatrzymuje żądanie i mówi to przy właściwym polu. R-19 zostaje otwarte wyłącznie na telefonie kontaktowym.
**Decyzje:** Powtórzenia zostają na froncie, `RegisterRequest` bez zmian: to pytanie "czy na pewno to wpisałeś", a nie dana o człowieku. Adres porównywany bez rozróżniania wielkości liter (jeden indeks, jedno konto), hasło dosłownie. Uzasadnienie w [`architektura.md`](architektura.md) (T-124).
**Uwaga:** `getByLabel` w Playwright dopasowuje po fragmencie i bez wielkości liter, więc "Adres e-mail" łapie też "Powtórz adres e-mail". Scenariusze e2e pytają teraz o etykietę dokładną; następna etykieta zaczynająca się od istniejącej zepsuje je tak samo.

## 2026-09-30 - nieczytelne ciało JSON kończy się 400, nie 500 (T-123, R-43)
**Zrobione:** Ucięty JSON, pole o złym typie i bajty spoza UTF-8 na dowolnej trasie z ciałem dają 400 jako ProblemDetails z polskim komunikatem, a w logu nie ma wpisu `Error`, więc monitoring (T-116) ani test obciążenia (T-118) nie liczą cudzego skanera jako awarii.
**Decyzje:** `IExceptionHandler` oddaje status z samego wyjątku zamiast stałego 400, więc ciało ponad limit Kestrela też przestaje być 500 (413). Komunikat stały, bo wyjątek potrafi zacytować ciało. Uzasadnienie w [`architektura.md`](architektura.md) (T-111).

## 2026-09-29 - kreator buduje formularz od zera i przestawia sekcje (T-26a)
**Zrobione:** Operator dodaje, usuwa i przestawia sekcje, zaczyna formularz od pustej sekcji zamiast kopiować konkurs, i nadaje wiersze tabeli o stałej liczbie wierszy, której kreator dotąd potrafił tylko dodać i nigdy naprawić. Ruch i usunięcie sekcji są zablokowane zdaniem o tym, co by się zepsuło, zamiast ścieżką JSON przy publikacji.
**Decyzje:** Blokada ruchu liczy dokument PO ruchu i odejmuje naruszenia, które dokument miał wcześniej, zamiast zakazywać ruchu sekcji z warunkiem: formularz zepsuty wcześniej nie zamraża się w miejscu. Uzasadnienie w [`architektura.md`](architektura.md) (T-26a).
**Uwaga:** Karta ruszona bez odpowiedzi na cztery pytania z B-10, decyzją Piotra. B-10 zostaje otwarty i zawęzi ten kreator, a nie przestawi.

## 2026-09-29 - umowa drukuje stronę ze złożenia, PDF nie pada na wcięciu
**Zrobione:** Nazwa, NIP i adres na umowie pochodzą z kopii karty zapisanej przy złożeniu, więc podpisana umowa drukuje się tak samo po zmianie karty. Wcięcie szersze niż pół linii nie wywraca już PDF-u wniosku, umowy ani pakietu umów. Zapis wartości umowy bez `values` to 400, rezygnacja wycofuje nieprzyjęte sprawozdanie, a podpisania nie da się zapisać na wycofanej umowie ani przy wniosku, który nie jest już dofinansowany.
**Uwaga:** Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-29 - testy PDF, pakietu umów i CSP mówią to, co obiecują
**Zrobione:** Test podzbioru czcionki sprawdza każdą składową glifu złożonego, test pakietu umów także brak wniosku odrzuconego, a naruszenie CSP w e2e oblewa test, który je spowodował. Nieaktualne komentarze o czcionce w całości poprawione.
**Decyzje:** Limit kosztów pośrednich w sprawozdaniu (od czego liczony, czy nadwyżka jest nieuznana sama) to pytanie do OCWIP, zapisane jako R-41; do odpowiedzi nadwyżkę odmawia operator.
**Uwaga:** Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-29 - przeszyfrowanie i log odczytów bez cichych luk
**Zrobione:** `reencrypt-data` czyta flagi `sensitive` z zapisanej definicji, gdy wersja formularza nie przechodzi dzisiejszego kontraktu, zamiast przepisać wrażliwe odpowiedzi jawnym tekstem. Log odczytów sprawdza wartość trasy przy starcie, nie liczy Forbid i przekierowań jako odczytu, a test trzyma listę ośmiu logowanych endpointów. Compose produkcyjne ma drugi klucz na czas rotacji.
**Decyzje:** Identyfikator wiersza poza danymi powiązanymi szyfrowania: podmiana między wierszami wymaga zapisu do bazy, a z nim atakujący zmienia i tak jawne kolumny. Uzasadnienie w [`architektura.md`](architektura.md) (T-47a).
**Uwaga:** Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-29 - konta zespołu, zmiana adresu, kopia i wzory bez luk
**Zrobione:** `reactivate-account` cofa wyłączenie konta, a ostatniego aktywnego operatora `deactivate-account` nie wyłączy. Zmiana adresu zapisuje adres i nazwę konta jednym zapisem, a adres zajęty dostaje powiadomienie bez linku, więc oba przypadki trwają tyle samo. Kopia konkursu pomija kontaktowego, który nie jest już operatorem, a dwie podmiany wzoru naraz idą po kolei.
**Decyzje:** Ponowna akceptacja regulaminu po jego zmianie to pytanie do OCWIP i IOD, zapisane jako R-40, nie poprawka.
**Uwaga:** Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-29 - terminy umów, maile po zapisie i odporny harmonogram
**Zrobione:** Awans z listy rezerwowej ma własne 14 dni i własne przypomnienie, a zadanie patrzy tylko na terminy z ostatniego tygodnia. Odmowa maila po rezygnacji albo awansie zostawia zmianę i mówi `mailSent: false` zamiast 500; mail o rezygnacji podaje prawdziwą przyczynę. Wysyłka SMTP ma limit czasu, pętlę zadań kończy tylko zatrzymanie hosta, a temat przypomnienia o naborze podaje chwilę końca.
**Decyzje:** Termin umowy od ostatniego przejścia na `Funded` w historii: dla awansowanego wynikiem jest awans. Założenie do potwierdzenia z OCWIP, opisane w [`architektura.md`](architektura.md) (T-109).
**Uwaga:** Log przekroczył limit, najstarszy wpis w archiwum.
