# Architektura

Jak system trzyma się do kupy i dlaczego tak, a nie inaczej. Decyzje, nie instrukcje. Instrukcje są w [`konwencje.md`](konwencje.md).

## Obraz całości

```
przeglądarka
    |
    |  HTTP, JSON, ciasteczko sesyjne HttpOnly
    v
frontend  (Next.js, TypeScript, Tailwind CSS)   :3000
    |
    |  HTTP, JSON, OpenAPI jako kontrakt
    v
backend   (.NET, minimal API)                   :8080
    |
    |  Npgsql
    v
baza      (PostgreSQL 16)                       :5432
```

Trzy kontenery w Docker Compose. Front i backend to dwa osobne procesy pod dwoma różnymi originami, więc CORS i sposób trzymania sesji są realnymi decyzjami, a nie szczegółem konfiguracji.

## Decyzje

### Backend na .NET, front na Next.js

Wymóg klienta, nie nasza optymalizacja. Konsekwencja: styk między nimi jest miejscem, w którym najłatwiej stracić czas. Front czeka na backend, backend zgaduje, czego potrzebuje front, a ręcznie pisane typy po obu stronach rozjeżdżają się po tygodniu. Dlatego kontrakt API ustalamy raz i generujemy z niego typowanego klienta TypeScript.

### Sesja w ciasteczku HttpOnly, nie token w nagłówku

Aplikacja jest wyłącznie przeglądarkowa, nie ma klienta mobilnego. Ciasteczko HttpOnly z SameSite jest odporne na wyciek tokenu przez XSS w sposób, w jaki token w `localStorage` nie jest. Kosztem jest konieczność `AllowCredentials` w CORS i jawnej listy originów, co jest w `Cors:Origins`.

Wylogowanie musi kończyć sesję po stronie serwera. Usunięcie ciasteczka w przeglądarce niczego nie unieważnia, a wnioskodawcy będą wchodzić z komputerów w bibliotekach i ze sprzętu współdzielonego.

### Konta na ASP.NET Core Identity, ale na naszym schemacie

Identity daje hashowanie haseł razem z przehashowaniem po zmianie parametrów, generatory tokenów do potwierdzenia adresu (T-12.2) i resetu hasła (T-12.4), blokadę konta po nieudanych próbach oraz `SecurityStamp`. Ten ostatni waży tu najwięcej: wymaganie z sekcji wyżej, żeby wylogowanie kończyło sesję po stronie serwera, bez niego oznacza własną tabelę sesji.

Ale **schemat zostaje nasz**, i to są cztery rozstrzygnięcia, z których każde odwraca się tylko migracją.

