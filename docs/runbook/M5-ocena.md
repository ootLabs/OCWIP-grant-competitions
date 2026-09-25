# M5 · Ocena

Kamień w większości zablokowany przez **B-02**, czyli brak wzoru karty oceny i zasad punktacji. Na Trello da się przed dokumentami ruszyć wyłącznie `T-37`, bo dotyczy przypisania, a nie punktacji.

**Raport przesuwa tę granicę i warto to wiedzieć przed rozmową z zamawiającym.** Cytat z raportu, krok 5.6: *"Mechanizm, ustawienia, przypisania, punktacja, rekomendowana kwota i dokumenty komisji są opisane wyżej i tego żaden wzór nie zmieni; to budujemy teraz. Wzór doda konkretne pytania i kryteria punktowe, czyli treść, która jest decyzją merytoryczną zamawiającego."*

Czyli B-02 blokuje **treść** kart oceny, a nie **mechanizm**. Co z tego wynika i jak podzielić karty, opisuje sekcja [Co B-02 blokuje naprawdę](#co-b-02-blokuje-naprawdę) na dole tego pliku oraz [`blokery.md`](blokery.md). Podziału nie robisz sam: to zmiana zakresu, więc idzie przez kartę na Trello.

Przebieg oceny krok po kroku: [`proces.md`](proces.md), ścieżka 5.

---

## T-37 [P0 / Backend] Przypisanie wniosków recenzentom

Karta: <https://trello.com/c/1EG1Ngzv> · **Jedyna karta M5 dostępna przed dokumentami.** · **Gotowe**, zakres dokładnie checklisty niżej.

**Co zostało celowo poza tą kartą.** Sekcja "Uzupełnienie z raportu" poniżej opisuje też powołanie komisji (wyszukiwanie po e-mail, tworzenie kont, rola formalna albo merytoryczna, dostęp podglądowy), blokadę oświadczeniem o braku konfliktu interesów i losowanie przypisań z podglądem przed zapisaniem. Żadne z tych trzech nie weszło do tej karty: to nowe, nieopisane jeszcze w `model-danych.md` encje i przepływy, a decyzja o rozdzieleniu ich na osobne karty (patrz propozycja `T-38a`/`T-38b`/`T-39a`/`T-39b` niżej) należy do Trello, nie do tej sesji. Zaimplementowany mechanizm: operator przypisuje i cofa przypisanie ręcznie, przez `POST`/`DELETE /applications/{id}/assignments`, relacja wiele do wielu.

**Kontekst.** Recenzent widzi wyłącznie wnioski przypisane mu przez operatora. To reguła dostępowa, nie funkcja pomocnicza: bez niej recenzent widzi wszystko, czyli dane osobowe wszystkich organizacji.

**Zakres.** Przypisanie wniosku recenzentowi i cofnięcie przypisania, wraz z regułą autoryzacji ograniczającą widoczność.

**Pytania otwarte z karty (B-02).** Ilu recenzentów ocenia jeden wniosek. Co przy rozbieżnych ocenach. Czy ocena jest anonimowa. Jak wykluczamy recenzenta przy konflikcie interesów. Do czasu odpowiedzi model przypisania projektujemy jako relację wiele do wielu, bo zawężenie jej później jest tanie, a rozszerzenie drogie.

**Zależności.** Blokuje nas: T-33, T-13.1, T-13.2. Blokujemy: T-38, T-40.

**Kryteria akceptacji.** Checklista pusta, wpisujesz na kartę tę:

- [ ] Operator przypisuje wniosek recenzentowi i cofa przypisanie
- [ ] Relacja wiele do wielu, nie jeden do jednego
- [ ] Recenzent widzi wyłącznie wnioski przypisane mu **w tym jednym konkursie**
- [ ] Reguła wpięta w warstwę autoryzacji z T-13.2, nie sprawdzana w endpointach osobno
- [ ] Test: recenzent otwiera nieprzypisany wniosek, więc 403
- [ ] Test: recenzent przypisany w konkursie A nie widzi wniosków z konkursu B
- [ ] Cofnięcie przypisania nie kasuje twardo wpisu, tylko go dezaktywuje

**Uzupełnienie z raportu, i większość z tego odpowiada na pytania otwarte z karty.**

Raport podaje **domyślne ustawienia oceny jako parametry konkursu** (krok 5.0), a nie jako stałe w kodzie:

| Ustawienie | Co robi |
|---|---|
| Liczba ekspertów na wniosek | ilu recenzentów ocenia jeden wniosek, domyślnie dwóch |
| Czy do systemu wchodzi tylko ocena zbiorcza komisji | przełącza tryb: każdy osobno albo jedna ocena wprowadzana przez przewodniczącego |
| Lista osób oceniających zbiorczo | pole tekstowe na skład komisji bez kont w systemie: imię i nazwisko, miejsce pracy, stanowisko |
| Sposób liczenia wyniku | suma albo średnia punktów |
| Próg rozbieżności ocen | domyślnie 30% skali |
| Czy publikować punktację | czy wnioskodawca widzi liczby, czy tylko wynik |
| Karta oceny widoczna dla wnioskodawcy | czy karty są udostępniane |
| Nazwa oceniającego | jak go nazywać w dokumentach, na przykład "Członek komisji konkursowej" |

To jest ustawienie konkursu, więc **należy do T-20 albo do osobnej karty**, nie do T-37. Pozycja `R-04` w [`rozbieznosci.md`](rozbieznosci.md).

Dalej, wprost do zakresu tej karty:

- **Powołanie komisji** (krok 5.1): osoby wyszukiwane po adresie e-mail wśród istniejących kont, można założyć nowe konto, **ekspert może być spoza OCWIP**. Przy każdej osobie decyzja o rodzaju udziału: ocena formalna albo merytoryczna. Osobno można nadać dostęp podglądowy bez oceniania.
- **Blokada oświadczeniem** (decyzja 11 z raportu): dopóki ekspert nie zaakceptuje oświadczenia o braku konfliktu interesów, **nie widzi treści żadnego wniosku**. Odmowa jest dopuszczalna, wymaga powodu i wyklucza go z oceny. To nie jest wymysł: obecne narzędzie ma gotowy znacznik wstawiający do dokumentu tabelę z listą ekspertów, informacją o akceptacji i powodem odmowy. Pozycja `R-05`.
- **Przypisanie przez losowanie albo ręcznie** (krok 5.2). Losowanie dzieli wnioski po równo między zaznaczone osoby i **pokazuje wynik do zatwierdzenia przed zapisaniem**, żeby dało się poprawić jedno przypisanie bez losowania od nowa. Losowanie pomija osoby bez podpisanego oświadczenia i osoby powiązane z wnioskodawcą, jeśli operator taką parę zaznaczy.
- **Twarda reguła, testowana automatycznie:** ekspert widzi wyłącznie wnioski przypisane mu w tym jednym konkursie. Jest już w kryteriach wyżej.

Decyzja 12 z raportu odpowiada na pierwsze pytanie otwarte karty: **dwóch recenzentów na wniosek jako domyślne ustawienie konkursu**, a przy rozbieżności powyżej progu (domyślnie 30% skali) wniosek dostaje ostrzeżenie i decyduje operator: średnia, trzecia ocena albo rozstrzygnięcie komisji, zawsze z uzasadnieniem. System nie rozstrzyga tego sam, bo to decyzja ludzka: daje na nią miejsce i ją dokumentuje.

---

## T-38 [P0 / Backend] Karta oceny i punktacja

Karta: <https://trello.com/c/OPGJGeOo> · **ZABLOKOWANE PRZEZ B-02.**

> **Stan 2026-09-25:** mechanizm zrobiony w T-38 (model w `docs/model-danych.md`, sekcja "Ocena wniosku", kontrakt w `docs/kontrakt-formularza.md`, sekcja "Karta oceny"). Treść kart 2026 (T-38b) leży w `backend/seed/evaluation-cards/` i publikuje ją `scripts/seed.py`; ustawienia oceny w konkursie przeszły do T-39.

**Kontekst.** Formularz, który wypełnia recenzent. Model danych świadomie nie zawiera encji Ocena, bo nie widzieliśmy realnego wzoru. Modelowanie tego z głowy byłoby zgadywaniem, a zgadywanie w modelu danych kosztuje najwięcej.

**Zakres po odblokowaniu.** Encja oceny, kryteria z punktacją, uzasadnienie tekstowe, zapis roboczy i zatwierdzenie oceny.

**Czego musimy się dowiedzieć, zanim zaczniemy.** Jakie są kryteria i ile punktów daje każde. Czy liczy się suma czy średnia. Czy jest próg punktowy odrzucający wniosek. Czy recenzent może zmienić ocenę po zatwierdzeniu.

**Czy da się to zrobić na kreatorze z M3.** Prawdopodobnie tak: karta oceny to też formularz o strukturze zapisanej w bazie. Warto sprawdzić przed pisaniem osobnego mechanizmu, bo to oszczędza połowę tej karty. Raport potwierdza ten kierunek wprost: struktura karty oceny i sprawozdania jest danymi w bazie tym samym mechanizmem co formularz wniosku.

**Zależności.** Blokuje nas: B-02 twardo, T-37. Blokujemy: T-39, T-40.

**Co raport dokłada do zakresu po odblokowaniu.**

- **Ocena ma dwa osobne etapy i nie łączymy ich w jeden przebieg:** formalna i merytoryczna.
- **Ocena formalna** (krok 5.3, decyzja 13): jedna osoba, domyślnie operator. Karta to tabela pytań zamkniętych tak albo nie, każde z miejscem na uzasadnienie. Wynik całej karty: pozytywny albo negatywny. **Wynik negatywny nie zamyka sprawy automatycznie:** obok wyniku stoi przycisk zwrotu do poprawy z gotową listą braków z karty. Odrzucenie wniosku za brakujący załącznik, gdy wystarczyło o niego poprosić, nie jest dobrym wydaniem publicznych pieniędzy.
- **Ocena merytoryczna** (krok 5.4): recenzent widzi kartę oceny, a pod nią cały wniosek, przewijany, bez przełączania ekranów. Uzupełnia punkty przy kryteriach, **kwotę rekomendowaną** (może być niższa od wnioskowanej) oraz **uzasadnienie obniżenia kwoty**. To ostatnie pole jest dopiskiem raportu: obniżka bez słowa wyjaśnienia jest pierwszą rzeczą, o którą wnioskodawca zadzwoni.
- "Zapisz" nie kończy etapu, "zapisz i zakończ etap" kończy.
- **Decyzja 14:** każdy ekspert ocenia osobno w systemie, ale **model danych dopuszcza ocenę zbiorczą** wprowadzoną przez operatora w imieniu komisji obradującej poza systemem: karta ma **osobno zapisanego autora i osobę wprowadzającą**. To różnica, która po wdrożeniu jest droga, a teraz nic nie kosztuje, więc wchodzi do modelu od razu, nawet jeśli ekran do jej włączenia jest poza MVP.

---

## T-39 [P0 / Backend] Lista rankingowa

Karta: <https://trello.com/c/j6yKe6Z6> · **ZABLOKOWANE PRZEZ B-02.**

**Kontekst.** Wnioski ułożone według liczby zebranych punktów. To na tej liście operator podejmuje decyzję, kto dostaje dotację.

**Zakres po odblokowaniu.** Wyliczenie punktów wniosku z ocen recenzentów i uszeregowanie wniosków w konkursie.

**Czego nie wiemy (B-02).** Czy punkty to suma czy średnia z ocen. Co się dzieje przy rozbieżnych ocenach, czyli czy jest trzeci recenzent, czy decyduje operator. Jak rozstrzygamy remis. Bez tych odpowiedzi ranking nie ma definicji, więc karta nie rusza.

**Czego nie robimy tutaj.** Ranking niczego nie przyznaje automatycznie. Decyzja o dofinansowaniu należy do operatora i jest osobną kartą (T-42).

**Zależności.** Blokuje nas: B-02 twardo, T-38. Blokujemy: T-41, T-42.

**Co raport rozstrzyga.** Sposób liczenia wyniku (suma albo średnia) jest **ustawieniem konkursu**, nie stałą, więc pierwsze z trzech pytań otwartych znika: budujesz oba i przełączasz parametrem. Próg rozbieżności też jest ustawieniem, domyślnie 30% skali. Nierozstrzygnięty zostaje remis.

---

## T-40 [P0 / Frontend] Panel recenzenta

Karta: <https://trello.com/c/eHJJ5x2w> · **ZABLOKOWANE PRZEZ B-02 w części dotyczącej karty oceny.**

**Kontekst.** Trzecia rola, trzeci widok systemu. Recenzent widzi wyłącznie wnioski jemu przypisane i nic poza tym.

**Zakres.** Lista przypisanych wniosków ze statusem oceny, podgląd pełnego wniosku wraz z załącznikami, wypełnianie karty oceny, zapis roboczy i zatwierdzenie.

**Dlaczego podgląd wniosku musi być czytelny, a nie surowy.** Recenzent czyta kilkanaście wniosków po 5 do 6 stron. Jeśli wniosek wyświetla się jak zrzut danych, ocena będzie pobieżna, a to psuje cały konkurs.

**Zależności.** Blokuje nas: T-37, T-38, T-28, T-15.1. Blokujemy: T-39 w praktyce.

**Co raport dokłada.** Nad listą wniosków **trzy sumy: kwot wnioskowanych, kwot rekomendowanych i puli konkursu**, żeby ekspert widział na bieżąco, że rekomendował już 140 tysięcy przy puli 100 tysięcy. Oświadczenie o konflikcie interesów jest bramą do tego panelu: bez podpisu ekspert nie widzi treści żadnego wniosku.

---

## T-41 [P0 / Frontend] Ocena i ranking w panelu operatora

Karta: <https://trello.com/c/NYI7jUxv> · **ZABLOKOWANE PRZEZ B-02 w części rankingowej.**

**Kontekst.** Miejsce, w którym operator prowadzi proces oceny: rozdziela wnioski, pilnuje postępu recenzentów i patrzy na wyniki.

**Zakres.** Przypisywanie wniosków recenzentom, widok postępu oceny w konkursie, lista rankingowa z punktami i możliwością wejścia w pojedyncze oceny.

**Skala.** Około 120 wniosków i kilku recenzentów. Przypisywanie po jednym kliknięciu na wniosek jest niepraktyczne przy tej liczbie, więc przewidujemy działanie na zaznaczonej grupie.

**Zależności.** Blokuje nas: T-37, T-39, T-35, T-15.3. Blokujemy: T-42.

**Co raport dokłada.** Tabela osób przypisanych do oceny ma kolumny: imię i nazwisko z adresem e-mail, licznik wniosków w ocenie formalnej, licznik w merytorycznej, dostęp podglądowy oraz stan oświadczenia o braku konfliktu interesów. Udostępnienie kart wnioskodawcom (krok 5.5) jest **jedną decyzją operatora na cały konkurs, nieodwracalną, z potwierdzeniem**, a wnioskodawca widzi treść karty i punktację **bez danych osoby oceniającej**. System wie, kto oceniał, ukrycie jest po stronie widoku.

**Stan 2026-09-25 (zrobione w T-41).** Ekran `/panel/operator/evaluation` z wyborem konkursu, a w konkursie: ustawienia oceny (T-39), tabela ekspertów z liczbą przypisanych wniosków i stanem deklaracji (ZR-06), lista rankingowa z postępem oceny i ekspertami każdego wniosku, przypisanie grupowe zaznaczonych wniosków (ZR-07). Dwie części raportu wydzielone do osobnych kart, żeby ta nie rosła bez końca: T-41a i T-41b niżej.

---

## T-41a [P0 / Frontend] Karta formalna operatora i wgląd w pojedyncze oceny

Karta: <https://trello.com/c/AVQdoH8h>

**Zakres.** Z wiersza listy rankingowej operator otwiera kartę oceny formalnej wniosku i ją wypełnia (trasy `formal-evaluation` z T-38 już są, brakuje ekranu), obok widzi cały wniosek jak ekspert w T-40. Z tego samego wiersza otwiera każdą zakończoną kartę merytoryczną, tylko do odczytu, z nazwiskiem eksperta.

**Zależności.** Blokuje nas: T-38, T-41. Blokujemy: T-41b, T-42 w praktyce.

**Stan 2026-09-25 (zrobione).** Numer wniosku na liście rankingowej prowadzi do `/panel/operator/evaluation/{konkurs}/{wniosek}`: karta formalna otwierana przyciskiem, karty ekspertów z nazwiskami tylko do odczytu (także niezakończone, z oznaczeniem), pod nimi wniosek. Nowa trasa `GET /applications/{id}/evaluations` tylko dla operatora.

---

## T-41b [P1 / Full-stack] Udostępnienie kart oceny wnioskodawcom

Karta: <https://trello.com/c/ziBUSMum>

**Zakres (raport, krok 5.5).** Jedna decyzja operatora na cały konkurs, nieodwracalna, z potwierdzeniem. Po niej wnioskodawca widzi w swoim panelu treść kart oceny swojego wniosku i punktację, **bez danych osoby oceniającej**. System wie, kto oceniał; ukrycie jest po stronie odpowiedzi API, nie tylko ekranu (dane idą do przeglądarki).

**Zależności.** Blokuje nas: T-41a, T-34.

---

## Co B-02 blokuje naprawdę

> **Stan 2026-09-25:** każdy wiersz "tak" poniżej ma już odpowiedź w dokumentach NOWE FIO 2026, łącznie z remisem (wcześniejsze złożenie wniosku). Podział z końca tej sekcji zastępuje propozycja w `docs/model-danych.md`.

Zestawienie, które trzeba położyć na stole przy najbliższej rozmowie z zamawiającym, bo odblokowuje cztery karty w połowie.

| Element | Blokuje B-02 | Uzasadnienie |
|---|---|---|
| Konkretne pytania oceny formalnej | tak | to treść merytoryczna, decyzja OCWIP |
| Kryteria merytoryczne i liczba punktów przy każdym | tak | jak wyżej |
| Próg punktowy odrzucający wniosek | tak | jak wyżej |
| Struktura karty oceny jako dane w bazie | **nie** | ten sam mechanizm co formularz wniosku, M3 |
| Dwa etapy oceny, formalny i merytoryczny | **nie** | opisane w raporcie, kroki 5.3 i 5.4 |
| Ustawienia oceny w konkursie (krok 5.0) | **nie** | osiem parametrów wypisanych wprost |
| Powołanie komisji i oświadczenie o konflikcie | **nie** | krok 5.1, decyzja 11 |
| Losowanie i ręczne przypisanie wniosków | **nie** | krok 5.2, decyzja 12 |
| Kwota rekomendowana i uzasadnienie obniżenia | **nie** | krok 5.4 |
| Suma albo średnia jako sposób liczenia | **nie** | ustawienie konkursu, budujemy oba |
| Zachowanie przy rozbieżności ocen | **nie** | decyzja 12: ostrzeżenie plus decyzja operatora z uzasadnieniem |
| Rozstrzyganie remisu | tak | nigdzie nie opisane |
| Dokumenty komisji (protokół, lista obecności, wyniki) | **nie** | krok 5.6, ten sam mechanizm wzorów co umowa |

Propozycja podziału, do założenia jako karty na Trello, a nie do zrobienia po cichu:

- `T-38a` mechanizm karty oceny na kreatorze z M3, dwa etapy, kwota rekomendowana, autor osobno od wprowadzającego.
- `T-38b` treść kart oceny dla konkursu 2026, zablokowana przez B-02.
- `T-39a` liczenie wyniku i ranking z parametrem suma albo średnia, plus obsługa rozbieżności.
- `T-39b` rozstrzyganie remisu, zablokowane przez B-02 i przez regulamin.
