# Błędy znalezione w przejściu przedprodukcyjnym

Przebieg z 2026-10-02, scenariusz: [`preproduction-test.md`](preproduction-test.md). Stos lokalny, front `http://localhost:3000`, backend `http://localhost:8080`.

Stan startowy: `health/db` 200, front 200, w bazie zero konkursów i zero wniosków, sześć kont (pięć z `create_test_users.py`).

Przejście objęło ścieżki od A do N na jednej bazie, w jednym przebiegu: dwa wnioski (organizacja i grupa nieformalna), zwrot do poprawy, ocena formalna, dwie karty merytoryczne na wniosek, rozstrzygnięcie, umowa, rezygnacja z wejściem z listy rezerwowej, sprawozdanie z rozliczeniem, próby terminów i próby dostępu.

Format wpisu: adres, co zrobione, co się stało, co miało się stać, a od poprawek także **Stan**.

**Stan na 2026-10-03:** wszystkie dwanaście znalezisk i cztery z siedmiu drobnych obserwacji są poprawione, każde z testem. Bez zmiany zostają świadomie obserwacje 3 i 5 (uzasadnienie przy nich), a obserwacje 6 i 7 były rozbieżnością scenariusza z produktem, więc poprawiony został scenariusz. Jedna rzecz zostaje otwarta jako pytanie do zamawiającego: adres grupy nieformalnej w umowie (znalezisko 11).

---

## Znaleziska

### 1. Hydration mismatch na stronach publicznych (ścieżka A)

- **Adres:** każda strona publiczna, sprawdzone na `/competitions`, `/kontakt`, `/login`.
- **Co zrobione:** wejście na stronę bez konta, konsola przeglądarki.
- **Co się stało:** React zgłasza błąd `A tree hydrated but some attributes of the server rendered HTML didn't match the client properties`. Różni się wartość `nonce` na skrypcie wstrzykiwanym w `<head>` przez `RootLayout`: serwer renderuje `nonce="OWM0Yzk5YWQ..."`, klient widzi `nonce=""`. To skrypt trybu wysokiego kontrastu (`try{if(JSO...`).
- **Co miało się stać:** scenariusz wymaga stron publicznych "bez ostrzeżeń w konsoli przeglądarki".
- **Zasięg:** sam przełącznik kontrastu działa poprawnie, więc to nie psuje funkcji, ale zaśmieca konsolę na każdej stronie i w produkcji oznacza ponowne renderowanie drzewa po stronie klienta.
- **Stan:** Poprawione: skrypt trybu kontrastu dostaje nonce po stronie klienta tak samo jak na serwerze, więc drzewo się nie rozjeżdża.

### 2. Komunikat "Hasła są różne" nie znika po poprawieniu pola (ścieżka B1)

- **Adres:** `/register`.
- **Co zrobione:** wysłanie formularza z różnym powtórzeniem adresu i różnym powtórzeniem hasła, potem poprawienie obu pól (zaznaczenie zawartości, wpisanie poprawnej wartości, wyjście z pola klawiszem Tab).
- **Co się stało:** komunikat przy adresie e-mail zniknął po poprawieniu, a komunikat `Hasła są różne. Wpisz je jeszcze raz.` został pod polem mimo zgodnych wartości (sprawdzone w DOM: oba pola identyczne). Zniknął dopiero po wysłaniu formularza.
- **Co miało się stać:** błąd znika, gdy pole jest poprawione. Scenariusz stawia to wprost jako obietnicę produktu: "błąd pojawia się tylko wtedy, gdy naprawdę jest, i znika, gdy poprawisz".
- **Waga:** drobne, ale myli akurat przy haśle, bo użytkownik nie widzi wpisanej treści i nie ma jak sprawdzić, czy komunikat mówi prawdę.
- **Stan:** Poprawione: oba powtórzenia (rejestracja, nowe hasło z linku) są przeliczane przy każdej zmianie pola od pierwszego wysłania, więc komunikat znika po poprawieniu któregokolwiek z dwóch pól i wraca, gdy znów się rozjadą. Testy: `register-form.test.tsx`, `reset-password-form.test.tsx`.

### 3. Po zapisie kreator pokazuje pusty formularz i komunikat "Konkurs jeszcze nie zapisany" (ścieżka C1)

