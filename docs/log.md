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
## 2026-09-26 - sprawozdanie: od formularza do przyjęcia (T-50a)
**Zrobione:** Wzór sprawozdania jako formularz `Report` z `prefillFrom` i `readOnly`. Wnioskodawca z dofinansowanym wnioskiem zakłada sprawozdanie z wartościami z wniosku obok wykonania, wypełnia z autozapisem i składa; operator przyjmuje albo zwraca z powodem. Tabele `reports` i `report_status_history`.
**Decyzje:** Wartości z wniosku przywraca serwer przy każdym zapisie; sprawozdanie jest `IEntityScoped` (ekspert nic). Uzasadnienia w [`architektura.md`](architektura.md), założenie ZR-12.
**Uwaga:** Rozliczenie, termin, sprawozdanie częściowe i załączniki to T-50b (czeka na umowę T-45). Pole `readOnly` nie może być wymagane. W atrapach tabel frontu `rows: []` znaczy tabelę o stałych zerowych wierszach. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-26 - propozycja modelu umowy i sprawozdania (T-45.0, T-50.0)
**Zrobione:** Rozbiór wzorów NOWE FIO 2026 (umowa, sprawozdania 4a, 4b, 4c) na model jako dane w [`model-danych.md`](model-danych.md): sprawozdanie jako formularz `Report` z tabelą `reports`, umowa jako wersjonowany wzór ze znacznikami i tabela `contracts`. Tylko dokumentacja.
**Decyzje:** Nic nie trafia do schematu przed przeglądem, jak przy T-38.0. Polskie znaki w PDF są warunkiem umowy, nie szczegółem.
**Uwaga:** Kolejka bez odblokowanych zadań: T-45 i T-50 czekają na przegląd tej propozycji i odpowiedzi z B-03 i B-04; T-26a, T-47, T-48, T-49 na dokumenty. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-26 - cały wniosek jako PDF (T-44)
**Zrobione:** `GET /applications/{id}/pdf` dla właściciela i operatora, link w obu widokach złożonego wniosku. Z zapisanej wersji formularza, tylko pola drukowane i widoczne, numer i suma kontrolna na każdej stronie. Eksport listy rankingowej był już w T-42a.
**Decyzje:** Te same reguły widoczności i wyliczeń co ekran (`AnswerCalculator`). Bez RTF i bez polskich znaków (ZR-11, P14). Uzasadnienia w [`architektura.md`](architektura.md).
**Uwaga:** `appliesTo` jest dozwolone tylko na kartach oceny; w formularzu wniosku o pokazaniu pola decyduje `visibleWhen`. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-26 - prawdziwa wysyłka maili przez SMTP (T-43a)
**Zrobione:** `SmtpEmailSender` przez `System.Net.Mail`, włączany zmiennymi `SMTP_*` (w `.env.example` i `docker-compose.yml`); bez `SMTP_HOST` mail zostaje w logu, poza Development bez treści. R-18 zamknięte po stronie kodu.
**Decyzje:** Bez nowej zależności; nadawca wybierany przy starcie, host bez nadawcy zatrzymuje start. Uzasadnienia w [`architektura.md`](architektura.md).
**Uwaga:** Dane przekaźnika od OCWIP są potrzebne do wdrożenia (T-48). Test nadawcy mówi prawdziwym SMTP do minimalnego przekaźnika w teście. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-26 - maile o wyniku konkursu (T-43)
**Zrobione:** Zatwierdzenie wyników zapisuje mail należny każdemu wnioskowi (`result_notifications`). Operator ustawia trzy treści (dofinansowany, rezerwa, odmowa) i wysyła, a przerwaną wysyłkę wznawia bez podwójnych maili. Wnioskodawca widzi wynik i przyznaną kwotę w złożonym wniosku.
**Decyzje:** Kolejka w transakcji wyników, zajęcie wiersza warunkowym UPDATE, adres czytany przy wysyłce. Uzasadnienia w [`architektura.md`](architektura.md).
**Uwaga:** `EmailSenderService` nadal tylko loguje: prawdziwy SMTP to T-43a (R-18). Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-26 - eksport i publikacja listy rankingowej (T-42a)
**Zrobione:** Pod listą rankingową PDF, XLSX i CSV (`/competitions/{id}/ranking/export/{format}`, tylko operator). Po zatwierdzeniu wyników publiczna strona `/competitions/{id}/results` z dofinansowanymi i listą rezerwową, link na stronie konkursu.
**Decyzje:** Jedne wiersze dla trzech plików; XLSX pisany ręcznie, bez zależności; publikacja razem z zatwierdzeniem i bez odrzuconych (ZR-10). Uzasadnienia w [`architektura.md`](architektura.md).
**Uwaga:** Kroki testowe po ocenie są wspólne w `EvaluationScene.cs`. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-26 - decyzja o dofinansowaniu i zatwierdzenie wyników (T-42)
**Zrobione:** Kwota przyznana i uwaga w wierszu listy rankingowej, pasek puli nad listą, "Zatwierdź wyniki konkursu" raz i dopiero po końcu oceny: statusy `Funded`, `Reserve`, `Rejected` z wpisem w historii. Wnioskodawca widzi wynik dopiero po zatwierdzeniu, a złożony wniosek dalej otwiera się jako złożony.
**Decyzje:** Zapisy decyzji i statusów omijają `UpdatedAt`, bo liczy się z niego suma kontrolna (D15). Lista rezerwowa jako wynik (ZR-09). Uzasadnienia w [`architektura.md`](architektura.md).
**Uwaga:** Constrainty numeru i daty złożenia liczą teraz od `status <> 'Draft'`; każde miejsce, które sprawdza `=== "Submitted"`, zgubi wnioski z wynikiem. Eksport i publikacja listy w T-42a. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-25 - udostępnienie kart oceny wnioskodawcom (T-41b)
**Zrobione:** Operator udostępnia karty raz dla całego konkursu (`POST /competitions/{id}/card-sharing`, potwierdzenie w oknie, drugi raz 409). Wnioskodawca widzi w złożonym wniosku zakończone karty z wynikiem i odpowiedziami (`GET /applications/{id}/evaluation-cards`), bez niczego o oceniających. Kolumna `competitions.evaluation_cards_shared_at`.
**Decyzje:** Anonimowość w osobnym kontrakcie odpowiedzi, nie w ekranie; trasa za rolą wnioskodawcy i własnością. Uzasadnienia w [`architektura.md`](architektura.md), założenie ZR-08.
**Uwaga:** Log przekroczył limit, najstarszy wpis przeniesiony do archiwum.