1. **Tabela nazywa się `users`, nie `AspNetUsers`.** Identity nazywa swoje tabele jawnie, więc konwencja snake_case ich nie tyka i schemat czytałby się w dwóch stylach naraz. Ważniejsze jest jednak to, że `AspNetUsers` obok istniejącego `users` to dwie migracje tworzące magazyn kont, niedziałający `scripts/seed.py` razem z całą jego sekcją weryfikacyjną i przewrócony test sprawdzający, że każda tabela kont jest w liczbie mnogiej i snake_case. Zachowanie nazwy jest dokładnie tym, co czyni z tej zmiany `ALTER TABLE`, a nie drugą tabelę kont obok pierwszej.
2. **`IdentityUserContext`, nie `IdentityDbContext`.** Pierwszy nie tworzy `AspNetRoles` ani `AspNetUserRoles`. Rola jest kolumną na koncie, z wartością domyślną i check constraintem (patrz sekcja o domyślnej roli), a dwa mechanizmy odpowiadające na pytanie "czy to operator" to o jeden za dużo. Do claimów kolumna trafia przy logowaniu, czyli w T-12.3.
3. **`is_verified` wypada, wchodzi `email_confirmed` z Identity.** Jedno pole na jeden fakt. Weryfikacja adresu zapisuje kolumnę Identity przez `UserManager`, więc nasza flaga byłaby tą, której nikt nie aktualizuje. Migracja **przepisuje wartość**, a starą kolumnę kasuje dopiero po tym: dwie kolumny na jeden fakt znaczy, że skasowanie pierwszej przed przepisaniem cofa każde potwierdzone konto do niepotwierdzonego, a T-12.3 wpuszcza do systemu po potwierdzonym adresie. `Down()` przepisuje w drugą stronę, bo wycofanie migracji też nie jest powodem do zapomnienia, kto potwierdził adres.
4. **Unikalność adresu przenosi się na `normalized_email`.** To domyka założenie, które schemat wcześniej zostawiał otwarte: `Adam@x.pl` i `adam@x.pl` to jedno konto. Unikalność stała wcześniej na adresie jak napisany, więc oba były przyjmowane, a reset hasła miał dwa wiersze do wyboru. Baza, która legalnie trzymała oba, nie ma się w co przekształcić, więc migracja **zaczyna od sprawdzenia kolizji** i zatrzymuje się, wypisując kolidujące adresy. Bez tego zatrzymuje się i tak, ale na unikalnym indeksie, z gołym 23505 i nazwą indeksu, którego operator nigdy nie widział. Które z dwóch kont zostaje, to decyzja produktowa, a nie coś, co migracja może zgadnąć.

**Czego nie bierzemy.** `phone_number`, `phone_number_confirmed` i `two_factor_enabled` są wyłączone z modelu przez `Ignore`, bo [`zakres.md`](zakres.md) odrzuca uwierzytelnianie dwuskładnikowe, a numeru telefonu nie zbieramy. Kolumna z danymi osobowymi, której nikt nie czyta, to kolumna, której nikt nie chroni. Ich nieobecność ma test, więc odwrócenie tej decyzji jest migracją, a nie przypadkiem.

**Co to kosztuje.** Trzy puste tabele, `user_claims`, `user_logins` i `user_tokens`, bo alternatywą jest własny `IUserStore`, czyli pisanie tego, czego uniknięcie jest całym sensem wyboru Identity. Ich klucze obce do konta Identity deklaruje z `ON DELETE CASCADE`, a [`model-danych.md`](model-danych.md) nie dopuszcza żadnego, więc są przestawione na `NO ACTION` w modelu i w migracji. Reguła ma teraz test przechodzący po całym modelu, bo dotychczasowe pilnowały po jednej relacji z nazwy i dlatego nie zobaczyły trzech kluczy, których nikt nie napisał ręcznie. `user_claims.id` jest przy tym jedynym kluczem głównym w schemacie, który nie jest UUID-em: jego typ należy do klasy z paczki, wyjątek jest zapisany w `model-danych.md` przy regule 3. Dwie dodatkowe kolumny na adres, `user_name` i `normalized_user_name`, bo Identity wymaga nazwy użytkownika, a my identyfikujemy konto adresem, więc nazwa go tylko powtarza i nie ma własnego indeksu unikalnego. Oraz to, że schemat kont przestał być w stu procentach naszą decyzją: podniesienie wersji paczki może dołożyć kolumnę.

**Skoro nazwa użytkownika powtarza adres, filtr znaków nazwy użytkownika jest wyłączony** (`options.User.AllowedUserNameCharacters` puste). Identity trzyma ten filtr dla nazw użytkownika i domyślnie przepuszcza tylko `a-zA-Z0-9-._@+`, więc w naszym układzie decydowałby o tym, **jakie adresy wolno zarejestrować**: `o'brien@example.org` jest adresem poprawnym, a Identity odrzuciłoby go angielskim komunikatem o literach i cyfrach, czyli dokładnie tym, czemu ma zapobiegać `CustomPasswordErrorConfiguration`, na podstawie reguły nigdzie nie zapisanej. Jak ma wyglądać adres, waliduje brzeg API w T-12.1: w jednym miejscu, po polsku i przeciw adresowi, a nie przeciw nazwie użytkownika, której nie mamy.