- **Adres:** `/panel/operator/competitions/new`, po zapisie `/panel/operator/competitions/<id>/edit`.
- **Co zrobione:** wypełnienie kroków od 1.1 do 1.6 kreatora, potem przejście na krok 1.7 Podsumowanie.
- **Co się stało:** adres zmienił się na `/panel/operator/competitions/45c605da-711e-4958-9cbe-25c37201f3b3/edit`, czyli konkurs został zapisany, ale ekran wrócił do kroku 1.1 z **pustymi polami** i nagłówkiem `Konkurs jeszcze nie zapisany.` Żadna z wpisanych danych nie była widoczna.
- **Stan faktyczny:** dane są w bazie. Wiersz w `competitions`: `number = 1/2026`, `title = Kierunek NOWE FIO 2026 (próba)`, `start_date = 2026-10-01 06:00+00`, `end_date = 2026-10-09 21:59+00`, `status = Draft`. Po ręcznym przeładowaniu strony kreator pokazuje komplet danych i banner `Zapisano jako roboczy 2.10.2026, 17:56:52.`
- **Co miało się stać:** po zapisie kreator zostaje na tym samym kroku z wpisanymi danymi i mówi, że zapisał. Scenariusz stawia to wprost: "zapis w trakcie i powrót nie gubi wpisanych danych".
- **Waga:** wysoka. To nie jest utrata danych, tylko jej pozór, a to gorsze: operator widzi pusty formularz i komunikat "nie zapisany", więc naturalną reakcją jest wpisanie wszystkiego jeszcze raz, co kończy się drugim konkursem albo kolizją numeru.
- **Stan:** Poprawione: po pierwszym zapisie kreator zostaje na swoim kroku z wpisanymi danymi i mówi, że zapisał.

### 4. Tabela "Członkowie grupy nieformalnej" nie znika przy wnioskodawcy będącym organizacją (ścieżki D3 i E)

- **Adres:** `/panel/applicant/applications/<id>`, Część I. Dane wnioskodawcy.
- **Co zrobione:** w polu "Wniosek składa" wybrana opcja `A. Młoda albo lokalna organizacja pozarządowa`.
- **Co się stało:** pojawiły się poprawnie pola dla organizacji (rodzaj organizacji, data wpisu do rejestru, roczny przychód), ale **tabela `Członkowie grupy nieformalnej *` została na ekranie**, razem z gwiazdką, dopiskiem `(wymagane)` i trzema wierszami na lidera i dwóch członków.
- **Co miało się stać:** pole ma zniknąć, tak jak znikają pozostałe pola grupy. Sprawdzone w tym samym przebiegu: `Nazwa grupy nieformalnej` oraz `Numer rachunku bankowego lidera grupy` **znikają poprawnie**.
- **Przyczyna, z definicji formularza:** w `backend/seed/forms/application-2026.json` pola `nazwa_grupy` (typ `shortText`) i `czlonkowie_grupy` (typ `fixedTable`) mają **identyczny** warunek `visibleWhen: {field: rodzaj_wnioskodawcy, equalsAnyOf: [PatronInformalGroup, InformalGroup]}`. Znika tylko pierwsze z nich, więc renderer nie stosuje `visibleWhen` do pól typu `fixedTable`.
- **Zasięg:** walidacja jest poprawna, pole nie trafia na listę braków i nie blokuje złożenia. Problem jest w tym, co widzi człowiek: organizacja dostaje do wypełnienia wymaganą tabelę danych osobowych trzech osób, która jej nie dotyczy.
- **Stan:** Poprawione: renderer stosuje `visibleWhen` także do tabel o stałej liczbie wierszy, więc tabela członków znika razem z pozostałymi polami grupy.

### 5. Odmowa złego formatu załącznika podaje formaty, których ten załącznik nie przyjmuje (ścieżka D4)

- **Adres:** `/panel/applicant/applications/<id>`, sekcja Załączniki, wymóg `Statut` ustawiony przez operatora na sam PDF.
- **Co zrobione:** próba wgrania pliku `notatka.txt`, czyli formatu spoza katalogu w ogóle.
- **Co się stało:** komunikat `Niedozwolony format pliku. Dozwolone formaty: PDF, DOC, DOCX, XLS, XLSX, JPG, ODT, ODS.`
- **Co miało się stać:** komunikat ma podać formaty dopuszczone **dla tego wymogu**, czyli sam PDF, tak jak kafelek obok ("Wymagany, formaty: PDF").
- **Dowód, że to niespójność, a nie tylko niezręczne zdanie:** próba wgrania `skan-statutu.jpg`, czyli formatu z tej wypisanej listy, kończy się odmową z poprawnym komunikatem `Załącznik "Statut" przyjmuje tylko: PDF.` System zna właściwe ograniczenie, ale pierwszy komunikat go nie używa.
- **Waga:** średnia. Wnioskodawca, który trafi na pierwszy komunikat, pójdzie zrobić DOCX i wróci po drugiej odmowie. To dokładnie ten telefon do OCWIP, którego scenariusz każe unikać.
- **Stan:** Poprawione: odmowa wymienia formaty dopuszczone dla tego wymogu, a nie cały katalog.