## 2026-09-25 - karta formalna operatora i wgląd w oceny (T-41a)
**Zrobione:** Numer wniosku na liście rankingowej otwiera ocenę wniosku: karta formalna (przyciskiem, z autozapisem i zakończeniem), karty ekspertów z nazwiskami tylko do odczytu, pod nimi wniosek. Trasa `GET /applications/{id}/evaluations` dla operatora. `EvaluationWorkspace` w `components/evaluation/`, zna etap formalny.
**Decyzje:** Lista kart czytana przez serwis oceny, nazwisko obok karty, nie w niej (pod T-41b). Uzasadnienia w [`architektura.md`](architektura.md).
**Uwaga:** Log przekroczył limit, najstarszy wpis przeniesiony do archiwum.

## 2026-09-25 - ocena i ranking w panelu operatora (T-41)
**Zrobione:** Pozycja "Ocena" w panelu operatora: wybór konkursu, a w nim ustawienia oceny, tabela ekspertów (przypisane wnioski, deklaracja) i lista rankingowa z postępem, ekspertami wniosku i przypisaniem grupowym zaznaczonych. Doszły `GET /reviewers` i `GET /competitions/{id}/assignments` dla operatora. Ekran "Recenzenci" czyta prawdziwe konta.
**Decyzje:** Przypisanie grupowe to seria istniejących przypisań, bez nowej trasy; po każdej zmianie ekran czyta wszystko od nowa. Uzasadnienia w [`architektura.md`](architektura.md), założenia ZR-06 i ZR-07.
**Uwaga:** Karta formalna operatora i wgląd w pojedyncze oceny wydzielone do T-41a, udostępnienie kart wnioskodawcom (krok 5.5) do T-41b. Log przekroczył limit, najstarszy wpis przeniesiony do archiwum.