**Dwie implementacje liczą tę samą normalizację** i muszą się zgadzać: normalizator Identity w .NET, po którego sięga też `Data/EmailNormalizer.cs`, oraz `upper(normalize(<adres>, NFC))` w SQL, w migracji i w `scripts/seed.py`. Rozjazd oznacza konto zasiane, którego `UserManager` nie znajduje, przy czym nic nigdzie nie krzyczy. Trzyma to razem `NormalizedAddressTests`.

**`normalize()` w tym wyrażeniu nie jest ozdobą** i review słusznie wytknęło jego brak. Normalizator Identity to `string.Normalize()`, a potem `ToUpperInvariant()`, a domyślną formą `string.Normalize()` jest NFC. Samo `upper()` zgadza się więc z .NET dla adresów, w których akcenty są złożone, i rozjeżdża się na tych, w których nie są: `é` zapisane jako `e` plus akcent łączący dostaje w SQL jedną wartość, a w `UserManager` inną. Skutek jest podwójny i cichy, bo konto już istniejące staje się nieodnajdywalne, a unikalny indeks przyjmuje ten sam adres po raz drugi w drugim zapisie. `normalize()` jest wbudowane w PostgreSQL od wersji 13, liczy z własnych tablic Unicode, więc w odróżnieniu od `upper()` nie zależy od locale bazy. Wersja z NFD zapisana przed migracją ma test w `IdentityMigrationTests`, a blok `DO` pilnujący kolizji grupuje po **tym samym** wyrażeniu, którym potem wypełnia kolumnę: inaczej przepuściłby parę adresów różniących się tylko zapisem akcentu i unikalny indeks odrzuciłby ją kilka zdań później, czyli dokładnie tym gołym 23505, przed którym blok ma chronić.

**Oba stampy Identity są w bazie wymagane i mają wartość domyślną**, `gen_random_uuid()::text`, i to też jest poprawka z review. `IdentityUser` inicjalizuje w konstruktorze `ConcurrencyStamp`, a `SecurityStamp` nie, więc kolumna nullable bez wartości domyślnej przyjmowała konta zapisane obok `UserManager` (`scripts/seed.py`, testy schematu) bez stampa, a konto bez `SecurityStamp` jest kontem, którego sesji nie kończy nic, czyli dokładnie tym, przed czym Identity miało nas tu uratować. Wymaganie samo w sobie zamieniłoby te inserty na błędy, a wartość domyślna sama w sobie pozwoliłaby zapisać jawny `NULL`, więc obie połowy są potrzebne i obie mają test.

Kod, który szuka konta, woła **normalizator, a nie własne wielkie litery**. Samo `ToUpperInvariant()` wygląda na to samo i nie jest: normalizator Identity zaczyna od `string.Normalize()`, więc adres z akcentem zapisanym rozkładowo trafia do bazy pod jednym napisem, a byłby szukany pod innym. Znany limit tej pary: `upper()` w PostgreSQL odwzorowuje niemieckie ostre s na `U+1E9E`, a niezmiennicze wielkie litery w .NET zostawiają je w spokoju. Dotyczy to wyłącznie adresów zapisywanych SQL-em (`scripts/seed.py`, migracja), a nie rejestracji przez API, i jest przypięte testem, żeby ktoś nie odkrył tego jako konta, do którego nie da się wejść. Rozszerzenie tego, na przykład kolumną `citext`, jest decyzją schematową i należy do tego, kto będzie potrzebował adresu z takim znakiem.

### Błędy w formacie ProblemDetails (RFC 7807)

.NET ma to wbudowane, a formularze będą zwracać dużo błędów pól naraz. Front musi umieć przypiąć każdy błąd do konkretnego pola, więc format błędu jest częścią kontraktu, nie szczegółem implementacji.

### Struktura formularza jako dane, nie jako kod