### 6. Potwierdzenie złożenia podaje godzinę w UTC, reszta produktu w czasie polskim (ścieżka D5)

- **Adres:** PDF z `http://localhost:8080/applications/<id>/confirmation` oraz mail "Potwierdzenie złożenia oferty" w logu backendu.
- **Co się stało:** wniosek złożony o 18:16 czasu polskiego. Ekran wnioskodawcy mówi `Wniosek został złożony 02.10.2026, 18:16.`, pełny wniosek w PDF mówi `Data złożenia: 2026-10-02 18:16 (czasu polskiego)`, ale **potwierdzenie w PDF** mówi `Data złożenia (UTC): 2026-10-02 16:16`, a mail do wnioskodawcy `Data złożenia: 2026-10-02 16:16 UTC`.
- **Co miało się stać:** jedna godzina w całym produkcie, w czasie polskim, bo to ten czas rozstrzyga o dotrzymaniu terminu naboru (`Nabór trwa do 09.10.2026 o godzinie 23:59 czasu polskiego`).
- **Waga:** niska technicznie, wyższa prawnie. Potwierdzenie złożenia to dokument, którym wnioskodawca dowodzi, że złożył wniosek w terminie, a ten dokument podaje inną godzinę niż ogłoszenie konkursu. Przy wniosku złożonym tuż przed północą różnica dwóch godzin przenosi datę na dzień wcześniejszy.
- **Uwaga:** oznaczenie `(UTC)` jest obecne, więc to niespójność, nie błąd rachunkowy.
- **Stan:** Poprawione: potwierdzenie w PDF i mail podają godzinę w czasie polskim, tak jak ekran i pełny wniosek.

### 7. Lista wniosków nie ma kolumny z wynikiem oceny formalnej (ścieżki F1 i G)

- **Adres:** `/panel/operator/applications/<id konkursu>`.
- **Co zrobione:** obie oceny formalne zakończone wynikiem "spełnia wymogi formalne", potem powrót na listę wniosków.
- **Co się stało:** nagłówki tabeli to `Lp. | Numer wniosku | Nazwa podmiotu | Rodzaj wnioskodawcy | Tytuł projektu | Całkowity koszt zadania | Wnioskowana kwota | Status | Data złożenia`. Kolumny z wynikiem oceny formalnej **nie ma**, a kolumna `Status` pokazuje dalej `Złożony` dla obu wniosków.
- **Co miało się stać:** scenariusz wymienia wynik oceny formalnej wśród kolumn listy wniosków (F1) i mówi wprost: "Wynik oceny formalnej ma pojawić się w kolumnie na liście wniosków" (G).
- **Gdzie wynik jednak jest:** na liście rankingowej w `/panel/operator/evaluation/<id konkursu>`, w kolumnie `Ocena formalna`.
- **Waga:** niska do średniej. Dane są w systemie, brakuje ich w tym jednym widoku, więc operator pracujący z listy wniosków musi przeskakiwać do listy rankingowej.
- **Stan:** Poprawione: `ApplicationListItem` niesie wynik oceny formalnej (`FormalStanding`, ta sama reguła co na liście rankingowej, `FormalStandingReader`), lista ma kolumnę "Ocena formalna" z sortowaniem od nierozpoczętych, a CSV i PDF tę samą kolumnę. Testy: `ApplicationListEndpointsTests`, `ApplicationListExportTests`, `operator/applications/[competitionId]/page.test.tsx`.

### 8. Suma punktów na karcie merytorycznej jest wyświetlana jako kwota w złotych (ścieżka H3)

- **Adres:** `/panel/reviewer/applications/<id>`, karta oceny merytorycznej, Część I.
- **Co zrobione:** wpisanie punktacji 18 + 14 + 9 + 3.
- **Co się stało:** pole podsumowania z etykietą `Suma (0-50 punktów)` pokazuje **`44,00 zł`**. Nagłówek karty liczy poprawnie i pisze `Suma punktów: 44, kryteria strategiczne: 0.`, więc arytmetyka jest dobra, złe jest formatowanie pola wyliczanego.
- **Co miało się stać:** `44` albo `44 z 50 punktów`. Punkty nie są kwotą.
- **To samo w drugim miejscu tej samej karty:** pole `Punkty za kryteria strategiczne` w Części II pokazuje `1,00 zł` zamiast `1`.
- **Przyczyna prawdopodobna:** pole wyliczane w karcie dziedziczy formatowanie walutowe po polach wyliczanych w budżecie wniosku, gdzie `zł` jest poprawne.
- **Waga:** niska funkcjonalnie, wyższa wizerunkowo: po udostępnieniu kart tę samą kartę widzi wnioskodawca, a na niej jego ocena merytoryczna stoi w złotówkach obok prawdziwej kwoty dotacji. Sprawdzone, że wnioskodawca faktycznie widzi `44,00 zł`.
- **Stan:** Poprawione: pole wyliczane w karcie oceny drukuje punkty jako punkty, nie jako kwotę.

