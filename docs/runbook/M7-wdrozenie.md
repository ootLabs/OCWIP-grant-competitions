# M7 · Zgodność i wdrożenie

Karty domykające. Dwie z nich blokują wdrożenie twardo: bez `T-47` system z PESEL-ami nie ma prawa stanąć pod publicznym adresem, a bez `T-36` z M4 nie ma prawa wyjść poza lokalną maszynę.

---

## T-46 [P0 / Frontend] Audyt dostępności WCAG AA

Karta: <https://trello.com/c/GbFFOBnp>

**Kontekst.** OCWIP jest organizacją wydającą środki publiczne, a ich własna strona ma tryb wysokiego kontrastu, czyli sami traktują dostępność poważnie. Zgodność WCAG na poziomie AA jest tu prawdopodobnie wymogiem formalnym, a nie naszą dobrą wolą.

**Zakres.** Przejście po wszystkich ekranach produkcyjnych: kontrast, widoczny focus, obsługa z klawiatury, etykiety pól, komunikaty błędów odczytywalne przez czytnik ekranu, sensowna struktura nagłówków.

**Dlaczego to karta domykająca, a nie początkowa.** Bo podstawy wbudowaliśmy już w T-15.1, T-23 i T-28. Tutaj sprawdzamy komplet i łapiemy to, co przeciekło. Gdyby ta karta była jedynym miejscem, w którym myślimy o dostępności, byłaby osobnym projektem, a nie kartą.

**Priorytet ekranów.** Formularz wniosku, publiczna lista konkursów i logowanie. Tam trafiają ludzie z zewnątrz, w tym grupy nieformalne wchodzące z telefonu.

**Zależności.** Blokuje nas: T-34, T-35, T-23. Blokujemy: wdrożenie produkcyjne.

**Kryteria akceptacji.** Checklista pusta, wpisujesz na kartę tę:

- [ ] Każdy ekran produkcyjny przeszedł audyt, wynik zapisany jako lista ustaleń, nie jako wrażenie
- [ ] Kontrast sprawdzony narzędziem (`frontend/lib/contrast.ts`), nie na oko
- [ ] Focus widoczny na każdym elemencie interaktywnym
- [ ] Cała aplikacja przechodzi się klawiaturą w sensownej kolejności
- [ ] Każde pole ma widoczną etykietę, nie tylko podpowiedź w środku pola
- [ ] Błąd opisany tekstem, nie samym czerwonym obramowaniem
- [ ] Struktura nagłówków na każdej stronie jest hierarchią, nie doborem rozmiaru
- [ ] Przeciąganie plików myszką nigdy nie jest jedyną drogą dodania załącznika
- [ ] Tryb wysokiego kontrastu działa na wszystkich ekranach, nie tylko na stronie tokenów

**Uzupełnienie z raportu.** Budujemy zgodnie z **WCAG 2.1 na poziomie AA**. Raport podaje wprost, że przy zadaniu finansowanym ze środków publicznych zwykle wraca to jako wymóg w umowie z grantodawcą, a wśród wnioskodawców są organizacje osób z niepełnosprawnościami. Praktyczne konsekwencje dla ścieżki wnioskodawcy są w kryteriach wyżej i to nie są sugestie.

---

## T-47 [P0 / Backend] Ochrona danych wrażliwych: szyfrowanie, logi, retencja

Karta: <https://trello.com/c/tG3SiRzy> · Zablokowane przez **B-05** w części formalnej.

**Kontekst.** System przetwarza dane organizacji i osób fizycznych, a przy umowach pojawiają się PESEL-e. Decyzja D8 mówi, że formalna strona RODO leży po stronie OCWIP, ale techniczna ochrona tych danych leży po naszej. Tego nie da się dokleić na końcu, dlatego od pierwszego dnia oznaczaliśmy wrażliwe pola komentarzem w kodzie.

**Zakres.** Szyfrowanie pól z danymi wrażliwymi, przegląd logów pod kątem wycieków, mechanizm retencji i oznaczania danych jako nieaktywne zamiast kasowania, przegląd nagłówków bezpieczeństwa aplikacji.

**Co konkretnie sprawdzamy w logach.** Że nigdzie nie logujemy haseł, tokenów sesji, PESEL-i ani treści wniosków. Log z takimi danymi to gorszy wyciek niż ten, przed którym się bronimy.

**Retencja.** Minimum 5 lat, więc twarde kasowanie jest wykluczone w całym systemie. Ta karta weryfikuje, że nigdzie nie wkradł się DELETE ani kaskada.