## 2026-09-25 - deklaracja bezstronności przed oceną (T-40a)
**Zrobione:** Tabela `reviewer_declarations`, trasy eksperta (odczyt z tekstem, decyzja raz) i widok operatora ze stanem wszystkich ekspertów konkursu. Bez akceptacji ekspert nie otwiera wniosku, załącznika ani karty, a jego lista pokazuje tylko liczbę czekających wniosków i deklarację do złożenia.
**Decyzje:** Brama w warstwie autoryzacji, nie w ekranie. Odmowa wymaga powodu i wyklucza. Każda decyzja zapisuje tekst, który ekspert widział. Uzasadnienia w [`architektura.md`](architektura.md).
**Uwaga:** Tekst deklaracji jest roboczy (ZR-05), prawdziwy to załącznik 1 do regulaminu komisji, o który pyta P8 na B-02. Testy z recenzentem akceptują teraz deklarację (`AcceptDeclarationAsync`). Log przekroczył limit, najstarszy wpis przeniesiony do archiwum.

## 2026-09-25 - panel recenzenta (T-40)
**Zrobione:** `/panel/reviewer` z listą przypisanych wniosków (`GET /reviewer/applications`, trzy sumy z raportu) i ekranem oceny: karta merytoryczna w `FormRenderer` z autozapisem i zakończeniem przez potwierdzenie, pod nią cały wniosek z załącznikami. Silnik frontu zna `appliesTo` i punkty. Enumy ocen i rankingu idą przez API tekstem.
**Decyzje:** Odczyt wniosku istniejącymi trasami za polityką zasobu, bez drugiego kontraktu. Załącznik przypisanego wniosku otwiera przypisany ekspert (dotąd 403). Uzasadnienia w [`architektura.md`](architektura.md).
**Uwaga:** Brak bramy deklaracji bezstronności (ZR-04, karta T-40a). Log przekroczył limit, najstarszy wpis przeniesiony do archiwum.

## 2026-09-25 - lista rankingowa i ustawienia oceny (T-39)
**Zrobione:** Ustawienia oceny na konkursie (liczba ekspertów, suma albo średnia, próg, próg ze strategicznymi, rozbieżność) pod `/competitions/{id}/evaluation-settings`, lista pod `/competitions/{id}/ranking`, obie tylko dla operatora. Nowy plik [`runbook/zalozenia-robocze.md`](runbook/zalozenia-robocze.md) na to, co zbudowano bez potwierdzenia (ZR-01 do ZR-03).
**Decyzje:** Lista liczona przy odczycie z zakończonych kart. Miejsce tylko przy pozytywnej ocenie formalnej i komplecie kart, remis po wcześniejszym złożeniu. Ustawienia osobną trasą, bo kreator ogłoszenia cofałby je przy każdym zapisie. Uzasadnienia w [`architektura.md`](architektura.md).
**Uwaga:** Kwota rekomendowana to średnia z kart (ZR-01), a skala rozbieżności to suma maksimów kryteriów (ZR-02); obie do potwierdzenia. Seed ustawia zasianemu konkursowi próg 50. Log przekroczył limit, najstarszy wpis przeniesiony do archiwum.

## 2026-09-25 - karty oceny NOWE FIO 2026 jako dane (T-38b)
**Zrobione:** Karta formalna (8 kryteriów, 2 tylko dla organizacji) i merytoryczna (4 kryteria z punktami i uzasadnieniem, kwota, kwestionowane pozycje, 3 kryteria strategiczne) jako dokumenty w `backend/seed/evaluation-cards/`. `seed.py` je publikuje i ustawia konkursowi wszystkie trzy wskazania wersji, test .NET sprawdza te same pliki kontraktem i wynikami.
**Decyzje:** Pliki w `backend/`, bo tylko ten katalog widzi kontener testów, a seed czyta je z hosta. Druga kwota z karty merytorycznej bez roli do czasu odpowiedzi na P3. Kryterium "młodej organizacji" zadawane każdej organizacji (`R-36`).
**Uwaga:** Seed wcześniej nie ustawiał konkursowi formularza w mocy, więc w zasianym konkursie nie dało się założyć nowego wniosku; teraz ustawia. Log przekroczył limit, najstarszy wpis przeniesiony do archiwum.