### 9. Potwierdzenie rezygnacji działa od razu, bez pytania o potwierdzenie (ścieżka K)

- **Adres:** `/panel/operator/evaluation/<id konkursu>`, panel "Umowy, rezygnacje i lista rezerwowa".
- **Co zrobione:** jedno kliknięcie w `Potwierdź rezygnację 001`.
- **Co się stało:** wniosek od razu przeszedł w stan `Resigned`, środki wróciły do puli (z 14 300 zł na 20 000 zł), a do wnioskodawcy poszedł mail "Rezygnacja z dotacji: wniosek 001". **Żadnego okna potwierdzenia nie było.**
- **Co miało się stać:** decyzja tej wagi zachowuje się jak pozostałe decyzje nieodwracalne w tym produkcie. Dla porównania, w tym samym przebiegu pytanie o potwierdzenie pojawiło się przy: publikacji konkursu, zamknięciu naboru, rozpoczęciu oceny, zatwierdzeniu wyników, udostępnieniu kart, archiwizacji, złożeniu wniosku, złożeniu sprawozdania, zapisaniu podpisania umowy i zakończeniu każdej karty oceny. Rezygnacja jest jedyną akcją tej wagi bez potwierdzenia.
- **Skutek pomyłki:** cofnięcia w interfejsie nie ma. Wnioskodawca dostaje mail o rezygnacji, której nie zgłaszał, a przyznana kwota wraca do puli.
- **Drobiazg przy okazji:** po rezygnacji, gdy nie ma już żadnego dofinansowanego wniosku, panel pisze `Wszystkie dofinansowane wnioski mają podpisaną umowę.`, co jest logicznie prawdziwe i mylące w praktyce.
- **Stan:** Poprawione: potwierdzenie rezygnacji przechodzi przez okno potwierdzenia, jak pozostałe decyzje nieodwracalne.

### 10. Przyznanie z listy rezerwowej gubi komunikat błędu z serwera (ścieżka K)

- **Adres:** `/panel/operator/evaluation/<id konkursu>`, przycisk `Przyznaj dofinansowanie 002`.
- **Co zrobione:** próba przyznania kwoty większej niż wolne środki w puli.
- **Co się stało:** interfejs napisał tylko `Nie udało się przyznać dofinansowania.` Bez przyczyny i bez podpowiedzi, co zrobić.
- **Co backend faktycznie odpowiada:** `400` z treścią `{"errors":{"awardedGrant":["W puli zostało 20000,00 zł. Kwota nie może być większa."]}}`, czyli gotowy, polski, konkretny komunikat. Interfejs go wyrzuca.
- **Co miało się stać:** komunikat z serwera pod polem kwoty, tak jak działa to w budżecie wniosku u wnioskodawcy ("Przekroczono dopuszczalną wartość o 1000,00 zł. Maksymalnie 7000,00 zł.").
- **Uwaga:** ten sam wzorzec, czyli połknięty komunikat serwera, nie powtórzył się przy składaniu wniosku po terminie. Tam odmowa `409` jest pokazana w całości.
- **Stan:** Poprawione: `apiErrorMessage` bierze komunikaty pól z `fieldErrors`, gdy odmowa walidacyjna nie ma `detail`, więc operator czyta zdanie backendu o pozostałej puli. Testy: `api-client.test.ts`, `resignation-panel.test.tsx`.

### 11. Umowa grupy nieformalnej wymaga rejestru i numeru w rejestrze (ścieżki J i K)

