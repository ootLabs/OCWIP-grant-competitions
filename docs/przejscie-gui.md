# Przejście ręczne GUI: scenariusz

Scenariusz do **fizycznego wyklikania na stosie lokalnym**, przed każdym wystawieniem na serwer. Nie zastępuje testów automatycznych: ma dać pewność, że komplet ścieżek użytkownika trzyma się do kupy w przeglądarce, na jednej bazie, w jednym przebiegu.

Jeden scenariusz na wszystkie przejścia, bez kopii: wyniki każdego przebiegu idą do [`przejscie-gui-bledy.md`](przejscie-gui-bledy.md), a ten plik poprawiamy tylko wtedy, gdy opisywał produkt niezgodnie z tym, co produkt robi.

Źródła: [`runbook/proces.md`](runbook/proces.md) (siedem ścieżek procesu), [`reguly-biznesowe.md`](reguly-biznesowe.md), [`wdrozenie.md`](wdrozenie.md) (pierwszy konkurs na pustej bazie), [`slownik.md`](slownik.md). Nie Trello.

---

## Zanim zaczniesz: kontekst, którego nie masz z kodu

**Kim jest OCWIP w tym systemie.** Organizatorem konkursu, nie wnioskodawcą. OCWIP rozdaje dalej pieniądze dostane od kogoś innego (regranting, tu: NOWE FIO), na podstawie własnego regulaminu. Stąd cała asymetria: operator układa reguły, wnioskodawca się w nie wpisuje.

**Kto występuje w tym scenariuszu.**

| Rola | Kto to w realnym świecie | Co robi u nas |
|---|---|---|
| Gość | ktokolwiek z internetu | czyta ogłoszenie, pobiera wzory, przegląda archiwum wyników |
| Wnioskodawca | osoba z organizacji albo z grupy nieformalnej | karta podmiotu, wniosek, sprawozdanie |
| Operator | pracownik OCWIP | konkurs, formularz, nabór, ocena formalna, rozstrzygnięcie, umowy |
| Ekspert (recenzent) | osoba powołana do komisji | tylko przypisane mu wnioski, karty oceny merytorycznej |

**Administratora w kodzie nie ma.** Dokumentacja opisuje go jako piątą rolę, ale `grant-role` zna tylko `Applicant`, `Operator`, `Reviewer`. Wszystko, co dokumentacja przypisuje administratorowi (awaryjne zatwierdzenie dostępu do karty organizacji, uruchomienie usunięcia danych po retencji), jest poza tym przejściem.

**Dwa rodzaje wnioskodawcy mają różne formularze.** Organizacja (młoda albo lokalna) podaje rejestr, NIP, przychód, reprezentantów. Grupa nieformalna podaje nazwę grupy, członków i rachunek lidera. Dlatego w scenariuszu składasz **dwa** wnioski, jeden każdego rodzaju: pola warunkowe są najczęstszym miejscem, w którym coś się rozjeżdża.

**Czego się spodziewać po pieniądzach.** Konkurs ma pulę (łączną kwotę), maksymalną dotację na wniosek, maksymalny procent kosztów pośrednich i procent rozwoju instytucjonalnego. Te liczby z kroku 1.4 kreatora nie są ozdobą: formularz wniosku liczy z nich na bieżąco i blokuje złożenie, gdy budżet ich nie trzyma. Wzorce 2026: dotacja do 7 000 zł, koszty pośrednie do 10 procent, przychód organizacji do 50 000 zł.

---

## Krok 0. Przygotowanie stosu i kont

```bash
cd ~/Documents/STAZ/OCWIP-grant-competitions
docker compose up -d
curl -s -o /dev/null -w "api=%{http_code}\n" http://localhost:8080/health/db   # 200
curl -s -o /dev/null -w "front=%{http_code}\n" http://localhost:3000/          # 200
```

Backend przy pierwszym starcie buduje się i wykonuje migracje, więc `health/db` odpowiada 200 po minucie albo dwóch, nie od razu.

**Konta.** Skrypt lokalny robi pełny prawdziwy przebieg (rejestracja, odczyt linku z maila w logu, potwierdzenie adresu, nadanie roli komendą), więc nie obchodzi żadnej reguły produktu:

```bash
python3 create_test_users.py
```

Zakłada pięć kont, wszystkie z hasłem `TestHaslo123!`:

| Adres | Rola | Do czego w scenariuszu |
|---|---|---|
| `operator@example.org` | Operator | konkurs, nabór, ocena formalna, wyniki, umowy |
| `wnioskodawca@example.org` | Applicant | wniosek organizacji |
| `grupa@example.org` | Applicant | wniosek grupy nieformalnej |
| `recenzent@example.org` | Reviewer | karta merytoryczna, ekspert 1 |
| `recenzent2@example.org` | Reviewer | karta merytoryczna, ekspert 2 |

**Poczta bez serwera poczty.** Lokalnie (`ASPNETCORE_ENVIRONMENT=Development`, brak `SMTP_HOST`) system nie wysyła maili, tylko wypisuje każdy w całości do logu backendu. To jest twoja skrzynka:

```bash
# ostatnie maile, z treścią i linkami
docker compose logs backend | grep -A 14 "DEV EMAIL" | tail -60

# wszystko, co poszło na jeden adres
docker compose logs backend | grep -A 14 "To: wnioskodawca@example.org" | tail -40
```

