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

## 2026-10-09 - poprawki po przejściu GUI, przebieg 4
**Zrobione:** Znaleziska `P4-01` do `P4-21` poza `P4-16` i większość obserwacji `O-xx` poprawione w jednym PR, każda poprawka z testem. Stan pozycja po pozycji w [`przejscie-gui-przebieg-4.md`](przejscie-gui-przebieg-4.md), decyzje w [`architektura.md`](architektura.md).
**Decyzje:** Kwota rekomendowana i przyznana nie wyżej niż wnioskowana; pomyłkowy załącznik wycofuje się miękko; nowa umowa bierze rejestr, reprezentanta i rachunek z zamrożonej karty jako wartości do zmiany. Statut grupy bez patrona (`P4-16`), sprawozdanie przed umową i kryterium patrona to pytania P25 do P27, nie zmiana.
**Uwaga:** Na liście rankingowej jeden wniosek nie przekroczy już puli, więc scenariusz I2 sprawdza odmowę ponad wnioskowaną zamiast czerwonej puli. Wzór umowy 2026 zmienił się (linia członków grupy tylko dla grup), więc import treści startowej opublikuje go jako wersję 2.


## 2026-10-08 - Cloudflare Turnstile na formularzach konta
**Zrobione:** Logowanie, rejestracja, "nie pamiętam hasła" i ponowna wysyłka linku mają widżet Turnstile, a API sprawdza token u Cloudflare przed handlerem (filtr po limicie). Lokalnie i w CI para testowa Cloudflare, na produkcji oba klucze wymagane.
**Decyzje:** Cloudflare nieosiągalny to 503, nie przepuszczenie. Klucz strony czytany w czasie żądania, nie wpiekany w obraz. `/reset-password` bez sprawdzenia, bo dowodem jest token z maila.
**Uwaga:** Skrypt wołający te trasy wprost na lokalnym stosie wysyła nagłówek `X-Turnstile-Token: XXXX.DUMMY.TOKEN.XXXX` (token, który przyjmuje testowy sekret).

## 2026-10-08 - kreator formularza: spis, panel pola i żywy podgląd
**Zrobione:** Kreator ma spis po lewej, panel otwartego pola albo sekcji pośrodku i podgląd wnioskodawcy po prawej, odświeżany przy każdym znaku. Pasek liczy zmiany względem formularza, z którego skopiowano (albo opublikowanej wersji), a pole pokazuje, co w nim zmieniono i jak się wcześniej nazywało.
**Decyzje:** Mechanizm dokumentu bez zmian, przebudowany jest ekran. Przeciąganie tylko w obrębie sekcji, tymi samymi krokami co przyciski.
**Uwaga:** Nazwy w spisie, panelu i podglądzie się powtarzają, więc testy szukają w regionie (`within` spisu albo podglądu), a nie na całym ekranie.

## 2026-10-08 - ekspert powoływany na konkurs (T-125, R-44)
**Zrobione:** Operator w sekcji "Komisja" powołuje istniejące konto po adresie albo zaprasza nową osobę; konto wnioskodawcy z powołaniem ocenia w tym konkursie i dalej widzi swoje wnioski, a przydziału do wniosku własnej organizacji system odmawia.
**Decyzje:** Claim `Reviewer` dla powołanego wnioskodawcy zamiast nowych polityk; dostęp do treści nadal tylko z bazy (`ExpertAppointments`). Zaproszenie to link resetu hasła, a udany reset potwierdza adres.
**Uwaga:** Zamrożony zegar testowy nie odświeża claimów sesji: po powołaniu test loguje osobę ponownie.