- **Adres:** `/panel/operator/evaluation/<id konkursu>/<id wniosku>`, sekcja Umowa, wniosek 002 złożony przez **grupę nieformalną bez osobowości prawnej**.
- **Co zrobione:** przygotowanie umowy i wypełnienie wszystkich pól, które dla grupy mają sens (reprezentant, funkcja, dane NIW, kontakt, terminy, rachunek lidera, bank, źródło danych).
- **Co się stało:** umowa nie weszła do paczki ZIP, a `braki.txt` wymienił: `002 Grupa Sąsiedzka Zaodrze: Rejestr, Numer w rejestrze`. Formularz umowy pokazuje grupie ten sam komplet czternastu pól co organizacji, w tym `Rejestr`, `Numer w rejestrze` i `Funkcja reprezentanta`.
- **Co miało się stać:** lista pól do wpisania zależy od rodzaju wnioskodawcy, tak jak zależy od niego formularz wniosku (znikają pola organizacji) i karta oceny formalnej (sześć kryteriów zamiast ośmiu). Grupa nieformalna nie ma rejestru ani numeru w rejestrze.
- **Obejście użyte, żeby przejść dalej:** wpisanie `nie dotyczy` w oba pola. To znaczy, że operator wpisuje nieprawdę do umowy, żeby system pozwolił ją wydać.
- **Przy okazji:** pole `Adres wnioskodawcy` w umowie grupy jest puste i drukuje się jako kropki, bo karta podmiotu grupy nieformalnej ma tylko nazwę. Adres lidera jest we wniosku, ale do umowy nie trafia.
- **Waga:** średnia. Blokuje wydanie umowy grupie nieformalnej, a grupy są jedną z dwóch grup docelowych konkursu.
- **Stan:** Poprawione w części blokującej: wzór umowy zna fragment dla wybranych rodzajów wnioskodawcy (`{{#Organisation,PatronInformalGroup}} ... {{/}}`), więc grupa nieformalna nie jest już pytana o rejestr, numer w rejestrze, NIP ani funkcję reprezentanta i da się jej umowę wydać bez wpisywania "nie dotyczy". Adresu grupy system nadal nie zna (karta podmiotu grupy ma tylko nazwę, a kolumn tabeli członków nic nie oznacza rolą), więc wzór 2026 pyta o `{{adres_lidera}}` jako pole do wpisania: **pytanie do zamawiającego**, czy adres lidera ma być polem wniosku zaciąganym do umowy. Zmiana wzoru dotyczy pliku startowego, więc konkurs z zaimportowanym wzorem potrzebuje nowej wersji wzoru (ekran wzoru umowy albo `import-content --contract`). Testy: `TemplatePlaceholdersTests`, `Contract2026TemplateTests`, `ContractEndpointsTests`.

### 12. Zwrot i przyjęcie sprawozdania nie wysyłają maila do wnioskodawcy (ścieżka L2)

- **Adres:** `/panel/operator/evaluation/<id konkursu>/reports/<id sprawozdania>`.
- **Co się stało przy zwrocie:** sprawozdanie przeszło w stan `Zwrócone do poprawy`, powód jest zapisany i widoczny u operatora oraz u wnioskodawcy w panelu, ale **w logu backendu nie pojawił się żaden mail**.
- **To samo przy przyjęciu:** przyjęcie sprawozdania (stan `Przyjęte`, wniosek przechodzi w `Settled`, kwota do zwrotu 100,00 zł) również nie wysyła maila. Wnioskodawca nie dowiaduje się ani o przyjęciu rozliczenia, ani o tym, że ma coś zwrócić.
- **Co miało się stać:** tak samo jak przy zwrocie **wniosku** do poprawy, gdzie system wysyła `Wniosek 001 zwrócony do poprawy` z powodem i terminem. Zwrot sprawozdania to ta sama sytuacja: piłka jest po stronie wnioskodawcy i on musi się o tym dowiedzieć.
- **Skutek:** wnioskodawca dowie się o zwrocie tylko wtedy, gdy sam zajrzy do panelu. Przy sprawozdaniu, które składa się raz na kilka miesięcy, to znaczy, że nie dowie się wcale.
- **Stan:** Poprawione: zwrot sprawozdania wysyła mail z powodem, a przyjęcie mail z kwotą do zwrotu, oba na adres konta, które sprawozdanie złożyło (`ReportService.Mail.cs`). Test: `ReportEndpointsTests`.

---

## Drobne obserwacje

Rzeczy, które nie blokują niczego i nie są błędami w ścisłym sensie, ale ktoś powinien o nich wiedzieć przed wystawieniem na serwer.

1. **Reset hasła bez powtórzenia.** `/reset-password` ma jedno pole `Nowe hasło`, bez `Powtórz hasło`, choć rejestracja i zmiana adresu takiego powtórzenia wymagają. Literówka w nowym haśle kończy się kolejnym resetem.
   **Stan:** poprawione, ekran pyta o nowe hasło dwa razy.