## 2026-09-25 - ocena wniosku: mechanizm kart formalnej i merytorycznej (T-38)
**Zrobione:** Karta oceny jako wersja `form_definitions` z przeznaczeniem (publikacja `/competitions/{id}/evaluation-cards/{formal|merit}`), kontrakt z `appliesTo`, punktami za "tak" i rolami oceny, tabela `evaluations`, trasy otwarcia, zapisu, zakończenia i odczytu oceny, wynik liczony przy odczycie. 918 testów backendu, klient TS przegenerowany.
**Decyzje:** Kryteria formalne jako pola z rolą, nie wiersze tabeli. Dostęp do oceny we własnym handlerze: operator czyta wszystko i pisze tylko formalną, ekspert tylko własną merytoryczną i tylko przy aktywnym przypisaniu. Uzasadnienia w [`architektura.md`](architektura.md).
**Uwaga:** `seed.py` dostał `purpose` przy wstawianiu wersji formularza i dwie tabele na liście pustości (w tym `application_assignments` z T-37, której tam brakowało). Ustawienia oceny w konkursie to T-39, treść kart 2026 to T-38b. Log przekroczył limit, najstarszy wpis przeniesiony do archiwum.

## 2026-09-25 - propozycja modelu oceny jako dane (T-38.0)
**Zrobione:** Karty oceny formalnej i merytorycznej NOWE FIO 2026 rozebrane na dokument kontraktu formularza, tabela `evaluations`, ustawienia oceny w konkursie, cztery rozszerzenia kontraktu i pięć pytań do klientki, w [`model-danych.md`](model-danych.md). Nic nie weszło do schematu: to propozycja do przeglądu, migracja dopiero po akceptacji.
**Decyzje:** Karta oceny to formularz w `form_definitions` z kolumną `purpose` (D16), nie osobne tabele kryteriów. Wyniki liczone przy odczycie, nie zapisywane. Remis to reguła (wcześniejsze złożenie), nie ustawienie.
**Uwaga:** Dopóki propozycja nie jest zaakceptowana, T-38 i dalsze stoją w kolejce jako zablokowane. Log przekroczył limit, najstarszy wpis przeniesiony do archiwum.

## 2026-09-25 - audyt dostępności WCAG 2.1 AA (T-46)
**Zrobione:** Audyt narzędziami (axe w Chrome w obu paletach, axe po każdym teście frontu, test tokenów kontrastu, test źródeł), 11 ustaleń w [`dostepnosc.md`](dostepnosc.md), 10 poprawionych: pełna paleta wysokiego kontrastu i przełącznik w każdym nagłówku, podkreślone linki na ekranach konta, krawędź pól 4,69:1 zamiast 1,3:1, widoczny fokus na polu pliku, "(wymagane)" dla czytnika, etykiety w edytorze limitów, `main` na stronach stanu. 502 testy frontu.
**Decyzje:** `axe-core` (deweloperska, bez zależności przechodnich) po każdym teście w `vitest.setup.ts` zamiast osobnego zestawu, bo testy już ustawiają każdy stan ekranu. Fokus w trybie kontrastu żółty, nie fiolet z researchu (2,1:1 na czarnym). Wybór trybu w przeglądarce, nie na koncie, przywracany skryptem w `<head>`. Uzasadnienia w [`architektura.md`](architektura.md).
**Uwaga:** Test, który renderuje ekran z naruszeniem WCAG, teraz się oblewa, także taki, który niczego o dostępności nie sprawdza. Panele nie były przechodzone klawiaturą w prawdziwej przeglądarce (brak kont z hasłem lokalnie), do powtórzenia przy T-48. Nowe `R-35`: zniekształcony link weryfikacyjny daje 500. Log przekroczył limit, najstarszy wpis (T-23) przeniesiony do archiwum.

