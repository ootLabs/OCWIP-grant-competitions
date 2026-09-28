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
## 2026-09-28 - formularz wniosku NOWE FIO 2026 jako dane (T-94)
**Zrobione:** `backend/seed/forms/application-2026.json`, jeden warunkowy formularz dla wzorów 1a, 1b i 1c; wniosek każdego rodzaju przechodzi walidację złożenia, drukuje się bez pól technicznych, a lista operatora bierze tytuł, koszt i dotację z ról. Seed publikuje ten formularz.
**Decyzje:** Rodzaj wnioskodawcy to pole wniosku (rola `applicantType`), zamrażane przy złożeniu w `applications.applicant_type`; ocena i reszta czytają `KindOfApplicant`, blokada typu karty z T-93 zniknęła. Formularz idzie za opublikowanymi wzorami 2026, nie za `pola.md` (R-38). Uzasadnienia w [`architektura.md`](architektura.md).
**Uwaga:** Po `pull` przeładuj seed (`down -v`): formularz i odpowiedzi seeda są nowe. Karta formalna nie czyta jeszcze pola `rodzaj_organizacji` (R-36). Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-28 - strona konkursu operatora i publikacja z listą braków (T-97)
**Zrobione:** `panel/operator/competitions/[id]`: stan, braki przed publikacją, przyciski z tabeli przejść, dezaktywacja i przywrócenie (R-26), odnośniki do formularza, wniosków i oceny. Kreator edytuje zapisany konkurs z serwera (`/[id]/edit`), `localStorage` tylko buforuje. Publikacja wymaga formularza i obu kart; zatwierdzenie wyników tylko w `UnderReview` i rozstrzyga konkurs.
**Decyzje:** `UnderReview` do `Resolved` ma własny wyzwalacz `ResultsApproval`, więc trasa statusu go nie wykona. Uzasadnienia w [`architektura.md`](architektura.md), sekcja T-97.
**Uwaga:** Panel nie wgrywa jeszcze kart oceny, więc do T-96 nowy konkurs publikuje się tylko z kartami z API albo seeda. W testach `CompetitionTestHost.ChangeStatusAsync` dopina brakujący formularz i karty przed publikacją, a testy zatwierdzenia wołają `StartReviewAsync`. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-27 - karta podmiotu przy pierwszym wniosku (T-93)
**Zrobione:** Nowe konto samo zakłada kartę podmiotu na kroku przed szkicem (`/me/entity`, pola z `pola.md` 2.2, sumy NIP, REGON, KRS i NRB) i składa wniosek; "Moje wnioski" bez podmiotu to pusta lista. Złożony wniosek trzyma kopię karty (`entity_snapshot`), a "Mój profil" pokazuje i poprawia kartę.
**Decyzje:** DZ-1 (1:1 przed B-09) z planem wyjścia w [`model-danych.md`](model-danych.md); grupa bez patrona ma podmiot z samą nazwą, rodzaj wnioskodawcy do T-94 w karcie i zamarza po złożeniu (R-37). Uzasadnienia w [`architektura.md`](architektura.md).
**Uwaga:** `contact_information` jest teraz `email`. Złożenie odmawia (409) przy niepełnej karcie, więc testy biorą `TestEntity.New()` z kompletną kartą. Po `pull` przeładuj seed (`down -v`), bo stare wiersze nie przejdą reguł. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-27 - odmowa startu przy złej konfiguracji produkcyjnej (T-91)
**Zrobione:** W `Production` API nie startuje przy pustym `DATABASE_URL` albo `SMTP_HOST`, adresie frontu lub originie CORS innym niż publiczne https i `ALLOWED_HOSTS=*`; jeden komunikat wymienia wszystkie błędne klucze. `FRONTEND_BASE_URL` i `ALLOWED_HOSTS` są w compose i `.env.example`.
**Decyzje:** Tylko `Production`, nie wszystko poza Development; staging stawiamy z `Production`, żeby przechodził tę samą kontrolę. Uzasadnienia w [`architektura.md`](architektura.md).
**Uwaga:** Na innym `FRONTEND_PORT` zmień też `FRONTEND_BASE_URL`, inaczej linki w mailach Development wskazują port 3000. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-27 - łatka Next.js i audyt zależności w CI (T-90)
**Zrobione:** `next` 15.5.26, `react` i `react-dom` 19.1.9, `vitest` 4.1.11; `overrides` podnosi `postcss` (8.5.26) i `sharp` (0.35.5), które Next przypina w podatnych wersjach. `npm audit` zero. CI oblewa się przy podatności wysokiej w zależnościach produkcyjnych frontu i w pakietach NuGet.
**Decyzje:** Zostajemy na 15.5.x (16 zmienia API). Audyt frontu bez zależności deweloperskich, żeby dziura w narzędziu testowym nie blokowała każdego PR; te łata Dependabot.
**Uwaga:** Lockfile przegenerowany przez `npx npm@11 install`: npm 10 z obrazu `node:22` wywraca się przy tej zmianie (`Cannot read properties of null (reading 'edgesOut')`). Działający stos lokalny zostaje na starym Next, dopóki nie przebudujesz go z nowym wolumenem: `docker compose up -d --build --renew-anon-volumes frontend` (bez flagi anonimowy wolumen `node_modules` przeżywa przebudowę, [`runbook.md`](../runbook.md)). `overrides` zdejmij, gdy Next sam podniesie te pakiety.