2. **Adres e-mail w parametrze adresu URL.** Link potwierdzający zmianę adresu ma postać `/confirm-email-change?userId=...&email=test.reczny3%40example.org&token=...`. Adres trafia do historii przeglądarki i do logów każdego pośrednika, a `userId` z tokenem by wystarczyły.
   **Stan:** poprawione, adres czeka na koncie (`users.pending_email`), link niesie samo `userId` i `token`, a druga prośba o zmianę unieważnia starszy link. Test: `AccountSettingsTests`.
3. **Przekroczenie puli na liście rankingowej jest w kolorze akcentu**, `rgb(159, 58, 12)`, a nie w czerwieni, której oczekuje scenariusz. Komunikat jest czytelny, ale nie czyta się jako alarm.
   **Stan:** świadomie bez zmiany. Produkt ma jeden kolor błędu z palety OCWIP, sprawdzony na kontrast w obu motywach (`contrast-tokens.test.ts`), a osobna czerwień wymagałaby drugiej pary kolorów i odpowiednika w trybie wysokiego kontrastu, gdzie czerwień na czarnym nie przechodzi AA. Informacja nie stoi na kolorze: komunikat nazywa kwotę przekroczenia i jest pogrubiony (`dostepnosc.md`, 1.4.1). Zmiana palety to decyzja dla zamawiającego, nie poprawka.
4. **Dwa formaty kwot.** Interfejs pisze `20 000,00 zł`, komunikat walidacyjny z backendu `20000,00 zł`.
   **Stan:** poprawione w zdaniach, które czyta człowiek (odmowa kwoty ponad pulę, mail o wyniku, mail o przyznaniu z listy rezerwowej, mail o przyjęciu sprawozdania): idą przez `PolishNumbers.Amount`, czyli tak jak ekrany. CSV i XLSX zostają bez separatora tysięcy świadomie, żeby arkusz czytał je jako liczbę.
5. **Przesunięcie terminu naboru wysyła przypomnienie jeszcze raz.** Po zmianie końca naboru z 05.10 na 02.10 zadanie w tle wysłało drugie przypomnienie, z nowym terminem. Wygląda to na zamierzone (klucz deduplikacji zawiera termin), ale warto wiedzieć, że każda zmiana daty to kolejny mail do wszystkich z rozpoczętym wnioskiem.
   **Stan:** zachowanie zamierzone, bez zmiany: przypomnienie o terminie, który się przesunął, byłoby nieprawdziwe, a klucz deduplikacji z terminem jest tym, co trzyma "najwyżej raz na termin" (T-105).
6. **Liczba oświadczeń.** Scenariusz mówi o trzynastu oświadczeniach w części IV, formularz 2026 ma ich dziesięć dla organizacji i osiem dla grupy nieformalnej. To rozbieżność scenariusza ze wzorem, nie błąd.
   **Stan:** poprawiony scenariusz ([`preproduction-test.md`](preproduction-test.md)), produkt bez zmiany.
7. **Data retencji w klauzuli RODO.** Scenariusz zapowiada datę z kroku 1.4 wprost w klauzuli we wniosku. Klauzula odsyła do "daty wskazanej w ogłoszeniu konkursu", a sama data (31.12.2032) jest na publicznej stronie konkursu. Spójne, tylko inaczej niż opisuje scenariusz.
   **Stan:** poprawiony scenariusz ([`preproduction-test.md`](preproduction-test.md)), produkt bez zmiany.

---

## Co wyszło dobrze i warto o tym wiedzieć

- **Reguła nieujawniania kont trzyma się wszędzie, gdzie trzeba.** Rejestracja na zajęty adres, reset hasła na nieistniejący adres i otwarcie cudzego wniosku dają odpowiedzi nie do odróżnienia od przypadku pozytywnego, a do adresów, których nie ma, nie poszedł żaden mail.
- **Log jest czysty.** Zero haseł, zero treści wniosku, zero numerów rachunku i NIP. Parametry zapytań SQL logują się jako znaki zapytania.
- **Izolacja danych działa w każdej sprawdzonej próbie:** cudzy wniosek, cudzy załącznik, cudzy PDF, cudze panele, konkurs roboczy bez publicznego adresu.
- **Polskie znaki są poprawne we wszystkich wygenerowanych plikach:** PDF wniosku, potwierdzenia, listy wniosków, listy rankingowej i umowy, a CSV wychodzi jako UTF-8 z BOM i średnikiem, czyli otworzy się w Excelu bez zabawy z importem.
- **Przycisk "Wyślij brakujące wiadomości" sam się wyłącza**, gdy komplet poszedł. Scenariusz zakładał kliknięcie i sprawdzenie, że nikt nie dostał drugiego maila; produkt nie dopuszcza nawet do kliknięcia.
- **Lista braków przy przycisku "Złóż wniosek" prowadzi kursorem prosto do brakującego pola.** Sprawdzone na polu "Pomysł na projekt": kliknięcie pozycji przewija ekran i ustawia kursor w tym polu.