Link z maila kopiujesz do przeglądarki ręcznie. Jeśli wolisz prawdziwą skrzynkę z listą wiadomości, `docker compose --profile test up -d mailpit` stawia Mailpit pod <http://localhost:8025>, ale wtedy trzeba mu wskazać `SMTP_HOST=mailpit` i `SMTP_FROM` w `.env` i zrestartować backend. Do tego przejścia log wystarczy.

**Dwie pułapki lokalne, o które łatwo się potknąć.**

- Pięć nieudanych logowań pod rząd blokuje konto na 15 minut (`AUTH_MAX_FAILED_LOGIN_ATTEMPTS`, `AUTH_LOCKOUT_MINUTES`). Jeśli chcesz sprawdzić blokadę celowo (ścieżka B5), rób to na koncie, którego nie potrzebujesz zaraz potem.
- Na trasy konta działa limit 10 żądań na minutę. Bardzo szybkie klikanie "Zaloguj się" skończy się odpowiedzią o zbyt wielu próbach i to jest poprawne zachowanie, nie awaria.

**Trzy okna przeglądarki, osobne profile albo tryb prywatny.** Sesja siedzi w ciasteczku, więc operator, wnioskodawca i ekspert muszą być w osobnych oknach, inaczej będziesz się wylogowywał przy każdym przeskoku. Najwygodniej: jedno okno zwykłe (operator), jedno prywatne (wnioskodawca), jedno prywatne drugiej przeglądarki (ekspert).

---

## Ścieżka A. Gość, czyli człowiek z internetu

To, co widzi ktoś bez konta. Na tym etapie baza jest pusta, więc sprawdzasz głównie, czy strony w ogóle stoją i czy pusty stan wygląda na zamierzony, a nie na błąd.

1. <http://localhost:3000> - strona główna. Ma wyjaśniać, co to za miejsce i prowadzić do konkursów.
2. `/competitions` - lista konkursów. Teraz pusta i ma o tym mówić po polsku ("brak", nie biała strona).
3. `/archive` - archiwum wyników. Też pusto.
4. `/regulamin`, `/klauzula-informacyjna`, `/deklaracja-dostepnosci`, `/kontakt` - strony informacyjne. Deklaracja dostępności jest wymagana prawnie i ma dane roboczych wartości OCWIP do podmiany, więc czytasz ją jak urzędnik: czy nie ma w niej oczywistej nieprawdy.
5. Przełącznik kontrastu w nagłówku - włącz i wyłącz. Ma działać bez przeładowania strony i przetrwać przejście na inną stronę.

**Na co patrzeć poza treścią.** Każda z tych stron ma działać bez konta, bez ostrzeżeń w konsoli przeglądarki (F12) i bez migania nieostylowanej treści.

---

## Ścieżka B. Konto

Skrypt z kroku 0 zrobił pięć kont za ciebie. Tutaj przechodzisz te same rzeczy **ręcznie, przez ekrany**, bo to robi realny użytkownik i to jest najczęstsze miejsce porzucenia.

### B1. Rejestracja

`/register`. Wpisujesz imię, nazwisko, adres dwa razy, hasło dwa razy i zaznaczasz obie zgody (regulamin i klauzula informacyjna). Użyj adresu spoza listy, na przykład `test.reczny@example.org`.

- Powtórzony adres i powtórzone hasło muszą się zgadzać, inaczej formularz mówi które.
- Bez zaznaczonych zgód konta nie założysz. Tak ma być: akceptacja jest zapisywana z wersją dokumentu.
- Po wysłaniu: komunikat "Sprawdź skrzynkę", **bez** informacji, czy ten adres był już zajęty. Powtórz rejestrację tym samym adresem: odpowiedź ma być identyczna. To nie jest niedoróbka, to reguła: nie ujawniamy, czy konto istnieje.

### B2. Potwierdzenie adresu

Wyciągnij link z logu (`grep -A 14 "To: test.reczny@example.org"`), otwórz w przeglądarce. Ekran mówi "Adres e-mail jest potwierdzony.".

- Zepsuj link (zmień jedną literę w tokenie) i otwórz ponownie: ma być czytelna odmowa po polsku, nie błąd 500 i nie biała strona.
- Przed potwierdzeniem logowanie tym kontem nie działa. Sprawdź to: konto bez potwierdzonego adresu jest nieczynne.

### B3. Logowanie i wylogowanie

`/login`, potem `/panel/applicant`. Wyloguj się z nagłówka panelu i spróbuj wejść na `/panel/applicant` wprost z adresu: ma cię odesłać na logowanie.

### B4. Reset zapomnianego hasła

`/forgot-password`, podaj adres istniejącego konta, potem adres, którego nie ma. **Oba razy ten sam komunikat.** Link z logu, nowe hasło, logowanie nowym hasłem, próba logowania starym (ma odmówić).

### B5. Blokada po nieudanych próbach

Na koncie `test.reczny@example.org`: pięć razy złe hasło. Szóste podejście, także z dobrym hasłem, ma odmówić z informacją o blokadzie. Po 15 minutach konto wraca. Jeśli nie chcesz czekać, `docker compose restart backend` nie kasuje blokady (siedzi w bazie), więc po prostu przejdź dalej innym kontem.

### B6. Zmiana hasła i adresu po zalogowaniu

`/panel/applicant/account`:

