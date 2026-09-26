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

## 2026-09-25 - ścieżka wnioskodawcy: robocze, złożenie, potwierdzenie (T-34)
**Zrobione:** Wnioskodawca wypełnia wniosek z autozapisem, dogląda załączniki, widzi podsumowanie z "popraw" i składa go przez jedno okno potwierdzenia; złożony wniosek ma osobny, tylko do odczytu ekran z PDF-em potwierdzenia. Trzy nowe odczyty backendu, których brakowało: `GET /applications`, `GET /applications/{id}/form-definition`, `GET /applications/{id}/attachments`. 865 testów backendu, 449 frontu.
**Decyzje:** Kompletność załączników sprawdzana zgrubnie (R-33, `Attachment` nie niesie identyfikatora wymogu), nigdy udając precyzji, której backend też nie ma. Złożenie dogania niewysłany albo w locie będący autozapis przed wywołaniem `submitApplication` (`flushPendingSave`), a każdy autozapis niesie numer kolejny, żeby wolniejsza odpowiedź nie cofnęła nowszej. Zmiana załączników wychodzi na zewnątrz na bieżąco (`onAttachmentsChange`), nie tylko przy złożeniu, żeby ekran potwierdzenia nigdy nie dostał nieaktualnej listy. Przesyłanie kilku plików kontynuuje mimo odrzucenia jednego. Uzasadnienia w [`architektura.md`](architektura.md).
**Uwaga:** R-33 zostaje otwarte. `AttachmentEndpoints.cs` dostał wspólny `AuthorizeAgainstApplicationAsync` zamiast trzeciej kopii wzorca autoryzacji, `api-client.ts` dostał `fillPath`/`apiErrorMessage` do wspólnego użytku z `operator-applications.ts`. `draft-workspace.tsx` przekroczył limit rozmiaru, `SubmitBar` wydzielony do `submit-bar.tsx`. Log przekroczył limit, trzy najstarsze wpisy (T-21, T-20a, T-20) przeniesione do archiwum.

## 2026-09-25 - lista wniosków operatora, podgląd oferty i eksport (T-35)
**Zrobione:** `GET /competitions/{id}/applications` (złożone i aktywne wnioski, bez szkiców, suma wnioskowanych kwot i reszta puli), złożona oferta z wersją formularza i aktywnymi załącznikami, eksport do CSV i PDF, wszystko tylko dla operatora. Ekrany: wybór konkursu, tabela z sortowaniem i filtrami statusu i rodzaju wnioskodawcy, podgląd oferty tylko do odczytu. Kontrakt formularza dostał opcjonalne `role` (tytuł, koszt, dotacja), a kreator wybór roli przy polu. `EntityType` jedzie na drucie tekstem.
**Decyzje:** Kolumny z ról pól, nie z umówionych kluczy ani nowych kolumn. Sortowanie i filtr w przeglądarce (około 120 wierszy), eksport zawsze pełny i po numerach. CSV zamiast XLSX, PDF przez rozszerzony `SimplePdfDocument`, bez nowych zależności. Obie decyzje z użytkownikiem, uzasadnienia w [`architektura.md`](architektura.md).
**Uwaga:** Formularz bez ról daje puste komórki: istniejące definicje trzeba oznaczyć w kreatorze. Brakuje kolumn wyniku oceny formalnej (M5) i daty wpływu koperty (brak miejsca w schemacie). PDF jest bez polskich znaków (fonty Base14). Log przekroczył limit, najstarszy wpis (T-15.3) przeniesiony do archiwum.

## 2026-09-24 - migracja załączników z T-32 na bazie z danymi, seed znów działa
**Zrobione:** `AddAttachmentEntityIdAndFormat` dodawała `entity_id` jako `NOT NULL` z zerowym UUID-em, więc na bazie z choćby jednym załącznikiem łamała własny klucz obcy i backend nie wstawał. Teraz obie kolumny wchodzą jako `NULL`, `entity_id` bierze się z wniosku, `format` z typu MIME albo rozszerzenia, a dopiero potem `NOT NULL`. `scripts/seed.py` znów przechodzi na świeżej bazie: numer konkursu, `entity_id` i `format` załącznika, wpis historii statusów złożonego wniosku, tabele kreatora i historii w sprawdzeniu pustości.
**Decyzje:** Migracja poprawiona w miejscu, nie nową migracją, bo nie była jeszcze na `main`, a na bazach, gdzie padła, cofnęła się w całości. Wiersz, którego formatu nie da się ustalić, zatrzymuje migrację (`RAISE EXCEPTION`) zamiast zgadywać format, pod który pobranie poda typ MIME.
**Uwaga:** Baza, na której stara wersja przeszła (bez załączników), zostaje z domyślnymi wartościami kolumn, których model nie zna. Nieszkodliwe. Log przekroczył limit, najstarszy wpis (T-15.2) przeniesiony do archiwum.

## 2026-09-24 - złożenie oferty i historia zmian statusu (T-33)
**Zrobione:** `POST /applications/{id}/submit` waliduje na poziomie złożenia (T-30), pyta `CompetitionIntake` (T-21), zamraża odpowiedzi przez istniejący strażnik z T-29, nadaje numer wniosku, dopisuje wpis do nowej tabeli `application_status_history` i wysyła e-mail potwierdzający; `GET /applications/{id}/confirmation` oddaje PDF potwierdzenia bez żadnej biblioteki. 810 testów backendu (w tym test dwóch równoczesnych złożeń tego samego wniosku), 380 frontu bez zmian.
**Decyzje:** Numer wniosku nadaje blokada doradcza `pg_advisory_xact_lock` na konkurs, nie ponowienie po `23505`: numer jest niewidoczny dla wnioskodawcy, więc oba równoczesne złożenia muszą się udać, nie jedno dostać 409. `ApplicationNumberAssigner` odczytuje status i `IsActive` na nowo wewnątrz blokady, bo dwa złożenia TEGO SAMEGO wniosku współdzielą tę samą blokadę. PDF pisany ręcznie bez biblioteki, tekst transliterowany na ASCII (fonty Base14 nie mają polskich znaków). Uzasadnienia w [`architektura.md`](architektura.md).
**Uwaga:** Kompletność wymaganych załączników NIE jest sprawdzana przy złożeniu: `attachments` nie ma powiązania z `competition_attachments`, a `entities` nie ma pola rejestru dla `RequiredOutsideKrs` (nowy punkt w `model-danych.md`). Wyścig złożenia z dezaktywacją własnego szkicu zawężony, nie domknięty do zera: `DeactivateAsync` (T-29) nie bierze blokady. Log przekroczył limit, najstarszy wpis (T-15.4) przeniesiony do archiwum.