---

## Karta wyniku przejścia

Kolumna "Wynik": OK znaczy zgodnie ze scenariuszem.

| Punkt | Wynik | Uwagi |
|---|---|---|
| 0 | OK | health 200, front 200, konta istnieją z rolami |
| A | OK z uwagą | puste stany po polsku, strony informacyjne kompletne, kontrast działa i przeżywa nawigację; znalezisko 1 |
| B1 | OK z uwagą | niezgodne pola nazwane po imieniu, obie zgody wymagane, ta sama odpowiedź dla zajętego adresu; znalezisko 2 |
| B2 | OK | zepsuty token: czytelna odmowa po polsku; dobry link potwierdza; przed potwierdzeniem logowanie odmawia i proponuje nowy link |
| B3 | OK | wylogowanie działa, `/panel/applicant` bez sesji odsyła na `/login?returnUrl=...` |
| B4 | OK | ta sama odpowiedź dla adresu istniejącego i nieistniejącego, zero maili do nieistniejącego, nowe hasło działa, stare odmawia |
| B5 | OK | piąta nieudana próba blokuje, szósta z dobrym hasłem odmawia z informacją o 15 minutach |
| B6 | OK | zmiana hasła nie zrywa sesji, link do zmiany adresu idzie na nowy adres, logowanie nowym adresem działa |
| C1 | BŁĄD | siedem kroków wypełnione, dane zapisane poprawnie w bazie, ale po zapisie ekran wraca do pustego kroku 1.1 z komunikatem "nie zapisany"; znalezisko 3 |
| C2 | OK | import wypisał pięć wersji 1, powtórzenie: "Nothing changed" pięć razy, bez dublowania |
| C3 | OK | formularz 2026 w kreatorze, cztery sekcje, zależności pól opisane, podgląd działa |
| C4 | OK | wzór PDF wgrany do wymogu Statut, publiczny odnośnik odpowiada 200 bez logowania |
| C5 | OK | publikacja przeszła, stan "Trwa nabór", konkurs widoczny publicznie z limitami i osobą kontaktową |
| C6 | OK | kopia 1/2026 na 2/2026 przeniosła ustawienia, formularz, karty oceny i wzór sprawozdania; kopię dało się opublikować bez importu |
| D1 | OK | wejście z publicznej strony, odesłanie na logowanie i powrót na ten sam konkurs; ekran "co przygotować" zgodny z ustawieniami |
| D2 | OK | karta podmiotu zapisana; przy drugim wniosku wypełniona, z przyciskami "Dane są aktualne" i "Popraw" |
| D3 | OK z błędem | autozapis przeżył przeładowanie, sumy i procenty liczą się same, limit 7000 zł i 10 procent kosztów pośrednich blokują z dokładną kwotą, licznik znaków działa, sekcje dostają "(gotowa)" albo "(są błędy)"; znalezisko 4 |
| D4 | OK z błędem | PDF przyjęty, podmiana działa, zły format odrzucony; znalezisko 5. Przeciąganie pliku myszką niesprawdzone |
| D5 | OK z uwagą | podsumowanie z "Popraw" przy każdej części, jedno potwierdzenie z ostrzeżeniem, numer 001, treść zamrożona, PDF z polskimi znakami, mail z numerem; znalezisko 6 |
| E | OK | grupa nieformalna: karta podmiotu zwężona do nazwy, w części I znikają pola organizacji, pojawiają się nazwa grupy, tabela członków i rachunek lidera, w części IV osiem oświadczeń zamiast dziesięciu; wniosek 002 złożony |
| F1 | OK | lista pokazuje tylko złożone, kolumny i sumy się zgadzają, sortowanie i filtr działają, CSV UTF-8 z BOM, PDF z polskimi znakami. Wyszarzonych zakładek nie dało się zobaczyć: każda miała już treść |
| F2 | OK | zwrot z wyborem sekcji, mail z powodem i terminem, u wnioskodawcy odblokowana tylko Część III, po ponownym złożeniu ten sam numer, dwie sumy kontrolne i pełna historia statusów |
| G | OK z uwagą | obie karty formalne zakończone, karta grupy ma sześć kryteriów zamiast ośmiu, po zakończeniu karta zablokowana; znalezisko 7 |
| H1 | OK | dwóch ekspertów, suma punktów, próg 50, strategiczne poza progiem, "Zapisano ustawienia oceny" |
| H2 | OK | obaj recenzenci przypisani do obu wniosków przypisaniem hurtem |
| H3 | OK z błędem | przed deklaracją widać tylko liczbę wniosków, po deklaracji listę; cztery karty wypełnione i zakończone (001: 44 i 44, 002: 30 i 30, kwoty 5700 zł); ekspert nie wchodzi na cudze panele; znalezisko 8 |
| I1 | OK | zamknięcie naboru i start oceny, publiczna strona mówi "Wniosku nie da się teraz rozpocząć" |
| I2 | OK z uwagą | ranking po punktach, kwota przyznana edytowalna wprost na liście, stan puli przelicza się po opuszczeniu pola, przekroczenie puli daje komunikat z kwotą, eksporty PDF, XLSX i CSV z polskimi znakami |
| I3 | OK | zatwierdzenie z ostrzeżeniem, dwa maile (dofinansowany z kwotą, lista rezerwowa), wysyłka brakujących wyłączona po komplecie |
| I4 | OK | udostępnienie kart nieodwracalne i potwierdzone, u wnioskodawcy karta formalna i dwie merytoryczne bez nazwisk ekspertów |
| I5 | OK | publiczne wyniki w dwóch tabelach, po archiwizacji konkurs znika z `/competitions`, jest na `/archive`, a jego strona działa pod tym samym adresem |
| J1 | OK | wzór umowy w wersji 1, podział znaczników na systemowe i do wpisania |
| J2 | OK | wartości zapisane, PDF umowy z polskimi znakami i bez nieuzupełnionych znaczników, u wnioskodawcy "Umowa jest przygotowana do podpisu" |
| J3 | OK | ZIP przed uzupełnieniem pól: sam `braki.txt`; po uzupełnieniu: `umowa-001.pdf` i żadnego `braki.txt` |
| J4 | OK | data z przyszłości odrzucona, data dzisiejsza zapisana po potwierdzeniu, stan "Podpisana 02.10.2026", znacznik daty wypełnił się słownie |
| K | OK z błędami | rezygnacja odnotowana z mailem, środki wróciły do puli, system zaproponował następny wniosek z listy rezerwowej, przyznanie 5700 zł i mail; znaleziska 9, 10, 11 |
| L | OK z błędem | sprawozdanie złożone, zwrócone z powodem, poprawione, przyjęte; rozliczenie policzyło 100 zł kosztów nieuznanych i 100 zł do zwrotu, wniosek w stanie Settled; znalezisko 12 |
| M1 | OK | po terminie odmowa 409 z komunikatem "Nabór został zamknięty 02.10.2026 o godzinie 19:13 czasu polskiego. Wniosku nie można już złożyć.", wersja robocza zostaje |
| M2 | OK | przypomnienie poszło tylko do konta z rozpoczętym i niezłożonym wnioskiem, z terminem i odnośnikiem; po przesunięciu terminu drugie, z nowym terminem (obserwacja 5) |
| N | OK | wszystkie próby dostępu zakończone odmową, log bez haseł i danych wrażliwych |