- "Zmień hasło": wymaga starego hasła, ustawia nowe, sesja ma dalej działać.
- "Zmień adres": potwierdzenie idzie na **nowy** adres (bo adres służy do logowania). Wyciągnij link z logu, otwórz, potem zaloguj się nowym adresem. To jest ścieżka `confirm-email-change`.

---

## Ścieżka C. Operator zakłada i publikuje konkurs

Tu jesteś pracownikiem OCWIP, który w listopadzie ogłasza konkurs na przyszły rok. Zwykle robi to w kilku podejściach, dlatego **walidacja nie blokuje przechodzenia między krokami**: niekompletny krok dostaje tylko oznaczenie, a kompletność sprawdza się przy publikacji.

Zaloguj się jako `operator@example.org` i wejdź w `/panel/operator/competitions/new`.

### C1. Kreator, siedem kroków

| Krok | Co wpisujesz w tym przejściu | Dlaczego dokładnie to |
|---|---|---|
| 1.1 Dane konkursu | Numer `1/2026`, tytuł `Kierunek NOWE FIO 2026 (próba)`, **początek naboru ustaw na wczoraj**, koniec na 7 dni do przodu | nabór ma być otwarty od razu, bez czekania na zegar; stany konkursu przestawiają się same z tych dat |
| 1.2 Opis konkursu | opis, zakładane rezultaty, odnośnik do regulaminu | to jest treść ogłoszenia, którą czyta gość |
| 1.3 Forma dostarczenia | zostaw bez papieru | przy "tak" dochodzi wariant "wymagany papierowo" i kolumna daty wpływu na liście wniosków, to osobna próba |
| 1.4 Limity | pula `20000`, maksymalna dotacja `7000`, koszty pośrednie `10` procent, maksymalny średni roczny przychód `50000`, daty realizacji projektu, data usunięcia danych osobowych | te liczby staną się blokadami w budżecie wniosku i wskazówkami dla wnioskodawcy; data usunięcia pokazuje się na publicznej stronie konkursu, do której odsyła klauzula RODO we wniosku |
| 1.5 Załączniki do oferty | dodaj jeden wymagany: `Statut`, dopuszczalny format PDF | wymagany załącznik blokuje złożenie wniosku i sam wpisuje się na ekran "co przygotować" |
| 1.6 Osoby kontaktowe | wskaż siebie, dopisz treść maila potwierdzającego złożenie | to jest nadawca odpowiedzi na pytania i treść potwierdzenia |
| 1.7 Podsumowanie | przeczytaj całość | ostatni ekran przed zapisem |

Zapisz. Po zapisie jesteś na stronie konkursu, a **identyfikator konkursu jest w adresie**: `/panel/operator/competitions/<id>`. Skopiuj go, zaraz będzie potrzebny.

Sprawdź po drodze dwie rzeczy: przejście do kroku dalej z niedokończonym krokiem jest dozwolone, a krok dostaje oznaczenie; zapis w trakcie i powrót nie gubi wpisanych danych.

### C2. Treść startowa: formularz i karty oceny

Świeży konkurs nie ma formularza wniosku ani kart oceny, a bez nich nie da się go opublikować. Na produkcji to samo robi się tą samą komendą, więc przejście przez nią jest częścią próby wdrożenia ([`wdrozenie.md`](wdrozenie.md), "Pierwszy konkurs na pustej bazie"):

```bash
docker compose exec backend dotnet run --project src/Ocwip.Api/Ocwip.Api.csproj --no-launch-profile \
  -- import-content --competition <ID_KONKURSU> \
  --application /src/seed/forms/application-2026.json \
  --formal /src/seed/evaluation-cards/formal-2026.json \
  --merit /src/seed/evaluation-cards/merit-2026.json \
  --report /src/seed/forms/report-2026.json \
  --contract /src/seed/templates/contract-2026.txt
```

Ścieżki są bezwzględne nie przez upodobanie: `dotnet run --project` rozwija względne od katalogu projektu, więc `seed/forms/...` nie zostanie znalezione. Na serwerze, gdzie ta sama komenda idzie przez `dotnet Ocwip.Api.dll`, względne `seed/...` działa.

**Git Bash na Windows** przepisuje `/src/seed/...` na ścieżkę Windows i komenda kończy się pięcioma błędami "could not be read". Poprzedź ją wtedy `MSYS_NO_PATHCONV=1` (przebieg 1 w [`przejscie-gui-bledy.md`](przejscie-gui-bledy.md)).

Ma wypisać między innymi `Published the application form as version 1.`. Uruchom ją drugi raz: ma nic nie zmienić i nie zdublować wersji (pliki identyczne z wersją w mocy).

Odśwież stronę konkursu. Sekcja "Karty oceny i wzór sprawozdania" pokazuje teraz wersje w mocy.

### C3. Formularz wniosku w kreatorze (obejrzeć, nie psuć)

`/panel/operator/forms/<ID_KONKURSU>`. Zobacz zaimportowany formularz 2026: cztery części (dane wnioskodawcy, informacje o projekcie, budżet, oświadczenia), pola warunkowe, tabele, pola liczone. Kliknij "Podgląd": tak widzi formularz wnioskodawca.

Jeśli chcesz sprawdzić sam kreator, dodaj sekcję testową, przesuń ją, usuń i **nie publikuj nowej wersji**. Publikacja nowej wersji w trakcie naboru jest osobnym tematem (wersjonowanie), a tutaj zepsułaby dalsze kroki.