Twarda reguła z analizy wymagań: OCWIP musi móc samodzielnie tworzyć i modyfikować formularze wniosków bez programisty. Z tego wynika wszystko inne:

- Definicja formularza to dokument JSONB w PostgreSQL, wersjonowany.
- Odpowiedzi wniosku to również JSONB, bo ich kształt zależy od definicji.
- **Wniosek wskazuje na WERSJĘ definicji formularza, nie na konkurs.** Formularz może zostać zmieniony przez operatora. Gdyby wniosek wskazywał tylko na konkurs, po edycji formularza stare wnioski przestałyby dać się poprawnie wyświetlić.

JSONB, a nie JSON ani tekst, bo docelowo będziemy po tej strukturze wyszukiwać i indeksować.

### Wniosek nie może rozjechać się z konkursem swojej wersji formularza

To dopięcie decyzji powyżej. Wniosek nosi obok siebie `competition_id` i `form_definition_id`, a wersja definicji formularza sama należy już do konkursu. Ta sama informacja stoi więc w dwóch miejscach i może się rozjechać: wniosek złożony w konkursie A przeciw formularzowi z konkursu B spełnia dwa zwykłe klucze obce i jest bezsensem, którego nikt później nie umie rozstrzygnąć. Dokładnie ta możliwość odbiera sens wskazywaniu na wersję.

Rozważane były trzy wyjścia. Check constraint nie potrafi tego wyrazić, bo musiałby zrobić podzapytanie. Trigger potrafi, ale wchodzi w interakcję z EF i dokłada mechanizm, którego nie mamy nigdzie indziej. Można też nie trzymać `competition_id` na wniosku i sięgać po konkurs przez definicję formularza, ale wtedy każde odczytanie terminu naboru, czyli najgorętsza ścieżka w systemie, dostaje dodatkowe złączenie.

Wybrane rozwiązanie jest deklaratywne: `form_definitions` dostaje klucz alternatywny `(competition_id, id)`, a `applications` **złożony** klucz obcy `(competition_id, form_definition_id)` na ten klucz. Rozjazdu nie da się zapisać, bez triggera i bez kodu, który trzeba pamiętać. Klucz alternatywny jest z definicji unikalny, bo `id` samo w sobie jest, ale PostgreSQL nie pozwala kluczowi obcemu wskazać pary kolumn bez zadeklarowanego constraintu unikalności nad dokładnie tą parą, więc jest zadeklarowany jawnie.

Klucz obcy `competition_id` do `competitions` staje się przez to zbędny dla integralności. Zostaje dla nawigacji, świadomie: termin zamknięcia naboru siedzi na konkursie i jest czytany przy każdym zapisie wniosku.

Nazwa tego constraintu jest ustawiona ręcznie na `fk_applications_form_definitions`. Wygenerowana miałaby 65 znaków, a PostgreSQL ucina identyfikatory na 63 i nie mówi o tym ani słowa, więc test twierdzący o nazwie constraintu przestałby cokolwiek znaczyć.

### Rola operatora nadawana komendą, nigdy przez HTTP

Rola jest kolumną na koncie, nie czymś, co widok wywnioskuje z danych. Trzy role, trzy różne systemy: [`reguly-biznesowe.md`](reguly-biznesowe.md).

Operatora nie da się nadać z aplikacji i to jest decyzja, nie zaległość. Operator widzi wnioski wszystkich organizacji razem z ich danymi osobowymi, więc każdy ekran nadający tę rolę jest jednocześnie drogą do jej zdobycia przez błąd w regułach autoryzacji. Ekranu, którego nie ma, nie da się obejść. Wspierane drogi to `Admin/GrantRoleCommand.cs` i instrukcja wpisana wprost do bazy, obie wymagające dostępu do powłoki albo do PostgreSQL, czyli uprawnień, które i tak dają wszystko.