## 2026-10-08 - kilka osób na jednej karcie organizacji (T-93a, RD7)
**Zrobione:** `entity_members` zastąpiło `users.entity_id`: zajęty NIP prowadzi do prośby o dostęp, zatwierdza ją osoba, która założyła kartę, a po 7 dniach operator z notatką. Współpracownik widzi i dokańcza szkice organizacji, osoba z kilkoma kartami wskazuje kartę przy starcie wniosku. RD1 do RD14 opisane jako ustalenia (raport zatwierdzony 2026-09-21), R-01 i PK-A zamknięte.
**Decyzje:** Eskalację rozpatruje operator, nie komenda (decyzja człowieka, roli administratora nie ma). Migracja nieaddytywna, bo przed G1; `Down` odmawia przy kilku kartach jednej osoby. NIP aktywnej karty unikalny w bazie, nie tylko w serwisie.
**Uwaga:** Sesja w testach liczy się zegarem testowym, więc przeskok o 7 dni wylogowuje klientów: zaloguj ponownie po przesunięciu. Testowe podmioty dostają NIP losowany na wywołanie (`TestEntity.NewNip`), bo baza testów jest wspólna. Odebranie dostępu nie istnieje (`R-45`, `S-40`).

## 2026-10-08 - Mailpit jako skrzynka stosu deweloperskiego
**Zrobione:** `docker compose up -d` stawia Mailpita bez profilu `test`, a backend domyślnie do niego wysyła, więc cała poczta systemu (weryfikacja, reset hasła, wyniki, przypomnienia z zadań w tle) jest do przeczytania pod <http://localhost:8025> zamiast w logu. Jawne puste `DEV_SMTP_HOST=` wraca do trybu maili w logu.
**Decyzje:** Dev compose czyta `DEV_SMTP_*`, nie `SMTP_*`: istniejący `.env` z `SMTP_PORT=587` i `SMTP_ENABLE_SSL=true` nadpisałby nowe domyślne i backend próbowałby STARTTLS na gołym 1025, tracąc każdy mail bez śladu. `SMTP_*` zostaje nazwą w compose produkcyjnym, stagingowym i testu kopii. Jeden dywiz w `${DEV_SMTP_HOST-mailpit}`, bo `:-` zjada jawnie pustą wartość i trybu logu nie dałoby się włączyć.
**Uwaga:** `dotnet test` w kontenerze dziedziczy jego zmienne `Smtp__`, więc `OcwipWebApplicationFactory` zeruje całą sekcję. Samo wyzerowanie hosta nie wystarczyło: test odmowy startu przekaźnika bez nadawcy przechodził, bo kontener podawał `Smtp__From`. Skrzynka trzyma wiadomości w pamięci, restart kontenera ją czyści, a Mailpit stoi teraz otwarty przy każdym `up -d` (odnotowane w S-24).

## 2026-10-07 - limity i uprawnienia kontenerów produkcyjnych
**Zrobione:** Każda usługa compose produkcyjnego ma sufit pamięci i procesora, limit procesów, `no-new-privileges` i `cap_drop: [ALL]`, a API, front, migracja i Caddy także system plików tylko do odczytu z `tmpfs` na tym, co runtime naprawdę pisze (S-13). Krok w CI oblewa usługę bez tej podłogi.
**Decyzje:** `read_only` nie wchodzi na bazę, kopię i odtwarzanie, każde z własnego powodu (zapisywalny `PGDATA`, zrzut `pg_dump` na dysku, nie w pamięci), a wyjątki są wymienione po imieniu w asercji CI, żeby ósma usługa nie dołączyła bez tej decyzji. Caddy i kopia zostają rootem z minimalnym zestawem uprawnień: bez nich nie zepną portów 80 i 443, nie przeczytają pierścienia kluczy, a cron kopii nie odpali ani jednego zadania.
**Uwaga:** `efbundle` rozpakowuje się do katalogu domowego, więc pod `read_only` migracja wychodziła z kodem 159; ma teraz `DOTNET_BUNDLE_EXTRACT_BASE_DIR` na tmpfs z `exec`, bo Docker montuje `tmpfs` z `noexec`, a podman nie, więc lokalnie tego nie widać. Każdy `tmpfs` ma `size`: bez niego jądro daje mu połowę pamięci maszyny, czyli więcej niż sufit kontenera.