## 2026-09-27 - plan do pierwszej wersji (plan v1)
**Zrobione:** [`runbook/plan-v1.md`](runbook/plan-v1.md): definicja v1, bramki G0 do G5, 36 zadań od T-90 z kryteriami, tory pracy, ryzyka i pakiet pytań do klientki. 35 kart w Backlogu, sekcja v1 w kolejce przed M1.
**Decyzje:** DZ-1 do DZ-6 (sekcja 7 planu): karta podmiotu przed odpowiedzią na B-09, NIP jawny, staging, T-45b odblokowane, wzór sprawozdania 2026 do T-95, deklaracja dostępności w zakresie. T-47 podzielone na T-47a i T-47b.
**Uwaga:** Na świeżej bazie nikt nie złoży wniosku, bo nic nie zakłada podmiotu (L1, T-93); testy i seed wstawiają podmioty z pominięciem API. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-27 - rozliczenie sprawozdania (T-50b)
**Zrobione:** Wzór sprawozdania oznacza budżet (`reportBudget`) i wydatek z dotacji (`grantSpent`); operator przy złożonym sprawozdaniu nie uznaje kosztów kwotą z powodem, serwer liczy kwotę do zwrotu, obie strony ją widzą; przyjęcie daje wniosek `Settled`.
**Decyzje:** Ocena w `reports.cost_review`, nie w odpowiedziach wnioskodawcy; kwota do zwrotu liczona przy odczycie. Uzasadnienia w [`architektura.md`](architektura.md), ZR-14.
**Uwaga:** Termin, sprawozdanie częściowe, załączniki, historia projektu i wzór 2026 przeszły do T-50c (B-04). `IsGranted` obejmuje teraz też `Settled`. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-27 - umowa ze wzoru (T-45)
**Zrobione:** Wzór umowy ze znacznikami, publikowany w ocenie konkursu; umowa dofinansowanego wniosku z polami systemowymi (kwota słownie, daty słownie) i wpisywanymi przez operatora, PDF z polskimi znakami, podpisanie z datą przestawia wniosek w `ContractSigned`. Wnioskodawca pobiera swoją umowę.
**Decyzje:** Nazwa spoza słownika to pole do wpisania; tekst składany przy druku, nie zapisywany. Uzasadnienia w [`architektura.md`](architektura.md), ZR-13.
**Uwaga:** Umowy hurtem i wzór 2026 w seedzie w T-45b. Dofinansowanie sprawdza się przez `ApplicationStatuses.IsGranted` (`Funded` albo `ContractSigned`), nie przez porównanie z `Funded`. Log przekroczył limit, najstarszy wpis w archiwum.

## 2026-09-26 - polskie znaki w PDF (T-45a)
**Zrobione:** Wszystkie PDF-y (potwierdzenie, lista wniosków, lista rankingowa, wniosek) z osadzoną czcionką Noto Sans / Noto Sans Mono jako CID z mapą ToUnicode. Polskie litery i typografia drukują się i kopiują; transliteracja usunięta. Sprawdzone `pdftotext` i renderem strony.
**Decyzje:** Własny generator zamiast biblioteki, czcionka w całości (około 300 KB na plik). Uzasadnienia w [`architektura.md`](architektura.md).
**Uwaga:** Testy czytają tekst PDF przez `PdfTextReader` (numery glifów przez ToUnicode), nie przez dekodowanie ASCII. Log przekroczył limit, najstarszy wpis w archiwum.

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