Komenda siedzi w procesie API i odgałęzia się **przed `WebApplication.CreateBuilder`**. Powód jest prosty: pojedynczy `UPDATE` nie ma po co budować hosta webowego, otwierać gniazda nasłuchu ani odpalać migracji przy starcie.

Odgałęzienie łapie **każdy** czasownik, nie tylko poprawnie napisany, i to jest istotniejsze od samego umiejscowienia. Dopasowanie wyłącznie do `grant-role` przepuszczałoby literówkę (`grant_role`, `grantrole`) do buildera, a w kontenerze backendu oznacza to drugi proces API obok już działającego: bierze wyłączną blokadę na historii migracji, aplikuje migracje i dopiero potem bije się o port 8080. Kto pomylił się w nazwie komendy, ma dostać błąd, a nie wdrożenie. Argument zaczynający się od `-` albo `/` jest ustawieniem hosta i nigdy komendą, cała reszta trafia do parsera, który odrzuca nieznane czasowniki.

Reguła "nie ujawniamy, czy konto istnieje" tutaj **nie obowiązuje**, i jest to zapisane także w kodzie, żeby nikt tego później nie "naprawił". Ta reguła broni rejestracji, logowania i resetu hasła przed obcym, który testuje adresy. Wywołujący tę komendę ma już powłokę w kontenerze backendu i może przeczytać tabelę `users` wprost, więc odpowiedź "nie ma takiego adresu" nic nie oddaje, a oszczędza administratorowi przekonania, że narzędzie jest zepsute.

Kod wyjścia jest częścią kontraktu, bo skrypt owijający tę komendę nie ma innego sposobu odróżnić nadania od adresu, który niczego nie trafił. Dlatego niedostępna baza albo baza bez zaaplikowanych migracji, osiągalna, bo komenda biegnie przed `ApplyPendingMigrations`, kończy się jednym zdaniem i kodem 1, a nie nieobsłużonym wyjątkiem, który daje 134 i stos wywołań.

Dwie decyzje, które komenda podejmuje poza samym nadaniem. Konto dezaktywowane dostaje odmowę: wiersze nie znikają (retencja minimum 5 lat), a konto poza listą aktywnych z rolą operatora to uprzywilejowane konto, na które nikt nie patrzy. Adres dopasowywany jest dosłownie, bo indeks unikalny na nim rozróżnia wielkość liter, a dopasowanie luźniejsze nadawałoby rolę pisowni, którą ścieżka logowania uzna za inne konto. Dosłownie znaczy też bez obcinania białych znaków: obcinanie psuje tę regułę w obie strony, bo konto z adresem naprawdę zakończonym spacją staje się nieosiągalne, a wywołujący trafia w wiersz inny niż wpisane przez siebie znaki.

### Domyślna rola i lista dopuszczalnych ról w schemacie

`Applicant` jest pierwszą wartością enuma, inicjalizatorem właściwości na encji **i** wartością domyślną kolumny. Trzy miejsca, jeden powód: kod, który zapomni ustawić rolę, ma wyprodukować konto najmniej uprzywilejowane, a nie najbardziej. Kolumnowa wartość domyślna nie jest przy tym powtórzeniem inicjalizatora, bo wspieraną drogą tworzenia operatora jest instrukcja, która nigdy nie dotyka change trackera, więc insert pomijający kolumnę jest realną ścieżką i ma lądować na `Applicant`, a nie na błędzie NOT NULL, który następna osoba obejdzie, wpisując rolę z palca.

Kolumna `role` jest też jedynym enumem tekstowym w tym schemacie ograniczonym check constraintem do swoich wartości (`ck_users_role_is_known`), i ta asymetria jest celowa. To kolumna uprawnień, a jej wspierana ścieżka zapisu to SQL wpisany ręcznie. Bez constraintu `UPDATE users SET role = 'operator'` małą literą przechodzi bez słowa i zostawia konto w roli, której nie dopasuje żadna reguła autoryzacji: konto dostaje odmowę wszędzie, poprawnie, z powodu niewidocznego w wierszu. Kosztem jest migracja przy czwartej roli i to jest sens tego constraintu, a nie jego cena.