### C4. Wzory załączników do pobrania

Na stronie konkursu sekcja "Wzory załączników": wrzuć dowolny plik PDF jako wzór do wymogu `Statut`. Potem sprawdź, że gość widzi go na publicznej stronie konkursu i może pobrać. "Wycofaj wzór" przestaje go oferować.

### C5. Publikacja

Na stronie konkursu kliknij "Opublikuj konkurs" i potwierdź.

Najpierw sprawdź odmowę: gdyby brakowało formularza albo którejś karty, przycisk zwraca listę braków ("Brak opublikowanego formularza wniosku.", "Brak karty oceny formalnej.", "Brak karty oceny merytorycznej."). Po C2 lista ma być pusta i publikacja ma przejść.

Po publikacji:

- `/competitions` u gościa pokazuje konkurs, a `/competitions/<id>` ma pełne ogłoszenie, terminy, limity przeliczone na złotówki, wymagane załączniki, osoby kontaktowe i działający przycisk "Wypełnij wniosek".
- Dopóki nabór nie trwa, przycisk jest nieaktywny z wyjaśnieniem. Dlatego w kroku 1.1 początek naboru ustawiłeś na wczoraj.

### C6. Kopia konkursu (opcjonalnie, ale tanio)

Na stronie konkursu "Skopiuj konkurs" i "Skopiuj karty i wzór" z innego konkursu. To jest normalna droga na następny rok. Nowy szkic ma przenieść ustawienia. Skopiowany szkic możesz zostawić nieopublikowany albo oznaczyć jako nieaktywny (nie kasujemy twardo).

---

## Ścieżka D. Wnioskodawca: organizacja składa wniosek

Najważniejsza ścieżka, bo przechodzi przez nią najwięcej ludzi. Wchodź w nią **z publicznej strony konkursu**, tak jak realny wnioskodawca, a nie wprost z panelu.

Okno prywatne, konto `wnioskodawca@example.org`.

### D1. Wejście i ekran "co przygotować"

1. `/competitions/<id>`, przycisk "Wypełnij wniosek".
2. Niezalogowanego odsyła na logowanie i po zalogowaniu **wraca na ten sam konkurs**. Sprawdź to, bo to jest realna droga.
3. Ekran "Co przygotować": lista rzeczy do zebrania, generowana z ustawień konkursu (wymagane załączniki z kroku 1.5, terminy z 1.1). Ma zgadzać się z tym, co wpisałeś jako operator.

### D2. Karta podmiotu

Pierwszy wniosek zaczyna się od danych organizacji, bo część I wniosku jest formularzem karty podmiotu. Wypełnij: pełna nazwa, forma prawna, rejestr i numer, NIP, adres, telefon, e-mail, rachunek bankowy, reprezentant. Potem "Zapisz dane i przejdź do wniosku".

**Co tu jest do sprawdzenia poza zapisem:** przy drugim wniosku tej samej organizacji ten blok jest już wypełniony i ma dwa przyciski, "Dane są aktualne" oraz "Popraw dane". Wrócisz do tego w ścieżce E, a właściwie sprawdzisz to składając drugi wniosek tym samym kontem, jeśli zechcesz.

### D3. Wypełnianie wniosku, cztery części

`/panel/applicant/applications/<id>`. Na każdym ekranie widać pasek sekcji ze stanem, informację "zapisano o ...", licznik dni do końca naboru i przycisk "Złóż wniosek" (wyłączony, dopóki czegoś brakuje).

- **Część I. Dane wnioskodawcy**: rodzaj wnioskodawcy, data wpisu do rejestru, roczny przychód, gmina realizacji, krótka charakterystyka.
- **Część II. Informacje o projekcie**: tytuł, czas trwania, cel, pomysł, opis działań, promocja, rezultaty i ich trwałość, liczba uczestników, sposób monitorowania, tabela pozostałych rezultatów, trzy pola dostępności (architektoniczna, cyfrowa, informacyjno-komunikacyjna).
- **Część III. Budżet**: tabele kosztów A (bezpośrednie), B (promocja), C (pośrednie). Sumy, wartość całkowita, wnioskowana kwota i procenty **mają się liczyć same**. Nie ma pola, w które wpisujesz sumę.
- **Część IV. Oświadczenia**: dziesięć oświadczeń dla organizacji (osiem dla grupy nieformalnej, bo dwa dotyczą tylko organizacji) i klauzula RODO. Klauzula odsyła do "daty wskazanej w ogłoszeniu konkursu", a sama data usunięcia danych z kroku 1.4 jest na publicznej stronie konkursu, nie w treści klauzuli.

**Rzeczy, które trzeba sprawdzić celowo, bo to są obietnice produktu:**

1. **Autozapis.** Wpisz coś, poczekaj na "zapisano o ...", zamknij kartę przeglądarki, wróć. Treść i miejsce mają wrócić.
2. **Liczby liczy komputer.** Zmień jedną pozycję budżetu i patrz na sumy oraz procenty.
3. **Limit z konkursu blokuje.** Wpisz w budżecie wnioskowaną kwotę powyżej 7 000 zł: ma być błąd mówiący o limicie konkursu, a nie ciche przyjęcie. To samo z kosztami pośrednimi powyżej 10 procent dotacji.
4. **Licznik znaków** przy polach z limitem ("176 z 500").
5. **Przejście dalej z niedokończoną sekcją jest dozwolone**, sekcja dostaje stan "są błędy".
6. **Jeden rodzaj komunikatu.** Podpowiedź stoi pod polem cicho, błąd pojawia się tylko wtedy, gdy naprawdę jest, i znika, gdy poprawisz.