## 2026-10-07 - wdrożenie po digeście obrazu, `deploy.sh` nie da się podstawić
**Zrobione:** CI zapisuje digesty obrazów każdego commita jako artefakt, workflow wdrożenia pobiera je dla wdrażanego SHA, a `deploy.sh` przypina do nich każdy `image:` (S-39). Skrypt odmawia wszystkiego, co nie jest pełnym SHA, i nie przepisuje się w trakcie działania (S-26). Klucz wdrożeniowy na stagingu dostaje `restrict,command=` i osobny klucz administracyjny obok (część S-11).
**Decyzje:** Digesty trzymane per commit, bo wycofanie wersji stawia inny commit: jeden wspólny plik uruchomiłby obrazy wersji, która właśnie padła, pod tagiem poprzedniej. Plik digestów jest walidowany dwa razy i nigdy nie trafia do `source` ani `eval`.
**Uwaga:** Niezmienność tagów w GHCR i limit uprawnień `GITHUB_TOKEN` zostają otwarte, bo to ustawienia właściciela organizacji. Krok zapisujący digesty biegnie tylko przy pushu do `dev` i `main`, więc pierwszy prawdziwy przebieg jest po merge'u, nie na pull requeście.


## 2026-10-06 - front po przebudowie: jeden krój, wyśrodkowane panele, kursor wraca
**Zrobione:** Nagłówki tracą krój szeryfowy, każdy panel ma ograniczony i wyśrodkowany wiersz (operator szerszy, bo tabele), bloki węższe od wiersza przestają przyklejać się do lewej krawędzi, przyciski znowu zmieniają kursor, a okna potwierdzenia otwierają się na środku, nie w rogu (przebieg 3, `W-01` do `W-05`).
**Decyzje:** Kursor i wyśrodkowanie okna dialogowego wracają w `@layer base`, nie przy kontrolkach: to nie są trzy niedopatrzenia, tylko dwie rzeczy, które zabrał preflight Tailwinda 4, a naprawiane przy kontrolce zostałyby zapomniane przy następnej. Szerokość wiersza panelu jest stałą czytaną przez ramę, nagłówek i stopkę, więc nie da się ich rozjechać.
**Uwaga:** Testy czytają źródło z wyciętymi komentarzami. Pierwsza wersja przechodziła na samym komentarzu, który cytował sprawdzaną regułę, i mutacja kodu jej nie obaliła.


## 2026-10-06 - załączniki szyfrowane na wolumenie
**Zrobione:** Treść pliku idzie przez `FileCipher` przy zapisie i odczycie (S-38), czyli zamknięte zostało ostatnie miejsce, w którym dane osobowe leżały jawnie obok szyfrowanych kolumn. `reencrypt-data` przepisuje też pliki.
**Decyzje:** Kawałki po 64 KiB, nie jednorazowe szyfrowanie całości: załącznik ma do 25 MB i inaczej siedziałby w pamięci dwukrotnie przy każdym przesłaniu. Plik bez nagłówka czyta się jak dotąd, więc zmiana wchodzi bez przestoju i bez migracji wolumenu.
**Uwaga:** Format wiąże kawałki numerem, nazwą pliku i znacznikiem zamykającym, więc obcięcie pliku jest odrzucane zamiast czytane jako krótszy dokument. Dziesięć testów, w tym jeden przez całą drogę HTTP, sprawdzony mutacją.


## 2026-10-06 - baza nie daje kasować śladu, suma kontrolna z kluczem
**Zrobione:** Rola aplikacyjna traci `DELETE` wszędzie i `UPDATE` na sześciu tabelach tylko dopisywanych, razem z domyślnymi uprawnieniami dla przyszłych tabel (S-09). Suma kontrolna wniosku to podpis kluczem, nie skrót (S-20). Klucz deweloperski znika z obrazu produkcyjnego, a produkcja odmawia startu, jeśli go zobaczy (S-21).
**Decyzje:** Dowód uprawnień w CI, nie w teście jednostkowym: w stosie deweloperskim rola `ocwip_app` nie istnieje, więc migracja nic tam nie robi i test nie miałby czego sprawdzać.
**Uwaga:** Rotacja klucza pól zmienia od teraz pokazywaną sumę kontrolną wniosku. Nic przez to nie przestaje działać, ale wnioskodawca z wydrukiem zobaczy inną wartość; opisane w `wdrozenie.md`.