### Czas w UTC

Odcięcie naboru działa co do minuty, a zmiana czasu w październiku trafia dokładnie w środek sezonu konkursowego. Baza i API operują na UTC, konwersja na czas lokalny dzieje się na brzegach: w przeglądarce i na wydrukach.

Pilnuje tego kod, nie komentarz. Npgsql nie konwertuje `DateTimeOffset` z niezerowym offsetem na `timestamptz`, tylko rzuca wyjątkiem, więc pierwszy operator wysyłający `2026-09-01T10:00:00+02:00` z polskiej przeglądarki wywaliłby `SaveChanges`. Normalizację robi `UtcDateTimeOffsetConverter`, założony w `AppDbContext.ConfigureConventions` na **każdą** właściwość `DateTimeOffset` w modelu.

Świadomie jedna decyzja dla całego modelu, a nie setter w encji: setter trzeba pamiętać przy każdym nowym polu i przy każdej nowej encji, a konwencja obowiązuje domyślnie. Znaczniki czasu w nowych encjach są typu `DateTimeOffset`, nie `DateTime`, bo `DateTime` zmapowany na `timestamptz` przenosi ten sam problem na `DateTimeKind`.

Okno konkursu jest dodatkowo ucinane do pełnej minuty, bo odcięcie naboru działa co do minuty, a dwa terminy renderujące się identycznie jako `12:00` nie mogą zachowywać się różnie: wnioskodawca, który przegrał wyścig, nie ma jak zobaczyć dlaczego. Ucinanie idzie w dół na obu końcach. Znaczniki audytowe zostają na pełnej precyzji, bo odpowiadają na pytanie "kiedy dokładnie to się stało", a nie "co obiecał operator".

**To ucinanie siedzi w setterze encji, nie w konwerterze, i to jest istotne.** EF nakłada konwerter właściwości także na drugą stronę porównania, więc konwerter ucinający przepisałby `EndDate >= now` o `12:00:45` na `EndDate >= 12:00:00`. Terminy są pełnymi minutami, więc przy ostrym `>` szkody nie widać, ale przy `>=` konkurs zamknięty o `12:00` spełniałby warunek jeszcze 59 sekund po zamknięciu, czyli dokładnie odwrotnie do tego, po co ucinanie istnieje. Normalizacja do UTC zostaje konwerterem, bo w odróżnieniu od ucinania zachowuje chwilę i w predykacie jest nieszkodliwa.

Pełnej minuty pilnuje też schemat, dwoma check constraintami z `AT TIME ZONE 'UTC'`. Setter działa tylko dla kodu przechodzącego przez encję, a dwuargumentowy `date_trunc` liczy w strefie sesji, więc bez jawnego UTC warunek zależałby od tego, kto jest podłączony.

### Znaczniki czasu stempluje kontekst, nie tylko baza

`created_at` i `updated_at` mają w schemacie domyślne `now()`, co zabezpiecza inserty omijające change tracker. Ta domyślna wartość odpala się jednak **tylko przy INSERT**, więc sama nie wystarcza: bez stemplowania kolumna nazwana `updated_at` raportowałaby chwilę utworzenia do końca życia wiersza, a kolumna, która kłamie, jest gorsza niż jej brak.

Dlatego `AppDbContext.SaveChanges` stempluje encje implementujące `IAuditedEntity`. Oba znaczniki biorą się z tego samego zegara, żeby dały się porównywać. Aktualizacja nigdy nie nadpisuje `created_at`, nawet gdy wywołujący ustawi je na śledzonej encji.

