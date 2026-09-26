# M6 · Wyniki i umowa

Rozstrzygnięcie konkursu, powiadomienia, eksport i umowa. Trzy pierwsze karty wiszą na B-02 przez ranking, czwarta na B-03 przez brak wzoru umowy.

Jedna część zakresu jest odblokowana już teraz i warto ją wyciąć jako osobną kartę: eksport **złożonego wniosku** do PDF nie potrzebuje ani rankingu, ani wzoru umowy, a jest realnie potrzebny do testów i do demonstracji. Patrz [T-44](#t-44-p0--backend-eksport-wniosku-i-wyników-do-pdf).

Przebieg rozstrzygnięcia i lista znaczników dokumentów: [`proces.md`](proces.md), ścieżka 6.

---

## T-42 [P0 / Backend] Decyzja o dofinansowaniu i kwoty dotacji

Karta: <https://trello.com/c/kJPn2EOF> · Zablokowane przez B-02 przez zależność od rankingu.

**Kontekst.** Operator ręcznie przypisuje kwoty dotacji, patrząc na listę rankingową. To decyzja człowieka, nie systemu: przyznana kwota bywa niższa niż wnioskowana i wynika z puli, która została w konkursie.

**Zakres.** Zapis decyzji na wniosku (dofinansowany, odrzucony, lista rezerwowa), przyznana kwota, uzasadnienie, wpis w historii statusów, oraz zbiorcze zatwierdzenie wyników konkursu.

**Dlaczego wyniki ogłaszamy zbiorczo.** Bo wnioskodawcy nie mogą dowiadywać się o wyniku pojedynczo, w miarę jak operator klika. Do momentu zatwierdzenia decyzje są robocze i niewidoczne na zewnątrz.

**Pilnowanie puli.** System pokazuje operatorowi sumę przyznanych kwot względem puli konkursu. Nie blokujemy przekroczenia, tylko sygnalizujemy, bo decyzja o puli należy do OCWIP.

**Zależności.** Blokuje nas: T-39, T-41. Blokujemy: T-43, T-44, T-45.

**Co raport dokłada.** Kwota przyznana jest **polem edytowalnym wprost na liście rankingowej**, obok kwoty wnioskowanej i rekomendowanej, plus kolumna uwag na wpisy ręczne. Zawsze widoczne podsumowanie: "przyznano 84 000 zł z 100 000 zł, zostało 16 000 zł", na czerwono po przekroczeniu. **Wpisanie kwoty oznacza, że wniosek dostał dofinansowanie.** Eksport listy rankingowej: PDF do publikacji, XLSX i CSV do liczenia, a listę da się opublikować na stronie bez przepisywania.

**Stan, który wygląda na zbędny, a nie jest.** Raport wprowadza `dofinansowany, umowa niepodpisana` jako osobny stan między przyznaniem dotacji a podpisem, bo umowę podpisuje się ręcznie, poza systemem, i między tymi momentami jest realny odstęp, w którym pieniądze są zarezerwowane, a zobowiązania jeszcze nie ma. Bez tego stanu na liście rankingowej nie widać różnicy między przyznane a przyznane i domknięte.

**Czego raport nie opisuje i co jest pytaniem do regulaminu.** Co się dzieje, gdy wnioskodawca po przyznaniu dotacji zrezygnuje albo nie podpisze umowy: czy zwolniona kwota wraca do puli i czy wchodzi wtedy kolejny wniosek z rankingu. Od tego zależy, czy potrzebny jest stan `rezygnacja` i lista rezerwowa. To jedna z dwóch rzeczy, przez które raport prosi o regulamin. Dopisz to do B-02 albo załóż osobny bloker.


**Stan 2026-09-26 (zrobione).** Kwota przyznana i uwaga edytowane w wierszu listy rankingowej, obok kwoty wnioskowanej i rekomendowanej; nad listą "przyznano X z Y, zostało Z", przekroczenie słowami i kolorem, bez blokady. "Zatwierdź wyniki konkursu" działa raz i dopiero wtedy, gdy każda ocena się skończyła; zapisuje `Funded` (kwota), `Reserve` (miejsce na liście powyżej progu, bez kwoty) albo `Rejected` z wpisem w historii statusów. Regulamin 2026 odpowiada na pytanie o rezygnację (umowa niepodpisana w 14 dni, środki idą na kolejny wniosek z listy spełniający próg), stąd stan listy rezerwowej (ZR-09); samo przejście przy rezygnacji należy do umowy (T-43, T-45). Eksport i publikacja listy wydzielone do T-42a.

---

## T-42a [P1 / Full-stack] Eksport i publikacja listy rankingowej

Karta: <https://trello.com/c/aaIcNYYr>

**Zakres (raport).** Eksport listy rankingowej: PDF do publikacji, XLSX i CSV do liczenia, tą samą drogą co eksport listy wniosków z T-35 (bez nowej zależności dla CSV i PDF; XLSX do decyzji, bo wymaga biblioteki). Publikacja listy na stronie konkursu bez przepisywania, dopiero po zatwierdzeniu wyników.

**Zależności.** Blokuje nas: T-42.

**Stan 2026-09-26 (zrobione).** Pod listą rankingową linki "PDF do publikacji", XLSX i CSV (każdy stan, PDF mówi w nagłówku, czy wyniki są zatwierdzone). Po zatwierdzeniu publiczna strona `/competitions/{id}/results` z dofinansowanymi i listą rezerwową, link "Wyniki konkursu" na stronie konkursu. XLSX bez nowej zależności. Bez odrzuconych na liście publicznej (ZR-10).
---

## T-43 [P0 / Backend] Powiadomienia o wynikach konkursu

Karta: <https://trello.com/c/EFTVE59t> · Zablokowane przez zależność od T-42.

**Kontekst.** Mail jest jedynym kanałem kontaktu z wnioskodawcą. Informacja o przyznaniu dotacji to najważniejsza wiadomość, jaką ten system w ogóle wysyła.

**Zakres.** Wysyłka powiadomień o wyniku po zatwierdzeniu wyników konkursu, z rozróżnieniem na dofinansowany, odrzucony i lista rezerwowa. Widok wyniku w panelu wnioskodawcy.

**Dlaczego wysyłka musi być odporna na błąd.** Przy 120 wnioskach wysyłka trwa i może się przerwać w połowie. Musi dać się bezpiecznie wznowić bez wysyłania drugi raz do tych samych osób. Podwójna informacja o przyznaniu dotacji to telefon do OCWIP z pytaniem, która wersja jest prawdziwa.

**Czego nie wysyłamy mailem.** Uzasadnień zawierających dane osobowe innych wnioskodawców ani punktacji innych ofert.

**Zależności.** Blokuje nas: T-42, T-12.2. Blokujemy: nic.

**Co raport dokłada.** Wraz z kartami oceny idzie e-mail z wynikiem, **także ten z odmową**: brak wiadomości przy odmowie to najczęstsza skarga na konkursy grantowe. Treść jest ustawiana przy konkursie, nie zaszyta w kodzie.

**Stan infrastruktury mailowej.** `EmailSenderService` na produkcji dziś **wyłącznie loguje treść maila**, nie wysyła nic naprawdę. Prawdziwy dostawca SMTP jest osobną kartą, której nie ma na Trello. Bez niej ta karta nie jest skończona, choć przejdzie testy. Pozycja `R-18` w [`rozbieznosci.md`](rozbieznosci.md).

Pełna lista wiadomości, które system wysyła, wraz z informacją, których treść ustawia OCWIP, a które są tekstem systemowym: [`proces.md`](proces.md), sekcja "Wiadomości".


**Stan 2026-09-26 (zrobione, poza prawdziwym SMTP).** Zatwierdzenie wyników zapisuje w tej samej transakcji mail zaległy dla każdego wniosku. Operator ustawia trzy treści w sekcji "Powiadomienia o wynikach" i wysyła; przebieg przerwany w połowie wznawia się tym samym przyciskiem, a wysłany mail nie idzie drugi raz. Wnioskodawca widzi wynik i przyznaną kwotę w widoku złożonego wniosku. Wysyłka nadal tylko loguje: prawdziwy dostawca to T-43a.

---

## T-43a [P0 / Backend] Prawdziwa wysyłka maili (SMTP)

Karta: <https://trello.com/c/RMHWS5Ht>

**Zakres.** Rozbieżność R-18. `IEmailSender` przez SMTP, konfigurowany zmiennymi środowiskowymi (host, port, TLS, użytkownik, hasło, nadawca, wszystko w `.env.example`); gdy SMTP nie jest ustawione, zostaje dzisiejszy log, ale bez treści maila poza środowiskiem deweloperskim, bo treść niesie dane osobowe (AGENTS.md, bezpieczeństwo, punkt 4). Błąd dostawcy wraca do wywołującego, żeby T-43 zapisał go jako nieudaną próbę.

**Zależności.** Blokuje nas: T-43. Dane dostawcy od OCWIP są potrzebne do wdrożenia (T-48), nie do zbudowania.

**Stan 2026-09-26 (zrobione).** `SmtpEmailSender` przez `System.Net.Mail`, wybierany przy starcie, gdy ustawiono `SMTP_HOST` (bez `SMTP_FROM` API nie startuje). Bez hosta zostaje log: w Development cały mail, gdzie indziej tylko temat. Zmienne w `.env.example` i `docker-compose.yml`. Do wdrożenia brakuje tylko danych przekaźnika od OCWIP.
---

## T-44 [P0 / Backend] Eksport wniosku i wyników do PDF

Karta: <https://trello.com/c/qRlz6aCv>

**Kontekst.** Dotacje to środki publiczne, więc dokumentacja musi dać się wydrukować i podpiąć do teczki konkursu. Retencja 5 lat oznacza, że PDF jest formą, w której te dokumenty przetrwają niezależnie od losów aplikacji.

**Zakres.** Eksport pojedynczego wniosku do PDF (odpowiedzi wraz z wersją formularza, według której był wypełniany) oraz eksport listy rankingowej i wyników konkursu.

**Dlaczego PDF generujemy z zapisanej wersji formularza.** Bo wniosek złożony według wersji 3 ma wyglądać tak, jak wtedy wyglądał, nawet jeśli formularz zmienił się później trzy razy.

**Dostępność wydruku.** Wygenerowany PDF ma być czytelny na papierze w czerni i bieli. Kolor nie może być jedynym nośnikiem informacji.

**Zależności.** Blokuje nas: T-33, T-42, T-25.

**Podział, który odblokowuje połowę tej karty.** Eksport pojedynczego wniosku potrzebuje tylko `T-33` i `T-25`, czyli **nic z M5 i M6**. Eksport listy rankingowej i wyników potrzebuje `T-42`. To są dwie różne rzeczy sklejone w jedną kartę i warto je rozdzielić, zwłaszcza że PDF potwierdzenia złożenia jest wymagany już w `T-33`. Propozycja: `T-44a` eksport wniosku, dostępny od razu, `T-44b` eksport wyników, zablokowany.

**Co raport dokłada.** Wydruk musi nieść **tę samą sumę kontrolną co wersja elektroniczna, na każdej stronie** (decyzja D15). Na wydruk trafiają wyłącznie pola oznaczone jako drukowane: pola techniczne, które istnieją tylko po to, żeby coś policzyć albo spiąć dwie sekcje, są widoczne w interfejsie, ale nie na wydruku (decyzja D14). Formaty, których używa obecne narzędzie i których raport się trzyma: **PDF do odczytu i RTF do dalszej edycji**. Jeśli zamawiający potrzebuje DOCX, to drobna różnica po naszej stronie, ale trzeba o tym wiedzieć.

**Stan 2026-09-26 (zrobione).** Eksport listy rankingowej i wyników zrobił T-42a. Tu doszedł cały wniosek jako PDF (`GET /applications/{id}/pdf`, link w widoku wnioskodawcy i operatora): z zapisanej wersji formularza, tylko pola drukowane i widoczne (D14), numer i suma kontrolna w nagłówku każdej strony (D15), sekcje wyróżnione wielkimi literami, nie kolorem. Świadomie bez RTF i bez polskich znaków w PDF (ZR-11).

---

## T-45 [P1 / Backend] Generowanie umowy ze wzoru

Karta: <https://trello.com/c/kbHK5Nsk> · **ZABLOKOWANE PRZEZ B-03** (brak wzoru umowy). Druga w kolejności do wycięcia, jeśli zabraknie czasu, zaraz po sprawozdawczości.

**Kontekst.** Operator jednym kliknięciem generuje umowy ze wzoru, z podstawionymi danymi podmiotu i kwotą dotacji. Podpis odbywa się poza systemem, ręcznie (decyzja D4). Nie integrujemy się z żadnym dostawcą podpisu elektronicznego.

**Zakres po odblokowaniu.** Wzór umowy z miejscami na dane, podstawienie danych podmiotu i kwoty, generowanie dokumentu, pobranie przez operatora i przez wnioskodawcę.

**Tutaj pojawiają się PESEL-e.** To pierwsze miejsce w systemie, w którym przetwarzamy PESEL osób fizycznych z grup nieformalnych. Każde pole trzymające taką daną oznaczamy komentarzem w kodzie. Szyfrowanie i reguły dostępu to karta T-47, ale **ta karta nie może wejść na produkcję przed nią**.

**Czego nie robimy.** Aneksów do umów ani zmian budżetu w trakcie realizacji.

**Zależności.** Blokuje nas: B-03 twardo, T-42. Blokujemy: sprawozdawczość, jeśli w ogóle wejdzie.

**Co raport dokłada, i jest tego dużo.** Raport nazywa to mechanizmem, o który zamawiający prosi najgłośniej, bo dziś zmiana nazwy jednego pola w umowie oznacza zgłoszenie do dostawcy i oczekiwanie liczone w miesiącach.

**Model:** dokument to treść z osadzonymi znacznikami, a znacznik jest nazwanym odwołaniem do danej z systemu. Wzór ustawia się raz, nie przy każdej umowie. Ustawienia wzoru:

| Ustawienie | Co znaczy |
|---|---|
| Poziom | wzór na całą organizację albo na konkretny konkurs; wzór z wyższego poziomu obowiązuje wszędzie, chyba że konkurs ma własny |
| Nazwa wzoru | na przykład "Umowa o dofinansowanie 2026" |
| Punkt | miejsce w systemie, z którego dokument da się pobrać; może być kilka |
| Uprawnienia | kto może pobrać: wszyscy, operator, wnioskodawca |
| Format | PDF do odczytu albo RTF do dalszej edycji |
| Filtr | których wniosków dokument dotyczy, na przykład tylko dofinansowanych |

**Znaczniki wstawiają nie tylko pojedyncze wartości, ale i całe tabele:** kosztorys, tabelę rezultatów, listę ekspertów z kolumną na podpis, wyniki oceny. Kwota słownie jest osobnym znacznikiem. Pełna lista znaczników mających pokrycie we wzorze wniosku na 2026: [`proces.md`](proces.md), krok 6.2.

**To nie jest generator umów, to generator dokumentów.** Raport pisze wprost: dokumenty komisji (protokół z posiedzenia, lista obecności, lista kontaktowa ekspertów, wyniki oceny, oświadczenia o konflikcie interesów, wykaz błędów formalnych) powstają **z tego samego mechanizmu wzorów**. Nie budujemy pięciu generatorów, budujemy jeden i podpinamy pod niego wzory, które zamawiający może zmieniać. Ta obserwacja zmienia priorytet karty: mechanizm wzorów jest potrzebny w M5, a nie dopiero w M6. Pozycja `R-06` w [`rozbieznosci.md`](rozbieznosci.md).

**Dane, których wniosek nie zbiera.** Dane urzędowe (organ wydający zarządzenia, numer i data zarządzenia o ogłoszeniu konkursu i o powołaniu komisji, numer, rok i data uchwały o programie współpracy, paragraf i klasyfikacja budżetowa) nie pochodzą ani z wniosku, ani z limitów, więc dostają osobną sekcję "Dane do dokumentów" w ustawieniach konkursu.

**Data rozpoczęcia projektu.** Wzór nie podaje jej jako daty z kalendarza, tylko jako "od dnia podpisania umowy". Znacznik daty rozpoczęcia bierze więc **datę podpisania umowy**, a nie osobne pole z wniosku. Operator wpisuje datę podpisania po fakcie i to jedno pole robi trzy rzeczy naraz: przestawia stan wniosku, zasila znacznik i wyznacza początek realizacji projektu.

**Generowanie hurtem.** Jeden dokument dla jednego wniosku albo hurtem dla całego konkursu, wtedy wynik przychodzi w jednym pliku zip, tyle dokumentów, ile wniosków po filtrze.