**Zależności.** Blokuje nas: T-45 (tam pojawiają się PESEL-e), B-05. Blokujemy: wdrożenie produkcyjne. Twardo.

**Kryteria akceptacji.** Checklista pusta, wpisujesz na kartę tę:

- [ ] Pola wrażliwe (PESEL, NIP, adres osoby fizycznej) zaszyfrowane, klucz poza repozytorium
- [ ] Zrzut bazy bez klucza jest bezużyteczny, potwierdzone próbą
- [ ] Przegląd logów: hasła, tokeny, PESEL-e i treści wniosków nie trafiają nigdzie
- [ ] Test przechodzący po całym modelu potwierdza brak `ON DELETE CASCADE`
- [ ] Nigdzie w kodzie nie ma twardego DELETE na danych domenowych
- [ ] Nagłówki bezpieczeństwa aplikacji przejrzane i ustawione
- [ ] Mechanizm retencji: ekran, na którym widać, czemu upłynął termin, i kto decyduje

**Dwie pułapki zapisane w modelu danych, o które ta karta się potknie.**

1. **Szerokości kolumn.** `nip` na 10 znaków i `pesel` na 11 mieszczą dokładnie tekst jawny i zero szyfrogramu. Ta karta musi te kolumny poszerzyć, inaczej pierwszy zaszyfrowany zapis wywali 22001.
2. **Odpowiedzi wniosku.** Nie da się zaszyfrować całej kolumny `answers`: szyfrogram nie jest ani obiektem, ani tablicą, więc padłby check constraint, a razem z kolumną jsonb zniknęłaby wyszukiwalność, po którą jsonb został wybrany. **Szyfrowane są pola wewnątrz dokumentu, nie dokument.**

**Uzupełnienie z raportu: cztery rzeczy do zrobienia poza kodem.**

1. **Umowa powierzenia przetwarzania danych** między OCWIP jako administratorem a nami jako podmiotem przetwarzającym. To jest B-05.
2. **Klauzula informacyjna dla osób, które nie są wnioskodawcą.** Formularz zbiera dane trzech członków grupy nieformalnej oraz osób uprawnionych do reprezentowania: imię, nazwisko, adres, telefon, e-mail. To dane osób trzecich i muszą one dostać informację o przetwarzaniu. Dziś we wzorze jest jedna klauzula, dla wnioskodawcy. Pozycja `R-16` w [`rozbieznosci.md`](rozbieznosci.md).
3. **Usuwanie po terminie retencji.** Data jest parametrem konkursu (krok 1.4), ale ktoś musi mieć ekran, na którym widzi, czemu upłynął termin, i decyduje. Bez tego parametr jest tylko liczbą w bazie. **Usunięcie po retencji dotyczy danych osobowych, nie samego wniosku:** wniosek zostaje w systemie w postaci pozbawionej danych osób, bo dokumentacja konkursu musi przetrwać dłużej niż dane kontaktowe.
4. **Retencja karty organizacji.** Karta nie należy do żadnego konkursu, więc parametr retencji z kroku 1.4 jej nie obejmuje. Propozycja raportu: karta, z której przez trzy lata nie złożono żadnego wniosku, trafia na listę do przeglądu u administratora, a nie kasuje się sama; karta powiązana ze złożonym wnioskiem żyje tak długo, jak najdłuższy termin retencji wśród jej wniosków. Pozycja `R-15`, zależna od `R-01`.

**Zasada, którą warto powtórzyć przy każdej kolejnej karcie:** każde pole, którego nie zbieramy, to mniej danych do zabezpieczenia i do usunięcia po terminie.

---

## T-48 [P0] Środowisko produkcyjne, kopie zapasowe, wdrożenie

Karta: <https://trello.com/c/RyzKqp6D> · Zablokowane częściowo przez **B-06** i przez pytanie o hosting.

**Kontekst.** Aplikacja, która działa wyłącznie na naszych laptopach, nie rozwiązuje problemu zamawiającego. To karta, która zamienia projekt w działającą usługę.

**Zakres.** Środowisko produkcyjne, domena, certyfikat, zmienne środowiskowe i sekrety, kopie zapasowe bazy wraz z przetestowanym odtworzeniem, monitoring dostępności, procedura wdrożenia nowej wersji.

**Dlaczego odtworzenie z kopii musi być przetestowane.** Kopia zapasowa, z której nikt nigdy nie odtwarzał bazy, jest założeniem, nie kopią. Przy retencji 5 lat i danych osobowych utrata bazy to nie jest problem techniczny, tylko prawny.