Zakres tego jest jednak węższy niż domyślnej wartości `now()` i warto to nazwać wprost: **raw INSERT dostaje oba znaczniki z bazy, ale raw UPDATE nie poruszy `updated_at`.** Symetryczne domknięcie to trigger `BEFORE UPDATE`, nie wartość domyślna, i jest odroczone osobną kartą, bo trigger wchodzi w interakcję z EF: wartość w śledzonej encji po `SaveChanges` rozjeżdża się wtedy z tym, co stoi w wierszu. Do czasu tej karty jedyną wspieraną ścieżką modyfikacji jest EF.

### Brak kaskadowego kasowania

Retencja minimum 5 lat wyklucza twarde usuwanie danych. Operator "usuwa" konkurs tylko w sensie oznaczenia go jako nieaktywny. Żaden `ON DELETE CASCADE` nie wchodzi do schematu bez rozmowy.

Kształt tego w encjach to `IsActive` plus `DeactivatedAt`, i `DeactivatedAt` jest **nullable**. Obowiązkowa data dezaktywacji dawałaby każdemu aktywnemu wierszowi `0001-01-01`, czyli wartość, która wygląda jak dane i przechodzi każdą walidację.

Te dwie kolumny są sparowane check constraintem `is_active = (deactivated_at IS NULL)`, na każdej encji z soft delete. Bez tego dają się rozjechać w obie strony: `is_active = false` bez daty to wiersz, którego nikt nie potrafi zadatować, a `is_active = true` z datą czyta się jednocześnie jako żywy i usunięty. Warunek `deactivated_at IS NULL` nigdy sam nie jest NULL-em, więc ten constraint nie da się spełnić przez przypadek.

**Czego soft delete jeszcze nie ma, świadomie: filtra po stronie odczytu.** Wiersz z `is_active = false` normalnie wraca z `context.Competitions`. `HasQueryFilter` odroczony osobną kartą, bo to decyzja o zachowaniu **każdego** zapytania, a nie o kolumnie: zmienia sens wszystkich odczytów, wymaga `IgnoreQueryFilters()` na widokach operatora i przenosi się na nawigacje. Dziś nie ma jeszcze ani jednego endpointu, więc koszt odroczenia jest zerowy, ale **musi wejść przed pierwszym endpointem czytającym konkursy**, bo dołożone później po cichu zmieni wyniki działającego kodu.

### Jeden sposób konfiguracji EF Core

Provider i konwencja nazw (`snake_case`) są ustawiane w jednym miejscu: `UseOcwipPostgres` w `Data/PostgresDbContextOptions.cs`. Używa tego aplikacja, `dotnet ef` i testy.

Konwencja nazw jest częścią modelu EF, a nie kosmetyką: z modelu powstaje snapshot, z którego generują się migracje, i SQL wysyłany w czasie działania. Ustawiona w kilku miejscach kiedyś się rozjedzie, a wtedy migracja utworzy `created_at`, gdy aplikacja pyta o `"CreatedAt"`. Taki rozjazd nie wywala się ani przy migracji, ani przy starcie: wychodzi przy pierwszym zapytaniu.

Z tego samego powodu `IDesignTimeDbContextFactory` czyta `ConnectionStrings:Postgres` z tych samych źródeł co aplikacja, a gdy go nie ma, rzuca wyjątkiem zamiast podstawiać wartość domyślną. `db` to nazwa usługi Compose w połowie projektów na jednym laptopie, więc zgadnięty adres pozwala `dotnet ef database update` zmienić cudzy schemat i zakończyć się kodem 0.

### Migracje przy starcie

Schemat aplikacji budują wyłącznie migracje EF Core (nie `db/init/`). Nowe migracje dodają się w kontenerze backendu (`dotnet ef migrations add ...`).

`Down()` jest obowiązkowy w jednej z dwóch postaci: odwraca `Up()`, albo rzuca z komentarzem dlaczego cofnięcie zniszczyłoby dane, których nie da się odtworzyć. Pusty `Down()` bez uzasadnienia nie wchodzi. Lokalny reset schematu to i tak `docker compose down -v`, nie łańcuch `Down` na produkcji.