### D4. Załącznik

W sekcji załączników wrzuć plik PDF na kafelek `Statut`, myszką i przyciskiem wyboru pliku. Podmiana to przeciągnięcie nowego na to samo miejsce. Plik w złym formacie albo za duży ma dostać czytelną odmowę.

### D5. Podsumowanie i złożenie

Ekran podsumowania: cały wniosek do przeczytania, sekcje zwinięte, przy każdej "popraw".

1. Zostaw celowo jeden brak (na przykład usuń załącznik). Przycisk "Złóż wniosek" ma być **widoczny i wyłączony**, a obok lista braków, w której każda pozycja jest odnośnikiem prowadzącym prosto do tego pola. To jest najważniejszy ekran w całym produkcie, bo od niego zależy, czy człowiek poradzi sobie bez telefonu do OCWIP.
2. Uzupełnij brak. Przycisk się włącza.
3. Kliknij "Złóż wniosek": **jedno** okno potwierdzenia, z ostrzeżeniem, że po złożeniu nie da się edytować. Potwierdź.
4. Po złożeniu: komunikat "Wniosek został złożony", wniosek ma numer, treść jest zamrożona, jest do pobrania PDF potwierdzenia.
5. W logu backendu ma być mail "Potwierdzenie złożenia oferty" z numerem wniosku. Maila **nie ma** dla wersji roboczej i to jest celowe.

---

## Ścieżka E. Wnioskodawca: grupa nieformalna

Okno prywatne, konto `grupa@example.org`. Ta sama droga co D, ale w części I wybierasz grupę nieformalną. Wtedy zamiast rejestru i przychodu pojawia się nazwa grupy, tabela członków grupy i rachunek lidera.

To jest próba pól warunkowych: sprawdź, że pola organizacji zniknęły, że tabela członków przyjmuje kilka wierszy i że wniosek da się złożyć bez danych, których od grupy nie wymagamy. Złóż go, bo w dalszych krokach grupa trafia na listę rezerwową.

---

## Ścieżka F. Operator prowadzi nabór

Wróć do okna operatora.

### F1. Lista wniosków

`/panel/operator/applications/<ID_KONKURSU>`. Ma pokazywać **tylko złożone** wnioski, oba, ponumerowane w kolejności wpływu. Kolumny: numer, nazwa podmiotu, tytuł projektu, całkowity koszt, wnioskowana kwota, status, wynik oceny formalnej. Na dole suma kwot wnioskowanych i ile zostało z puli.

Sprawdź: sortowanie, filtrowanie po rodzaju wnioskodawcy i statusie, "Pobierz arkusz (CSV)", "Pobierz cały wniosek (PDF)" na pojedynczym wniosku (polskie znaki mają wyglądać poprawnie).

Sprawdź też zakładki konkursu w stanie "trwa nabór": te, które jeszcze nie mają treści, mają być **wyszarzone z wyjaśnieniem**, a nie ukryte.

### F2. Zwrot wniosku do poprawy

Mechanizm przeniesiony z narzędzia, którego OCWIP używa dziś, i to jest jedyna droga, którą wniosek wraca do edycji.

1. Wejdź w wniosek organizacji, "Zwróć do poprawy": wskaż, **które sekcje** wnioskodawca może edytować, napisz co ma poprawić i do kiedy.
2. W logu ma być mail do wnioskodawcy z powodem i terminem.
3. W oknie wnioskodawcy: na wniosku stan "Wniosek zwrócony do poprawy", przycisk "Popraw wniosek", odblokowane **tylko** wskazane sekcje, reszta szara, przy każdej odblokowanej powód, widoczny licznik dni.
4. Popraw i złóż ponownie. **Poprawka bez ponownego złożenia się nie liczy.**
5. U operatora: wniosek ma dwie sumy kontrolne, pierwotną i ostatnią, oraz dwie wersje w historii. Numer wniosku zostaje ten sam.

---

## Ścieżka G. Ocena formalna

Jedna osoba, pytania tak albo nie. Robi to operator, nie ekspert.

`/panel/operator/evaluation/<ID_KONKURSU>`, potem wejdź w wniosek i "Rozpocznij ocenę formalną". Karta 2026 pyta między innymi: złożony w terminie, uprawniony wnioskodawca, siedziba w województwie, działania w województwie i w terminie, kwota do 7 000 zł, a dla organizacji dodatkowo przychód do 50 000 zł i wpis do rejestru w ciągu 60 miesięcy. Przy każdym pytaniu jest uzasadnienie.

Zrób oba wnioski na "tak" i "Zakończ ocenę". Wynik oceny formalnej ma pojawić się w kolumnie na liście wniosków.

**Wynik negatywny nie zamyka sprawy:** wniosek z brakiem formalnym można zwrócić do poprawy również na tym etapie. Jeśli masz czas, zrób to na jednym wniosku i cofnij, ale wtedy pamiętaj, żeby na koniec oba wnioski były po ocenie formalnej pozytywnej.

---

## Ścieżka H. Eksperci i ocena merytoryczna

### H1. Ustawienia oceny