**Termin naboru a wdrożenie.** Nie wdrażamy nowej wersji w dniu zamknięcia konkursu. Odcięcie tnie co do minuty i awaria w tym oknie kosztuje wnioskodawców cały nabór.

**Zależności.** Blokuje nas: T-36, T-46, T-47, B-06 oraz odpowiedź o hosting. Blokujemy: pierwszy realny konkurs w systemie.

**Kryteria akceptacji.** Checklista pusta, wpisujesz na kartę tę:

- [ ] Środowisko produkcyjne stoi, domena i certyfikat działają
- [ ] Sekrety poza repozytorium, `.env.example` opisuje wyłącznie nazwy
- [ ] Kopia zapasowa bazy robi się automatycznie, z opisaną częstotliwością i retencją
- [ ] Odtworzenie z kopii **przetestowane**, nie zadeklarowane, z zapisanym czasem odtworzenia
- [ ] Monitoring dostępności odpytuje `/health` i `/health/db` osobno
- [ ] Procedura wdrożenia nowej wersji spisana i przejdzie ją ktoś, kto jej nie pisał
- [ ] Migracje odpalane osobnym krokiem deployu, nie przez proces obsługujący ruch
- [ ] Okno wdrożeniowe wyklucza dzień zamknięcia naboru

**Jawne uproszczenie MVP do cofnięcia w tej karcie.** Dziś API woła `Database.Migrate()` przy starcie pod flagą `Database:MigrateOnStartup`, włączoną tylko w Development. Docelowo migracje odpala osobny krok deployu, osobną rolą bazodanową: **proces obsługujący ruch nie powinien mieć praw DDL na stałe w systemie, który będzie trzymał PESEL-e przez pięć lat.** To jest karta, w której ta zmiana wchodzi.

**Pytania, które trzeba zadać, i są w B-06.** Kiedy najbliższy konkurs, który miałby ruszyć na naszym narzędziu (to wyznacza deadline MVP), oraz kto płaci za hosting w roku czwartym, skoro retencja 5 lat to zobowiązanie utrzymaniowe, a nie tylko wdrożeniowe.

---

## T-49 [P1] Instrukcja obsługi dla operatora OCWIP

Karta: <https://trello.com/c/Tonpp3Uy> · Zależy od T-48.

**Kontekst.** Osoba, która będzie tego używać, mówi o sobie, że kompletnie nie zna się na technikaliach, a po zakończeniu prac zostanie z tym systemem sama. Bez instrukcji każde pytanie wraca do nas, a my nie jesteśmy utrzymaniem.

**Zakres.** Krótka instrukcja po polsku, prowadząca przez pełny cykl: ogłoszenie konkursu, ułożenie formularza, śledzenie wniosków, przypisanie recenzentów, zatwierdzenie wyników, wygenerowanie umów.

**Forma.** Zrzuty ekranu i krótkie kroki, nie dokumentacja techniczna. To jest materiał dla osoby, która chce ogłosić nabór, a nie zrozumieć system.

**Co musi tam być obowiązkowo.** Jak nadaje się rolę operatora (komendą `grant-role`, nie z interfejsu, i dlaczego tak). Co zrobić, gdy wnioskodawca dzwoni, że nie może się zalogować. Co oznacza, że konkursu nie da się już edytować.

**Zależności.** Blokuje nas: T-48. Instrukcja opisuje działający system, nie planowany. Blokujemy: przekazanie systemu zamawiającemu.

**Kryteria akceptacji.** Checklista pusta, wpisujesz na kartę tę:

- [ ] Instrukcja prowadzi przez pełny cykl konkursu, od ogłoszenia do rozliczenia
- [ ] Zrzuty ekranu z działającego systemu, nie z makiet
- [ ] Rozdział o nadawaniu roli operatora komendą, z wyjaśnieniem dlaczego nie ekranem
- [ ] Rozdział "co zrobić, gdy" z trzema najczęstszymi telefonami
- [ ] Ktoś spoza zespołu przeszedł po niej pełny cykl i zgłosił, gdzie się zaciął
- [ ] Język bez żargonu, sprawdzony na osobie, która nie zna systemu

---

## Poza MVP: T-50 sprawozdawczość

Karta: <https://trello.com/c/4nEU9AlG> · **ZABLOKOWANE PRZEZ B-04** (brak wzoru sprawozdania). Pierwsza rzecz do wycięcia, jeśli zabraknie czasu.