## 2026-10-06 - druga bariera przed CSRF, sesja z sufitem, mocniejsze hasła
**Zrobione:** `CrossSiteRequestFilter` odrzuca żądanie zmieniające stan, które deklaruje obce pochodzenie, a `SameSite=None` w produkcji wymaga jawnego potwierdzenia (S-15). Sesja ma bezwzględny sufit liczony od zalogowania, hasło 12 znaków minimum i 128 maksimum (S-17, bez MFA).
**Decyzje:** Sufit liczony z własnego znacznika w ticketcie, nie z `IssuedUtc`: odnowienie przy oknie przesuwnym przestawia `IssuedUtc`, więc liczony od niego sufit nie istnieje. Wywołujący bez `Origin` i `Sec-Fetch-Site` przechodzi, bo filtr broni przed przeglądarką, a nie przed skryptem.
**Uwaga:** Wadę liczenia od `IssuedUtc` pokazał dopiero test symulujący pracę co dwie godziny przez dziesięć; sam kod wyglądał poprawnie. MFA zostaje pytaniem do zamawiającego, bo zmienia przebieg logowania.



## 2026-10-06 - wejście do konta utwardzone, limit liczy sieć IPv6
**Zrobione:** Trasy anonimowe pytają o kształt wejścia jedną funkcją `AccountInput`, więc zniekształcony link daje 400, nie 500 (S-31). Limit zapytań partycjonuje IPv6 po /64 (S-03). Reset hasła ma narastający cooldown na konto, jak ponowna wysyłka weryfikacji (S-04).
**Decyzje:** Cooldown kluczowany po koncie, nie po wpisanym adresie: nieznany adres nie zostawia niczego w pamięci podręcznej, a odpowiedź i tak jest jednakowa. Adres IPv4 w opakowaniu IPv6 liczy się jako IPv4, inaczej jedno połączenie dwustosowe dostawałoby własną sieć.
**Uwaga:** `AccountInput` powstał, bo tę samą gardę dopisywano trzy razy osobno i czwarty raz zapomniano; nowa trasa anonimowa ma teraz gdzie zapytać.



## 2026-10-06 - sprawozdanie ma wersje, ocena kosztów nie znika
**Zrobione:** Tabela `report_versions` zapisywana przy zwrocie do poprawy w tej samej transakcji co zmiana statusu, z odpowiedziami, wartościami z wniosku i oceną kosztów tej wersji (S-35). Uzasadnienie operatora przestaje ginąć, bo kopia je trzyma; samo przycinanie przy ponownym złożeniu zostaje.
**Decyzje:** Nowa tabela ma osobne purposes szyfrowania dla `answers` i `prefill`, inaczej niż `reports`, gdzie dzielą jeden (uwaga z części C8 przeglądu). Przycinanie oceny kosztów zostaje trwałe, bo filtr przy odczycie wskrzeszałby wpis sprzed dwóch poprawek, gdy kwota wróci do starej wartości.
**Uwaga:** `EncryptionAtRestTests` i `reencrypt-data` pokrywają od razu nową tabelę, żeby nie powtórzyć luki, przez którą S-08 przetrwał zielone CI: kolumna z konwerterem bez testu w spoczynku i poza rotacją klucza.


## 2026-10-05 - nowy wygląd frontu według makiet
**Zrobione:** Wspólny system wyglądu (`components/ui/`: przyciski, karty, tabele, `StatusBadge`) i przebudowane ekrany z makiet: strona główna, lista i karta konkursu, "Moje wnioski" z filtrami, kreator w dwóch kolumnach, lista wniosków operatora z kaflami, ocena z nawigacją etapów. Nagłówki trzech paneli z jednego `panel-header-parts.tsx`.
**Decyzje:** Z makiet tylko to, co ma dane; uzasadnienie w [`architektura.md`](architektura.md), sekcja "Wygląd". Lista braków w kreatorze pokazuje pięć pozycji, reszta po kliknięciu, bo nowy szkic ma ich kilkadziesiąt.
**Uwaga:** Etykiety `sr-only` w przewijanej tabeli poszerzały całą stronę (ocena: 1970 px w oknie 1440); każdy `overflow-x-auto` ma teraz `relative`, pilnuje tego test źródła. Log przekroczył limit, najstarszy wpis w archiwum.



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