Przy starcie API wywołuje `Database.Migrate()` tylko wtedy, gdy `Database:MigrateOnStartup` jest włączone, czyli w Development. Świeży wolumen po `docker compose up` jest wtedy od razu używalny, bez drugiej komendy. Poza Development domyślną wartością jest fałsz, a host testowy (`OcwipWebApplicationFactory`) wymusza wyłączenie, żeby `dotnet test` nie przebudowywał schematu bazy, na której pracujemy.

Chwilowa niedostępność bazy (backup, failover) dostaje pięć prób z narastającym opóźnieniem, a błąd niebędący chwilowym przerywa od razu na pierwszej próbie. To nie znosi decyzji o rozdzieleniu `/health` i sondy bazy poniżej: migracja w procesie obsługującym ruch sprzęga start API z dostępnością bazy, więc tam, gdzie `/health` ma odpowiadać niezależnie od bazy, flaga zostaje wyłączona.

**Jawne uproszczenie MVP.** Docelowo migracje odpala osobny krok deployu, osobną rolą bazodanową. Proces obsługujący ruch nie powinien mieć praw DDL na stałe w systemie, który będzie trzymał PESEL-e przez pięć lat.

### Health endpoint oddzielony od sondy bazy

`/health` odpowiada, gdy proces żyje. `/health/db` odpowiada, gdy API dosięga PostgreSQL. Rozdzielone celowo: orkiestrator restartujący API dlatego, że baza jest chwilowo niedostępna, zamienia małą awarię w dużą.

### Wszystko przez Dockera

Lokalna instalacja Node, .NET SDK czy Postgresa nie jest wspierana. Zespół jest studencki i rozproszony, a różnice wersji między maszynami kosztują więcej niż nauka trzech komend Compose.

### Dane testowe jako skrypt obok aplikacji, nie jako komenda w API

`scripts/seed.py` wykonuje SQL przez `docker compose exec db psql`. Rozważane były trzy inne miejsca i każde przegrało z konkretnego powodu.

**Nie komenda w `Ocwip.Api`**, choć byłaby wygodniejsza i miałaby EF pod ręką. Seed tworzy operatora, a operator widzi dane osobowe każdej organizacji. Kod, który potrafi taki rachunek założyć, wkompilowany w binarkę API, jest drogą do odpalenia go tam, gdzie nikt tego nie chciał. Poza aplikacją ta droga po prostu nie istnieje.

**Nie `db/init/*.sql`**, bo ten katalog uruchamia się wyłącznie na pustym wolumenie. "Jedna komenda" byłaby wtedy prawdą raz w życiu klonu, a seed omijałby migracje.

**Nie osobny projekt konsolowy**, bo `.csproj`, wpis w solucji i warstwa w obrazie to duży narzut na jeden zestaw wierszy.

Cena tego wyboru jest realna i przyjęta świadomie: surowy SQL powtarza wiedzę o nazwach kolumn, więc rozjedzie się ze schematem. Trzyma go w ryzach to, że skrypt odmawia startu na niepustej bazie i na końcu odczytuje wstawione wiersze z powrotem, sprawdzając ich liczbę, sparowanie statusu wniosku z numerem i datą złożenia oraz to, że oba wnioski należą do różnych podmiotów. Rozjazd kończy się więc błędem i wycofaną transakcją, a nie połową danych w bazie.

## Czego tu jeszcze nie ma

Uwierzytelnianie, autoryzacja, kreator formularzy, moduł oceny, generowanie umów, sprawozdawczość, wysyłka maili, przechowywanie plików. Rola istnieje w modelu i ma jak zostać nadana, ale nic jej jeszcze nie czyta: warstwa autoryzacji to T-13.2. Z modelu danych brakuje encji Ocena, Umowa i Sprawozdanie, i to jest decyzja: nie mamy od zamawiającego wzorów tych dokumentów.

Każde z tych ma kartę na Trello. Model danych i jawne założenia: [`model-danych.md`](model-danych.md).