**Kontekst.** Ostatni etap cyklu życia konkursu: podmiot po realizacji projektu składa sprawozdanie, operator je przyjmuje i rozlicza dotację. Etap realny i potrzebny, ale świadomie ustawiony jako pierwszy do wycięcia. Publikacja, składanie i ocena to rdzeń, bez którego system nie ma sensu. Sprawozdawczość bez rdzenia jest bezużyteczna.

**Zakres po odblokowaniu.** Formularz sprawozdania (prawdopodobnie na tym samym kreatorze co M3), termin złożenia, przyjęcie i akceptacja przez operatora, statusy rozliczenia.

**Czego nie wiemy.** Wzoru sprawozdania. Czy sprawozdanie jest jedno na wniosek, czy są sprawozdania cząstkowe. Model danych przyjmuje jedno na wniosek jako jawne założenie.

**Zależności.** Blokuje nas: B-04 twardo, T-45.

**Co raport dokłada, i jest to najważniejsza rzecz w tej karcie.** Zasada **"było i jest"**: jak we wniosku był budżet, to w sprawozdaniu ma być kolumna z wniosku i kolumna wykonania, plus wyliczona różnica. Obok każdego pola wykonania stoi nieedytowalna wartość z wniosku.

| Pozycja | We wniosku | W wykonaniu | Różnica |
|---|---|---|---|
| Wynajem sali, 10 spotkań po 200 zł | 2 000 zł | 1 600 zł | -400 zł |
| Liczba uczestników | 60 | 72 | +12 |
| Rezultat: przeszkolone osoby | 40 | 38 | -2 |

Trzy skutki tego układu: wnioskodawca nie przepisuje danych z wniosku, więc nie robi literówek; widzi różnicę przed złożeniem, więc sam dopisuje wyjaśnienie tam, gdzie się rozjechało; a OCWIP sprawdza sprawozdanie, czytając jedną kolumnę, a nie porównując dwa dokumenty na biurku.

**Rozliczenie.** System liczy kwotę do zwrotu jako dotację przyznaną minus wydatki uznane, z rozbiciem na pozycje nieuznane. Przy każdej pozycji wykonania finansowego operator ma przełącznik "uznane albo nieuznane" i pole na powód. Kwota do zwrotu liczy się z tego sama, a wnioskodawca po zwrocie sprawozdania do poprawy widzi powód przy konkretnej pozycji, a nie jedną liczbę na końcu.

**Rodzaje.** Sprawozdanie częściowe i końcowe. Częściowe włącza się w ustawieniach konkursu i domyślnie jest wyłączone. To rozstrzyga pytanie otwarte z karty, ale dopiero wzór potwierdzi.

**Po zamknięciu.** Cała historia projektu, od wniosku przez umowę do sprawozdania, do pobrania w jednym pliku. To dokumentacja, którą i tak trzeba trzymać pięć lat.

## T-50a [P1 / Full-stack] Sprawozdanie: formularz, wypełnianie, złożenie, przyjęcie albo zwrot

Karta: <https://trello.com/c/Qu1iIPTf> · część T-50 według przyjętej propozycji T-50.0 (`docs/model-danych.md`).

**Stan 2026-09-26 (zrobione).** Wzór sprawozdania to formularz o przeznaczeniu `Report` z `prefillFrom` i `readOnly` (`docs/kontrakt-formularza.md`). Wnioskodawca z wnioskiem `Funded` przechodzi do sprawozdania z widoku wniosku: wartości z wniosku stoją jako tekst obok pól wykonania, autozapis, złożenie przez potwierdzenie z listą braków. Operator widzi sprawozdania w ocenie konkursu, przyjmuje albo zwraca z powodem, który wnioskodawca czyta nad formularzem. Serwer przywraca wartości z wniosku przy każdym zapisie. Założenia: ZR-12.

## T-50b [P1 / Full-stack] Rozliczenie: uznawanie kosztów, kwota do zwrotu, termin, historia projektu

Karta: <https://trello.com/c/JcsVwexF> · **ZABLOKOWANE PRZEZ T-45** (termin z umowy) i częściowo B-04 (P18, P19).

**Zakres.** Kolumny operatora przy pozycjach budżetu ("uznane", "kwota nieuznana", "powód"), kwota do zwrotu jako pole wyliczane, termin sprawozdania z umowy (§ 9: 10 dni roboczych od końca realizacji) z przypomnieniem, sprawozdanie częściowe za przełącznikiem w konkursie, załączniki sprawozdania (`attachments.report_id`), stan wniosku "rozliczony" po przyjęciu, cała historia projektu w jednym pliku, wzór sprawozdania 2026 w seedzie (wymaga pełnego formularza wniosku 2026 w seedzie, dziś jest jednopolowy).