`/panel/operator/evaluation/<ID_KONKURSU>`, sekcja ustawień:

- "Liczba ekspertów oceniających jeden wniosek": `2`
- "Wynik wniosku z kart ekspertów": `suma punktów`
- "Próg punktowy": `50`
- "Punkty za kryteria strategiczne liczą się do progu": odznaczone

Zapisz ustawienia. Karta merytoryczna 2026 ma maksymalnie 50 punktów, więc przy dwóch ekspertach i sumie próg 50 znaczy "średnio połowa punktów".

### H2. Powołanie i przypisanie

`/panel/operator/reviewers`: dwa konta recenzentów są widoczne jako zespół. Na liście rankingowej albo na liście wniosków zaznacz oba wnioski i "Przypisz zaznaczone ekspertowi", osobno dla `recenzent@example.org` i `recenzent2@example.org`. Każdy wniosek ma mieć dwóch ekspertów.

### H3. Deklaracja bezstronności i karty

Okno eksperta, konto `recenzent@example.org`, `/panel/reviewer`.

1. Zanim cokolwiek zobaczy, ma złożyć deklarację ("Składam deklarację"). **Przed deklaracją treści wniosków nie widzi**: to jest oświadczenie o konflikcie interesów i jest warunkiem wejścia.
2. Po deklaracji: lista przypisanych wniosków, z kolumną "Twoja karta".
3. Wejdź w wniosek organizacji, wypełnij kartę merytoryczną: pomysł i cel (0-20), rezultaty (0-16), promocja (0-10), budżet (0-4), przy każdym uzasadnienie. Suma liczy się sama. Dalej proponowana kwota dotacji, ewentualne kwestionowane pozycje budżetu, uzasadnienie obniżenia, na koniec kryteria strategiczne (białe plamy, grupa z patronem, młoda organizacja).
4. Daj organizacji dużo punktów (na przykład 18 + 14 + 9 + 3 = 44) i kwotę `5700`, grupie mniej (12 + 10 + 6 + 2 = 30) i też kwotę `5700`. "Zakończ ocenę".
5. Powtórz to samo kontem `recenzent2@example.org`.

**Co ma być sprawdzone przy okazji:** ekspert widzi wyłącznie wnioski przypisane mu w tym konkursie i nic więcej. Spróbuj wejść z jego konta na `/panel/operator` i na cudzy wniosek z adresu: ma odmówić.

---

## Ścieżka I. Rozstrzygnięcie i wyniki

Okno operatora, `/panel/operator/evaluation/<ID_KONKURSU>`.

### I1. Zamknięcie naboru i start oceny

Na stronie konkursu: "Zamknij nabór", potwierdź, potem "Rozpocznij ocenę", potwierdź. Po zamknięciu naboru nikt nie złoży już wniosku, także przy naborze ciągłym. Sprawdź to w oknie wnioskodawcy: przycisk "Wypełnij wniosek" na publicznej stronie ma przestać działać.

### I2. Lista rankingowa i kwoty

Lista ma być ułożona według punktów, z kwotą wnioskowaną i rekomendowaną, a **kwota przyznana jest polem edytowalnym wprost na liście**. Zawsze widoczne: ile przyznano z puli i ile zostało, na czerwono po przekroczeniu.

1. Wpisz organizacji `5700`. Samo wpisanie kwoty oznacza, że wniosek dostał dofinansowanie.
2. Grupie nie wpisuj nic: jest nad progiem, ale bez pieniędzy, więc trafi na listę rezerwową.
3. Sprawdź czerwony stan: wpisz na chwilę kwotę większą niż pula i cofnij.
4. Eksport: PDF do publikacji, XLSX i CSV do liczenia.

### I3. Zatwierdzenie i wiadomości

1. "Zatwierdź wyniki konkursu", potwierdź. Ma pojawić się "Wyniki zatwierdzono ...". To jedna nieodwracalna decyzja: w tej samej transakcji konkurs przechodzi w rozstrzygnięty.
2. "Wyślij wiadomości o wynikach". W logu mają być dwa maile: dla organizacji "wniosek dofinansowany" z przyznaną kwotą, dla grupy "wniosek na liście rezerwowej" z numerem wniosku.
3. "Wyślij brakujące wiadomości" jest dla przypadku, gdy część wyjdzie, a część nie. Kliknij: nie ma wysłać nikomu drugiego maila.

### I4. Udostępnienie kart oceny wnioskodawcom

"Udostępnij karty wnioskodawcom". To **jedna decyzja na cały konkurs i jest nieodwracalna**, więc kliknij ją świadomie. Potem w oknie wnioskodawcy na jego wniosku mają być widoczne: karta oceny formalnej i dwie karty merytoryczne.

### I5. Widok publiczny i archiwum

W oknie bez konta: `/competitions/<id>/results` pokazuje dwie tabele, dofinansowane z kwotą i rezerwowe bez kwoty. Potem przenieś konkurs do archiwum ("Przenieś do archiwum" na stronie konkursu) i sprawdź `/archive`: konkurs znika z listy aktualnych, a jego strona zostaje pod tym samym adresem.

Archiwizację zrób **po** ścieżkach J i K, jeśli chcesz zachować kolejność zgodną z życiem.

---

## Ścieżka J. Umowa

Umowę podpisuje się ręcznie, poza systemem. System ma ją sporządzić z danych i zapisać fakt podpisania.