## 2026-09-25 - przypisanie wniosków recenzentom (T-37)
**Zrobione:** `POST`/`DELETE /applications/{id}/assignments`, operator przypisuje i cofa recenzenta, wiele do wielu (nowa tabela `application_assignments`). Regułę widoczności czyta wprost `EntityScopedHandler.cs`: recenzent widzi wniosek wtedy i tylko wtedy, gdy ma dla niego aktywny wiersz przypisania, nigdy przez endpoint z osobną regułą. `ReviewerAssignmentTests` na prawdziwej ścieżce HTTP: nieprzypisany 403, przypisany 200, przypisanie w jednym konkursie nie odblokowuje wniosku z drugiego, cofnięcie dezaktywuje zamiast kasować. 878 testów backendu.
**Decyzje:** Zakres zawężony do checklisty karty na Trello, bez komisji, oświadczenia o konflikcie interesów i losowania z raportu (`docs/runbook/M5-ocena.md` ma teraz notatkę, dlaczego): trzy nowe, nieopisane jeszcze w `model-danych.md` przepływy, decyzja o osobnych kartach należy do Trello. Jeden wiersz na parę wniosek plus recenzent (unikalny indeks), ponowne przypisanie po cofnięciu reaktywuje ten sam wiersz zamiast wstawiać drugi.
**Uwaga:** Recenzent nie ma dziś żadnego ekranu ani odczytu listy przypisanych wniosków (T-40, zablokowane przez B-02): ta karta dotyczy wyłącznie mechanizmu po stronie operatora i reguły dostępu.

## 2026-09-25 - poprawki po review listy wniosków operatora (T-35)
**Zrobione:** Lista wniosków czyta definicje formularza jednym zapytaniem po wersjach, zamiast dociągać całą definicję przy każdym wierszu. W PDF-ie zbyt długi tytuł konkursu jest przycięty do szerokości tabeli, a obie grupy nieformalne są rozróżnialne w kolumnie "Rodzaj", bo pełna etykieta wychodziła dwa razy jako to samo "Grupa nieformal...". Kontrakt pola czyta `kind` wyliczenia tak jak czyta go parser, więc "Ratio" nie przechodzi tam, gdzie "ratio" nie przechodzi. 881 testów backendu, 449 frontu.
**Decyzje:** Krótsza etykieta tylko w PDF-ie, bo tylko tam ogranicza ją szerokość kolumny: CSV i ekran zostają przy pełnej nazwie z [`reguly-biznesowe.md`](reguly-biznesowe.md). Strefa czasowa eksportu wyszukiwana raz do pola statycznego, bo obraz nie dorabia bazy stref w trakcie działania.
**Uwaga:** Przycięcie tytułu i skrót rodzaju dotyczą tylko układu PDF (`ApplicationListPdfBuilder`); zmiana szerokości kolumn tam wymaga przeliczenia obu.

## 2026-09-25 - testy izolacji danych wnioskodawcy na kompletnej ścieżce (T-36)
**Zrobione:** `ApplicantDataIsolationTests`, rozszerzenie T-13.3 na prawdziwe dane: dwóch wnioskodawców w dwóch różnych konkursach, każdy z realną wersją roboczą, zapisanymi odpowiedziami i przesłanym załącznikiem. Podmiana identyfikatora wniosku, cudzej definicji formularza, cudzego załącznika (pobranie i lista) i cudzego złożenia, zawsze 403 z `problem+json`, zawsze sparowane z właścicielem, który nadal przechodzi, i ze stanem w bazie sprawdzonym po odmowie. Nowy test: `StoragePath` załącznika (goły GUID na dysku) nie działa jako adres pobrania z pominięciem `/attachments/{id}`. 872 testy backendu.
**Decyzje:** Nie osobny plik na test, jedna wspólna scena budowana raz na test, żeby żadna asercja nie zależała od kolejności testów w tej samej bazie. Dwa różne konkursy, nie jeden, bo to jest dosłownie kryterium karty "konkurs, w którym podmiot nie startował": każda odmowa poniżej dowodzi też, że sam fakt innego konkursu niczego nie odblokowuje. `PermissionSuiteCiGuardTests` rozszerzony o tę suitę (`GuardedSuites`) zamiast osobnego strażnika, żeby T-13.3 i T-36 nie mogły rozjechać się w to, które kryją. R-01 (dostęp za organizacją) świadomie pominięty: karta każe dopisać go dopiero po decyzji, a schemat wciąż wiąże konto z podmiotem jeden do jednego (B-09).
**Uwaga:** T-29/T-32/T-33 miały już własne testy "owner vs stranger" na pojedynczych endpointach; ta karta ich nie usuwa (nie jej zakres), tylko dokłada jedno miejsce, które nie zniknie, gdy któryś z tamtych plików zostanie kiedyś zrefaktorowany.