### Czego nie sprawdzono

- Wyszarzone zakładki konkursu w stanie "trwa nabór": na tym etapie każda zakładka miała już treść, więc przypadek nie wystąpił.
- Przeciąganie pliku myszką do kafelka załącznika: użyto wyboru pliku z okna systemowego.
- Prawdziwa wysyłka SMTP: świadomie poza zakresem przejścia lokalnego, maile czytane z logu backendu.

### Dane zostawione w bazie po przejściu

| Co | Identyfikator |
|---|---|
| Konkurs 1/2026 "Kierunek NOWE FIO 2026 (próba)", archiwalny | `45c605da-711e-4958-9cbe-25c37201f3b3` |
| Konkurs 2/2026 "(próba terminów)", nabór zamknięty | `b96454bd-2e07-4891-a8f8-538305c71c07` |
| Konkurs 3/2026 "Konkurs roboczy do próby dostępu", roboczy | `b68a09ab-fe58-42a9-b9ea-3516daad5cde` |
| Wniosek 001 organizacji, stan Resigned | `be0a8f0e-5ca5-4c09-8799-9c5c304e1341` |
| Wniosek 002 grupy, stan Settled | `859fbcaf-2e06-4b37-970e-dfab91064007` |
| Wniosek roboczy w konkursie 2/2026, stan Draft | `d7463a10-adc8-47c6-a3d2-9247d34dfc4e` |

Konto `test.reczny2@example.org` założone w ścieżce B zostało zmienione na `test.reczny3@example.org` i jest zablokowane po próbie B5 (blokada wygasa 15 minut od 2026-10-02 18:01). Hasła kont testowych z `create_test_users.py` są bez zmian.