### J1. Wzór umowy

`/panel/operator/evaluation/<ID_KONKURSU>`, sekcja wzoru umowy. Wzór 2026 przyszedł z importu w C2. Zobacz listę znaczników: te oznaczone jako systemowe system wypełnia sam (nazwa podmiotu, kwota, tytuł projektu, członkowie grupy), pozostałe operator wpisuje przy każdej umowie. Numer i datę umowy z NIW można wpisać wprost we wzór konkursu i opublikować nową wersję, zamiast wpisywać je przy każdej umowie.

### J2. Sporządzenie i pola

Wejdź w wniosek dofinansowany, "Przygotuj umowę". Wypełnij pola niesystemowe, zapisz. Pobierz PDF: polskie znaki mają być poprawne.

W oknie wnioskodawcy na jego wniosku ma być "Umowa jest przygotowana do podpisu." i odnośnik "Pobierz umowę (PDF)".

### J3. Hurtem

"Pobierz wszystkie umowy (ZIP)" sporządza brakujące umowy wszystkich dofinansowanych wniosków i pobiera komplet. Umowa z niewypełnionym polem nie wchodzi do paczki, a plik `braki.txt` w archiwum ją wymienia. Sprawdź oba przypadki: raz z kompletem pól, raz z brakiem.

### J4. Data podpisania

Na wniosku wpisz "Data podpisania" (dzisiejsza, nigdy przyszła) i "Zapisz podpisanie umowy", potwierdź. To jedno pole robi trzy rzeczy: przestawia stan wniosku na "umowa podpisana", zasila znacznik daty we wzorze i wyznacza początek realizacji projektu, bo wzór liczy czas trwania "od dnia podpisania umowy".

W oknie wnioskodawcy stan ma być "Umowa podpisana <data>".

---

## Ścieżka K. Rezygnacja i lista rezerwowa

Realny przypadek: organizacja dostała dotację i przed podpisaniem umowy się wycofuje, a pieniądze mają iść dalej. Rezygnację zgłasza wnioskodawca poza systemem (telefon, mail), a operator ją potwierdza.

Jeśli w ścieżce J podpisałeś już umowę organizacji, zrób K na drugim dofinansowanym wniosku albo cofnij się i wykonaj K przed J4. Rezygnacja dotyczy wniosku **dofinansowanego z umową niepodpisaną**.

1. `/panel/operator/evaluation/<ID_KONKURSU>`, panel rezygnacji: widać dofinansowane wnioski bez podpisanej umowy, z oznaczeniem tych, którym minęło 14 dni od ogłoszenia wyników.
2. "Potwierdź rezygnację <numer>". W logu ma być mail o rezygnacji. Stan wniosku: `Rezygnacja`.
3. System sam proponuje następny wniosek z listy rezerwowej i kwotę. Wpisz "Kwota dotacji z listy rezerwowej" i "Przyznaj dofinansowanie <numer>". Kwota ma się mieścić w tym, co zostało z puli.
4. W logu mail "Dofinansowanie z listy rezerwowej" z kwotą. Grupa jest teraz dofinansowana i ma prawo do umowy (wróć do J dla niej).

---

## Ścieżka L. Sprawozdanie i rozliczenie

Formalnie poza MVP (opis pełnej ścieżki czeka na dokument od zamawiającego), ale mechanizm jest zrobiony i wzór 2026 przyszedł z importem, więc warto przejść.

1. Okno wnioskodawcy, na dofinansowanym wniosku z podpisaną umową: "Przejdź do sprawozdania". Wypełnij formularz sprawozdania (zasada "było i jest": plan z wniosku obok wykonania), "Złóż sprawozdanie".
2. Okno operatora, `/panel/operator/evaluation/<ID_KONKURSU>/reports/<id>`: przeczytaj sprawozdanie, potem albo "Zwróć" z powodem (wtedy wnioskodawca poprawia i składa ponownie), albo "Przyjmij".
3. "Rozliczenie dotacji": uznawanie kosztów pozycja po pozycji, kwota do zwrotu. Po przyjęciu wniosek przechodzi w stan "rozliczony", co zamyka projekt i uruchamia liczenie retencji.

---

## Ścieżka M. Terminy i zadania w tle

Dwie rzeczy dzieją się bez kliknięcia. Zadania w tle chodzą lokalnie co 60 sekund (`BACKGROUND_JOBS_ENABLED=true`).

1. **Twarde odcięcie terminu naboru.** Na osobnym konkursie próbnym ustaw koniec naboru na 2 minuty do przodu, rozpocznij wniosek w drugim oknie i spróbuj go złożyć po upływie terminu. Ma odmówić, co do minuty, komunikatem o zamkniętym naborze. Wersja robocza zostaje.
2. **Przypomnienie trzy dni przed końcem naboru.** Na konkursie próbnym ustaw koniec naboru na 3 dni minus kilka minut, zostaw rozpoczęty i niezłożony wniosek, poczekaj na przebieg zadania i sprawdź log: mail ma iść **tylko** do osób z rozpoczętym i niezłożonym wnioskiem, jeden raz.

---

## Ścieżka N. Izolacja danych, czyli próby, które mają się nie udać

To nie jest dodatek do scenariusza, to jest jego druga połowa. Reguła: **wnioskodawca nigdy nie widzi cudzego wniosku**, a brak reguły oznacza brak dostępu.

Zanotuj identyfikator wniosku organizacji i spróbuj po kolei:

| Próba | Oczekiwane |
|---|---|
| konto `grupa@example.org` otwiera `/panel/applicant/applications/<id wniosku organizacji>` | odmowa, nie treść |
| konto wnioskodawcy otwiera `/panel/operator` i `/panel/operator/competitions/<id>` | odesłanie albo odmowa |
| konto wnioskodawcy otwiera `/panel/reviewer` | odmowa |
| ekspert otwiera wniosek, którego nie ma przypisanego | odmowa |
| ekspert otwiera konkurs, do którego nie jest powołany | odmowa |
| wylogowany otwiera dowolny adres w `/panel/...` | logowanie |
| wylogowany otwiera `/competitions/<id nieopublikowanego konkursu>` | nie znaleziono; konkurs roboczy nie ma publicznego adresu |
| pobranie cudzego załącznika z adresu pliku | odmowa |

Sprawdź też, czego **nie ma w logu**: żadnego hasła, żadnego PESEL-u, żadnej treści wniosku.

```bash
docker compose logs backend | grep -i "TestHaslo123\|password" | grep -v "DEV EMAIL"
```

---

## Czego tym przejściem nie sprawdzisz

Żeby nie szukać tego godzinę.

| Rzecz | Dlaczego nie teraz |
|---|---|
| Prawdziwa wysyłka maili (SMTP) | świadomie zostawione na serwer; lokalnie mail idzie do logu |
| Prośba o dostęp do istniejącej karty organizacji po NIP-ie i jej zatwierdzenie | nie ma tego ekranu; model wiąże dziś użytkownika z podmiotem jeden do jednego (rozbieżność `R-01`) |
| Cokolwiek roli administratora | rola nie istnieje w kodzie, `grant-role` zna `Applicant`, `Operator`, `Reviewer` |
| Retencja i usuwanie danych osobowych po terminie | zadanie zablokowane na decyzję zamawiającego (`T-47b`, bloker `B-05`) |
| Aneksy i transze wypłat | poza MVP, interfejs pokazuje jedną wypłatę |
| Oferty wspólne i wgranie oferty z pliku XML | świadomie nie przenosimy z obecnego narzędzia |
| HTTPS, nagłówki przez proxy, hasło stagingu, kopie zapasowe | to compose produkcyjny i staging, nie stos deweloperski; próby opisuje [`wdrozenie.md`](wdrozenie.md) |
| Ścieżka odwoławcza od oceny | brak regulaminu, więc nie jest zaprojektowana |

---

## Karta wyniku

Odhaczaj w trakcie, a nie z pamięci po godzinie. Przy porażce notuj: adres strony, co kliknąłeś, co się stało, co miało się stać.

```
[ ] 0  stos wstaje, health 200, pięć kont z create_test_users.py
[ ] A  strony publiczne i przełącznik kontrastu
[ ] B1 rejestracja z ekranu, obie zgody, ta sama odpowiedź dla zajętego adresu
[ ] B2 potwierdzenie adresu, zepsuty link odmawia czytelnie
[ ] B3 logowanie, wylogowanie, ochrona panelu
[ ] B4 reset hasła, ta sama odpowiedź dla nieistniejącego adresu
[ ] B5 blokada po pięciu nieudanych próbach
[ ] B6 zmiana hasła i adresu z potwierdzeniem na nowy adres
[ ] C1 kreator konkursu, siedem kroków, zapis w trakcie
[ ] C2 import treści startowej, powtórzenie nic nie psuje
[ ] C3 formularz w kreatorze i podgląd
[ ] C4 wzór załącznika do pobrania u gościa
[ ] C5 publikacja, lista braków, publiczna strona konkursu
[ ] D1 wejście z publicznej strony i powrót po zalogowaniu
[ ] D2 karta podmiotu
[ ] D3 cztery części, autozapis, liczby liczone, limity blokują
[ ] D4 załącznik, podmiana, odmowa złego formatu
[ ] D5 lista braków z odnośnikami, jedno potwierdzenie, numer, PDF, mail
[ ] E  grupa nieformalna: pola warunkowe i złożenie
[ ] F1 lista wniosków, kolumny, sumy, eksporty, wyszarzone zakładki
[ ] F2 zwrot do poprawy, tylko wskazane sekcje, dwie sumy kontrolne
[ ] G  ocena formalna obu wniosków
[ ] H1 ustawienia oceny
[ ] H2 przypisanie dwóch ekspertów do obu wniosków
[ ] H3 deklaracja przed treścią, dwie karty merytoryczne, zakończenie
[ ] I1 zamknięcie naboru odcina składanie
[ ] I2 ranking, kwota wprost na liście, stan puli, eksporty
[ ] I3 zatwierdzenie wyników i maile, bez duplikatów
[ ] I4 udostępnienie kart widoczne u wnioskodawcy
[ ] I5 publiczne wyniki i archiwum
[ ] J  wzór umowy, sporządzenie, PDF u wnioskodawcy, ZIP, data podpisania
[ ] K  rezygnacja i przejście środków na listę rezerwową
[ ] L  sprawozdanie: złożenie, zwrot, przyjęcie, rozliczenie
[ ] M  odcięcie terminu i przypomnienie trzy dni przed końcem
[ ] N  wszystkie próby dostępu zakończone odmową, log bez haseł i danych
```
