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

### Kontrakt API: dokument OpenAPI i generowany z niego klient (T-17)

Backend jest jedynym źródłem prawdy o kształcie API. Dokument OpenAPI powstaje z kodu, a front generuje z niego typy komendą `npm run api:generate`. Typ pisany ręcznie po stronie frontu jest kopią, która rozjedzie się w tydzień, i takiej kopii tu nie ma.

**Kody odpowiedzi deklaruje sygnatura, nie komentarz.** Endpointy zwracają `TypedResults` z jawnym `Results<...>` jako typem zwracanym, więc kompilator odrzuca kod oddający cokolwiek spoza kontraktu. Wyjątkiem jest `ProblemHttpResult`, którego status jest argumentem czasu wykonania: 503 na `/register`, 400 na `/verify-email` i 503 na `/health/db` deklarujemy ręcznie przez `.Produces<ProblemDetails>`, z komentarzem w miejscu deklaracji. Bez tego dokument opisywał każdy endpoint jako "zawsze 200", bo minimalne API nie zgaduje kodów, których nikt mu nie podał.

**Ciała odpowiedzi są nazwanymi rekordami.** Typu anonimowego nie da się wpisać w sygnaturę, więc endpoint z anonimowym ciałem zostaje w dokumencie dziurą, a po stronie frontu typem `unknown`. Stąd `HealthResponse` i `DatabaseHealthResponse` w `Contracts/`.

**Dokument wystawiony tylko w środowisku deweloperskim.** `MapOpenApi()` stoi pod `IsDevelopment()`. Specyfikacja to kompletna mapa endpointów i kształtów danych, a system przetwarza dane osobowe i docelowo PESEL-e. Zewnętrznych integratorów nie mamy, więc na produkcji nikt jej nie potrzebuje. Cena jest taka, że generowanie klienta wymaga działającego stacku deweloperskiego. Gdy zacznie to przeszkadzać w CI, następnym krokiem jest generowanie dokumentu do pliku przy budowaniu (`Microsoft.Extensions.ApiDescription.Server`), a nie odsłanianie go na produkcji.

**Generujemy typy, nie kod wykonywalny.** `openapi-typescript` produkuje wyłącznie typy (`frontend/lib/api-schema.ts`, plik commitowany i nieedytowalny ręcznie). Wysyłka żądań zostaje w pisanym ręcznie `apiFetch`, bo tam siedzą decyzje o `credentials: "include"` i o generycznym komunikacie błędu. Generator pełnego klienta zbudowałby własną warstwę wysyłki, a te decyzje trzeba by odtwarzać w jego konfiguracji. Plik jest w repozytorium, żeby front kompilował się bez działającego backendu i żeby dało się w CI wykryć rozjazd z kodem.

**Konwencja JSON:** nazwy pól w camelCase (domyślny serializator ASP.NET Core, nic nie konfigurujemy), enumy jako nazwy (atrybut na typie, patrz decyzja niżej), daty i znaczniki czasu jako ISO 8601 w UTC. Pierwszym endpointem zwracającym datę jest T-20 i tam ta reguła dostanie test.

### Sesja w ciasteczku HttpOnly, nie token w nagłówku

Aplikacja jest wyłącznie przeglądarkowa, nie ma klienta mobilnego. Ciasteczko HttpOnly z SameSite jest odporne na wyciek tokenu przez XSS w sposób, w jaki token w `localStorage` nie jest. Kosztem jest konieczność `AllowCredentials` w CORS i jawnej listy originów, co jest w `Cors:Origins`.

Wylogowanie musi kończyć sesję po stronie serwera. Usunięcie ciasteczka w przeglądarce niczego nie unieważnia, a wnioskodawcy będą wchodzić z komputerów w bibliotekach i ze sprzętu współdzielonego.

Wdrożone w T-12.3, `Configuration/AuthenticationConfiguration.cs`. Ciasteczko nazywa się `ocwip.session`, żyje 8 godzin bezczynności (`Auth:SessionLifetimeHours`, sliding), jest HttpOnly, `SameSite=Lax` i `Secure` wszędzie poza Development. `Lax`, a nie `Strict`, bo `Strict` nie dołącza ciasteczka do pierwszego żądania po kliknięciu w link z zewnątrz, więc osoba wchodząca z maila o wynikach ląduje wylogowana dokładnie na tej ścieżce, dla której reguła powrotu na stronę konkursu została napisana.

### Wylogowanie obraca SecurityStamp, a stamp jest sprawdzany co żądanie

`SignOutAsync` prosi przeglądarkę o skasowanie ciasteczka i nic poza tym. Kopia ciasteczka, zabrana ze współdzielonego komputera, tej prośby nigdy nie zobaczy i zostaje ważna do własnego wygaśnięcia. Dlatego wylogowanie obraca `SecurityStamp` konta, a `SecurityStampValidatorOptions.ValidationInterval` jest ustawiony na zero.

Dwie konsekwencje, obie przyjęte świadomie. Pierwsza: wylogowanie kończy **wszystkie** sesje konta, nie tylko tę jedną. Osoba przy komputerze w bibliotece nie ma jak sprawdzić, ile sesji zostawiła otwartych, więc bezpieczna odpowiedź to zamknąć wszystkie. Druga: interwał zero oznacza odczyt konta z bazy przy każdym uwierzytelnionym żądaniu i przepisanie ciasteczka. Przy skali tego systemu (jeden operator, około 120 ofert na konkurs) to jest tanie, a domyślne 30 minut znaczyłoby, że "wyloguj" działa w ciągu pół godziny.

Sprawdzenie, czy konto jest wciąż aktywne, siedzi w tym samym miejscu: `Services/ActiveAccountStampValidator.cs` rozszerza walidator stampa, więc każde żądanie z sesją je przechodzi. Nie w endpoincie, bo nie kasujemy twardo, więc wyłączenie konta jest jedynym "usunięciem", jakie mamy, a sprawdzenie wpisane w jeden endpoint chroni jeden endpoint. Rejestracja idzie przez `AddScoped`, nie `TryAddScoped`: Identity ma tam już swój walidator, więc `TryAdd` niczego nie podmienia i cicho wyłącza tę regułę.

### Niepotwierdzony adres sprawdzany PO haśle, nie przed

Karta T-12.3 chce czytelnego komunikatu dla konta bez potwierdzonego adresu, a reguła 3 zabrania ujawniania, kto ma u nas konto. Obie rzeczy trzymają się naraz tylko wtedy, gdy komunikat stoi za sprawdzeniem hasła: widzi go wyłącznie ktoś, kto hasło już zna.

Dlatego `SignIn.RequireConfirmedEmail` zostaje **wyłączone**, a logowanie idzie przez `CheckPasswordSignInAsync` i sprawdza `EmailConfirmed` samo. Włączona opcja każe Identity sprawdzić potwierdzenie w `PreSignInCheck`, czyli przed dotknięciem hasła, i wtedy sama odpowiedź na dowolne hasło mówi, że taki adres ma konto. Konto nieaktywne (soft delete) dostaje z tego samego powodu odpowiedź generyczną, a dla nieistniejącego adresu liczony jest hash atrapy, żeby kont nie dało się wyliczyć stoperem.

### Reset hasła dostaje własny token provider, nie ten od weryfikacji adresu

`GenerateEmailConfirmationTokenAsync` (T-12.2) i `GeneratePasswordResetTokenAsync` (T-12.4) to pod maską ten sam `DataProtectorTokenProvider`, zarejestrowany raz przez `AddDefaultTokenProviders` pod nazwą "Default", i czytający jeden `IOptions<DataProtectionTokenProviderOptions>`. Ustawienie tam `TokenLifespan` ustawia je OBA naraz, a karta chce dla resetu krótkiego czasu życia (proponowane 1h), różnego od 24h weryfikacji adresu: link resetujący jest jedynym linkiem w systemie, który może oddać cudze konto komuś, kto akurat z niego korzysta, więc zasługuje na krótszy czas.

`Configuration/PasswordResetTokenProvider.cs` rozwiązuje to bez pisania własnej logiki tokenu: `PasswordResetTokenProviderOptions` to podklasa `DataProtectionTokenProviderOptions`, a `PasswordResetTokenProvider<TUser>` to podklasa `DataProtectorTokenProvider<TUser>` zbudowana nad tymi opcjami. Osobny typ opcji dostaje w DI własny slot `IOptions<T>`, więc `PasswordReset:TokenLifetimeHours` (domyślnie 1) nie rusza `EmailVerification:TokenLifetimeHours` (24) i odwrotnie. Provider jest rejestrowany w `Program.cs` przez `.AddTokenProvider<PasswordResetTokenProvider<User>>("PasswordReset")`, a `IdentityOptions.Tokens.PasswordResetTokenProvider` wskazuje na tę nazwę, więc `UserManager.ResetPasswordAsync`/`GeneratePasswordResetTokenAsync` idą przez ten provider, nie przez "Default". Konstruktor opcji dodatkowo nadpisuje `Name` na `"PasswordResetTokenProvider"`, żeby token resetu i token weryfikacji były chronione pod różnymi kluczami Data Protection, a nie tylko rozróżniane polem `purpose` w środku danych.

Unieważnienie wszystkich sesji po resecie nie potrzebowało nowego kodu. `UserManager.ResetPasswordAsync` przy sukcesie sam obraca `SecurityStamp` (ta sama droga co `UpdateSecurityStampAsync` w wylogowaniu, T-12.3), a `SecurityStampValidatorOptions.ValidationInterval = Zero` sprawdza stamp przy każdym żądaniu, więc każde inne ciasteczko tego konta przestaje działać na własnym, kolejnym żądaniu. Token jednorazowy wychodzi z tego samego mechanizmu bez dodatkowego zapisu: `DataProtectorTokenProvider` niesie w sobie `SecurityStamp` z momentu wygenerowania, więc token użyty raz i obracający stamp przestaje się zgadzać przy drugim użyciu tego samego linku.

### Ochrona przed brute force ma dwa niezależne wymiary

Karta T-12.5 wymaga limitu liczonego po adresie IP ORAZ po koncie, bo każdy z osobna ma dziurę. Limit tylko po IP nie chroni konta, jeśli atak idzie z wielu adresów naraz. Limit tylko po koncie pozwala złośliwie zablokować cudze konto, wystarczy wpisywać błędne hasło do skutku z adresu, który nie jest atakującego. Oba wymiary są więc osobnymi mechanizmami, nie jedną konfiguracją.

**Wymiar konta: blokada z Identity, nie własna tabela.** Kolumny blokady (`lockout_enabled`, `lockout_end`, `access_failed_count`) istniały od T-12.0 i czekały puste. `SessionService.LoginAsync` woła teraz `CheckPasswordSignInAsync` z `lockoutOnFailure: true`, a `IdentityConfiguration.cs` ustawia `Lockout.MaxFailedAccessAttempts` na 5 i `Lockout.DefaultLockoutTimeSpan` na 15 minut (`Auth:MaxFailedLoginAttempts`/`Auth:LockoutMinutes`, z odmową startu przy wartości niedodatniej, tym samym powodem co `Auth:SessionLifetimeHours`). `CheckPasswordSignInAsync` sprawdza blokadę PRZED hasłem (`PreSignInCheck`), więc poprawne hasło wpisane w trakcie blokady wciąż dostaje `LockedOut`, nie sukces: nie ma sposobu na przedwczesne odblokowanie się poprawną próbą.

**To jest jedyne miejsce, w którym odpowiedź na `/login` legalnie zdradza istnienie konta.** Reguła 3 z `AGENTS.md` (jeden komunikat na złe dane i nieznany adres) obowiązuje do momentu blokady: nieznany adres nigdy nie dotyka `CheckPasswordSignInAsync`, więc nigdy się nie blokuje, a odpowiedź `LockedOut` (429, "konto zostało tymczasowo zablokowane... spróbuj za X minut") jest osiągalna wyłącznie po pięciu złych hasłach na TEN JEDEN adres. Karta wprost prosi o czytelny komunikat z czasem odblokowania, co jest ważniejsze tu niż dalsze skrywanie stanu konta.

**Konto dezaktywowane jest z tego wyjątku wyłączone.** Reguła 5 mówi, że nie kasujemy twardo, więc "tego konta już nie ma" musi nadal wyglądać jak "tego konta nigdy nie było". Blokada odpowiadałaby 429 po pięciu próbach i zamieniłaby soft delete w sposób na wyliczenie byłych użytkowników, więc `SessionService` oddaje dla nich zwykłe `InvalidCredentials` (`password.IsLockedOut && user.IsActive`). Nic przez to nie tracimy: konto nieaktywne i tak się nie zaloguje, więc nie ma tam komunikatu, który komukolwiek by pomógł.

**Czego wymiar IP NIE rozwiązuje: złośliwej blokady cudzego konta.** Pięć złych haseł co piętnaście minut to około 0,33 żądania na minutę, czyli grubo poniżej limitu 10 na 60 sekund, więc ktoś, kto zna adres, może trzymać cudze konto zablokowane w nieskończoność i żaden limit po IP tego nie zauważy. Nie udajemy, że to naprawiliśmy. Odpowiedzią jest ścieżka wyjścia, a nie wykrycie: **udany reset hasła kasuje blokadę** (`PasswordResetService` woła `ResetAccessFailedCountAsync` i `SetLockoutEndDateAsync(user, null)`), więc właściciel konta odzyskuje je sam, bez czekania i bez telefonu do OCWIP. Bez tego dwie karty znosiłyby się nawzajem: zapomniane hasło jest główną drogą do blokady, a reset oddawałby konto, które nadal odpowiada 429 przez kwadrans.

**Wymiar adresu IP: jedna wspólna polityka, nie pięć osobnych liczb.** `Configuration/RateLimitingConfiguration.cs` rejestruje jedną nazwaną politykę (`sensitive-auth`) na `/login`, `/register`, `/forgot-password`, `/reset-password` i `/resend-verification`, licznik stałego okna partycjonowany po adresie IP (`RateLimiting:PermitLimit`/`WindowSeconds`, domyślnie 10 na 60 sekund). Budżet jest WSPÓLNY dla wszystkich pięciu endpointów jednego adresu, nie liczony osobno na każdy: to jedno zdanie do przemyślenia ("tyle żądań tego typu z tego adresu w tym oknie"), a nie pięć niezależnych, które mogłyby się rozjechać. Odrzucenie to 429 z `Retry-After` i polskim komunikatem bez szczegółów technicznych, tym samym formatem błędu co resztę API. Blokada konta też ustawia `Retry-After`, bo `/login` ma teraz dwa różne 429 i bez tego nagłówka klient nie odróżni tego, co mija za chwilę, od tego, co mija za kwadrans.

**Warunek, bez którego ten wymiar nic nie chroni: adres musi być prawdziwy.** Partycja jest liczona z `Connection.RemoteIpAddress`, co jest poprawne tak długo, jak API stoi bezpośrednio, tak jak dziś w `docker-compose.yml`. Za reverse proxy albo load balancerem wszystkie żądania przychodzą z adresu proxy, więc cały ruch produktu ląduje w JEDNEJ partycji i dziesiąte logowanie w skali całego systemu w ciągu minuty dostaje 429: awaria uwierzytelniania spowodowana obroną, nie atakiem. Od T-111 adres klienta podaje middleware nagłówków przekazanych (`ForwardedHeadersConfiguration`), ale tylko od proxy i sieci z `ForwardedHeaders:KnownProxies` i `ForwardedHeaders:KnownNetworks`; compose produkcyjne wpisuje tam stały adres Caddy (`172.30.0.200`, poza pulą `ip_range` przydzielaną kontenerom), a nie całą sieć wewnętrzną, bo w niej są też front, kopia i na stagingu Mailpit. Świadomie **nie** `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`: ten przełącznik czyści listę zaufanych i wierzy każdemu nadawcy, a `X-Forwarded-For` bez zaufanego źródła to limit, który atakujący obchodzi dowolną wartością nagłówka. Test `ReverseProxyTests` pilnuje obu stron, dla adresu i dla sieci (też listy po przecinku).

**Ta sama partycja oznacza też jeden budżet na jedno biuro.** Kilkanaście osób za jednym NAT-em albo na współdzielonym adresie operatora komórkowego wydaje te same dziesięć żądań, a wnioskodawcami bywają organizacje pracujące z jednego biura. Dźwignią jest `RATE_LIMIT_PERMIT_LIMIT`, a **nie** drobniejsza partycja: liczenie po parze (adres, wpisany e-mail) dałoby atakującemu świeży budżet na każdy kolejny próbowany adres, czyli dokładnie ten atak, przed którym ten wymiar ma bronić. Wymiar konta jest od tego, żeby pojedyncze konto było chronione mimo wspólnego adresu.

**Odpowiedź dla konta dezaktywowanego musi kosztować tyle samo, co dla nieistniejącego adresu.** Konto zablokowane nie dociera do sprawdzenia hasła w ogóle, bo `CheckPasswordSignInAsync` sprawdza blokadę pierwsza, więc ścieżka dezaktywowana odpowiadałaby po jednym zapytaniu do indeksu, podczas gdy nieznany adres nadal płaci pełne wyliczenie klucza z `DecoyHash`. Ten sam status, ta sama treść i różnica rzędu stu milisekund, która mówi "ten adres miał u nas konto" każdemu ze stoperem, czyli odtwarza dokładnie ten kanał, który `DecoyHash` istnieje żeby zamknąć. Dlatego ta ścieżka też pali jedno wyliczenie (`BurnPasswordCheck`). Przypięte testem porównującym mediany: z paleniem stosunek czasów to około 0,9, bez niego 0,04.

**Dlaczego reset hasła jest w tej samej polityce co logowanie, mimo że token nie jest praktycznie zgadywalny.** `/reset-password` sam siebie nie da się brute force'ować sensownie, ale karta czyta "reset hasła" jako cały mechanizm, nie tylko mail startujący go, a jedna reguła bez wyjątków dla jednego z pięciu endpointów jest tańsza do utrzymania niż wyjątek, który trzeba by wyjaśniać za rok.

### Enum jedzie po drucie nazwą, i pilnuje tego atrybut na typie

Domyślna serializacja dałaby `"role": 1`, przez co KOLEJNOŚĆ wartości w `Models/Role.cs` stałaby się częścią kontraktu API: dołożenie roli w środku enuma po cichu zamienia operatora w recenzenta dla każdego klienta, który zapamiętał liczby. W bazie ta sama kolumna jest tekstem z dokładnie tego powodu.

`[JsonConverter(typeof(JsonStringEnumConverter<Role>))]` na enumie, a **nie** opcja serializatora w `Program.cs`. Opcja obowiązuje tylko w potoku HTTP, więc każdy, kto czyta ten typ zwykłym `JsonSerializer`, rozjeżdża się z API co do tego, czym jest rola; pierwszym takim wołającym był test asercjujący na ciele odpowiedzi i wywrócił się dokładnie na tym. Atrybut jedzie razem z typem i takiej dziury nie ma. Każdy kolejny enum wychodzący na drut dostaje ten sam atrybut.

### Cel przekierowania po zalogowaniu rozstrzyga serwer

`Services/LoginLandingPath.cs` mapuje rolę na ścieżkę panelu (`/panel/operator`, `/panel/applicant`, `/panel/reviewer`) i zwraca ją w odpowiedzi logowania jako `redirectPath`. Panele powstają w T-15.2 i T-15.3, więc dziś ta ścieżka jest obietnicą, a nie istniejącą trasą.

Serwer, a nie front, bo reguła ma dwie połowy i niebezpieczna jest ta druga. Rola to prosta tabela. Raport (krok 3.1) chce jednak, żeby wnioskodawca wrócił na stronę konkursu, z której przyszedł, a to jest cel proponowany przez przeglądarkę, czyli otwarte przekierowanie, jeśli ktoś odeśle go bez sprawdzenia. Trzymanie obu połówek razem sprawia, że sprawdzenia nie da się pominąć, pamiętając tylko o tej łatwej. Wszystko, co nie jest jedną lokalną ścieżką, jest odrzucane, a nie naprawiane: sanitizer to spis sztuczek, o których ktoś już pomyślał.

Ceną jest jeden plik w backendzie, który zna trasy frontu. Jest to jeden plik i jeden produkt, i ta cena jest tu zapisana.

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

### Rejestracja nie ma jak zdradzić, że konto istnieje

Reguła bezpieczeństwa 3 mówi, że adres zajęty i wolny dostają tę samą odpowiedź. Pierwsza wersja rejestracji tę regułę **znała i złamała**: serwis zwracał `IdentityResult`, więc udanie sukcesu wymagało zwrócenia `IdentityResult.Failed` z wymyślonym kodem, a endpoint odczytał to jako błąd i odpowiedział 400 dla zajętego adresu, a 201 dla wolnego. Komentarz mówił jedno, kod robił drugie.

Dlatego kontrakt serwisu **nie zwraca typu Identity**, tylko `RegistrationOutcome` o dwóch wartościach, w którym "konto utworzone" i "adres już zajęty" to **ta sama wartość**. Nie ma czego porównać, nie ma gałęzi do pomylenia i nie ma jak przetłumaczyć tego na 409. Trzecia wartość, `Rejected`, dotyczy wyłącznie żądania (hasło nie spełnia polityki) i dlatego wolno jej się różnić: o istnieniu konta nie mówi nic.

Do "adres zajęty" prowadzą **dwie drogi i obie muszą dać ten sam wynik**. Walidator Identity sprawdza to `SELECT`-em przed `INSERT`, ale ten sprawdzian przegrywa wyścig z rejestracją w tej samej chwili, i wtedy odpowiada unikalny indeks przez 23505. Pominięcie którejkolwiek zostawia różnicę do zaobserwowania, więc obie są obsłużone, a testy pokrywają obie, drugą przez wyłączenie walidatora zamiast czekania na wyścig.

Odpowiedzią jest **202 z pustym ciałem**, nie 201: przy zajętym adresie nic nie powstało, a `Created` obiecuje jeszcze nagłówek `Location`, który trzeba by wymyślić. Co dalej, człowiek dowiaduje się z maila (T-12.2), i to jest jedyny kanał, który ma prawo wiedzieć, który z dwóch przypadków zachodził.

Wolno się różnić tylko dwóm odpowiedziom i obie mówią o samym żądaniu: 400, gdy odpada walidacja brzegu albo polityka hasła, i **503 na hoście bez bazy**. Ten drugi nie jest awarią, tylko wspieranym trybem pracy, bo sondy zdrowia odpowiadają bez bazy, a `IAccountService` jest wtedy niezarejestrowany. Dlatego endpoint bierze go jako typ **nullowalny**: `GetRequiredService` zwróciłby 500 z nazwą wewnętrznego typu w ciele, czyli dokładnie to, czego `/health/db` starannie unika.

Walidator brzegu powtarza jeden sprawdzian Identity, `EmailAddressAttribute`, i to nie jest reguła w dwóch miejscach. `UserValidator` uruchamia go sam przy `RequireUniqueEmail`, więc adres przepuszczony na brzegu i odrzucony przez niego nie wraca z polskim komunikatem przy polu adresu, tylko z angielskim `InvalidEmail` przy polu **hasła**, bo innego pola endpoint w tym miejscu już nie ma. `"a@b"@example.org` jest takim adresem: `MailAddress` go parsuje i odwzorowuje w obie strony, `EmailAddressAttribute` liczy dwie małpy i odmawia.

**Identyczna odpowiedź to za mało, jeśli różni się czas.** Rejestracja, `/forgot-password` i `/resend-verification` zwracały to samo ciało i ten sam status, ale dla istniejącego adresu (rejestracja: dla nowego) czekały w żądaniu na przekaźnik SMTP, a dla pozostałych wracały od razu. Różnica jednej rozmowy z przekaźnikiem to setki milisekund, mierzalne przez sieć, więc kto zna adresy, odróżniał konta od niekont stoperem. Mail idzie teraz do kolejki (`IAccountMailQueue`, w pamięci procesu), a żądanie wraca tak samo szybko niezależnie od tego, czy cokolwiek wysłano. Kolejkę czyta jeden `BackgroundService` (`QueuedAccountMail`), który próbuje trzy razy z rosnącą przerwą i po trzeciej odmowie rezygnuje z wpisem w logu (sam temat, bez treści, bo w treści jest link logujący). Koszt: błąd przekaźnika nie wraca już do wołającego jako 500, więc człowiek, któremu mail nie doszedł, prosi o niego jeszcze raz (nowy reset albo ponowna wysyłka po cooldownie), a mail, który czekał w chwili zatrzymania API, przepada. To to samo założenie jednej instancji, co w `BackgroundJobScheduler`. Zostaje różnica rzędu milisekund w samej bazie przy rejestracji (zgody i zatwierdzenie transakcji tylko dla nowego konta), bo hasło jest haszowane w obu przypadkach, zanim Identity sprawdzi duplikat; to szum poniżej tego, co da się zmierzyć przez sieć.

### Błędy w formacie ProblemDetails (RFC 7807)

.NET ma to wbudowane, a formularze będą zwracać dużo błędów pól naraz. Front musi umieć przypiąć każdy błąd do konkretnego pola, więc format błędu jest częścią kontraktu, nie szczegółem implementacji.

Jeden format dla całego API, bez wyjątków: odpowiedź błędu ma `content-type: application/problem+json`, a błędy walidacji siedzą w obiekcie `errors` pod kluczami równymi nazwom pól żądania. Prawdziwa odpowiedź `POST /register` na trzy niepoprawne pola:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "email": ["To nie jest poprawny adres e-mail."],
    "firstName": ["Imię jest wymagane."],
    "lastName": ["Nazwisko jest wymagane."]
  },
  "traceId": "00-7d55fbdbacf94cbbf118c17057a4b071-9630a4bd6e571d2d-00"
}
```

Po stronie frontu `apiFetch` przepisuje `errors` na `ApiError.fieldErrors`, a komunikat samego wyjątku zostaje generyczny. To jedyna treść od serwera, którą wpuszczamy do interfejsu, i jest nią celowo: te zdania backend pisze po polsku dla wnioskodawcy.

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

### Autoryzacja: odmowa jest domyślna i wymuszona przez framework

Reguła "brak reguły oznacza brak dostępu" da się realizować dwojako: dyscypliną albo konfiguracją. Wybieramy konfigurację, bo dyscyplina tutaj nie skaluje się z powodu, który widać dopiero po awarii: zapomniany `RequireAuthorization` wygląda w review **identycznie** jak endpoint celowo publiczny. Nie ma czego zauważyć. Dlatego `AuthorizationConfiguration` ustawia `FallbackPolicy` wymagający zalogowania: trasa, która nie deklaruje nic, jest odmawiana przez framework, a nie serwowana wszystkim.

Ceną jest to, że każda naprawdę publiczna trasa musi teraz powiedzieć to na głos przez `AllowAnonymous`, i to jest właściwa strona tej wymiany: publiczny endpoint jest czyjąś decyzją i teraz tak właśnie się czyta. Dotyczy to dziewięciu tras (rejestracja, logowanie, wylogowanie, weryfikacja i odsyłanie adresu, oba kroki resetu hasła, obie sondy zdrowia) oraz **dokumentu OpenAPI**, bo `npm run api:generate` czyta go bez sesji, a tym, co nie czyni z niego publicznej mapy API nad danymi osobowymi, jest warunek `IsDevelopment` postawiony w T-17, nie brak dostępu.

Dwie z tych dziewiątek są nieoczywiste i warto wiedzieć dlaczego. `/logout` zostaje anonimowe, bo T-12.3 rozstrzygnęło, że wylogowanie jest idempotentne, a 401 dostałby dokładnie ten, kto najpewniej klika "wyloguj", czyli osoba z wygasłą już sesją. `/health` zostaje anonimowe, bo sonda żywotności wymagająca logowania nie jest w stanie zaraportować, że aplikacja leży.

### Dostęp do zasobu zależy od tego, CZYJ on jest, więc nie da się go zapisać atrybutem roli

Dwóch wnioskodawców ma tę samą rolę i różne prawa do tego samego wniosku. To jest cała przyczyna, dla której autoryzacja stoi na wymaganiu i handlerze (`EntityScopedRequirement`, `EntityScopedHandler`), a nie na atrybutach rozsypanych po endpointach. Handler jest jednym `switch`-em po roli i czyta się go jak tabelę uprawnień, bo nią jest: operator widzi wszystko, wnioskodawca swoje, recenzent nic.

**Recenzent jest odmawiany celowo, nie przez przeoczenie.** Ma widzieć wnioski **przypisane** sobie, a nic jeszcze nic nie przypisuje, bo to T-37. Uczciwą odpowiedzią do tego czasu jest "nie", ponieważ alternatywą jest recenzent widzący każdy wniosek w systemie przez całe okno między tymi dwiema kartami.

**Handler nigdy nie woła `Fail`, tylko `Succeed`.** Wymaganie, którego nikt nie spełnił, jest wymaganiem odmówionym, więc nowa rola dopisana do enuma (R-02 proponuje administratora) wpada w gałąź domyślną i jest odmawiana, dopóki ktoś nie napisze jej reguły. To jest bezpieczny kierunek mylenia się. Polityki ról powstają natomiast pętlą po `Enum.GetValues<Role>()`, więc dodanie roli jest wartością w enumie, a nie przepisaniem handlerów, dokładnie jak każe R-02.

**Domyślna odmowa dotyczy też żądań, które nie trafiają w żaden endpoint**, i to jest pułapka, której `FallbackPolicy` sam nie rozbraja: ASP.NET Core stosuje ją także wtedy, gdy routing nie dopasował niczego, więc literówka w ścieżce dostawała 401 "zaloguj się" zamiast 404. To złe w dwie strony. Anonimowemu wołającemu sugeruje, że wymyślona przez niego ścieżka być może istnieje za logowaniem, a panelom (T-15.2, T-15.3), które będą traktować 401 jako "sesja wygasła, idź na logowanie", kazałoby wylogowywać użytkownika przy każdej pomyłce w adresie. Dlatego `Program.cs` mapuje na końcu trasę `MapFallback` z `AllowAnonymous`, oddającą 404 jako `problem+json`. Jest terminalna, więc nie osłabia niczego: trasy, które istnieją, dopasowują się wcześniej.

**Odpowiedź na pytanie "czyj to zasób" siedzi w jednej metodzie** (`ResourceOwnership.BelongsTo`) i to też jest wykonanie instrukcji z R-01, która mówi nie rozsypywać `user.EntityId` po serwisach. Dziś to porównanie podmiotu konta z podmiotem zasobu, po R-01 stanie się sprawdzeniem członkostwa w organizacji, a zmieni się wyłącznie ta metoda. Konto bez podmiotu nie jest właścicielem niczego i jest to zapisane jawnie, bo `EntityId` jest dziś `null` dla **każdego** konta (rejestracja nie zakłada Podmiotu, B-09), a po stronie zasobu `EntityId` jest nienullowalne: bez tego strażnika odczyt `.Value` wywracałby każde takie żądanie w 500 zamiast odmówić. Odmowa jest przy tym poprawną odpowiedzią sama z siebie, więc to nie jest wyłącznie zabezpieczenie przed nullem.

### Endpoint chroniony najpierw wczytuje zasób, dopiero potem pyta o dostęp (T-13.3)

Kolejność jest tu całą treścią reguły i dlatego jest zapisana jako decyzja, a nie zostawiona autorowi każdego kolejnego endpointu: **wczytaj wiersz, zapytaj o wczytany wiersz, odpowiedz.** Trasa, która sprawdza uprawnienie na podstawie identyfikatora z adresu, przechodzi wszystkie testy T-13.2 i wycieka cudze dane, bo identyfikator w adresie kontroluje wołający. Sonda `PolicyProbeEndpoints.ApplicationById` jest napisana w tej kolejności i testy T-13.3 stoją na niej właśnie dlatego, że jest w kształcie prawdziwego endpointu produktowego. T-29, T-32 i T-33 mają skopiować **kolejność**, nie zapytanie: sonda nie filtruje po `IsActive`, bo nie jest jeszcze rozstrzygnięte, czy wniosek oznaczony jako nieaktywny znika własnemu podmiotowi, pozostając widoczny operatorowi, który odpowiada za niego przez pięć lat retencji. Pytanie stoi w [`runbook/rozbieznosci.md`](runbook/rozbieznosci.md) i należy do tych kart, nie do sondy testowej.

**Wiersz, którego nie ma, to 404, a wiersz cudzy to 403.** Karta T-13.2 wymaga 403 wprost, więc sklejenie obu odpowiedzi w jedną byłoby jej złamaniem. Rozróżnienie nie daje przy tym nic do enumeracji: wnioski adresuje się GUID-em w wersji 4, czyli nie ma ciągu, po którym można iść, a to, co jest odgadywalne (numer `001`, `002`), nie adresuje wiersza. Gdyby kiedyś numer trafił do adresu, ta decyzja wymaga ponownego rozstrzygnięcia i wtedy jedną odpowiedzią na oba przypadki będzie 404.

**Test negatywny sprawdza ciało, nie tylko kod statusu.** 403, które przy okazji wysyła odpowiedzi z wniosku, jest dokładnie tym wyciekiem, przed którym ta karta broni. Dlatego sonda oddaje w ciele sukcesu odpowiedzi wniosku, a testy twierdzą o NIEOBECNOŚCI markerów drugiego podmiotu w każdej odpowiedzi, którą wołający dostał.

**Suita, która potrafi zostać pominięta, nie blokuje niczego.** `[RequiresDatabaseFact]` raportuje Skipped bez bazy, a Skipped jest wystarczająco zielone, żeby zmergować. Stąd `PermissionSuiteCiGuardTests`: żadna metoda w `PermissionDenialTests` nie może być zwykłym `[Fact]`, a w CI connection string musi istnieć. Kryterium "testy blokują merge" jest więc przypięte testem, nie pamięcią recenzenta.
### Ochrona tras panelu stoi na pytaniu do serwera, nie na obecności ciasteczka (T-15.2)

Panel wnioskodawcy jest chroniony klientowym strażnikiem, który pyta `GET /me` i dopiero z odpowiedzi rysuje ramę. Nie jest to skrót: ciasteczko `ocwip.session` jest HttpOnly i wystawia je origin backendu, więc nie czyta go ani kod w przeglądarce, ani middleware Next.js renderujące na serwerze frontu. Nawet gdyby czytało, odpowiadałoby na złe pytanie, bo wylogowanie gdzie indziej obraca `SecurityStamp`, a ciasteczko wciąż leżące w przeglądarce może być już nic niewarte. Jedynym, kto wie, jest backend.

Z tego wychodzą trzy reguły, których strażnik pilnuje i które mają testy. **401 to brak sesji, a każdy inny błąd to awaria**, bo padnięty backend potraktowany jako wygasła sesja wylogowuje kogoś, kto jest zalogowany; dlatego `MapFallback` z T-13.2 oddaje 404, a nie 401. **Rama nie może mignąć przed rozstrzygnięciem**, bo nagłówek nazywa podmiot, a nawigacja obiecuje dostęp; stan wraca do "sprawdzamy" przed **każdym** pytaniem, nie tylko przed pierwszym, inaczej sesja wygasła w trakcie czytania zostawia poprzednią odpowiedź na ekranie na czas przekierowania. **Cudza rola dostaje odmowę, nie przekierowanie na logowanie**, bo sesja operatora jest ważna i ponowne logowanie niczego nie zmieni.

To jest ochrona wygody i prywatności ekranu, nie ochrona danych. Danych pilnuje wyłącznie backend (T-13.2), a strażnik, którego da się ominąć wyłączeniem JavaScriptu, ma z tego ominięcia zobaczyć puste ekrany.

### Oznaczenie trybu operatora jest na tokenach stanu aktywnego, nie na kolorze marki (T-15.3)

Pasek "Tryb operatora" stoi na `--color-active-bg` i `--color-active-text`, a nie na akcencie marki, bo tryb wysokiego kontrastu z T-15.1 przemalowuje właśnie te tokeny, a tokenów brandowych nie rusza. Napisany akcentem pasek wyglądałby poprawnie tylko w palecie podstawowej, a w kontraście zostałby pomarańczowym paskiem na czerni, czyli dokładnie tam, gdzie jest najmniej czytelny. Sam pasek jest pierwszym elementem nagłówka, więc jest też pierwszym, co czyta czytnik ekranu i co widać przy pokazywaniu ekranu na spotkaniu: operator ogląda cudze dane osobowe i nie może istnieć moment, w którym nie wie, czyj widok ma przed sobą.

Nagłówek jest przyklejony, a obszar treści przewija się poziomo sam (`overflow-x-auto`), bo tabela szersza od okna poszerzyłaby dokument, a przyklejony nagłówek trzyma się okna, nie dokumentu. Efektem byłoby oznaczenie trybu wyjeżdżające w lewo dokładnie przy czytaniu setnego wiersza cudzych danych, czyli w jedynym momencie, w którym operator naprawdę tej listy potrzebuje.

### Front pokazuje 403, ale nie jest tym, co go egzekwuje (T-15.3)

Panel operatora odmawia wejścia każdej roli poza operatorem i mówi to kodem 403, bo tego wymaga karta i bo człowiek ma zobaczyć konkretną odpowiedź, a nie pustą stronę. Prawdziwe 403 daje polityka domyślnej odmowy z T-13.2: żadna trasa API nie jest dostępna, dopóki reguła jej nie przepuści, i to obowiązuje niezależnie od tego, co narysuje front. Reguła jest jedna: **przepuszczamy jedną dozwoloną rolę, a nie odrzucamy listę niedozwolonych**, więc rola dołożona później do enuma wpada w odmowę, tak samo jak w handlerze autoryzacji po stronie backendu.

Ekran odmowy nie przekierowuje i nie ma nawigacji, bo sesja jest ważna i nie ma do czego przekierować. Dostaje za to link do własnego panelu odmówionego konta, ale **tylko wtedy, gdy ten panel istnieje**: panel recenzenta to zablokowany T-40, a ekranu logowania nie ma wcale (`R-25`), więc rola bez zbudowanego panelu nie dostaje żadnego linku zamiast linku na 404.

### Czekanie ma kształt panelu, a nie wyśrodkowanego komunikatu (T-15.4)

Strażnik sesji pyta `GET /me`, zanim cokolwiek narysuje, więc każde wejście do panelu ma moment oczekiwania. Wcześniej stał w nim wyśrodkowany komunikat, czyli **inny layout niż rama**, którą po odpowiedzi zastępował: nagłówek, nawigacja i pierwszy wiersz treści wskakiwały na swoje miejsca chwilę po odpowiedzi serwera. Operator czyta listy po sto kilkadziesiąt wniosków i klika w konkretne wiersze, a układ, który rusza się pod kursorem, zamienia kliknięcie w jeden wiersz w kliknięcie w sąsiedni. Przy przypisywaniu dotacji to kosztowna pomyłka.

Zamiast tego rysowany jest szkielet ramy o tej samej geometrii: te same odstępy, szerokość wiersza i tyle miejsc w nawigacji, ile panel ma realnych linków (liczone z jego modułu `navigation.ts`, nie wpisane liczbą). Szkielet **nie nazywa nikogo i nie opisuje trybu**: w tym momencie serwer nie odpowiedział jeszcze, kto jest zalogowany, więc napis "Tryb operatora" albo nazwa podmiotu byłyby zgadywaniem cudzych danych. Pulsowanie jest pod `motion-safe`, bo animacja bez wyjścia jest problemem dostępności, a klient jest podmiotem publicznym.

### Strona błędu nie mówi nic o błędzie (T-15.4)

`app/error.tsx` i `app/global-error.tsx` nie renderują ani `message`, ani `stack`, ani `digest`. Powód jest podwójny. Po stronie użytkownika: wnioskodawcami są organizacje pozarządowe i grupy nieformalne, a po stronie operatora osoba, która sama mówi, że nie zna się na technikaliach, więc komunikat techniczny nie pomaga nikomu. Po stronie bezpieczeństwa: opis wnętrza aplikacji przetwarzającej dane osobowe dostaje wtedy każdy, kto potrafi ją wywrócić.

Wyjścia są dwa, bo przyczyny są dwie: `reset()` ponawia ten ekran w miejscu (pojedyncze nieudane żądanie mija przy następnej próbie i nie ma powodu wyrzucać kogoś z wypełnianego wniosku), a link na stronę główną ratuje z ekranu, który jest zepsuty na stałe. 403 nie dostaje własnej trasy, bo front nie jest miejscem egzekwowania dostępu: pokazuje je tam, gdzie odmowa faktycznie zachodzi, czyli w strażniku panelu.

### Reguły bazowe CSS siedzą w warstwie base (T-15.4)

Reguła napisana poza wszystkimi warstwami wygrywa z każdą regułą w warstwie, niezależnie od specyficzności. Nasze `body`, nagłówki i `a` stały obok `@import "tailwindcss"`, więc `a { color }` wygrywało z klasą narzędziową postawioną na konkretnym linku: droga wyjścia z ekranu 403 dostawała pomarańczowy tekst na pomarańczowym tle w momencie najechania. Teraz są w `@layer base`, czyli są tym, czym miały być: wartościami domyślnymi, które klasa na pojedynczym elemencie nadpisuje.

Jeden wyjątek zostaje poza warstwami celowo: obramowanie `:focus-visible`. Żadna klasa narzędziowa nie ma prawa przypadkiem zdjąć widocznego fokusu.

### Stan konkursu liczy się przy odczycie, nie przestawia go zadanie w tle (T-20)

Raport mówi wprost, że nabór otwiera się i zamyka sam, z dat, i że nikt tych przejść nie przestawia. Drogi były dwie: zadanie cykliczne przepisujące kolumnę albo wyliczanie stanu przy odczycie. Wybrane jest drugie i rozstrzyga o tym jedna rzecz: zadanie cykliczne zostawia kolumnę **nieprawdziwą między tyknięciami**, a minuta, w której jest nieprawdziwa, to minuta zamknięcia naboru, czyli dokładnie ta, co do której decyzja D7 zabrania się mylić. Do tego w tym stacku nie ma żadnego schedulera, więc zadanie w tle trzeba by najpierw postawić.

Kolumna trzyma więc **ostatni stan, który wybrał człowiek**, a `CompetitionLifecycle.Effective` dokłada to, co od tamtej pory zrobił zegar. Pętla, nie jeden krok: konkurs z obiema datami w przeszłości jest w tej samej chwili otwarty i zamknięty, a odpowiedź "trwa nabór" byłaby kłamstwem z terminem ważności. Operator działa na stanie efektywnym, więc konkurs zapisany jako `opublikowany`, którego termin minął, przyjmuje przejście do oceny bez niczyjego wcześniejszego przepisania wiersza. Stany `trwa nabór` i `nabór zamknięty` po prostu **nigdy nie trafiają do kolumny**, dopóki nie zapisze ich operator.

T-21 buduje regułę "czy konkurs przyjmuje jeszcze wnioski" **na tym**, a nie obok tego.

### Przejścia stanów jako tabela par, nie jako rozsypany switch (T-20)

Wszystkie dozwolone ruchy siedzą w `Models/CompetitionStatusTransitions.cs` jako wiersze `(z, do, kto)`. Powód jest w `R-17`: raport ma trzy stany, których karty nie mają, a każdy z nich jest tani do dołożenia wyłącznie dopóki reguły o nich są jednym zbiorem, a nie gałęziami rozsypanymi po serwisie. Serwis nie porównuje statusów sam ani razu.

`AllowsOperator` odrzuca parę oznaczoną jako terminowa, i to nie jest szczegół: `opublikowany -> trwa nabór` **jest** w tabeli, ale tylko jako ruch zegara. Gdyby funkcja odpowiadała na pytanie "czy ta para jest wypisana", operator mógłby otworzyć nabór przed datą startu, czyli obejść jedyną rzecz, do której te daty służą. `nabór -> zamknięty` występuje w dwóch wierszach, terminowym i operatorskim, i to też nie jest duplikat: nabór ciągły nie ma daty zamknięcia, więc zegar go nigdy nie domknie i bez wiersza operatorskiego nie dałoby się go zamknąć w ogóle.

Odpowiedź API niesie `allowedTransitions` prosto z tej tabeli, żeby panel rysował przyciski z reguły, a nie z jej kopii. Konkurs nieaktywny dostaje tam pustą listę niezależnie od tabeli, bo lista służy do rysowania przycisków, a przycisk dający za każdym razem 409 jest gorszy niż jego brak.

### Konkurs roboczy nie ma publicznego adresu, więc odpowiada 404 (T-20)

Nie chodzi o ukrycie odnośnika. `GET /public/competitions/{id}` odpowiada **404** dla szkicu, dla konkursu nieaktywnego i dla identyfikatora, którego nie ma, tym samym komunikatem. 403 byłoby potwierdzeniem, że zgadnięty identyfikator nazywa coś prawdziwego, czyli tą samą klasą wycieku, przed którą broni reguła 3 z `AGENTS.md` przy kontach.

Konkurs **archiwalny** celowo adres zachowuje, choć wypada z listy: trwały odnośnik, który przestaje działać w dniu zarchiwizowania, nie jest trwały, a publiczne archiwum wyników (`R-14`) jest z tych właśnie stron zbudowane.

Widok publiczny ma też **własny typ odpowiedzi**, a nie ten sam z wyzerowanymi polami. Typ, który nie potrafi nieść śladu audytowego ani listy ruchów operatora, nie może ich wypuścić, i nie zależy to od uważności następnej osoby piszącej mapowanie.

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

**Poza Development migracje to osobny krok, osobną rolą (T-113).** Cel `migrate` w `backend/Dockerfile.prod` to `dotnet ef migrations bundle`: jeden plik wykonywalny, uruchamiany raz przed startem API rolą `ocwip_migrator`, która może zmieniać schemat. API łączy się rolą `ocwip_app` z samymi prawami do wierszy (`SELECT`, `INSERT`, `UPDATE`, `DELETE`); nadaje je migracja `AppRoleGrants`, razem z prawami domyślnymi na tabele z kolejnych migracji, i tylko wtedy, gdy rola `ocwip_app` istnieje, więc stos deweloperski z jedną rolą działa jak dotąd. Job CI na obrazach produkcyjnych migruje rolą DDL, sprawdza, że rola API nie utworzy ani nie zmieni tabeli, i dopiero wtedy puszcza smoke test. Usługę `migrate` w compose produkcyjnym dokłada T-111; kroki tworzenia ról są w [`wdrozenie.md`](wdrozenie.md).

**Strefa czasowa sesji na połączeniu, nie na bazie.** `UseOcwipPostgres` ustawia `Timezone=UTC` w każdym połączeniu. `ALTER DATABASE ocwip SET timezone` z `db/init` wymagał właściciela bazy i jej nazwy na sztywno, a zarządzany PostgreSQL w ogóle nie uruchamia `db/init`. Nieużywane rozszerzenia (`pgcrypto`, `unaccent`) wypadły: `gen_random_uuid()` jest wbudowane od PostgreSQL 13.

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

### Parametry konkursu z kreatora: jedna tabela plus trzy listy, a data usunięcia danych liczona od zamknięcia naboru

`T-20a` dołożył parametry kroków 1.2 do 1.6 kreatora ogłoszenia. Trzy decyzje z tego są nieoczywiste i wracałyby przy każdej kolejnej karcie, która dotknie konkursu.

**Parametry skalarne siedzą jedną tabelą, a nie po wierszu na krok kreatora.** Kreator dzieli je na ekrany i to jest podział interfejsu, nie danych: raport wprost każe nie blokować przechodzenia między krokami, więc konkurs wypełniany w kilku podejściach jest normalnym stanem. Wiersz na krok zamieniłby "połowa ustawień jeszcze nieuzupełniona" w pięć wierszy do utrzymania w zgodzie i pięć zapytań przy odczycie. Wszystkie kolumny są więc nullowalne albo mają wartość domyślną, a kompletności pilnuje dopiero publikacja.

**Listy poszły do tabel, nie do JSON-a na konkursie.** Wymagane załączniki dostaną w `T-32` wzór pliku, czyli klucz obcy do przechowywania plików, a klucza obcego nie da się wystawić z pola jsonb. Osoby kontaktowe wskazują konto pracownika zamiast kopiować nazwisko i adres, bo kontakt jest publikowany na stronie konkursu, a kopia zrobiona w dniu ogłoszenia pokazuje potem adres kogoś, kto już nie pracuje. Kategorie kosztów są tabelą, bo `R-12` mówi, że to ustawienie konkursu, a nie stała systemu, i obecność wiersza JEST tym ustawieniem: flaga obok obecności dawałaby dwa sposoby powiedzenia "wyłączona" i jeden z nich cichy.

Relacje mają `NoAction` jak cały schemat (reguła 1 z [`model-danych.md`](model-danych.md)), mimo że są to ustawienia konkursu, a nie czyjeś ślady. Cena jest jedna: wiersze porzucone przy edycji kasuje jawnie `CompetitionService`. Kaskada byłaby krótsza o tę pętlę i jednocześnie niewidoczna z poziomu schematu, a reguła 1 istnieje dokładnie po to, żeby żadne kasowanie nie działo się bez śladu w kodzie.

**Data usunięcia danych osobowych przy naborze ciągłym liczy się od otwarcia naboru.** Raport mówi "nie wcześniej niż pięć lat od zamknięcia naboru", a nabór ciągły nie ma zamknięcia. Czytanie literalne odrzuciłoby każdą datę i zamieniło wymagane pole w pole niemożliwe do wypełnienia. Podłogą jest więc otwarcie naboru: najwcześniejsza chwila, w której konkurs w ogóle może wyprodukować czyjeś dane osobowe. Nigdy nie jest to ostrzejsze od reguły i nigdy nie pozwala na krótszą niż pięcioletnią retencję danych już zebranych. Gdy nabór ciągły dostanie kiedyś jawne zamknięcie, podłoga przenosi się na nie.

### Odcięcie terminu jest jedną regułą, a godzina lokalna powstaje dopiero na brzegu (T-21)

Decyzja D7 mówi, że kto wejdzie o 12:05 przy zamknięciu o 12:00, ten już nie złoży wniosku. `T-21` zamienia to w jedno miejsce w kodzie, `CompetitionIntake`, i trzy karty (`T-29`, `T-32`, `T-33`) mają je pytać zamiast porównywać daty u siebie. Powód nie jest estetyczny: ten sam warunek napisany w składaniu, w autozapisie i w uploadzie to trzy kopie, które rozjadą się przy pierwszej zmianie, a minuta, w której się rozjadą, to minuta zamknięcia naboru.

**Reguła stoi NA `CompetitionLifecycle`, nie obok niego.** Ucinanie obu dat do pełnej minuty, pętla po przejściach terminowych i brak daty zamknięcia przy naborze ciągłym są już rozstrzygnięte tam. Powtórzenie któregokolwiek z nich dałoby drugą regułę udającą tę pierwszą. `CompetitionIntake` dokłada nad tym dokładnie dwie rzeczy: konkurs nieaktywny sprawdzany **przed** zegarem (inaczej otwarte okno wygrywa z dezaktywacją) oraz rozróżnienie stanów, których zegar nie rozróżnia.

**Cztery stany, nie flaga.** Odpowiedź "nie" ma trzy różne powody i każdy z nich mówi co innego wnioskodawcy: `NotYetOpen` ma datę w przyszłości, `Closed` ma datę w przeszłości, a `Unavailable` (szkic, konkurs nieaktywny) nie ma żadnej i nie wolno mu jej wymyślić. Sama flaga zmusiłaby każdy ekran do odtworzenia tego podziału z dat, czyli do napisania tej samej reguły jeszcze raz.

**Stan naboru jedzie z każdą odpowiedzią o konkursie**, publiczną i operatorską. To jest ta sama zasada, co `allowedTransitions` z `T-20`: panel rysuje przycisk z reguły, a nie z kopii reguły. Licznik do zamknięcia z `T-23` dostaje instant, nie tekst, bo tekst nie tyka.

**Godzina lokalna powstaje wyłącznie w `CompetitionIntakeMessage`.** Wewnątrz wszystko jest w UTC (zob. [Czas w UTC](#czas-w-utc)), więc zmiana czasu w październiku, która trafia w środek sezonu konkursowego, jest tu pytaniem o formatowanie, a nie o dane: ta sama godzina ścienna po obu stronach zmiany to dwa różne instanty i każdy jest nazywany swoją godziną. System obsługuje jeden region, więc czasem lokalnym jest czas polski, rozstrzygany w tym jednym miejscu. Gdy w obrazie zabraknie bazy stref czasowych, komunikat mówi wprost "czasu UTC", zamiast owijać polskie zdanie wokół godziny o jedną obok: zła godzina w komunikacie o terminie jest dokładnie tym, czemu ta karta ma zapobiegać.

### Strony publiczne renderuje serwer, a treść bierze z reguły, nie z zegara przeglądarki (T-23)

Lista konkursów i strona pojedynczego konkursu są komponentami serwerowymi. Powód jest potrójny i żaden z nich nie jest estetyczny: znaczniki Open Graph muszą istnieć w HTML-u, zanim jakikolwiek JavaScript się uruchomi, bo robot budujący podgląd wklejonego odnośnika go nie uruchamia; strona ma być czytelna z telefonu grupy nieformalnej, także wtedy, gdy skrypt nie dojechał; a wyszukiwarka to jedyny sposób, w jaki ktoś spoza kręgu OCWIP w ogóle trafi na nabór.

**Serwer Next.js woła API pod innym adresem niż przeglądarka.** `NEXT_PUBLIC_API_URL` jest adresem dla przeglądarki i prowadzi na opublikowany port hosta. Wewnątrz kontenera frontu ten sam adres wskazuje na ten kontener, więc renderowanie na serwerze potrzebuje nazwy usługi z compose. Stąd `API_SERVER_URL` i `baseUrl` w `apiFetch`: adres jest parametrem jednego wywołania, a nie drugim klientem API.

**Żadna strona nie liczy sama, czy nabór trwa.** Odpowiada na to reguła z `T-21`, która jedzie w `intake` razem z konkursem: przycisk "Wypełnij wniosek" rysuje się z `acceptsApplications`, a zdanie o terminie jest drukowane tak, jak przyszło, razem z datą, godziną i nazwą strefy (D12). Front dokłada wyłącznie licznik "pozostało", bo to jedyna rzecz, której instant nie niesie. Napisanie tego zdania jeszcze raz po stronie przeglądarki dałoby dwie odpowiedzi na jedno pytanie i rozjechałyby się przy pierwszej zmianie po którejkolwiek stronie.

**Licznik dolicza się dopiero po zamontowaniu.** Policzony także na serwerze rozjeżdżałby hydrację o równo minutę za każdym razem, gdy żądanie trafi blisko granicy minuty. Odświeża się co 30 sekund i nie pokazuje sekund: odcięcie jest co do minuty, a obszar zmieniający się co sekundę jest dla czytnika ekranu nie do użycia. Osoba bez JavaScriptu i tak dostaje zdanie z terminem, czyli tę część, która niesie decyzję.

**Konkurs roboczy nie dostaje metadanych.** `generateMetadata` pyta o ten sam zasób co strona i przy 404 nie zwraca ani tytułu, ani opisu konkursu. Inaczej tytuł nieogłoszonego naboru wyciekałby do podglądu odnośnika, mimo że sama strona odpowiada 404. Znacznika `og:url` nie ma wcale, bo publiczny adres jest faktem wdrożenia, a nie builda, a odnośnik względny rozwiązany przez robota do jego własnego originu prowadzi donikąd.

### Kontrakt definicji formularza: korzeń jako obiekt, reguły deklaratywne, walidacja w jednym miejscu (T-24)

Pełny opis kontraktu jest w [`kontrakt-formularza.md`](kontrakt-formularza.md). Tutaj zostają cztery rozstrzygnięcia, które zmieniłyby kształt systemu, gdyby zapadły później.

**Korzeń dokumentu jest obiektem, nie tablicą sekcji.** Check constraint w bazie dopuszcza obie postaci i zostaje szeroki, bo pilnuje kształtu, a nie treści. Kontrakt wybiera obiekt, bo dokument musi nieść własną wersję kontraktu obok wersji formularza: to są dwie różne odpowiedzi (jak czytać dokument kontra które pola widział wnioskodawca), a tablica nie ma gdzie zapisać pierwszej z nich.

**Ani warunek, ani obliczenie nie jest wyrażeniem.** Warunek widoczności to para "pole plus lista wartości", obliczenie to jeden z czterech sposobów plus lista składników, a limit to reguła plus podstawa. Wynika to z dwóch rzeczy naraz: kreator (`T-26`) ma pozwolić ustawić to klikaniem osobie, która mówi o sobie, że nie zna się na technikaliach, a decyzja D12 każe silnikowi **odwrócić** regułę limitu, żeby komunikat podał kwotę graniczną zamiast procentu. Wzoru zapisanego tekstem nie da się ani wyklikać, ani odwrócić.

**Budżet nie ma własnego rodzaju pola.** Jest tabelą o zmiennej liczbie wierszy, której kolumna wartości jest zwykłym polem wyliczanym, a kwota dotacji (D11) polem wyliczanym z różnicy. To jest test całego schematu: gdyby budżet potrzebował wyjątku, znaczyłoby to, że schemat powstał pod coś innego, a najtrudniejszy ekran produktu został doklejony z boku.

**Definicja jest sprawdzana przy zapisie, nie przy rysowaniu.** Jedno wejście, `FormSchemaValidator`, i jedna reguła: do bazy nie trafia definicja, której renderer nie umie wyświetlić. Odmowa niesie komplet powodów naraz, każdy jako ścieżka JSON dla kreatora i zdanie po polsku nazywające pole dla człowieka. Wariant "renderer poradzi sobie z tym, co dostanie" przegrywa, bo kontrakt ma trzy strony (kreator, renderer, walidacja odpowiedzi z `T-30`) i reguła sprawdzana w każdej z nich osobno rozjedzie się w pierwszym tygodniu.

**Nazwy mają jedną pisownię (R-34, 2026-09-29).** Sposób obliczenia, rodzaj limitu i format pliku backend czytał bez względu na wielkość liter, a front porównuje je dosłownie, więc `"Ratio"` przechodził bramkę, backend liczył udział procentowy, a ekran pokazywał `0,00 zł`. Wybrana strona to backend: `FormJsonReader.TryParseName` przyjmuje tylko nazwę w camelCase, tak jak rodzaje pól i `appliesTo` już wcześniej. Odrzucona normalizacja we froncie: siedem miejsc wejścia definicji i dwie pisownie tej samej wartości zostające w zapisanym JSON-ie, które każdy kolejny czytelnik musiałby znać.

Liczby konkursu (kwoty i procenty) **nie wchodzą do definicji**: limit odwołuje się do nich po nazwie, na przykład `competition.maxGrantAmount`. Ten sam formularz służy konkursom o różnych limitach, a kwota skopiowana do dokumentu jest tą, która za rok będzie nieprawdziwa, i nikt nie będzie wiedział, w którym z formularzy siedzi.

### Publikacja wersji formularza dokłada wiersz, a wersja w mocy jest tylko wskaźnikiem (T-25)

**Publikacja nigdy nie nadpisuje.** Jedyna droga zapisu do `form_definitions.definition` to `POST /competitions/{id}/form-definitions`, który dokłada wiersz z kolejnym numerem wersji. Nie ma ani `PUT`, ani `DELETE` na wersji, a brak tych tras jest projektem, nie luką: wniosek wskazuje na wersję, retencja trwa pięć lat, więc dokument podmieniony pod wnioskiem oznacza, że złożonej oferty nie da się już odtworzyć w postaci, w jakiej ją pokazano. Test `There_is_no_route_that_replaces_a_published_version` przypina tę nieobecność, żeby dołożenie takiej trasy było świadomym aktem, a nie wygodą dorzuconą przy okazji.

**Nowa wersja staje się wersją w mocy dla konkursu, a wnioski zostają przy swojej.** To są dwie różne odpowiedzi na dwa różne pytania: `competitions.form_definition_id` mówi, co dostanie NASTĘPNY wnioskodawca, a `applications.form_definition_id` mówi, co widział ten, który już zaczął. Wariant, w którym publikacja wyłącznie dokłada wiersz, a wskazanie zostaje na edycji konkursu, przegrywa, bo zostawia opublikowaną wersję bez żadnego skutku i każe wykonać dwa kroki, żeby zrobić jedną rzecz. Wybór wersji przez `PUT /competitions/{id}` nadal działa i to on obsłuży cofnięcie się do poprzedniej wersji oraz wybór wersji w ogłoszeniu z `T-22`.

**Edycja konkursu bez podanej wersji formularza niczego nie cofa.** `PUT /competitions/{id}` przepisuje `form_definition_id` tylko wtedy, gdy żądanie je niesie: null znaczy "nie zmieniam wersji", a nie "zabierz formularz". Przed `T-25` kolumna i tak była zawsze pusta, więc rozróżnienie nie miało znaczenia; teraz edycja samych terminów wysłana bez tego pola cofałaby trwający nabór do stanu bez formularza, cicho i z kodem 200. Wyczyszczenie wskazania nie jest żadną operacją produktową, więc nie ma dla niego osobnej drogi.

**Numer wersji liczy się jako `max + 1` razem z wersjami nieaktywnymi.** Numer raz zużyty nie wraca, bo to on jest tym, co człowiek cytuje przy wniosku sprzed trzech lat, a drugie "wersja 3" w jednym konkursie czyni ten cytat niejednoznacznym na cały okres retencji. Inaczej niż numer konkursu, który przy dezaktywacji wraca do puli, bo tam unikalność jest ograniczeniem nazewnictwa, a nie śladem tego, co komu pokazano.

**Identyfikator wersji nadaje kod, nie kolumna.** Domyślne `gen_random_uuid()` zostaje w schemacie, ale serwis wpisuje własny identyfikator, bo konkurs musi wskazać na nową wersję w TYM SAMYM zapisie: klucz obcy jest złożony na `(id, form_definition_id)`, a odczytanie klucza z bazy oznaczałoby drugą rundę i okno, w którym wersja już istnieje, a w mocy nie ma nic.

**Wyścig dwóch publikacji kończy się na 409, nie na 500.** Kolejny numer wyliczany jest `SELECT`-em, a `SELECT` ten wyścig przegrywa, więc odpowiada unikalny indeks `(competition_id, version_number)`, rozpoznawany po nazwie, i wołający dostaje odpowiedź, którą może po prostu powtórzyć. Ten sam wzorzec co numer konkursu w `CompetitionService` i adres w `AccountService`.

### Kreator formularzy: szkic w przeglądarce, kopia jako domyślny punkt startowy (T-26, T-26a)

**Szkic siedzi w `localStorage`, nie w bazie.** `T-24` i `T-25` świadomie nie dały definicji formularza żadnej drogi zapisu poza publikacją: nie ma `PUT` ani `PATCH` na wersji. Dorobienie encji "szkic definicji formularza" w bazie tylko po to, żeby kreator miał gdzie zapisywać pracę w toku, powielałoby to, co retencja i wersjonowanie już rozwiązują dla dokumentu OPUBLIKOWANEGO, a szkic z definicji nigdy nie jest publikowany bez udziału operatora. Kryterium karty ("praca kreatora przeżywa zamknięcie przeglądarki") jest więc rozwiązane w przeglądarce, jeden szkic na konkurs, kluczowany identyfikatorem konkursu. Cena: szkic nie przenosi się między przeglądarkami ani urządzeniami, tylko między otwarciami tej samej. Publikacja (`T-27`) czyta z tego samego miejsca i dopiero ona woła `POST /competitions/{id}/form-definitions`, czyli bramkę kontraktu z `T-24`.

**Kopia jest domyślnym punktem startowym, ale nie jedynym (T-26a).** Do `T-26a` kreator potrafił wyłącznie skopiować formularz z konkursu, który już go ma, więc pierwszy formularz w systemie musiał powstać poza kreatorem (dziś odpowiednik tego w `scripts/seed.py`). Teraz obok kopiowania stoi "Zacznij od zera", a przy pustym systemie jest to jedyne wyjście, więc ten zewnętrzny krok przestał być wymagany. Kopiowanie zostaje pierwszą propozycją świadomie: raport czyta realny sposób pracy OCWIP jako "zeszłoroczny formularz z poprawkami", a pusta strona jest trudniejszym startem dla osoby, dla której ten kreator powstał. Formularz od zera dostaje od razu jedną sekcję, nie zero, bo dokument bez ani jednej sekcji jest odrzucany przy publikacji i operator nie ma oglądać punktu startowego, którego system nie przyjmie.

**Blokada przestawienia sekcji liczy dokument po ruchu, zamiast zakazywać ruchu sekcji z warunkiem.** Przestawienie sekcji to jedyna operacja kreatora, która psuje warunek widoczności, nie dotykając ani jednego warunku: rusza odpowiedź, nie pytanie. Obliczenia i limity są poza zasięgiem, bo oba są wewnątrzsekcyjne (`document-candidates.ts`). Rozważone były trzy warianty. Zakaz ruchu każdej sekcji, która ma jakikolwiek warunek, jest najprostszy i odbiera ruch także tam, gdzie jest całkowicie bezpieczny, na przykład przy zamianie dwóch sekcji, z których żadna nie czyta drugiej. Przepuszczanie każdego ruchu i odmowa dopiero przy publikacji daje operatorowi ścieżkę JSON zamiast zdania. Wybrany wariant, `lib/forms/section-guards.ts`, składa dokument po ruchu i sprawdza na nim tę samą regułę co `FormSchemaReferences` na backendzie, więc reguła jest jedna, a nie dwie rozjeżdżające się kopie. Pozycją sekcji jest pozycja jej pierwszego pola, dokładnie jak w `CheckSectionConditions`.

**Blokada odejmuje naruszenia, które dokument miał już wcześniej.** Bez tego formularz, który przyszedł zepsuty (import, starszy kontrakt), miałby zamrożoną każdą sekcję przez usterkę, której operator nie spowodował i której ten ruch nie pogarsza. Z odejmowaniem blokujemy wyłącznie to, co ten konkretny ruch psuje.

**Warunek widoczności, obliczenie i limit są zawsze wyborem z listy, nigdy wpisanym tekstem, po stronie frontu tak samo jak w kontrakcie.** `lib/forms/document-candidates.ts` liczy, co wolno wybrać, tak żeby wybór był już poprawny na drucie: warunek widoczności może wskazać wyłącznie pole stojące wyżej w dokumencie, obliczenie i limit wyłącznie pole liczbowe w zasięgu (kolumna własnej tabeli, albo pole tej samej sekcji, albo ustawienie konkursu z przedrostkiem `competition.`). Silnik po stronie backendu (`FormSchemaValidator`, `T-24`) i tak sprawdzi to samo przy publikacji, ale odmowa dopiero wtedy byłaby dla operatora komunikatem bez adresu: pole, które sam dodał, już nie stoi pod tym numerem, co widziała ścieżka JSON.

**Klucz pola generuje się raz, z etykiety, i potem stoi.** Zmiana etykiety nie zmienia klucza: klucz jest tym, po czym odwołuje się do pola warunek i obliczenie gdzie indziej w dokumencie, a regenerowanie go przy każdej literówce w etykiecie po cichu odrywałoby te odwołania od celu.

### Renderer formularza: silnik odpowiedzi osobno od kontraktu, wartość wyliczana nigdy nie jest odpowiedzią (T-28)

**`lib/forms/evaluate.ts` czyta dokument i odpowiedzi, nigdy nie pisze do odpowiedzi.** Warunek widoczności i wartość pola wyliczanego są funkcjami czystymi `(dokument, odpowiedzi) -> wynik`, liczonymi na nowo przy każdym renderze, a nie zapisywanymi do stanu formularza. Alternatywa, w której wynik pola wyliczanego trafia do tych samych odpowiedzi co pole wpisane przez wnioskodawcę, była pierwszą wersją i miała usterkę, którą złapało własne review: nic nigdy nie wywoływało silnika, więc pole wyliczane pokazywało "0" na stałe, mimo że komunikat błędu obok, liczony tym samym silnikiem gdzie indziej, był poprawny. Dwa źródła prawdy o jednej liczbie na jednym ekranie to usterka czekająca na ujawnienie, nie oszczędność.

**Stan `touched` gates the widoczny komunikat błędu, nie samo istnienie błędu.** Pasek sekcji (`section-nav.tsx`) liczy prawdziwy stan każdej sekcji zawsze, z pełnych, aktualnych odpowiedzi, więc "gotowa" znaczy naprawdę gotowa. Komunikat POD polem czeka na dotknięcie tego pola (albo na opuszczenie całej sekcji, co dotyka wszystkiego, co w niej widoczne), zgodnie z proces.md regułą 4: "błąd pojawia się tylko wtedy, gdy naprawdę jest". Pole wyliczane jest wyjątkiem świadomym: nie da się go dotknąć samodzielnie (jest `readOnly`), więc jego jedyny błąd, przekroczony limit, pokazuje się od razu.

**Stan `touched` komórki tabeli trzyma się po pozycji `wiersz[indeks].kolumna`, więc przestawienie i usunięcie wiersza muszą przenieść go razem z danymi.** Własne review złapało to jako osobną usterkę: bez przenoszenia klucza błąd zostaje przy starym numerze wiersza, nie przy danych, które go wywołały. `renderer-context.tsx` eksportuje `touchedAfterRowRemoved` i `touchedAfterRowsSwapped` właśnie po to, żeby ta księgowość działa się w jednym miejscu, wywoływanym z każdej operacji na wierszu (`form-renderer.tsx`), a nie osobno przy każdej z nich.

**Tabela o stałej liczbie wierszy nie ma nic w odpowiedziach, dopóki ktoś nie dotknie komórki.** Rekord w `answers` powstaje leniwie, a `table.rows` (trzej nazwani członkowie grupy) jest jedynym źródłem prawdy o tym, ile wierszy istnieje i jak się nazywają. `resolveTableRows` w `answer-types.ts` jest jedynym miejscem, które to wie: czyta strukturę dla tabeli stałej, odpowiedzi dla zmiennej, i zawsze oddaje gęstą tablicę, bez dziur, które `Array.prototype.forEach` po cichu pomija. Sekcja z pustą, nietkniętą tabelą stałą liczy się jako "w toku", nie jako "gotowa": czytanie samych odpowiedzi zgłosiłoby zero wierszy do sprawdzenia.

**Renderer nie zna backendu.** Zero wywołań API, zero zapisu wniosku: to komponent czysto kliencki, biorący dokument i odpowiedzi jako `props` i oddający zmiany przez `onChange`. Ustawienia konkursu, względem których liczą się limity (D12), przychodzą jako osobny `props`, nie są czytane z dokumentu formularza (ten sam formularz obsługuje konkursy o różnych limitach, patrz sekcja o T-24 wyżej). Persystencja, autosave i przycisk złożenia wniosku to `T-34`; podgląd operatora na tym samym komponencie to `T-27`.

### Podgląd i publikacja: ten sam renderer, jedna nowa rzecz w `ApiError` (T-27)

**Podgląd nie ma własnego komponentu.** `preview-panel.tsx` woła `FormRenderer` z T-28 wprost na dokumencie kreatora, bez pośredniej kopii. Jedyna rzecz, jakiej podgląd świadomie nie robi, to podpięcie zapisu: `onChange` zostaje nieprzekazane, więc nie ma dokąd zapisać, co jest całym mechanizmem stojącym za kryterium "da się przejść do końca bez zapisywania czegokolwiek jako wniosek". Przycisk "od nowa" remontuje renderer (`key`), więc każde wejście w podgląd zaczyna od pustych odpowiedzi, nie od tego, co ktoś wpisał poprzednio.

**Ekran podglądu i publikacji siedzi na tej samej trasie co kreator (`T-26`), nie jako osobna.** Oba działają na dokładnie tym samym dokumencie, który operator właśnie edytuje, włącznie ze szkicem jeszcze niezapisanym jako wersja: osobna trasa musiałaby albo dublować ten stan, albo czytać go pośrednio, a to jest dokładnie ryzyko, przed którym ostrzega karta ("osobny komponent podglądu rozjedzie się z prawdziwym formularzem").

**`ApiError` dostał pole `detail`, oddzielone od `message`.** Backend pisze `ProblemDetails.detail` świadomie w miejscach, gdzie dwa różne powody dają ten sam kod HTTP (publikacja formularza: 409 znaczy albo wyścig dwóch publikacji, albo konkurs oznaczony jako nieaktywny w międzyczasie, `FormDefinitionEndpoints.cs`). Generyczny `message` zostaje domyślny wszędzie, łącznie z ekranami logowania, bo tam ujawnienie czegokolwiek jest dokładnie tym, czego reguła 3 zabrania; `detail` to osobne, opcjonalne pole, które trzeba świadomie przeczytać, więc żaden istniejący ekran nie zaczyna nagle pokazywać więcej, niż pokazywał.

**Publikacja czyści szkic w `localStorage`, nie zostawia go osieroconego.** Opublikowana wersja ma już swój wiersz w bazie; trzymanie szkicu dalej groziłoby tym, że kolejne otwarcie kreatora czyta stary, lokalny zapis zamiast tego, co właśnie stało się wersją w mocy. Panel publikacji wraca do stanu przycisku, gdy dokument zmieni się po publikacji albo po odrzuconej próbie (porównanie referencji, nie głębokie): bez tego "Opublikowano wersję 1" albo komunikat błędu zostawałyby na ekranie na stałe i wersji 2 nie dałoby się opublikować bez przeładowania strony, co złapało własne review.

### Kreator ogłoszenia konkursu: zapis stopniowy, nie jeden strzał na końcu (T-22)

**Szkic w `localStorage` do pierwszego zapisu, potem konkurs jest bazą prawdy.** W przeciwieństwie do kreatora formularzy (`T-26`), backend konkursu NIE ma osobnej drogi na "szkic": `CompetitionRequestValidator` sprawdza te same reguły przy `POST` i przy `PUT`, więc każdy prawdziwy zapis musi już mieć numer, tytuł, terminy i maksymalną dotację. Dopóki tych pól nie ma, kreator trzyma wszystko wyłącznie lokalnie (jeden slot "nowy konkurs", bo przed pierwszym zapisem konkurs nie ma jeszcze identyfikatora, którym dałoby się kluczować szkic jak w T-26). "Zapisz" jest dostępny z każdego kroku i próbuje `POST`, a po pierwszym sukcesie przechodzi na `PUT`, więc "walidacja nie blokuje przechodzenia między krokami" (proces.md) jest prawdą interfejsu, nie backendu: nawigacja nigdy nie czeka na zapis, ale zapis czeka na komplet pól.

**Podgląd przed publikacją czyta zapisany konkurs z API, nie renderuje lokalnego szkicu.** Kryterium karty ("podgląd pokazuje dokładnie to, co zobaczy wnioskodawca") jest spełnione przez dosłowne użycie tych samych komponentów co publiczna strona konkursu (`CompetitionFacts`, `CompetitionAttachments`, `CompetitionContacts` z `T-23`), karmionych odpowiedzią `GET /competitions/{id}` operatora, nie kopią przeliczaną w przeglądarce. Stąd wejście na krok 1.7 zawsze najpierw próbuje zapisać: bez zapisanego konkursu nie ma czego pokazać, a pokazanie czegoś zbudowanego lokalnie byłoby drugim, potencjalnie rozjeżdżającym się źródłem prawdy o tym samym ekranie.

**Zdanie o terminie zamknięcia (krok 1.1) świadomie NIE jest regułą T-21.** `describeDeadline` w `lib/competition-wizard/intake-sentence.ts` tylko formatuje wpisaną datę: konkurs jeszcze nie istnieje (albo istnieje, ale operator dopiero co zmienił pole, którego zapis jeszcze nie widział), więc nie ma czego zapytać regułę `CompetitionIntake`, która potrzebuje zapisanego wiersza i zegara. To osobna, węższa odpowiedź niż "czy nabór trwa", nie druga kopia tamtej reguły: żadna trasa produktowa jej nie woła.

**"Wybór formularza wniosku" jest linkiem do kreatora z T-26, nie polem w tym formularzu.** `form_definitions.competition_id` wskazuje na WŁASNY konkurs (złożony klucz obcy w `CompetitionConfiguration`), więc formularza nie da się przypiąć do konkursu, który jeszcze nie ma identyfikatora. Krok 1.1 pokazuje link do `/panel/operator/forms/{id}` dopiero po pierwszym zapisie; `formDefinitionId` na konkursie ustawia się dopiero publikacją wersji formularza (`T-25`/`T-27`), nie tym ekranem.

**Krok 1.6 potrzebował nowego, małego endpointu: `GET /accounts/operators`.** Lista "osoby kontaktowe: wybór z listy pracowników" (pola.md) nie miała skąd wziąć kont, a każący operatorowi wpisywać GUID ręcznie łamałby wymóg prostoty z karty. Nowy endpoint czyta dokładnie tę samą regułę, jaką `CompetitionService.FindContactsAsync` i tak sprawdza przy zapisie (`Role == Operator && IsActive`), więc katalog nigdy nie zaproponuje konta, którego zapis by odrzucił.

**Kwoty i procenty jadą na drut jako tekst, nie jako sparsowana liczba.** `CompetitionRequest` na drucie deklaruje te pola jako `number | string` (backend akceptuje `AllowReadingFromString`, sprawdzone ręcznie przez prawdziwe żądanie, nie tylko przez typy), a `to-request.ts` przekazuje dosłownie to, co wpisał operator. Zamiana na `Number()` po drodze zgubiłaby ostatni grosz przy niektórych wartościach dziesiętnych, dokładnie to, przed czym ostrzega komentarz w `format.ts` przy `formatAmount`.

**Zmienione w T-97.** Kreator nie publikuje już konkursu i nie jest jedynym miejscem jego edycji: publikacja przeszła na stronę konkursu, a kreator otwiera zapisany konkurs z serwera (niżej, sekcja T-97). Akapity powyżej o `localStorage` i "Zapisz" opisują dziś tylko konkurs przed pierwszym zapisem.

**Świadomie pominięte w tej karcie.** Krok 0 (kopia konkursu z poprzedniego roku): karta go nie wymienia, a pełna wersja z raportu wymaga kart oceny i wzorów dokumentów, których system jeszcze nie ma (`R-11`). Zaplanowana data publikacji z kroku 1.1 raportu: publikacja zostaje świadomym klikiem, jak zbudował to `T-20` (`R-27` nadal otwarte). Edycja istniejącego, już opublikowanego konkursu z ostrzeżeniem o wpłyniętych wnioskach (proces.md): wnioski jeszcze nie istnieją jako trasa API (`T-29`, `T-33`), więc ostrzeżenie nie miałoby czego pokazać; ten ekran obsługuje wyłącznie tworzenie nowego konkursu.

### Wersja robocza wniosku: suma kontrolna licząca się na nowo, nie zapisywana (T-29)

**Suma kontrolna (D15) jest funkcją czystą, nie kolumną.** `Models/ApplicationChecksum.cs` liczy ją z identyfikatora wniosku, ostatniego zapisu (`UpdatedAt`) i odpowiedzi, za każdym odczytem od nowa. Alternatywa, kolumna `checksum` zapisywana obok `answers`, ma dokładnie tę wadę, przed którą ostrzega `docs/model-danych.md`: dwa fakty o tym samym wierszu, aktualizowane osobno, rozjeżdżają się w dniu, w którym ktoś doda drugą drogę zapisu odpowiedzi i zapomni dopisać przeliczenia sumy. Czysta funkcja nie ma jak się rozjechać ze stanem, który opisuje.

**Suma liczy się z KANONICZNEJ postaci odpowiedzi, nie z surowego tekstu JSON.** PostgreSQL, zgodnie z `docs/model-danych.md`, nie trzyma w `jsonb` ani kolejności kluczy obiektu, ani białych znaków. Bez tego zastrzeżenia suma policzona zaraz po zapisie (z odpowiedzi, jaką przysłał wnioskodawca) różniłaby się od sumy policzonej przy kolejnym odczycie (z odpowiedzi, jaką oddaje baza), mimo że treść się nie zmieniła: ekran pokazywałby inną "sumę kontrolną" po każdym przeładowaniu strony. `ApplicationChecksum` sortuje klucze obiektów przed haszowaniem i to zastrzeżenie ma test w `ApplicationChecksumTests.cs`, który świadomie porównuje dwa zapisy tej samej treści w innej kolejności kluczy.

**Ten sam problem, o jedną cyfrę precyzji, dotyczy `UpdatedAt`.** `timestamptz` w PostgreSQL trzyma mikrosekundy, a tick .NET jest o rząd wielkości dokładniejszy, więc wartość zapisana i odczytana z powrotem różni się na ostatniej cyfrze. `ApplicationService` woła `Entry(...).ReloadAsync` po każdym zapisie, zanim policzy sumę do odpowiedzi: bez tego przeglądarka dostawałaby jedną sumę zaraz po zapisie i inną przy najbliższym odświeżeniu, dla dokładnie tych samych danych.

**Wersja robocza nie miała osobnej ścieżki dla braków, bo T-30 jeszcze nie istniało.** W T-29 `SaveDraftAsync` sprawdzał wyłącznie kształt korzenia (obiekt albo tablica, ten sam warunek co check constraint w bazie): zapisanie tam reguły pól byłoby zgadywaniem kontraktu, który ustaliło dopiero T-30. Od T-30 autozapis przechodzi przez walidator na poziomie szkicu, patrz niżej.

**Zakładanie wniosku pyta o Podmiot wołającego, nigdy o identyfikator z ciała.** `CreateDraftAsync` czyta `User.EntityId` z konta zalogowanego wnioskodawcy przez `ClaimsPrincipal`, a nie z parametru żądania: przyjęcie identyfikatora podmiotu od klienta byłoby furtką do założenia wniosku pod cudzym Podmiotem, dokładnie tym, przed czym broni `resource.owner` z T-13.2 przy odczycie. Konto bez Podmiotu (dziś każde, `B-09`) dostaje 403 z osobnym komunikatem, a nie 404 czy 500: karta organizacji (proces.md, ścieżka 2) jest osobną, jeszcze niezbudowaną drogą, więc ta karta świadomie NIE zakłada Podmiotu przy okazji.

**Odczyt, zapis i dezaktywacja idą tą samą drogą co `PolicyProbeEndpoints.cs` z T-13.3: wczytaj zasób, zapytaj `IAuthorizationService`, dopiero potem działaj.** `ApplicationEndpoints.cs` jest pierwszym prawdziwym endpointem produktowym idącym tą drogą, którą T-13.3 udowodniła wyłącznie na trasie testowej. Trzy trasy (`GET`/`PUT`/`DELETE /applications/{id}`) nie niosą własnej polityki roli: o dostęp pyta wyłącznie polityka `resource.owner`, więc operator widzi wszystko, wnioskodawca tylko swoje, a recenzent nic, dopóki `T-37` nie przydzieli mu wniosków.

### Walidacja odpowiedzi: dwa poziomy jednego walidatora, klucze błędów renderera (T-30)

**Prawda jest po stronie backendu, a renderer jest wygodą.** `Models/Forms/AnswerValidator.cs` czyta ten sam dokument co renderer (`frontend/lib/forms/validate.ts`) i ma te same reguły, ale to jego odmowa decyduje. Bez niego każdy mógł wysłać dowolny JSON prosto na `PUT /applications/{id}` i mieć go zapisanego jako wniosek, który potem czyta wydruk, raport i wzór umowy.

**Jeden walidator, dwa poziomy, a granica między nimi to pytanie "czy renderer mógł to wysłać".** Szkic odrzuca tylko to, czego formularz nie potrafi wyprodukować: klucz spoza definicji, klucz dwa razy, zły rodzaj wartości, opcję spoza listy, tekst ponad `maxLength`, odpowiedź w polu wyliczanym, nadmiar wierszy. Braków, zakresów i limitów w szkicu nie sprawdza, bo formularz wypełnia się tygodniami, a budżet przekracza limit w połowie wpisywania pozycji: autozapis, który odmawia zapisu w tej chwili, gubi pracę. Złożenie (`T-33`) dokłada resztę. Odrzucony wariant: jeden poziom z listą wyjątków dla szkicu. Wyjątki mnożą się z każdym rodzajem pola, a pytanie o renderer rozstrzyga każdy nowy przypadek bez dopisywania wyjątku.

**Nieznany klucz jest odrzucany, nie pomijany.** Klucz pominięty po cichu dziś jest kluczem, który jutro przeczyta wydruk albo wzór umowy, a wnioskodawca i operator zobaczą różne wnioski. Z tego samego powodu odrzucany jest klucz podany dwa razy: jeden czytelnik bierze pierwszy, drugi ostatni.

**Wartość wyliczana nigdy nie przychodzi w żądaniu (D11).** Walidator odmawia odpowiedzi w polu `calculated` już na poziomie szkicu i liczy każdą wartość sam, na `decimal` z pełną precyzją (D13). Przyjęcie jej z żądania pozwoliłoby przysłać własną kwotę dotacji obok każdego limitu, który ją mierzy. Arytmetyka nasyca się na największym `decimal` zamiast rzucać wyjątek, bo każda liczba pochodzi z żądania, a 10^20 razy 10^20 w dwóch komórkach ma dać komunikat o limicie, a nie błąd 500.

**Walidacja względem wersji wniosku, nie najnowszej.** Serwis wczytuje `Application.FormDefinition`, czyli wersję, na której wniosek rozpoczęto (T-25). Wniosek wypełniany według wersji 3 nie może przestać przechodzić, kiedy operator opublikuje wersję 4.

**Klucz błędu jest kluczem renderera.** `ValidationProblemDetails.errors` ma klucz pola albo `tabela[wiersz].kolumna`, dokładnie jak `cellKey` w `renderer-context.tsx`. Alternatywa, ścieżka JSON jak przy odmowie definicji (`$.sections[1]...`), pasuje do kreatora, który pracuje na dokumencie, ale nie do formularza, który pracuje na polach: front musiałby tłumaczyć klucze, a tłumaczenie to miejsce, w którym dwie strony się rozjeżdżają.

**Komunikat limitu podaje granicę wyliczoną dla tego wniosku (D12)**, w tym samym brzmieniu co renderer, a kwoty formatuje ręcznie w polskim zapisie, bez kultury `pl-PL`. Z tego samego powodu `CompetitionIntakeMessage` formatuje daty w kulturze niezmiennej: obraz bez danych ICU nie zgłasza błędu, tylko po cichu formatuje po angielsku.

**Zamknięty nabór wygrywa z błędną odpowiedzią.** Kolejność w `SaveDraftAsync` jest taka: kształt korzenia, wiersz, status, nabór (T-21) i dopiero na końcu odpowiedzi. Wnioskodawca po terminie ma usłyszeć o terminie, a nie o polu, którego i tak już nie poprawi.

**Zapisana definicja, która nie przechodzi dzisiejszej bramki, kończy autozapis błędem 500, nie cichym przepuszczeniem.** Walidacja bez dokumentu nie ma względem czego sprawdzać, więc odmowa jest jedyną bezpieczną odpowiedzią (reguła 1). Konsekwencja dla przyszłych zmian: zaostrzenie `FormSchemaValidator` o regułę, której nie spełniają definicje już zapisane, zatrzymuje autozapis we wszystkich wnioskach tych konkursów. Taka zmiana idzie więc razem z nowym `schemaVersion` albo z migracją zapisanych definicji. Ten sam powód wymusił w `T-30` przepisanie definicji w `scripts/seed.py`, która pochodziła sprzed kontraktu.

**Załącznik to na razie nazwa i rozmiar.** Formaty i limit rozmiaru sprawdzi `T-32` na prawdziwym pliku, bo nazwa i liczba wpisane w żądanie niczego o pliku nie dowodzą. Poziom złożenia nie ma jeszcze trasy HTTP: wepnie go `T-33`, a tutaj stoi na testach jednostkowych.

### Limity budżetu: próg z konkursu, suma kilku sum i pozycja przekroczenia (T-31)

**Cztery reguły budżetu to cztery limity w definicji formularza, nie osobny moduł.** Dotacja pod `competition.maxGrantAmount`, suma tabeli B i suma tabeli C każda pod swoim progiem procentowym, a wartość wiersza jako iloczyn liczby jednostek i ceny. Wszystkie siedzą na walidatorze z `T-30`. Odrzucony wariant to kod znający tabele A, B i C po nazwie: kategorie kosztów są ustawieniem konkursu (`pola.md`), więc kod ze stałymi literami tabel rozjechałby się z pierwszym konkursem, który jedną z nich wyłączy.

**Procent progu pochodzi z konkursu, nie z formularza.** Limit dostał `percentFrom` obok `percent`, zawsze dokładnie jedno z nich, a `percentFrom` może wskazać wyłącznie jeden z dwóch progów procentowych konkursu. Próg wpisany liczbą do formularza to liczba, która za rok będzie nieprawdziwa, a próg tabeli B różni się między wzorami na 2026. Pusty próg oznacza, że kategoria nie ma w tym konkursie ograniczenia, więc limit nie jest sprawdzany. Nie jest to granica równa zeru, która odrzucałaby każdą złotówkę.

**`sum` przyjmuje kilka składników.** Dotacja (D11) to koszty z trzech tabel minus wkład własny. Z `sum` ograniczonym do jednej kolumny formularz nie umiał tego zapisać, bo `difference` tylko odejmuje. Zmiana jest zgodna wstecz: istniejące definicje z jednym składnikiem znaczą to samo. Kolumna tabeli dalej nie sumuje pola spoza tabeli, bo w wierszu nie ma to sensu.

**Pozycja przekroczenia to wiersz, w którym suma bieżąca pierwszy raz przechodzi granicę.** Komunikat na sumie tabeli nazywa tabelę, a drugi komunikat stoi pod kluczem komórki wartości w tym wierszu. Wskazanie wszystkich wierszy albo ostatniego nie mówi wnioskodawcy nic, czego nie widzi sam, natomiast "od tej pozycji" pokazuje, gdzie zacząć ciąć. Dotacja nie ma jednej pozycji, bo zależy od całego budżetu, więc jej komunikat stoi na niej samej.

**Świadomie pominięte w tej karcie.** Przełącznik podstawy procentu z konkursu (kwota dotacji albo całkowita wartość projektu, `PercentageBasis`): dziś podstawę wskazuje `basis` limitu, a oba wzory na 2026 liczą od dotacji. Walidator nie czyta jeszcze tego ustawienia, więc konkurs przełączony na wartość projektu wymaga formularza z odpowiednim `basis`. Stały komunikat progu przychodu ("przekroczono limit zgodny z Regulaminem. Podmiot nieuprawniony"): dziś działa zwykły limit kwotowy z komunikatem D12. Minimalna kwota dotacji: nie ma rodzaju limitu "co najmniej". Żadnej z tych trzech rzeczy nie ma na liście kryteriów karty.

### Ekran logowania: cel przekierowania zawsze z backendu (T-12.7)

**Front nie filtruje `returnUrl` i nie buduje celu przekierowania.** Wartość z adresu idzie do `POST /login` bez zmian, a ekran przechodzi wyłącznie na `redirectPath` z odpowiedzi. O tym, czy cel jest bezpieczny, decyduje `Services/LoginLandingPath.cs`. Druga kopia tej reguły we froncie mogłaby się tylko z nią rozjechać, a front składający cel sam to prosta droga do otwartego przekierowania.

**Komunikat zależy od statusu, nie od treści błędu.** 401, 403 i 429 pokazują zdanie, które backend pisze świadomie dla użytkownika (blokada podaje minuty, limit z adresu to inne zdanie na tym samym statusie). 400 dostaje komunikat złych danych, żeby źle zbudowany adres nie był osobną, rozróżnialną odpowiedzią. 5xx i brak odpowiedzi to zawsze "spróbuj za chwilę": padnięty backend nie może wyglądać jak złe hasło. Hasło znika z pola tylko po odmowie serwera, bo po zerwanym połączeniu ponowienie potrzebuje właśnie tego samego hasła.

**Jedna rama dla stron bez sesji.** `components/public-frame.tsx` wydzielony z ramy stron konkursu, bo logowanie jest miejscem, w które prowadzi "Wypełnij wniosek", i nie powinno wyglądać jak wyjście z serwisu.

### Ekrany konta: droga powrotna przez skrzynkę pocztową (T-12.8)

**`returnUrl` jedzie w mailu weryfikacyjnym, a nie w przeglądarce.** Raport (krok 3.1) chce, żeby wnioskodawca po drodze przez rejestrację wrócił na konkurs, z którego przyszedł. Między rejestracją a logowaniem jest kliknięcie w mail, często w innym oknie albo na innym urządzeniu, więc `localStorage` czy `sessionStorage` gubiłyby cel właśnie wtedy, kiedy jest potrzebny. Dlatego `RegisterRequest` i `ResendVerificationRequest` mają opcjonalne `ReturnUrl`, a `EmailVerificationService` dopisuje go do linku. To nie jest pole z R-19: wnioskodawca go nie wpisuje, to strona, z której przyszedł.

**W mailu tylko to, co przeszłoby przy logowaniu.** Przed dopisaniem do linku wartość przechodzi `LoginLandingPath.SafeOrNull`, tę samą regułę, której używa `Resolve`. Odrzucona jest pomijana bez błędu: konto powstaje, mail wychodzi, tylko bez drogi powrotnej, a odpowiedź `/register` pozostaje ta sama. Mail z adresu tego produktu nie może nieść cudzego adresu, nawet jeśli logowanie odrzuciłoby go ponownie. Z tego samego powodu ścieżka dłuższa niż 512 znaków jest odrzucana: strona konkursu jest krótka, a długi query string to cudzy tekst w naszym mailu, bo zarejestrować da się dowolny adres. Front i tak nigdy nie przechodzi na `returnUrl` z maila, tylko wkłada go do linku do `/login`.

**Ekrany nie mówią, czy konto istnieje.** Po rejestracji, prośbie o reset i ponownej wysyłce linku widać jeden ekran dla każdego adresu, bez powtórzenia adresu, bo tak odpowiada backend (reguła 3 z `AGENTS.md`).

**Zawsze jest droga do nowego linku.** Mail potwierdzający może nie dotrzeć, a wtedy strona z linku jest nieosiągalna. `/verify-email` otwarte bez `userId` i `token` to więc formularz ponownej wysyłki, a linkują do niego ekran po rejestracji i ekran logowania po 403.

**Potwierdzenie adresu idzie raz na stronę.** `/verify-email` wysyła POST przy wejściu, ze strażnikiem w `useRef`. Drugi POST tym samym tokenem daje 400 ("już potwierdzone"), więc podwójny efekt Reacta w trybie deweloperskim zamieniłby sukces w błąd. Ekran błędu mimo to prowadzi do logowania, bo jedną z przyczyn tego 400 jest adres już potwierdzony.

**Dwa rodzaje 400 przy resecie hasła.** Rozróżnia je backend: `fieldErrors.newPassword` oznacza hasło odrzucone przez politykę i formularz zostaje, 400 bez pól oznacza martwy link i formularz znika na rzecz drogi do nowego linku, bo żadne wpisane hasło by tu nie pomogło.

### Załączniki: format decyduje bajt, nie deklaracja, a podmiana nigdy nie kasuje (T-32)

**`Attachment.EntityId` jest kopią `Application.EntityId`, nie odczytem przez nawigację.** `Authorization/IEntityScoped.cs` zapowiadał to wprost: każdy kolejny zasób wnioskodawcy dołącza przez zaimplementowanie tego interfejsu, nic więcej. Bez kopii pobranie załącznika musiałoby dociągać wniosek tylko po to, żeby zapytać o właściciela, czyli join na najgorętszej ścieżce (pobranie pliku) w zamian za kolumnę, która i tak nigdy się nie zmienia: wniosek, do którego należy załącznik, jest ustalony raz, przy uploadzie.

**Format pliku ustala `AttachmentFormatDetector` z sygnatury bajtów i rozszerzenia nazwy, nigdy z deklarowanego `Content-Type`.** Klient kontroluje nagłówek dowolnie, więc oparcie białej listy na nim byłoby proszeniem się o obejście: plik `wirus.exe` zadeklarowany jako `application/pdf` musi paść tak samo jak zadeklarowany szczerze. Cztery sygnatury bajtowe pokrywają osiem formatów, bo dwie rodziny (ZIP: docx/xlsx/odt/ods, OLE: doc/xls) dzielą jedną sygnaturę; rozstrzyga rozszerzenie, a niezgodność jednego z drugim jest odrzuceniem, nie zgadywaniem. Głębsza weryfikacja (rozpakowanie ZIP-a i odczyt wewnętrznego typu treści) rozstrzygnęłaby dokładniej kontrafabrykowany plik, ale nie chroni przed niczym, co T-32 ma chronić: dostępem do cudzych danych. Odnotowane jako świadomie węższy zakres, nie przeoczenie.

**Pobranie odpowiada `Content-Type` z wykrytego formatu, nie z kolumny `content_type`.** Odwrotnie, przeglądarka mogłaby dostać instrukcję wyrenderowania czegoś inline jako coś innego, niż jest naprawdę: dokładnie ten sam problem co ufanie deklaracji przy uploadzie, tyle że po drugiej stronie operacji. `Content-Disposition: attachment` na każdym pobraniu z tego samego powodu, niezależnie od typu.

**Rozmiar czytany w kawałkach do limitu, a nie ufanie `Content-Length`.** `AttachmentService.StageAsync` przerywa czytanie strumienia w momencie przekroczenia limitu pliku, więc sfałszowany nagłówek długości nie kupuje więcej niż jeden dodatkowy kawałek pamięci. Limity (rozmiar pliku, rozmiar całego wniosku) są ustawieniami konkursu z `T-20a` (`Competition.MaxAttachmentSizeInBytes`/`MaxApplicationSizeInBytes`), nieużywanymi aż do tej karty.

**Podmiana wstawia nowy wiersz i dezaktywuje poprzedni, nigdy nie nadpisuje ani nie kasuje pliku.** Ten sam wzorzec soft delete co reszta modelu (reguła 5 z `AGENTS.md`, `docs/model-danych.md` reguła 1): stary wiersz zostaje czytelny pod swoim identyfikatorem, z własnymi bajtami na dysku, dokładnie jak wersja robocza wniosku zostaje czytelna po dezaktywacji (T-29). Front dostaje nowy identyfikator w odpowiedzi i to na nim ma polegać dalej; stary trwa wyłącznie jako historia.

**Upload i podmiana pytają `CompetitionIntake`, status wniosku i `IsActive`, ten sam potrójny strażnik co `ApplicationService`.** `CompetitionIntake.cs` nazwał wprost upload załącznika jako trzecie miejsce, w którym ta sama reguła nie może być napisana osobno (R-29 w `rozbieznosci.md`): inaczej odcięcie terminu przy zapisie odpowiedzi i przy załącznikach rozjechałoby się przy pierwszej zmianie jednego z nich.

**Przechowywanie na dysku lokalnym, nie w bazie ani u zewnętrznego dostawcy.** Najprostsza rzecz, która spełnia dzisiejsze kryteria (ścieżka nieodgadywalna, pobranie za kontrolą uprawnień), bez rozstrzygania architektury przechowywania na produkcji, czego karta nie prosi. Wolumen Dockera (`attachments-data`) przeżywa przebudowę kontenera; przeżycie prawdziwego wdrożenia jest pytaniem dla `T-48`.

### Złożenie oferty: numer pod blokadą doradczą, historia jako osobna tabela dopisywana, a nie nadpisywana (T-33)

**Numer wniosku przydziela blokada doradcza (`pg_advisory_xact_lock`) na konkurs, nie ponowienie po `23505`.** `CompetitionService` i `AccountService` łapią wyścig o unikalny numer właśnie tak: SELECT sprawdzający zajętość przegrywa wyścig, UNIQUE odpowiada, wołający dostaje 409 i wpisuje inny numer. Ten wzorzec nie przenosi się na numer wniosku, bo tam numer wpisuje człowiek i potrafi wpisać drugi, a tutaj numer nadaje system i wnioskodawca go nie widzi, dopóki nie istnieje: 409 kazałoby poprawić coś, czego nigdy nie dotknął. Kryterium karty wprost wymaga, żeby DWIE jednoczesne próby złożenia zakończyły się DWOMA różnymi numerami, nie jedną porażką do powtórzenia. Blokada `pg_advisory_xact_lock(hashtext(competition_id::text)::bigint)`, trzymana przez cały czas trwania transakcji `ApplicationNumberAssigner.AssignAsync` (`Services/ApplicationNumberAssigner.cs`), daje to wprost: drugi wołający liczy kolejny numer dopiero po zatwierdzeniu transakcji pierwszego, więc nigdy nie zobaczy wartości, którą ktoś właśnie zajął. Blokada zwalnia się sama przy commit albo rollback (wariant `XACT`, nie sesyjny), więc awaria w trakcie żądania nie zawiesza numeracji konkursu dla następnego wnioskodawcy. Klucz to hash identyfikatora konkursu, nie sam UUID: `pg_advisory_xact_lock` bierze `bigint`, a przypadkowe współdzielenie blokady przez dwa różne konkursy kosztuje tylko chwilę zbędnego czekania, nigdy błędny numer, bo sam numer i tak liczy się osobno dla każdego konkursu. Numer to `MAX(number::integer) + 1` w obrębie konkursu liczony po stronie SQL, nie w pamięci (żeby nie wydłużać czasu trzymania blokady), zerowany z lewej do trzech cyfr (`"001"`, zgodnie z `scripts/seed.py`), rosnący naturalnie ponad trzy cyfry.

**Blokada sama w sobie nie wystarcza: trzeba jeszcze odczytać stan NA NOWO, wewnątrz niej.** Dwa złożenia TEGO SAMEGO wniosku (podwójny klik, powtórzone żądanie) też współdzielą blokadę tego samego konkursu, więc się serializują, ale każde z nich wczytało swoją własną kopię wiersza PRZED czekaniem na blokadę. Bez odczytu na nowo drugie żądanie po prostu nadpisałoby numer i status pierwszego, dopisując drugi wiersz "Draft do Submitted" do tabeli, która ma być rejestrem zdarzeń, nie kolejką nadpisań. `ApplicationNumberAssigner.AssignAsync` po wzięciu blokady odpytuje status i `IsActive` wprost z bazy i przerywa (`RollbackAsync`, bez zapisu), jeśli któryś już się zmienił, oddając wołającemu świeżą informację zamiast powtarzać nieaktualne sprawdzenia sprzed blokady. Ten sam mechanizm zawęża też wyścig złożenia z dezaktywacją własnego szkicu, choć nie zamyka go w każdym możliwym uszeregowaniu: `ApplicationService.DeactivateAsync` (T-29) nie bierze żadnej blokady, więc mikroskopijne okno między odczytem wewnątrz blokady a `SaveChangesAsync` tej samej transakcji teoretycznie zostaje. Domknięcie tego do zera oznaczałoby blokadę wierszową (`SELECT ... FOR UPDATE`) także w `DeactivateAsync`, czyli zmianę w pliku T-29, a to wykracza poza tę kartę; odnotowane jako znana, wąska luka, nie przeoczenie.

**Historia statusów to osobna tabela, `application_status_history`, dopisywana, nigdy aktualizowana.** Wiersz niesie kto (`changed_by_user_id`), kiedy (`changed_at`) i przejście (`from_status` do `to_status`), z check constraintem odrzucającym przejście, które niczego nie zmienia. Kolumna `applications.status` zostaje jedynym bieżącym stanem; ta tabela istnieje właśnie po to, żeby bieżący stan nie był jedynym miejscem, w którym historia w ogóle siedziała. `FromStatus`/`ToStatus` używają tego samego enuma co `Application.Status`, celowo nie zawężone do pary Draft/Submitted: `R-03` (zwrot do poprawy) dopisze kiedyś przejście Submitted do Draft do tej samej tabeli, a nie do drugiej wymyślonej na poczekaniu. Ta karta tego przejścia nie buduje, tylko zostawia miejsce.

**Zamrożenie odpowiedzi nie jest osobnym mechanizmem.** `PUT /applications/{id}` z T-29 już odrzuca zapis do wniosku, który nie jest Draft (`ApplicationOutcome.AlreadySubmitted`). W chwili, gdy złożenie przestawia status na Submitted, ta sama, już istniejąca i przetestowana reguła zamraża wszystko bez dopisywania drugiej blokady.

**Drugi otwarty punkt (rozjazd nawigacji EF kontra konkurs) sprawdzany jest obronnie, nie jako nowa reguła biznesowa.** Złożony klucz obcy (`ApplicationConfiguration`) nie dopuszcza w bazie pary `competition_id`/`form_definition_id`, która by się rozjechała, ale nawigacje EF potrafią po cichu wyrównać `CompetitionId` do konkursu definicji zamiast odrzucić niezgodność (opisane przy `Models/Application.cs`). Dzisiejsze ścieżki zapisu nie potrafią wyprodukować takiego wiersza (para jest ustawiana raz, przy `CreateDraftAsync`, i nic później jej nie rusza), więc `ApplicationSubmissionService.SubmitAsync` sprawdza tę parę i rzuca wyjątkiem, gdy się nie zgadza, tym samym wzorcem co `FormDocumentFor`: to sygnalizacja "wiersz zmienił się spoza własnych ścieżek zapisu tego produktu", a nie przypadek, który wywołujący potrafi wywołać.

**Kompletność wymaganych załączników NIE jest sprawdzana przy złożeniu, i to jest świadomie zostawiona otwarta kwestia, nie przeoczenie.** `Attachment` (T-32) nie niesie żadnego wskazania, KTÓRY wymóg z `competition_attachments` plik zaspokaja, a `Entity` nie ma pola rejestru, więc `AttachmentRequirement.RequiredOutsideKrs` nie da się w ogóle wyliczyć. Zliczenie samych plików bez dopasowania do konkretnego wymogu byłoby zgadywaniem, którego `AGENTS.md` wprost zabrania ("nie zgaduj w modelu danych"). Blokada listy wymaganych plików w kroku 3.7 kreatora jest zadaniem frontu (T-34, `docs/runbook/M4-wnioski.md`), a powiązanie pliku z wymogiem po stronie backendu czeka na osobną decyzję modelu danych.

**PDF potwierdzenia jest napisany ręcznie, bajt po bajcie, bez żadnej biblioteki.** `Ocwip.Api.csproj` nie ma dotąd żadnej zależności do PDF, a treść to garść linii na jednej stronie: dokładnie tyle, ile `Services/Pdf/SimplePdfDocument.cs` umie, bez silnika layoutu i bez osadzania fontu. Fonty Base14 (Helvetica) rozumieją wyłącznie `WinAnsiEncoding`, w którym nie ma polskich znaków diakrytycznych; osadzenie prawdziwego fontu dla jednej strony tekstu byłoby nieproporcjonalnym nakładem, więc `ApplicationConfirmationPdfBuilder` transliteruje treść na ASCII przed przekazaniem jej do writer'a. To jest świadome zawężenie WYŁĄCZNIE tego jednego dokumentu: reszta produktu (UI, dokumentacja, e-mail) zostaje po polsku, zgodnie z `AGENTS.md`.

### Lista wniosków operatora: kolumny z ról pól, sortowanie w przeglądarce, eksport bez nowej zależności (T-35)

**Tytuł projektu, całkowity koszt i wnioskowaną kwotę lista czyta z pól oznaczonych rolą (`role` w kontrakcie formularza), nie z umówionych kluczy ani z nowych kolumn.** Klucze pól wybiera operator w kreatorze, więc nic poza znacznikiem w definicji nie mówi, które pole trzyma tytuł. Umówiony klucz (`tytul_projektu`) po literówce zostawiłby kolumnę pustą bez żadnego sygnału, a nieznana rola jest odrzucana przy publikacji. Kolumny w `applications` wymagałyby zapisu tej samej wartości dwa razy (w odpowiedziach i obok), a D15 już raz odrzuciło taki podwójny fakt. Wartości liczy `ApplicationRoleValues` tym samym `AnswerCalculator` co walidacja, na wersji formularza, na której wniosek wypełniono, więc dotacja wyliczana z D11 wychodzi na liście tak samo jak w formularzu. Rola jest opcjonalna i `schemaVersion` się nie zmienia: definicja bez ról daje puste komórki, nie błąd. Wybrano z użytkownikiem 2026-09-25.

**Na liście jest wszystko, co nie jest szkicem, a nie tylko `Submitted`.** Stany z oceny (M5) i zwrot do poprawy (`R-03`) dojdą do tego samego enuma, a wniosek oceniony nadal jest wnioskiem, który wpłynął. Wiersz wycofany przez wnioskodawcę (`is_active = false`) na liście nie stoi. Szczegół oferty pod `/competitions/{id}/applications/{applicationId}` odpowiada 404 zarówno dla szkicu, jak i dla wniosku innego konkursu, bez rozróżniania, żeby adres nie pokazywał operatorowi, co wnioskodawca dopiero pisze.

**Wszystkie trasy listy mają politykę roli operatora, a nie polityki zasobu.** `resource.owner` odpowiada dla jednego wiersza naraz, a lista z definicji pokazuje wnioski wielu podmiotów. Odmowa dla wnioskodawcy i recenzenta pada, zanim cokolwiek zostanie odczytane.

**Sortowanie i filtrowanie dzieje się w przeglądarce.** Konkurs przynosi około 120 ofert, cała lista to jedna odpowiedź, a zapytanie przy każdym kliknięciu w nagłówek byłoby najwolniejszym sposobem na przestawienie tabeli, która już jest na ekranie. Przy rzędzie wielkości tysięcy wniosków trzeba będzie to przenieść na serwer. Liczba porządkowa liczy wiersze tak, jak są pokazane, a suma pod filtrem dotyczy widocznych wierszy i tak jest podpisana. Eksport zawiera zawsze całą listę w kolejności numerów, niezależnie od widoku, bo lista przekazana komisji nie powinna zależeć od tego, gdzie operator ostatnio kliknął.

**Arkusz to CSV, PDF pisany ręcznie, bez nowej zależności.** Decyzja z użytkownikiem z 2026-09-25. CSV ma BOM, średnik i CRLF, bo tak polski Excel otwiera plik bez okna importu, a kwotę z przecinkiem czyta jako liczbę. Komórka zaczynająca się od `=`, `+`, `-` albo `@` dostaje apostrof (tytuł wpisany przez wnioskodawcę nie może wykonać się w arkuszu operatora), a zwykła liczba nie, żeby przekroczona pula została liczbą ujemną. PDF używa `SimplePdfDocument` z T-33, rozszerzonego o wiele stron i układ poziomy z Courierem, w którym kolumny dopełnione spacjami trzymają się w pionie. Nagłówek powtarza się na każdej stronie. Transliteracja polskich znaków obejmuje teraz oba PDF-y (`PdfText`), a tekst dłuższy niż kolumna jest ucinany: pełna treść jest na ekranie i w arkuszu.

**Kolumny z raportu, których lista jeszcze nie ma:** wynik oceny formalnej (moduł oceny, M5) i data wpływu koperty przy konkursie z wymogiem papieru (schemat nie ma na nią miejsca). Obie wymagają danych, których dziś nie ma.

### Ścieżka wnioskodawcy: trzy wąskie odczyty zamiast operatorskiego kontraktu, front jako jeden warsztat stanu (T-34)

**Karta była oznaczona jako "Frontend", ale bez wnioskodawcy nie dało się przeczytać ani dokumentu formularza, ani listy własnych wniosków, ani listy własnych załączników: żaden z tych odczytów nie istniał.** `GET /competitions/{id}/form-definitions` i cała reszta `FormDefinitionEndpoints` niosą politykę operatora, `GET /competitions/{id}/applications` (T-35) też, a `GET /attachments/{id}` czyta jeden załącznik ze znanym identyfikatorem, nie listę. Zgadywanie zakresu tych trzech odczytów byłoby dokładnie tym, czego `AGENTS.md` zabrania w modelu danych, więc każdy z nich jest osobnym, wąskim dodatkiem, tym samym wzorem `AuthorizeAsync` (wczytaj zasób, zapytaj `IAuthorizationService`, dopiero potem działaj) co reszta `ApplicationEndpoints.cs`:

- `GET /applications` (`ApplicationOverviewService`) - własne wnioski wywołującego przez wszystkie konkursy naraz, szkic i złożony razem, filtrowane po `EntityId` z konta, nigdy po identyfikatorze z trasy. Polityka roli, nie zasobu: nie ma tu jednego zasobu do wczytania przed autoryzacją, zakres sam w sobie jest regułą.
- `GET /applications/{id}/attachments` (`IAttachmentService.ListAsync`) - aktywne załączniki jednego wniosku, autoryzacja przez zasób WNIOSKU jak `POST` uploadu, z tego samego powodu: pojedynczy załącznik nie jest tu adresowany.
- `GET /applications/{id}/form-definition` (`IApplicationService.GetFormDefinitionAsync`) - dokument formularza przy wersji przypiętej do wniosku, nigdy przy najnowszej konkursu, ten sam wymóg co walidacja T-30. Osobny kontrakt (`ApplicationFormResponse`) od `ApplicationResponse`, bo ten drugi wraca z każdego autozapisu: powtarzanie całego dokumentu formularza przy każdym zapisanym polu byłoby czystym marnotrawstwem, dokument czytany jest raz, kiedy ekran wypełniania się otwiera.

Żaden z trzech nie potrzebował nowej kolumny ani migracji: dane już tam były, brakowało tylko trasy. `CompetitionLimitSettings` do walidacji limitów budżetu front buduje sam, z `PublicCompetitionResponse`, które wnioskodawca i tak już ma w ręku (`limitSettingsFrom`) - operatorski `fetchCompetitionLimitSettings` (T-27) zostaje dla kreatora, drugie zapytanie do trasy z polityką operatora byłoby tu niepotrzebne i niedostępne.

**Kompletność załączników nadal nie jest sprawdzana, teraz też po stronie frontu, świadomie.** T-33 już to zostawiła otwarte (`Attachment` nie niesie identyfikatora wymogu, `R-33`). Ekran T-34 pokazuje dwie osobne listy, czego wymaga konkurs i co już przesłano, zamiast kafelka na wymóg z podpiętym uploadem: sparowanie jednej pozycji z drugą byłoby udawaniem powiązania, którego backend nie ma i nie sprawdza, a front udający precyzję, której nie ma, myli bardziej niż jej brak. Lista braków przed złożeniem (`submissionGaps`) z tego samego powodu liczy wyłącznie pola formularza, nigdy załączniki: gdyby liczyła, przycisk "Złóż wniosek" kłamałby dokładniej niż sam backend, który złożenie mimo braków i tak przyjmuje.

**`FormRenderer` (T-28) dostał opcjonalną, sterowalną z zewnątrz bieżącą sekcję (`activeSectionKey`/`onActiveSectionChange`), zamiast kopii komponentu dla wnioskodawcy.** Lista braków musi umieć skoczyć do konkretnej sekcji z zewnątrz, a renderer trzymał to dotąd wyłącznie we własnym stanie. Bez kontrolowanego trybu obie karty (T-27 i T-34) już dziś czytają ten sam komponent bez zmian - druga kopia rendererów rozjeżdżałaby się przy pierwszej poprawce jednego z nich. Pole `id` wokół każdego pola (`field-anchor.ts`, `field-${key}`) jest osobne od `id` samego kontrolki: skok ustawia fokus na pierwszy sterowalny element WEWNĄTRZ tego opakowania (`querySelector`), bo samo opakowanie nie jest fokusowalne, a to jest droga, którą klawiatura ma faktycznie dotrzeć do pola, nie tylko przewinąć ekran do niego.

**Autozapis czeka sekundę ciszy w pisaniu, nie zapisuje na każdy znak i nie odkłada się na pięć minut.** Karta T-29 wprost zabrania odstępu pięciu minut, ale "po każdym wypełnionym polu" czytane dosłownie jako każdy znak zamieniłoby wpisywanie tytułu projektu w seriê żądań PUT, z których każde kolejne unieważnia poprzednie zanim zdąży wrócić. Debounce 1000 ms po ostatniej zmianie jest kompromisem bez własnej karty: żadne kryterium akceptacji nie podaje liczby, a sekunda jest krótsza niż czas, w którym ktokolwiek zdąży zamknąć kartę przeglądarki.

**Złożenie dogania autozapis, zamiast zatwierdzać to, co serwer akurat ma.** Potwierdzenie w oknie dialogowym mogło paść w środku sekundy ciszy albo w trakcie samego żądania PUT: `handleConfirmedSubmit` w obu przypadkach czeka (`flushPendingSave`), zanim wywoła `submitApplication`, bo backendowy endpoint złożenia czyta odpowiedzi już zapisane na wierszu, nie dostaje ich w treści żądania. Bez tego czekania podsumowanie na ekranie mogłoby pokazywać nowszą treść niż ta, którą serwer właśnie zatwierdza. Każdy autozapis dostaje też swój numer kolejny (`saveSeq`): odpowiedź, która wróci później niż odpowiedź nowszego, już zastosowanego zapisu, jest odrzucana, więc wolniejsze żądanie z gorszej sieci nie potrafi cofnąć banera "Zapisano o" ani sumy kontrolnej na coś starszego.

**Złożenie idzie przez dwa ekrany tego samego komponentu, nie przez osobną trasę.** "Wypełnianie" i "podsumowanie" (proces.md kroki 3.2 i 3.7) to jeden stan (`Stage`) wewnątrz `DraftWorkspace`, nie dwie strony: ten sam przycisk "Złóż wniosek" w pierwszym etapie przełącza na podsumowanie (dopiero gdy lista braków jest pusta), a w drugim otwiera jedyne okno potwierdzenia w całej ścieżce. Osobna trasa dla podsumowania wymagałaby przenoszenia niezapisanego stanu formularza przez nawigację Next.js, czego `FormRenderer` (kontrolowany z zewnątrz odpowiedziami w stanie rodzica) i tak nie potrzebuje.

**Okno potwierdzenia to natywny `<dialog>` z `showModal()`, nie własny overlay.** Przechwytuje fokus i Escape samo, bez pisania pułapki fokusu ręcznie, co „cała ścieżka przechodzi się klawiaturą” (T-34) dostaje za darmo. Wywołanie `showModal` jest warunkowe (`?.`), bo jsdom (środowisko testów) nie ma tej metody wcale; w prawdziwej przeglądarce koszt tego warunku jest zerowy.

**Publiczny przycisk "Wypełnij wniosek" (`apply-link.tsx`, T-23) pyta `GET /me` raz przy montowaniu.** Strona konkursu jest anonimowa z założenia (D6), więc większość odwiedzin nie ma z kim rozmawiać, ale wnioskodawca, który już jest zalogowany i wraca na stronę konkursu (na przykład po `returnUrl` z logowania), musi dostać prawdziwy przycisk zakładający wniosek, nie kolejne skierowanie na `/login`, które nie prowadzi już donikąd. Każdy inny stan (anonim, inna rola, sesja jeszcze niesprawdzona) dostaje dokładnie dotychczasowy odnośnik logowania - awaria `GET /me` też, bo backendowa czkawka nie jest tym samym co brak sesji.

### Ocena wniosku: karta jako formularz, wynik liczony przy odczycie, dostęp w osobnym handlerze (T-38)

**Karta oceny to wersja `form_definitions` z innym przeznaczeniem, nie osobny mechanizm.** Decyzja D16 i propozycja T-38.0, przejrzana 2026-09-25. Jeden kontrakt, jeden walidator (`AnswerValidator` na dwóch poziomach: szkic przy zapisie, całość przy "zakończ etap"), jeden kalkulator i to samo wersjonowanie z T-25. Odrzucone osobne tabele kryteriów, bo karta 2026 potrzebuje tabeli kwestionowanych pozycji, kwoty i pola wyliczanego, czyli wszystkiego, co formularz już ma. Przeznaczenie stoi w kolumnie, a nie w dokumencie, bo o tym, czym jest dokument, decyduje trasa publikacji, a ta sama struktura JSON może być czymkolwiek.

**Kryteria formalne to pola, nie wiersze tabeli** (zmiana względem propozycji). Pole z rolą `formalCriterion` i `appliesTo` wystarcza do wszystkiego, czego chce karta 2026, a warunek widoczności na wierszu tabeli byłby nowym pojęciem kontraktu tylko dla tej jednej karty.

**`appliesTo` i punkty wolno dziś tylko na karcie.** Renderer wnioskodawcy ich nie zna, więc formularz wniosku z nimi pokazałby pytanie, które serwer potraktuje jak niezadane. Ograniczenie zniknie, gdy T-40 nauczy silnik frontu tych samych reguł.

**Wynik karty nie jest zapisywany.** Wynik formalny, suma merytoryczna i strategiczna oraz kwota rekomendowana liczy `EvaluationScores` z odpowiedzi przy każdym odczycie. Zapisana suma byłaby drugim faktem obok odpowiedzi (D15), a ranking (T-39) i tak musi czytać odpowiedzi, żeby pokazać, skąd wynik.

**Dostęp do oceny ma własne wymaganie i handler, nie wymaganie zasobu podmiotu.** Ocena nie należy do żadnego podmiotu, którego właścicielem mógłby być wnioskodawca, a wnioskodawca nie widzi jej wcale, dopóki operator nie udostępni kart (krok 5.5, osobna karta). Tabela ról w `EvaluationAccessHandler`: operator czyta każdą ocenę, ale pisze tylko formalną (punkty merytoryczne należą do ekspertów); recenzent czyta i pisze wyłącznie własną kartę merytoryczną i tylko póki jest przypisany do wniosku. Dwie oceny tego samego wniosku są niezależne (regulamin 2026), więc ekspert nie widzi karty drugiego eksperta. Zakładanie oceny idzie dwiema trasami ze stałymi politykami ról, a przy karcie merytorycznej dodatkowo przez politykę zasobu z T-37, czyli przez przypisanie.

**Karta ma jednego autora i osobno osobę wprowadzającą** (decyzja 14 raportu), nawet jeśli ścieżka "operator wprowadza kartę komisji obradującej na papierze" nie ma w MVP ekranu. Kolumna teraz nic nie kosztuje, dołożona po wdrożeniu kosztowałaby migrację danych.

**Migracja nie daje się cofnąć, gdy są już oceny albo karty.** `Down()` odmawia wprost zamiast kasować oceny (reguła 5) albo wywracać się na starym indeksie wersji, który nie zna przeznaczenia.

### Lista rankingowa: liczona przy odczycie, ustawienia oceny osobną trasą (T-39)

**Lista nie jest zapisywana.** Miejsce, sumy i ostrzeżenia liczy `RankingCalculator` z zakończonych kart przy każdym odczycie. Zapisany ranking byłby nieaktualny w chwili, gdy ekspert kończy kolejną kartę, a około 150 wniosków razy dwie karty to odczyt, który nie potrzebuje pamięci podręcznej. Reguła jest osobnym, czystym kodem bez bazy, żeby każda klauzula regulaminu miała własny test.

**Miejsce dostaje tylko wniosek z pozytywną oceną formalną i kompletem zakończonych kart merytorycznych.** Reszta stoi na końcu listy, po numerze, z postępem ("1 z 2 kart"), bo operator potrzebuje widzieć, na co czeka, a nie tylko to, co już jest. Remis rozstrzyga wcześniejsze złożenie (regulamin 2026) i to jest reguła w kodzie, nie ustawienie.

**Ustawienia oceny mają własną trasę, a nie pola w `CompetitionRequest`.** Kreator ogłoszenia (T-22) wysyła przy każdym zapisie cały konkurs, więc nowe pola w tym żądaniu cofałyby ustawienia oceny do domyślnych przy każdej zmianie tytułu.

**Rozbieżność tylko ostrzega.** System nie dobiera trzeciego eksperta ani nie uśrednia za operatora (decyzja 12 raportu). Skala i sposób liczenia kwoty rekomendowanej to założenia robocze ZR-01 i ZR-02.

### Panel recenzenta: ten sam renderer, reguła dostępu z T-37 także dla załączników (T-40)

**Karta oceny rysuje się tym samym `FormRenderer` co wniosek**, z nowym opcjonalnym `applicant`, który ukrywa kryterium niezadane rodzajowi wnioskodawcy (`appliesTo`). Silnik frontu liczy punkty za "tak" tak samo jak `AnswerCalculator` na serwerze. Odczyt wniosku i jego formularza idzie istniejącymi trasami wnioskodawcy (`/applications/{id}`, `/form-definition`, `/attachments`), bo polityka zasobu z T-37 wpuszcza przypisanego eksperta; nie powstał osobny, drugi kontrakt odczytu dla recenzenta.

**Załącznik idzie za swoim wnioskiem.** Reguła recenzenta w `EntityScopedHandler` znała tylko `Application`, więc lista załączników przechodziła, a pobranie pliku nie. Od T-40 załącznik przechodzi, gdy ekspert jest przypisany do jego wniosku, tym samym wierszem `application_assignments`.

**Lista eksperta czyta tylko jego przypisania i tylko jego karty**, więc suma rekomendacji nad listą to jego suma, nie komisji.

**Bez bramy deklaracji bezstronności** (ZR-04, T-40a): raport stawia ją przed panelem, ale nie ma jeszcze modelu ani treści deklaracji.

### Deklaracja bezstronności jako brama w autoryzacji, nie w ekranie (T-40a)

**Brama stoi w `EntityScopedHandler` i `EvaluationAccessHandler`, nie w panelu.** Raport mówi, że bez deklaracji ekspert "nie widzi treści żadnego wniosku", a treść wychodzi trasami wniosku, załączników i ocen; brama tylko w ekranie zostawiłaby je otwarte dla każdego, kto zna adres. Ten sam warunek w jednym zapytaniu z przypisaniem, więc nie ma drugiego miejsca, które mogłoby się rozjechać.

**Lista eksperta przed akceptacją pokazuje tylko liczbę przypisanych wniosków**, bez numerów i tytułów: tytuł projektu to już treść wniosku.

**Decyzja raz i z kopią tekstu.** Odmowa wyklucza z oceny (raport), a tekst deklaracji może się zmieniać między edycjami, więc przy decyzji zostaje dokładnie to, co ekspert widział.

### Ocena w panelu operatora: jeden ekran z istniejących tras, przypisanie grupowe po stronie przeglądarki (T-41)

**Ekran składa się z tras, które już były, plus dwóch małych odczytów.** Ustawienia, ranking i deklaracje przyszły z T-39 i T-40a, przypisywanie z T-37. Doszły tylko `GET /reviewers` i `GET /competitions/{id}/assignments` (`ReviewerDirectoryEndpoints.cs`), obie wyłącznie dla operatora: lista ekspertów to dane osobowe, a przypisania mówią, kto ocenia czyj wniosek, czego ekspert o innych ekspertach wiedzieć nie powinien. Odrzucone: dołożenie przypisań do odpowiedzi rankingu, bo ranking czyta też T-42, a jemu nazwiska ekspertów do niczego.

**Przypisanie grupowe to seria zwykłych przypisań, nie nowa trasa.** Przy około 120 wnioskach i kilku ekspertach to najwyżej kilkadziesiąt żądań, a każde przechodzi tę samą walidację co pojedyncze (rola, aktywne konto, brak duplikatu). Wniosek, który ekspert już ma, jest pomijany przed wysłaniem, pierwsza odmowa zatrzymuje serię i jest pokazana, a to, co przeszło, zostaje. Trasa zbiorcza z transakcją warta jest budowy dopiero wtedy, gdy częściowe przypisanie okaże się problemem (ZR-07).

**Po każdej zmianie całość jest czytana od nowa.** Pięć lekkich odczytów zamiast łatania stanu w przeglądarce: liczniki ekspertów, stan deklaracji i postęp w rankingu zależą od przypisań, a drugi sposób liczenia tego samego po stronie frontu to rozjazd czekający na okazję.

### Karty wniosku dla operatora: lista przez serwis oceny, karta formalna dopiero na żądanie (T-41a)

**`GET /applications/{id}/evaluations` czyta każdą kartę przez `IEvaluationService.GetAsync`**, a nie własnym zapytaniem z wynikami. Wniosek ma kartę formalną i po jednej merytorycznej na eksperta, więc kilka odczytów więcej nic nie kosztuje, a karta na liście i ta sama karta otwarta osobno na pewno mają ten sam dokument i te same punkty. Trasa jest tylko dla operatora: ekspert nie czyta cudzych kart (niezależność ocen z T-38), a nazwisko oceniającego jest w osobnym polu obok karty, żeby udostępnienie wnioskodawcy (T-41b) mogło je pominąć bez przerabiania kontraktu karty.

**Karta formalna nie otwiera się przy wejściu na wniosek.** `POST .../evaluations/formal` zakłada kartę, a obejrzenie wniosku to jeszcze nie ocena; inaczej lista rankingowa pokazywałaby "w toku" przy każdym wniosku, który ktoś tylko przejrzał.

**`EvaluationWorkspace` przeniesiony do `components/evaluation/`**, bo używają go teraz dwa panele. Wynik karty w jednym zdaniu (`EvaluationSummary`) jest wspólny dla karty wypełnianej i oglądanej.

### Karty dla wnioskodawcy: anonimowość w kontrakcie, nie w ekranie (T-41b)

**Odpowiedź dla wnioskodawcy to osobny kontrakt (`ApplicantEvaluationCard`) bez identyfikatora oceny, autora i konta, które kartę wpisało**, a nie `EvaluationResponse` ukrywany w przeglądarce. Wszystko, co idzie do przeglądarki, wnioskodawca może przeczytać w narzędziach deweloperskich, więc "system wie, kto oceniał, ukrycie jest po stronie widoku" z raportu znaczy tu: po stronie odpowiedzi API. Test sprawdza surowy JSON, że nie ma w nim identyfikatora eksperta, identyfikatora karty ani nazwiska.

**Trasa wymaga roli wnioskodawcy i własności wniosku.** Sama polityka zasobu z T-13.2 wpuszcza też przypisanego eksperta z deklaracją, a karty innych ekspertów to dokładnie to, czego ekspert czytać nie może (T-38).

**Udostępnienie to jeden warunkowy UPDATE** (`evaluation_cards_shared_at IS NULL`), więc dwa równoczesne potwierdzenia nie zapiszą dwóch dat; drugie dostaje 409.

### Decyzja o dofinansowaniu: robocza w wierszu, wynik naraz, suma kontrolna nietknięta (T-42)

**Kwota i uwaga są kolumnami wniosku, a wynik jego statusem.** Raport chce kwoty edytowanej wprost na liście rankingowej i mówi, że wpisanie kwoty oznacza dofinansowanie, więc osobna encja "decyzji" byłaby drugą drogą do tego samego faktu. Zapis jest roboczy do zatwierdzenia wyników: żadna trasa wnioskodawcy nie zwraca `awarded_grant`, a status zmienia się dopiero przy zatwierdzeniu, dla wszystkich wniosków konkursu w jednej transakcji, żeby nikt nie poznał wyniku w kolejności kliknięć operatora.

**Zatwierdzenie odmawia, dopóki któraś ocena trwa.** Wniosek bez kompletu kart dostałby `Rejected` tylko dlatego, że zatwierdzono za wcześnie. Odmowa podaje, ile wniosków czeka.

**Decyzje i statusy piszemy przez `ExecuteUpdateAsync`, a nie `SaveChanges`.** `AppDbContext` przy `SaveChanges` przestawia `UpdatedAt`, a z `UpdatedAt` liczy się suma kontrolna złożonego wniosku (D15), wydrukowana na potwierdzeniu, które wnioskodawca już ma. Decyzja operatora nie zmienia treści wniosku, więc suma nie może się zmienić; test to sprawdza. Kiedy i kto zmienił status, zapisuje `application_status_history`. Odrzucone: przeliczenie sumy od `SubmittedAt` zamiast `UpdatedAt`, bo zmieniłoby sumy już wydrukowane.

**Pula jest sygnałem, nie blokadą** (karta): nad listą "przyznano X z Y, zostało Z", po przekroczeniu słowami i kolorem.

### Lista rankingowa na zewnątrz: jedne wiersze, trzy pliki, XLSX bez biblioteki (T-42a)

**`RankingExport` buduje wiersze raz, a CSV, XLSX i PDF tylko je zapisują**, więc trzy pliki nie mogą się rozjechać w kolumnie. Komórka niesie tekst i, gdy to kwota albo punkty, liczbę, żeby arkusz liczył.

**XLSX jest pisany ręcznie.** W T-35 wybrano CSV zamiast XLSX, żeby nie dokładać zależności; raport chce jednak XLSX do liczenia, a arkusz z tekstem i liczbami to ZIP pięciu małych części XML, które `System.IO.Compression` składa bez biblioteki. Tekst idzie jako inline string, którego arkusz nigdy nie liczy, więc tytuł wpisany jako formuła zostaje tekstem bez apostrofu, jakiego potrzebuje CSV. Test parsuje arkusz jako XML.

**Publiczne wyniki są anonimowe i dopiero po zatwierdzeniu**, a przed nim trasa odpowiada 404 tak samo jak dla braku konkursu, żeby odpowiedź nie zdradzała, że decyzje już zapadają. Na liście są tylko dofinansowani i lista rezerwowa (ZR-10).

### Maile o wyniku: kolejka w transakcji wyników, wysyłka do wznowienia (T-43)

**Lista należnych maili powstaje w tej samej transakcji co wyniki** (tabela `result_notifications`, jeden wiersz na wniosek). Wyniki bez maili albo maile bez wyników nie mogą istnieć, nawet gdy proces padnie w trakcie zatwierdzania.

**Wysyłka to przebieg po zaległych wierszach, który można puścić jeszcze raz.** Każdy wiersz jest zajmowany warunkowym UPDATE (niewysłany i niezajęty albo zajęty dawniej niż 10 minut), więc dwa przebiegi naraz nie wyślą tego samego maila. Po wysłaniu wiersz dostaje `sent_at`, po błędzie jest od razu zwalniany z opisem błędu, w którym jest tylko typ wyjątku, bo komunikat dostawcy potrafi zawierać adres albo treść. Odrzucone: wysyłka w tle przez usługę hostowaną. Przy 120 mailach przebieg na żądanie operatora trwa chwilę, a operator widzi wynik i sam decyduje o ponowieniu; usługa w tle potrzebowałaby własnego monitoringu, którego jeszcze nie ma (T-48).

**Adres czytany w chwili wysyłki z konta, które złożyło wniosek**, a nie kopiowany do kolejki: tabela nie trzyma danych osobowych, a mail idzie tam, gdzie poszło potwierdzenie złożenia.

**Kwota w panelu wnioskodawcy tylko przy statusie `Funded`.** `ApplicationResponse.AwardedGrant` jest wypełniane wyłącznie po zatwierdzeniu, więc decyzja robocza (T-42) nigdy nie wychodzi do wnioskodawcy.

### Poczta: SMTP z `System.Net.Mail`, wybór przy starcie, log bez treści poza Development (T-43a)

**Przekaźnik SMTP przez klienta z frameworka, bez biblioteki.** System wysyła wyłącznie zwykły tekst do jednego odbiorcy przez STARTTLS; to robi `System.Net.Mail.SmtpClient`. MailKit, który Microsoft poleca zamiast niego, daje rzeczy tu nieużywane (IMAP, OAuth, podpisy) za cenę zależności i jej aktualizacji. Wrócimy do tego, jeśli dostawca OCWIP będzie wymagał OAuth zamiast hasła.

**Nadawca wybierany raz, przy starcie.** Z `SMTP_HOST` idzie `SmtpEmailSender`, bez niego zastępczy log. Host bez `SMTP_FROM` zatrzymuje start: poczta skonfigurowana w połowie zawodziłaby przy każdym mailu, a zobaczyłby to dopiero wnioskodawca.

**Log zastępczy poza Development zapisuje tylko temat.** Treść maila niesie link logujący (weryfikacja, reset hasła) i dane osobowe, a log to złe miejsce na jedno i drugie (AGENTS.md, bezpieczeństwo, punkt 4). W Development zostaje cały mail, bo stąd programista bierze link weryfikacyjny.

**Błąd przekaźnika wraca do wywołującego.** Maile o wyniku (T-43) zapisują go jako nieudaną próbę do ponowienia; maile konta odpowiadają błędem, zamiast udawać, że link wyszedł.

### Wniosek na papierze: z zapisanej wersji, tylko pola drukowane, suma na każdej stronie (T-44)

**PDF składa się z tego samego dokumentu formularza, według którego wniosek wypełniono**, i z tych samych reguł co ekran: widoczność sekcji i pól, wiersze tabel i pola wyliczane liczy `AnswerCalculator`, więc papier nie może pokazać czegoś, czego ekran by nie pokazał. Pola z `printed: false` (D14) i pola ukryte warunkiem nie trafiają na papier nawet wtedy, gdy zostało w nich stare ukryte wejście.

**Numer i suma kontrolna są nagłówkiem każdej strony** (D15), więc luźna kartka z teczki wciąż mówi, do której wersji elektronicznej należy.

**Bez koloru i pogrubień.** Tekst Base14 ich nie niesie, więc sekcje są wyróżnione wielkimi literami i wcięciem; wydruk czarno-biały nic nie traci. Transliteracja polskich znaków zostaje taka jak w potwierdzeniu (ZR-11): osadzenie czcionki to osobna decyzja z własnym kosztem.

### Sprawozdanie: ten sam kontrakt, "było i jest" pilnowane przez serwer (T-50a)

**Wzór sprawozdania to formularz o przeznaczeniu `Report`**, a nie osobny model: wersjonowanie, walidacja odpowiedzi, renderer i podgląd są te same co przy wniosku i kartach oceny (D16). Dwie nowe właściwości kontraktu, `prefillFrom` i `readOnly`, są dozwolone tylko w tym przeznaczeniu.

**Wartości z wniosku są zapisane przy sprawozdaniu (`prefill`) i przywracane przy każdym zapisie.** Ukrycie pola w ekranie nie wystarczy: ręcznie zbudowane żądanie zmieniłoby "planowaną wartość z wniosku", a operator czyta sprawozdanie, porównując właśnie ją. Przepisanie następuje raz, przy założeniu, a nie przy każdym odczycie, bo wniosek po zatwierdzeniu wyników i tak się nie zmienia, a sprawozdanie ma pokazywać to, co było w chwili jego założenia.

**Sprawozdanie jest `IEntityScoped` z kopią `EntityId` wniosku**, jak załącznik: operator widzi wszystko, wnioskodawca swoje, a ekspert nic, bo `EntityScopedHandler` przyznaje recenzentowi tylko wnioski i ich załączniki. Zakładanie, zapis i złożenie mają dodatkowo politykę roli wnioskodawcy, zwrot i przyjęcie roli operatora.

### Polskie znaki w PDF: osadzona czcionka CID, bez biblioteki (T-45a)

**Czcionka Noto Sans (i Noto Sans Mono do tabel) jest osadzana w każdym PDF jako Type0 z czcionką CIDFontType2 i kodowaniem Identity-H.** Tekst linii to numery glifów, szerokości idą w tablicy `/W`, a mapa ToUnicode oddaje tekst przy kopiowaniu i wyszukiwaniu. Wybrana zamiast biblioteki PDF, bo wszystkie dokumenty systemu to linie tekstu, a potrzebny kawałek formatu TrueType (`cmap`, `hmtx`, metryki) to jedna mała klasa (`TrueTypeFont`). Noto ma licencję SIL OFL 1.1, która pozwala osadzać i rozpowszechniać czcionkę; licencja leży obok plików.

**Do T-45c czcionka szła w całości.** Od T-45c w pliku są tylko glify, które dokument rysuje, razem z częściami glifów złożonych (patrz "Podzbiór czcionki" niżej), bo przyszła wysyłka hurtem: umowy całego konkursu w jednym ZIP-ie (T-45b).

**Noto Sans Mono ma szerokość 600/1000 jak Courier**, więc układ kolumn list PDF z T-35 i T-42a został bez zmian. Treść stron zostaje nieskompresowana: numery glifów obok mapy ToUnicode to jedyne, co czytają testy (`PdfTextReader`).

### Umowa: wzór ze znacznikami, tekst składany przy druku (T-45)

**Wzór umowy to wersjonowany tekst ze znacznikami `{{nazwa}}`, a nie formularz.** Umowa to dwadzieścia paragrafów stałego tekstu z kilkudziesięcioma lukami, więc kontrakt formularza (sekcje, pola, walidacja odpowiedzi) byłby tu nie na miejscu. Znaczniki ze słownika (`TemplatePlaceholders`) wypełnia system z wniosku, podmiotu, decyzji o dotacji i konkursu; każda inna nazwa staje się polem, które operator wpisuje przy umowie. Nowa luka w tekście OCWIP nie wymaga więc zmiany kodu, zgodnie z D16.

**Tekst umowy nie jest zapisywany.** Umowa trzyma wersję wzoru i wartości wpisane przez operatora, a tekst składa się przy każdym druku. Zapisany tekst byłby drugą kopią tych samych faktów, która rozjeżdża się przy pierwszej poprawce wartości. Po podpisaniu wartości są zamrożone, a wersja wzoru i tak się nie zmienia, więc druk jest powtarzalny. Strona umowy (`nazwa_realizatora`, `nip`, `adres`) pochodzi z kopii karty zapisanej przy złożeniu wniosku, a nie z karty bieżącej, którą wnioskodawca może dalej edytować; karta bieżąca tylko dla wniosku sprzed kopii (T-93). Wcięcie linii szersze niż pół strony jest w druku pomijane, bo przy wcięciu szerszym od strony zawijanie nie miało miejsca na żaden znak.

**Podpisanie wymaga kompletu pól.** Luka drukuje się jako kropki, jak w papierowym wzorze, więc projekt umowy da się wydrukować wcześniej; umowy z luką nie da się jednak oznaczyć jako podpisanej. Stan wniosku przechodzi w `ContractSigned` z pominięciem `UpdatedAt`, z powodu sumy kontrolnej opisanego przy T-42.

**Dostęp na samej umowie (`IEntityScoped`), nie na wniosku**, jak przy sprawozdaniu: polityka wniosku wpuszcza przypisanego eksperta, a umowa to sprawa wnioskodawcy i operatora.

**Fragment wzoru dla wybranych rodzajów wnioskodawcy: `{{#Organisation,PatronInformalGroup}} ... {{/}}`.** Dla pozostałych rodzajów taki fragment nie drukuje się, a jego znaczniki nie są ani pokazywane do wpisania, ani wymagane przy podpisaniu. Powód jest konkretny: grupa nieformalna nie ma rejestru, numeru w rejestrze ani NIP-u, a wzór 2026 żądał wszystkich trzech, więc umowy nie dało się wydać, dopóki operator nie wpisał w nie "nie dotyczy", czyli nieprawdy do dokumentu, który ktoś podpisuje. Decyzja trzyma warunek w tekście OCWIP, nie w kodzie, tak jak `visibleWhen` we formularzu wniosku i `appliesTo` w karcie oceny, i tą samą pisownią rodzajów (`EntityType`), żeby jedno pojęcie miało jedną nazwę w trzech miejscach, które na nim warunkują. Odrzucone: osobny wzór na rodzaj wnioskodawcy (dwie kopie dwudziestu paragrafów rozjeżdżają się przy pierwszej poprawce) oraz podstawianie "nie dotyczy" przez system (to nadal nieprawda w umowie, tylko wpisana automatem). Edytor wzoru pokazuje cały tekst i wszystkie pola, bo redaguje jeden tekst dla wszystkich rodzajów. Adresu grupy nieformalnej system nie zna, bo jej karta podmiotu ma tylko nazwę, a adres lidera siedzi w tabeli członków we wniosku, której kolumn nic nie oznacza rolą; do czasu rozstrzygnięcia tego z zamawiającym wzór pyta grupę o `{{adres_lidera}}` jako pole do wpisania.

### Rozliczenie: ocena kosztów obok sprawozdania, kwota do zwrotu liczona przy odczycie (T-50b)

Propozycja T-50.0 miała kolumny operatora ("kwota nieuznana", "powód") w tabeli budżetu wnioskodawcy ze znacznikiem `operatorOnly`. Zbudowaliśmy inaczej: ocena to osobna kolumna `reports.cost_review`, a wzór wskazuje tylko rolami, która tabela jest budżetem (`reportBudget`) i która kolumna to wydatek z dotacji (`grantSpent`). Operator nie pisze w dokumencie wnioskodawcy, wnioskodawca nie może ręcznym żądaniem zmienić oceny, a kontrakt i renderer nie uczą się kolumn widocznych tylko dla jednej strony.

Ocena wskazuje wiersz numerem i pamięta wydatek, który oceniała. Wiersz, który wnioskodawca po zwrocie zmienił albo usunął, traci ocenę zamiast przenieść ją na inny koszt; przy ponownym złożeniu takie oceny odpadają. Odrzucone: stały identyfikator wiersza w odpowiedziach, bo wymagałby zmiany kontraktu odpowiedzi dla każdej tabeli.

Kwota do zwrotu nie jest zapisywana. Serwer liczy ją przy każdym odczycie (`ReportSettlement`), bo budżet może się jeszcze zmienić do złożenia, a zapisana liczba mogłaby się rozjechać z odpowiedziami. Przyjęcie sprawozdania zajmuje je warunkowym UPDATE i w tej samej transakcji przestawia wniosek na `Settled` przez `ExecuteUpdate`, jak podpisanie umowy, bo `updated_at` wniosku wchodzi do sumy kontrolnej. Stan sprawozdania jest tokenem współbieżności EF: zwrot, ocena kosztów, zapis i złożenie przez `SaveChanges` sprawdzają stan, w którym przeczytały sprawozdanie, więc operator, który w tej samej chwili zwraca albo ocenia sprawozdanie przyjęte przez drugiego, dostaje 409 zamiast nadpisać przyjęte i rozliczone. Założenie robocze ZR-14.

### Dostępność: paleta kontrastu na całą aplikację, axe po każdym teście (T-46)

**Tryb wysokiego kontrastu przestawia każdy token koloru, nie tylko te, które pokazywała strona tokenów.** Do T-46 blok `[data-contrast="true"]` nadpisywał tło, tekst, fokus i trzy tokeny stanu aktywnego, a reszta zostawała z jasnej palety na czarnym tle: linki wychodziły na 1,95:1, szare panele na 1,05:1. Teraz przestawiony jest każdy token poza pomarańczem logo, którego nikt nie używa jako tekstu, i pilnuje tego test (`app/contrast-tokens.test.ts`), który czyta obie palety wprost z `globals.css`. Akcent, linki i fokus to w trybie kontrastu żółty `#FFE800` z palety OCWIP. **Fokus nie jest fioletem `#663399` z researchu:** na czarnym ma 2,1:1, poniżej 3:1, których wymaga wskaźnik fokusu, a narzędzie wygrywa z paletą.

**Przełącznik żyje w przeglądarce, nie na koncie.** Strony, które najbardziej go potrzebują (konkurs, logowanie), czyta się przed zalogowaniem. Wybór siedzi w `localStorage`, a skrypt w `<head>` (`contrastBootScript` w `lib/contrast-mode.ts`) ustawia atrybut przed pierwszym malowaniem, żeby strona nie mignęła na biało. Odrzucone: ciasteczko czytane w `layout.tsx` (zrobiłoby każdą stronę dynamiczną tylko po to, żeby przeczytać jeden bit) i efekt w komponencie (mignięcie przy każdym wejściu). Skrypt jest napisem, więc test sprawdza go na tych samych danych co `restoreContrast`, żeby się nie rozjechały. Przycisk jest w nagłówku każdej ramy (publicznej, wnioskodawcy, operatora).

**Krawędź pola formularza ma własny token.** `--color-border` ma na białym 1,3:1, co wystarcza karcie i linii tabeli, ale nie polu, w którym ktoś ma coś wpisać (WCAG 1.4.11 wymaga 3:1). `--color-border-control` to szary `#6C757D` z tej samej skali Bootstrapa co pozostałe szarości OCWIP (4,69:1). Karty i tabele zostały przy starym tokenie celowo, żeby ekran nie zrobił się ciężki od ramek.

**Każdy test z renderem jest testem dostępności.** `axe-core` (jedyna nowa zależność, deweloperska, bez zależności przechodnich) chodzi w `vitest.setup.ts` po każdym teście na tym, co ekran pokazał, i oblewa test przy naruszeniu WCAG 2.1 A lub AA albo przeskoku w nagłówkach. Wybrano to zamiast osobnego zestawu testów dostępności, bo pliki testowe już ustawiają każdy stan każdego ekranu (błąd, pusto, wczytywanie, podsumowanie), a osobny zestaw musiałby odtworzyć te same atrapy API i zawsze zostawałby w tyle. Kontrast w jsdom się nie liczy, więc pilnuje go test na tokenach, a to, czego axe bez prawdziwej przeglądarki nie zobaczy, test źródeł (`app/accessibility-source.test.ts`). Hooki Vitesta chodzą w kolejności rejestracji (`sequence.hooks: "list"`), a `cleanup` z Testing Library jest opakowany, żeby zachować kopię strony, którą `afterEach` pliku testowego posprząta przed hookiem głównym.

**Linki w tekście są podkreślone, nawigacja jawnie nie.** Kolor linku ma do tekstu 1,51:1, więc bez podkreślenia link w zdaniu rozpoznaje się tylko kolorem (WCAG 1.4.1). Konwencja istniała już w większości ekranów, T-46 dopisał ją do ekranów konta i zamienił w regułę testu: link musi mieć `underline`, jawne `no-underline`, wygląd przycisku albo być logo.

Pełna lista ustaleń audytu, ekran po ekranie, i sposób powtórzenia go w przeglądarce: [`dostepnosc.md`](dostepnosc.md).

### Konfiguracja produkcyjna: odmowa startu zamiast cichej awarii (T-91)

**W `Production` API sprawdza pięć ustawień, zanim cokolwiek wystartuje** (`Configuration/ProductionConfiguration.cs`): niepusty `DATABASE_URL`, `FRONTEND_BASE_URL` i każdy wpis `CORS_ORIGINS` jako publiczny adres https (nie localhost, nie adres pętli zwrotnej), niepusty `SMTP_HOST` i `ALLOWED_HOSTS` bez `*`. Każde z nich ma wartość domyślną dobrą na komputerze programisty i złą wszędzie indziej, a żadne nie zawodzi głośno samo: link do localhost wychodzi w mailu, mail bez przekaźnika trafia do logu, a użytkownik widzi tylko, że nic nie przyszło. Wzorem jest odmowa przy SMTP na porcie 465 (T-43a).

**Origin CORS bez ścieżki i bez ukośnika na końcu.** `CorsPolicyBuilder` porównuje origin z nagłówkiem `Origin` jako tekst, zmieniając tylko wielkość liter, a przeglądarka nigdy nie wysyła w nim ścieżki. `https://konkursy.example.pl/` nie pasowałby do niczego i każde wywołanie z frontu padałoby bez śladu w logu API.

**Jeden komunikat ze wszystkimi błędnymi kluczami**, w pisowni zmiennej środowiskowej i z nazwą z `.env`. Pięć restartów, żeby poznać pięć błędów, to koszt, który ktoś przy pierwszym wdrożeniu zapłaciłby na pewno.

**Tylko `Production`, nie "wszystko poza Development".** Testy i narzędzia uruchamiają API w innych środowiskach bez produkcyjnych sekretów. Staging (T-117) ma przypominać produkcję, więc stawiamy go z `ASPNETCORE_ENVIRONMENT=Production` i przechodzi tę samą kontrolę; Mailpit jest tam jego `SMTP_HOST`. Odrzucone: wymóg https tylko dla adresu frontu. Ciasteczko sesji poza Development ma `Secure`, więc origin po http i tak nie utrzymałby sesji.

**Zapasowy `http://localhost:3000` w `EmailVerificationService` i `PasswordResetService` zostaje** dla Development i testów; w `Production` nie może zadziałać, bo pusty adres zatrzymuje start.

### Karta podmiotu: tworzona przy pierwszym wniosku, kopia w złożonym wniosku (T-93)

**Trasy bez identyfikatora podmiotu.** `GET`, `POST` i `PUT /me/entity` zawsze działają na podmiocie wywołującego, znalezionym przez `ResourceOwnership.EntityIdOf`. Nie ma trasy, w której dałoby się podać cudzy podmiot, więc nie ma czego sprawdzać polityką zasobową. Ta sama metoda zastąpiła trzy miejsca, które czytały `user.EntityId` wprost: gdy przyjdzie R-01 (kilka osób przy organizacji), zmienia się ona i nic poza nią.

**Karta powstaje przed szkicem.** "Wypełnij wniosek" prowadzi na `/panel/applicant/start/[competitionId]`: pusta karta przy pierwszym wniosku, wypełniona z "dane są aktualne" i "popraw" przy każdym następnym (`pola.md`, część I). Szkic zakłada się dopiero potem, więc nie istnieje szkic bez podmiotu. Odrzucone: karta jako sekcja formularza wniosku. Formularz jest danymi operatora (T-24), a karta ma stałe pola z raportu i sumy kontrolne, których kontrakt formularza nie zna.

**Dwa równoległe `POST` zakładają jeden podmiot.** Nowy podmiot i wskazanie na niego w `users` idą jednym `SaveChanges`, a `ConcurrencyStamp` konta, podmieniany przy tym zapisie, odrzuca drugi. Bez tego oba żądania wstawiłyby podmiot, a pierwszy zostałby bez właściciela.

**Nazwa wnioskodawcy z kopii, jedną funkcją.** `EntitySnapshots.NameOf` jest jedynym miejscem, z którego czyta się nazwę złożonego wniosku: lista operatora i jej eksporty, ranking z publiczną listą, archiwum i trzema eksportami, ekran i lista sprawozdań, ekran umowy, `braki.txt` w paczce oraz mail o terminie umowy. Powód jest w `PUT /me/entity`: karta nie ma bramki etapu, więc nazwę da się zmienić po złożeniu, po publikacji wyników i po podpisaniu umowy. Dokument drukowany zawsze czytał kopię, a ekrany czytały wiersz, więc dwa dokumenty organizatora o jednym naborze potrafiły się nie zgadzać, a wybierał o tym wnioskodawca (`S-06`). Trzy z tych miejsc to projekcje tłumaczone na SQL: tam do zapytania wchodzi kopia, a nazwa składa się po materializacji, bo `NameOf` nie jest tłumaczalne.

**Kopia karty w złożonym wniosku.** `applications.entity_snapshot` zapisuje się tym samym `SaveChanges`, który nadaje numer, pod blokadą naboru, więc nie ma złożenia bez kopii ani kopii bez złożenia. Złożenie odmawia (409) przy karcie, która nie przechodzi `EntityCardValidator`: karta zapisana przed tymi regułami albo ręcznie nie może stać się tym, co dostaje zamawiający. Wnioskodawca, operator i PDF czytają kopię. Wiersz złożony przed T-93 kopii nie ma i czyta bieżącą kartę.

**Numery sprawdza API, zapisuje same cyfry.** NIP (suma ważona mod 11), REGON 9 i 14 cyfr, KRS 10 cyfr (bez sumy kontrolnej) i NRB jako polski IBAN (mod 97) w `RegistryNumbers`. Spacje, dywizy i przedrostek "PL" są zdejmowane, bo tak ludzie kopiują te numery z KRS i wyciągów.

**Rodzaj wnioskodawcy tymczasowo w karcie.** Karty oceny wybierają kryteria po `Entity.Type`, więc rodzaj wybiera się przy zakładaniu karty i zamarza po złożeniu pierwszego wniosku. T-94 przenosi go do wniosku, gdzie umieszcza go `pola.md` (R-37).

### Strona konkursu operatora: publikacja z listą braków, wyniki rozstrzygają konkurs (T-97)

**Publikacja wymaga formularza wniosku i obu kart oceny.** `CompetitionService.PublicationGaps` liczy braki z samego wiersza (`form_definition_id`, `formal_card_definition_id`, `merit_card_definition_id`), `CompetitionResponse.PublicationGaps` pokazuje je szkicowi, zanim ktoś kliknie, a przejście do `Published` bez nich odpowiada 409 z tą samą listą pod kluczem `publication`. Wzór sprawozdania nie jest warunkiem: potrzebny jest miesiące później. Konsekwencja: kreator nie może już publikować jednym krokiem, bo formularz i karty da się podpiąć dopiero do zapisanego konkursu, więc publikacja przeszła na stronę konkursu `panel/operator/competitions/[id]`.

**Szkic z serwera, `localStorage` jako bufor.** Po pierwszym zapisie adres kreatora staje się `/competitions/[id]/edit` (`history.replaceState`, bez przeładowania), a ta trasa czyta konkurs z API (`fromCompetition`, odwrotność `to-request`). Bufor w przeglądarce trzyma tylko zmiany wpisane od ostatniego zapisu, osobno dla każdego konkursu, razem z `updatedAt`, na którym je wpisano. Wraca wyłącznie na tę samą wersję: przywrócony na nowszą cofnąłby po cichu czyjś późniejszy zapis. Edycja zostaje otwarta w każdym stanie (decyzja z `CompetitionService.UpdateAsync`), a gdy wpłynęły wnioski, kreator mówi to nad krokami.

**Rozstrzygnięcie tylko przez zatwierdzenie wyników.** Para `UnderReview` do `Resolved` ma w tabeli przejść własny wyzwalacz `ResultsApproval`, więc trasa statusu jej nie wykona, a `results/approve` sprawdza ją w tabeli i w tym samym warunkowym `UPDATE`, który zajmuje zatwierdzenie, ustawia `Resolved`. Zatwierdzone wyniki i rozstrzygnięty konkurs nie istnieją jeden bez drugiego, a zatwierdzenie przy otwartym naborze odpada (409 z odesłaniem do strony konkursu). Tabela zostaje jedynym źródłem przejść: przyciski strony konkursu rysuje `allowedTransitions`.

**Przywracanie dezaktywowanego konkursu (R-26).** `POST /competitions/{id}/restore`, idempotentne. Indeks numeru jest filtrowany po `is_active`, więc gdy numer zajął w międzyczasie inny aktywny konkurs, zapis łapie naruszenie indeksu i odpowiada 409, a konkurs zostaje nieaktywny.

**Karty oceny na stronie konkursu (T-96).** Sekcja "Karty oceny i wzór sprawozdania" pokazuje wersje w mocy i kopiuje je z innego konkursu, a na pustej instalacji treść wgrywa komenda `import-content` (niżej).

### Formularz wniosku 2026 jako dane, rodzaj wnioskodawcy zamrażany przy złożeniu (T-94)

**Jeden formularz warunkowy dla trzech wzorów** (`backend/seed/forms/application-2026.json`), rozgałęziany polem `rodzaj_wnioskodawcy`, jak karty oceny z T-38b. Źródłem są opublikowane wzory 2026, nowsze niż `pola.md`; różnice pole po polu w `pola.md` ("Formularz 2026 w systemie") i w R-38.

**Rodzaj wnioskodawcy jest polem wniosku, a ocena czyta go z wniosku.** Nowa rola `applicantType` na polu wyboru, którego opcje nazywają się jak `EntityType`. Przy złożeniu `ApplicantKinds.Resolve` czyta odpowiedź, sprawdza ją z kartą (karta organizacji składa jako organizacja albo patron, grupa bez patrona tylko jako grupa) i zapisuje w `applications.applicant_type` tym samym `SaveChanges`, który nadaje numer. Ocena, ranking, sprawozdanie, lista wniosków i PDF czytają `Application.KindOfApplicant` (`ApplicantType ?? Entity.Type`). Odrzucone: czytanie odpowiedzi w każdym z tych miejsc. Każde musiałoby wczytać wersję formularza i przejść po definicji, a kolumna jest jedną wartością, której da się też pilnować check constraintem. Skutek uboczny: blokada zmiany typu karty z T-93 zniknęła, bo karta nie wpływa już na ocenę złożonych wniosków.

**Dotacja to suma budżetu.** Wzór 2026 nie zbiera wkładu własnego, więc `dotacja` i `koszt_calkowity` liczą tę samą sumę trzech tabel, a udział dotacji wynosi 100%. D11 (dotacja jako różnica) czeka na odpowiedź o czwartej tabeli ze źródłami finansowania.

### Obrazy produkcyjne obok deweloperskich (T-110)

**Osobne pliki `Dockerfile.prod`, deweloperskie zostają.** Obraz deweloperski potrzebuje SDK, `dotnet watch` i `next dev` z pollingiem na zamontowanym źródle; produkcyjny nie może mieć żadnego z nich. Jeden plik z dwoma celami dałby to samo, ale każda zmiana obrazu produkcyjnego dotykałaby pliku, z którego każdy codziennie buduje lokalny stos.

**Backend:** `dotnet publish -c Release` samego projektu API, runtime `aspnet:10.0` jako `app` (UID 1654). Dane strefy czasowej przychodzą z obrazu bazowego, a build pada, gdy ich zabraknie, bo bez `Europe/Warsaw` eksporty i komunikaty naboru przeszłyby na UTC. `.NET` na Linuksie czyta strefy właśnie z `/usr/share/zoneinfo`, więc sprawdzenie pliku jest sprawdzeniem tego, co zobaczy `TimeZoneInfo`. Doinstalowany `libgssapi-krb5-2`, bo Npgsql szuka go przy każdym połączeniu i bez niego loguje błąd. Czcionki PDF są zasobem wbudowanym w dll, a `backend/seed/` jest kopiowany jawnie, bo leży poza projektem.

**Front:** `output: "standalone"`, `node server.js` jako `node`, w obrazie tylko wynik builda. `NEXT_PUBLIC_API_URL` jest argumentem builda, bo trafia do paczki przeglądarki; zniknie, gdy front i API staną pod jednym originem (T-111).

### Treść startowa na produkcji: komenda, nie seed (T-96)

**`import-content` obok `grant-role`.** Formularz wniosku, obie karty oceny i wzór sprawozdania trafiają do konkursu z plików `backend/seed/`, które obraz produkcyjny niesie w `/app/seed` (T-110). Komenda rozgałęzia się przed budową hosta webowego, jak `grant-role`, i publikuje przez `FormDefinitionService`, czyli tę samą bramkę kontraktu i tę samą numerację wersji co ekrany operatora, w jednej transakcji.

**To nie łamie decyzji "Dane testowe jako skrypt obok aplikacji, nie jako komenda w API".** Tamta dotyczy danych testowych: kont, haseł, wymyślonych wniosków, które nie mogą trafić na produkcję. Treść konkursu to dokumenty z regulaminu zamawiającego, bez kont i bez danych osobowych, potrzebne właśnie na produkcji; `seed.py` zostaje deweloperski i dalej odmawia pracy na niepustej bazie.

**Wszystko albo nic, i bezpieczne powtórzenie.** Każdy plik przechodzi bramkę kontraktu, zanim cokolwiek zostanie zapisane, więc jeden zły plik zostawia bazę nietkniętą. Plik identyczny (`JsonElement.DeepEquals`) z wersją w mocy niczego nie publikuje i mówi to, więc ponowne uruchomienie po częściowym sukcesie albo w skrypcie wdrożenia nie mnoży wersji.

### Wymogi załączników: wiersze dopasowywane po `id`, plik przypięty do wymogu (T-101)

**Edycja konkursu dopasowuje wiersze list, zamiast je kasować i wstawiać od nowa.** Wymogi załączników po `id` z żądania (`CompetitionAttachmentRequest.Id`, kreator odsyła je z odpowiedzi), osoby kontaktowe po koncie, kategorie kosztów po rodzaju. Wiersz, który został na liście, zachowuje identyfikator; zdjęty z listy dostaje `is_active = false` i `deactivated_at`, a nie `DELETE`; dodany z powrotem wraca ten sam wiersz, bo konto i rodzaj kategorii są unikalne w konkursie. Wcześniej `Drop` kasował twardo, co łamało regułę "nie kasujemy twardo" i osierociłoby każde przypięcie pliku. Odczyty pokazują tylko wiersze aktywne.

**Plik wie, na który wymóg odpowiada.** `attachments.competition_attachment_id` (nullable, `NoAction`): upload przyjmuje `requirementId`, który musi być aktywnym wymogiem konkursu tego wniosku, a format pliku musi być na liście wymogu. Podmiana pliku zachowuje przypięcie. Plik bez wymogu (sprzed T-101) zostaje, ale nie liczy się przy złożeniu, i ekran mówi to wprost.

**Złożenie liczy pliki per wymóg.** Każdy wymóg `Required`, a `RequiredOutsideKrs` wtedy, gdy karta wnioskodawcy nie wskazuje rejestru KRS, musi mieć co najmniej jeden aktywny plik; braki wracają jako błąd walidacji pod kluczem `attachments`, każdy z nazwą wymogu (D12). Front liczy te same braki dla `Required`, a `RequiredOutsideKrs` zostawia serwerowi, który zna rejestr z karty.

### Klucze DataProtection na wolumenie (T-113)

**Jedna nazwa aplikacji i klucze w katalogu z konfiguracji.** `SetApplicationName("ocwip")` i `PersistKeysToFileSystem` na ścieżce `DataProtection:KeysPath` (w obrazie produkcyjnym `/data/keys`, w compose deweloperskim wolumen `dataprotection-keys`). Wcześniej każdy nowy kontener generował własne klucze, więc wylogowywał wszystkich i unieważniał linki weryfikacji i resetu hasła z maili. `Production` nie startuje bez ścieżki (T-91). Test: sesja wydana przez jeden host działa w drugim, który czyta ten sam katalog, i nie działa w trzecim z własnym.

**Odrzucone: klucze w bazie.** Tabela kluczy w tej samej bazie wiązałaby każde logowanie z jej dostępnością i trafiałaby do tych samych kopii co dane. Katalog na wolumenie jest prostszy przy jednej instancji API; kopie zapasowe obejmą go w T-114. Szyfrowanie danych (T-47a) **nie** opiera się na DataProtection, tylko na osobnym kluczu z sekretu: utrata katalogu kluczy oznacza wylogowanie, a nie utratę danych.

### Dane wrażliwe: szyfrowanie w aplikacji, log odczytów (T-47a)

**AES-256-GCM po stronie aplikacji, klucz z sekretu.** `Data/Encryption/FieldCipher.cs` szyfruje każdą wartość osobno, z losowym nonce, w formacie `enc:<wersja>:<base64>`. Nazwa kolumny jest danymi powiązanymi, więc szyfrogramu przeniesionego do innej kolumny nie da się odszyfrować. Szyfrogram skopiowany do innego wiersza tej samej kolumny odszyfruje się: identyfikator wiersza nie jest w danych powiązanych, bo konwerter EF go nie zna, a dołożenie go znaczyłoby szyfrowanie w każdym serwisie i przepisanie wszystkich wierszy. Świadomie zostaje: taka podmiana wymaga prawa zapisu do bazy, a z nim atakujący i tak zmienia jawne kolumny (kwoty, statusy); przed nim bronią role bazy (T-113) i kopie. Klucze leżą w `FieldEncryption:Keys:<wersja>` (w produkcji `FieldEncryption__Keys__1`), szyfruje najwyższa wersja, a czyta każda skonfigurowana. Klucz nie jest w repozytorium, w bazie ani w DataProtection. `Production` nie startuje bez niego (T-91). Dev ma swój klucz w `appsettings.Development.json`, tylko dla danych fikcyjnych.

**Szyfrowanie w konwerterach EF, nie w serwisach.** `entities.address`, `correspondence_address`, `phone`, `email`, `bank_account`, `representatives` (teraz `text` z zaszyfrowanym JSON-em) i `users.pesel` to kolumny szyfrowane w całości. Dokumenty jsonb zostają dokumentami: szyfrowane są wartości w środku (`EncryptedDocument`), więc check constraint na kształt i wyszukiwanie po reszcie działają dalej.
- **Kopia karty** (`applications.entity_snapshot`): szyfruje te same pola co karta.
- **Wartości umowy** (`contracts.values`): szyfruje wszystkie, bo znacznik to dowolna nazwa wpisana przez autora wzoru.
- **Odpowiedzi** (`applications.answers`, `application_versions.answers`, `reports.answers`, `reports.prefill`, `evaluations.answers`): szyfruje tylko pola z flagą `sensitive` w kontrakcie formularza. Robi to serwis, bo tylko on zna formularz, a konwerter tylko odszyfrowuje. Pole sprawozdania wypełniane z wrażliwej odpowiedzi wniosku też jest wrażliwe. Karta oceny ma tę samą drogę od S-34: kryterium potrafi pytać o funkcje konkretnych osób, więc oznaczone pole karty nie może leżeć w kolumnie jawnie.
- **Tekst udający szyfrogram** (`enc:...` wpisany w zwykłe pole) też jest szyfrowany. Inaczej odczyt próbowałby go odszyfrować i wywracałby cały dokument.

Konwertery czytają pierścień kluczy z pola statycznego, bo EF buduje model raz na typ kontekstu i trzyma konwertery w pamięci podręcznej.

**NIP jawny (DZ-2).** Kolumny szyfrowane nie mają długości w bazie, bo szyfrogram jest dłuższy od tekstu. Limit tekstu jawnego pilnuje walidator na brzegu API. Wyszukiwanie po zaszyfrowanym polu, jeśli kiedyś będzie potrzebne, idzie przez osobny indeks HMAC z innym kluczem.

**Stare wiersze i rotacja: jedna komenda.** Wartość bez prefiksu czyta się jako jawną, więc migracja `EncryptSensitiveFields` nie potrzebuje klucza. `reencrypt-data` przepisuje wszystko bieżącym kluczem, partiami, bez ruszania `updated_at` (`AppDbContext.KeepUpdatedAt`). Służy do zaszyfrowania wierszy sprzed T-47a, a po dodaniu klucza `n+1` do wycofania starego. Procedura jest w [`wdrozenie.md`](wdrozenie.md). Wrażliwe odpowiedzi wersji formularza, która nie przechodzi już dzisiejszego kontraktu (reguła zaostrzona później, R-34), komenda czyta z flag zapisanych w definicji (`SensitiveAnswers.MarkedKeys`), a nie jako "nic wrażliwego": inaczej przepisałaby je jawnym tekstem. Dotyczy to obu stron sprawozdania (`MarkedReportKeys`, S-37): ta ścieżka wcześniej czytała wzór ściśle i rzucała, a sprawozdania są przepisywane ostatnie, więc rotacja kończyła się z resztą bazy pod nowym kluczem i sprawozdaniami pod starym, czyli tym, który miała wycofać. Flaga `sensitive` żyje w wersji formularza, więc formularz zaimportowany przed T-47a nie ma jej wcale; seed 2026 ma ją od T-47a, a produkcji z danymi sprzed T-47a nie ma, więc nie ma czego uzupełniać. Gdyby była, trzeba opublikować nową wersję formularza z flagami i dopiero wtedy uruchomić komendę.

**PESEL w umowie maskowany.** Znacznik z `pesel` w nazwie wraca na ekran jako gwiazdki z ostatnimi czterema znakami. Pełny numer jest tylko w PDF-ie umowy. Maska odesłana bez zmian zachowuje zapisany numer.

**Log odczytów w filtrze endpointu.** `.LogsPersonalDataRead(zasób, wartość trasy)` dopisuje do `personal_data_reads` konto, zasób, jego identyfikator i trasę po każdej udanej odpowiedzi. Filtr jest na każdej trasie, która oddaje wniosek, listę albo pobranie załącznika, umowę albo sprawozdanie, **niezależnie od czasownika HTTP**: także na sporządzeniu umowy, zapisie jej wartości, podpisie, na rozpoczęciu sprawozdania oraz na jego zwrocie, przyjęciu i ocenie kosztów (S-36). Te trasy odpowiadają tym samym ładunkiem co odczyt obok nich, a sporządzenie umowy, która już istnieje, oddaje jej wartości bez zmiany jakiegokolwiek wiersza, więc było cichym odczytem PESEL-u i rachunku. Listę trzyma test `PersonalDataReadTests` z tabeli endpointów aplikacji. Nie ma go na potwierdzeniu złożenia (numer i suma kontrolna, bez odpowiedzi), eksportach list i własnej karcie podmiotu. Nazwa wartości trasy jest sprawdzana przy budowaniu endpointu: literówka zatrzymuje start, zamiast po cichu pomijać log. Forbid, Challenge i przekierowanie nie liczą się jako odczyt. Log nie kopiuje danych, tylko je nazywa.

Zapis idzie osobnym INSERT-em poza śledzeniem zmian, a jeśli się nie uda, żądanie kończy się błędem. Na trasie zapisu zmiana jest wtedy już zacommitowana, więc 500 z tego powodu nie znaczy, że zapis się nie wykonał: powtórzone żądanie odpowie 409 albo odda ten sam wiersz. Eksporty list nie są logowane, bo nie zawierają danych osobowych; paczka umów nie jest logowana i to jest otwarte znalezisko (`S-19`). Kryterium dla nowej trasy: nie "czy to GET", tylko "czy odpowiedź niesie `ApplicationResponse`, `AttachmentResponse`, `ContractResponse` albo `ReportResponse`, których wołający nie przysłał w tym samym żądaniu". Druga połowa tego zdania jest potrzebna: zapis wersji roboczej, złożenie wniosku, wysłanie i podmiana pliku oraz zapis i złożenie sprawozdania oddają treść właśnie przysłaną, więc nie są odczytem i filtra nie mają.

**Zastąpiony załącznik: zostaje, ale nie wychodzi.** Podmiana oznacza poprzedni wiersz nieaktywnym i nigdy nie kasuje ani wiersza, ani pliku (reguła 5, kryterium T-32 "poprzedni nie znika twardo"). Trasa pobrania serwuje jednak tylko wersję obowiązującą (`S-22`): żadna lista nieaktywnego wiersza nie pokazuje, więc kto prosi o niego po identyfikatorze, ten go sobie zapamiętał, a przypisany ekspert nie ma czytać wersji, którą wnioskodawca wycofał. Retencja mieszka w wierszu i w pliku na wolumenie, nie w tym, co oddaje API.

**Zwrot sprawozdania zachowuje wersję.** `report_versions` trzyma odpowiedzi, wartości wzięte z wniosku i ocenę kosztów z chwili zwrotu, zapisane w tej samej transakcji co zmiana statusu (`S-35`), tak jak `application_versions` dla wniosku. Ocena kosztów jest przy ponownym złożeniu nadal przycinana (wpis o kwocie, którą wnioskodawca zmienił, odpada i operator patrzy jeszcze raz), ale uzasadnienie przestało przez to ginąć, bo kopia wersji trzyma je razem ze stanem, do którego było pisane. Przycinanie musi być trwałe, a nie filtrem przy odczycie: kwota, która przez dwie poprawki idzie 1400, 1399,99 i znowu 1400, pasowałaby wtedy do wpisu sprzed dwóch rund i odmawiała kosztu uzasadnieniem, którego nikt nie napisał do tej wersji. Obie kolumny z dokumentem mają w tej tabeli **osobne** purposes szyfrowania, inaczej niż w `reports`, gdzie dzielą jeden. Oba purposes przepisuje `reencrypt-data`, bo kolumna szyfrowana poza rotacją zostaje pod kluczem, który rotacja ma wycofać.

**Zatwierdzenie wyników zamyka ocenę.** Ranking i publiczna lista liczą punkty i miejsca z kart przy każdym odczycie, więc po `ResultsApprovedAt` odmawiają: ustawienia oceny, założenie nowej karty, zapis i zakończenie karty oraz dopisanie i odpięcie aktywnego eksperta (`S-05`). Trasa zakładająca kartę oddaje jednak kartę już otwartą, bo tą samą trasą ekran eksperta ją czyta, a odczyt niczego nie przesuwa. Wszystkie pięć odpowiada 409, bo to stan konkursu, nie zły kształt żądania. Przypisanie jest na tej liście, choć wygląda niewinnie: nowy ekspert zakłada kartę, a jego punkty zmieniają ogłoszone miejsca bez śladu w historii. Czy ma istnieć droga powrotna, pyta P23; dopóki nie ma odpowiedzi, nie ma wyjątku.

**Zapis i zakończenie karty pod blokadą wiersza.** Obie metody biorą `SELECT ... FOR UPDATE` na ocenie w jednej transakcji (`S-07`), bo widełki punktowe sprawdza dopiero zakończenie: bez blokady zakończenie walidowało odpowiedzi, które równoległy zapis zaraz nadpisywał, i karta kończyła się z punktacją spoza skali, policzoną do rankingu.

**Lista rezerwowa jest kolejką.** Promocja liczy kandydata tą samą ścieżką co podgląd operatora i odmawia komukolwiek poza następnym w kolejności (409), a kwoty ponad wnioskowaną nie przyjmuje (`S-33`, ZR-09). Wcześniej brała identyfikator i kwotę z żądania, więc dało się dofinansować wniosek z dalszego miejsca ponad ogłoszony pułap konkursu.

**Retencja sprawdzana po całym modelu.** `RetentionModelTests` przechodzi po wszystkich kluczach obcych modelu i po prawdziwym schemacie (`information_schema`): żaden nie kasuje ani nie zeruje zależnych wierszy. W kodzie nie ma `Remove` ani `ExecuteDelete` na danych domenowych, a `CompetitionService.Drop` zniknął w T-101. Retencję po terminie (anonimizacja) robi T-47b.

### Zwrot do poprawy: nowy stan, kopia wersji, jedno okno edycji (T-103)

**Stan `Returned`, nie powrót do `Draft`.** Zwrócony wniosek zachowuje numer, chwilę złożenia i rodzaj wnioskodawcy, więc przechodzi przez wszystkie constrainty `status <> 'Draft'` bez zmiany schematu. `IsGranted` go nie obejmuje. Powrót do `Draft` kasowałby numer, a numer jest widoczny u operatora i w mailach.

**Kopia zwracanej wersji, nie wersjonowanie każdego zapisu.** Odpowiedzi są nadpisywane w miejscu, a suma kontrolna liczy się z `UpdatedAt`, więc przy zwrocie wersja w obiegu trafia do `application_versions`. Kopia zawiera odpowiedzi, kopię karty, sumę z potwierdzenia, chwilę złożenia i wersję formularza, zaszyfrowane jak we wniosku (T-47a). Wiersz wniosku to zawsze wersja bieżąca. Pierwszej wersji nie kopiujemy przy złożeniu: bez zwrotu nie ma czego odróżniać, a złożone wnioski nie potrzebują uzupełnienia danych.

**Jedno okno edycji dla trzech ścieżek.** `ApplicationEditWindow` odpowiada autozapisowi, załącznikom i złożeniu na jedno pytanie: czy teraz i co.
- Szkic jest otwarty według naboru.
- Wniosek zwrócony jest otwarty według terminu zwrotu (`CompetitionIntake.ForCorrection`, jawna gałąź obok naboru), także po zamknięciu naboru. Zamyka się w minucie terminu.
- Poza odblokowanymi sekcjami serwer odrzuca każdą zmianę odpowiedzi (`LockedSections`). Pola wyliczane pomija, bo zmieniają się same.
- Załączniki są otwarte tylko wtedy, gdy zwrot je odblokował.

**Zwrot w transakcji, stan warunkowym UPDATE.** Z dwóch równoczesnych zwrotów przechodzi jeden, a filtrowany indeks unikalny pozwala na jeden otwarty zwrot na wniosek. Dotychczasowe oceny są dezaktywowane, nie kasowane, a robocza decyzja o kwocie z listy rankingowej jest czyszczona, bo zapadła na starej wersji. Wniosek poprawiony ocenia się od nowa, a zwrócony odmawia oceny. Otwarcie karty oceny blokuje wiersz wniosku (`FOR UPDATE`) do zapisu karty, więc karta nie powstanie obok równoczesnego zwrotu.

**Zatwierdzenie wyników a zwrot.** Wniosek zwrócony w terminie poprawy wstrzymuje zatwierdzenie, jak każdy nieoceniony. Po terminie okna już się nie otworzy, więc zatwierdzenie odrzuca taki wniosek razem z resztą (`Returned` na `Rejected`, w historii ze stanu `Returned`) i wysyła mu mail o wyniku. Bez tego jeden wnioskodawca, który nie odpisał, blokowałby rozstrzygnięcie całego konkursu.

Ponowne złożenie (`ResubmitAsync`) najpierw warunkowo zamyka zwrot, więc dwa kliknięcia dają jedno przejście. Numer zostaje, chwila złożenia i suma kontrolna są nowe, a wnioskodawca dostaje nowe potwierdzenie. Kopia karty podmiotu zostaje z pierwszego złożenia: zwrot odblokowuje sekcje formularza, nigdy kartę, więc zmiana na karcie po zwrocie nie wchodzi do nowej wersji.

**Kiedy (PK-H, przyjęte domyślnie "na obu").** Zwrot jest możliwy w stanach konkursu `OpenForApplications`, `Closed` i `UnderReview`, tylko ze stanu `Submitted` i nigdy po zatwierdzeniu wyników. Jeśli klientka odpowie "tylko w naborze", zmienia się jedna tablica w `ApplicationReturnService`.

### Zadania w tle: jeden harmonogram, rejestr przebiegów, najwyżej raz (T-105)

**Jedna instancja API.** To założenie całego systemu. Dotyczy harmonogramu zadań w tle, pamięci podręcznej ponownej wysyłki maila weryfikacyjnego i kluczy DataProtection na jednym wolumenie. Gdyby kiedyś potrzebne były dwie instancje, trzeba przejrzeć te trzy miejsca razem. Zadanie w tle i tak nie wyśle niczego dwa razy, bo zajęcie przebiegu to warunkowy UPDATE, ale obie instancje by patrzyły.

**`BackgroundJobScheduler` i `IBackgroundJob`.** Jeden `BackgroundService` co `BackgroundJobs:IntervalSeconds` uruchamia każde zarejestrowane zadanie we własnym zakresie DI. Nowe zadanie to nowa klasa i jedna linia w `Program.cs`. `BackgroundJobs:Enabled=false` wyłącza wszystko, a fabryka testowa robi to zawsze: testy wołają zadanie wprost, z ustawionym zegarem.

**Idempotencja w bazie, nie w pamięci.** `scheduled_job_runs` ma unikalny klucz (zadanie, obiekt, termin). Przebieg powstaje raz, zajmuje go warunkowy UPDATE (wzór z T-43) i kończy zapis po efekcie. Termin jest częścią klucza, więc przesunięty koniec naboru daje nowe przypomnienie o nowej dacie.

**Najwyżej raz, a nie co najmniej raz.** Przebieg zajęty i niezakończony (proces padł między mailem a zapisem) nie jest brany ponownie. Nikt nie wie, czy mail wyszedł, a drugie przypomnienie jest gorsze niż żadne. To odwrotnie niż przy mailach o wyniku (T-43): mail o wyniku jest należny, a operator ponawia go świadomie. Błąd, który zadanie samo zobaczyło (odmowa przekaźnika), zwalnia zajęcie z typem błędu, bo wtedy wiadomo, że nic nie wyszło. Są najwyżej trzy próby. Wysyłka SMTP ma własny limit czasu (`SmtpEmailSender.SendTimeout`, 60 s), bo `SmtpClient.Timeout` nie obejmuje wysyłki asynchronicznej, a przekaźnik, który przyjął połączenie i zamilkł, zatrzymałby cały harmonogram. Pętlę kończy tylko zatrzymanie hosta: każde inne anulowanie wewnątrz zadania to zwykły błąd tego zadania, a kolejne zadania ruszają dalej.

**Pierwszy konsument: R-09.** Przypomnienie trzy dni przed końcem naboru idzie tylko w naborze otwartym według cyklu życia konkursu, do kont podmiotu z aktywnym szkicem, na potwierdzony adres. Temat podaje chwilę końca naboru, a nie "za 3 dni", bo przypomnienie wysłane później (termin przesunięty na bliżej, harmonogram stał) nadal musi mówić prawdę. Treść jest stała, bo konkurs nie ma pola na własny tekst (zostaje otwarte przy R-09). Termin podpisania umowy (T-109) i termin sprawozdania (T-50c) dołączą jako kolejne `IBackgroundJob`.

### Rezygnacja i lista rezerwowa: zegar przypomina, operator decyduje (T-109)

**Regulamin 2026:** umowa niepodpisana w 14 dni od ogłoszenia wyników oznacza rezygnację, a środki idą na kolejny wniosek, który spełnia próg. Termin to 14 dni od chwili, w której wniosek dostał dofinansowanie: dla listy z zatwierdzenia `results_approved_at` (T-42a publikuje listę razem z nim), dla awansu z listy rezerwowej chwila awansu, czyli ostatnie przejście na `Funded` w historii statusów. Awansowany w dwudziestym dniu nie jest więc od razu "po terminie". To przyjęte założenie: regulamin mówi o ogłoszeniu wyników, a dla awansowanego wynikiem jest awans; jeśli OCWIP liczy inaczej, zmienia się `ResignationService.FundedAtAsync`.

**Zegar tylko przypomina.** `ContractDeadlineJob` po terminie wysyła jeden mail osobom kontaktowym konkursu, a bez nich wszystkim operatorom, z listą niepodpisanych umów tego terminu. Przebieg jest jeden na odbiorcę, konkurs i termin, klucz wyprowadzony z obu identyfikatorów, więc odmowa przekaźnika u jednej osoby nie wysyła drugiego maila innej, a awans z późniejszym terminem dostaje własne przypomnienie. Zadanie patrzy tylko na terminy z ostatnich 7 dni (`RecentWindow`): bez tego co minutę przeglądałoby każdy stary konkurs, a operator dodany po miesiącach dostałby przypomnienie o dawno zamkniętej sprawie. Ekran pokazuje te same umowy z oznaczeniem "termin minął", a awansowane z własnym terminem.

**Rezygnację potwierdza operator.** `Funded` przechodzi na `Resigned` warunkowym UPDATE: podpisana umowa jest w stanie `ContractSigned`, więc tą drogą jej się nie cofa. Niepodpisany szkic umowy zostaje wycofany (`is_active = false`), a z nim sprawozdanie rozpoczęte, ale nie przyjęte: nie ma już dotacji do rozliczenia. Kwota zostaje na wierszu jako zapis tego, z czego zrezygnowano, ale `AwardedTotal` i wolna pula jej nie liczą.

**Awans z listy rezerwowej.** System proponuje pierwszy wniosek `Reserve` w kolejności rankingu, z kwotą wnioskowaną albo resztą puli, gdy reszta jest mniejsza. Operator zatwierdza i może zmienić kwotę. Serwer sprawdza kwotę z wolną pulą pod blokadą doradczą konkursu, więc dwa awanse naraz nie zmieszczą się w tych samych pieniądzach.

Oba przejścia trafiają do historii statusów, a wnioskodawca dostaje mail. Mail o rezygnacji podaje przyczynę, która zaszła: termin minął albo operator odnotował rezygnację wcześniej. Mail idzie po zatwierdzeniu transakcji, więc odmowa przekaźnika nie cofa zmiany i nie kończy się błędem 500: odpowiedź mówi `mailSent: false`, a ekran prosi operatora o powiadomienie inną drogą (ponowienie trafiłoby już na nowy stan). Żadne przejście nie rusza `UpdatedAt` (D15). Opublikowana lista wyników czyta stany na bieżąco, więc zrezygnowany znika z niej, a awansowany jest dofinansowany. Czy lista rezerwowa jest ogłaszanym wynikiem (ZR-09, PK-L), zostaje otwarte.

### Kopia konkursu: te same ścieżki co kreator, nowe wiersze wszędzie (T-98)

**Ustawienia przez `CompetitionRequest`, nie kopią wiersza.** `CompetitionCopyService` buduje z konkursu źródłowego to samo żądanie, które wysyła kreator, i przepuszcza je przez `CompetitionRequestValidator` i `CompetitionService.CreateAsync`. Kopia nie może więc zapisać konkursu, którego kreator by nie przyjął.

Kopiowanie kolumna po kolumnie wyglądałoby na prostsze, ale każda nowa reguła walidacji musiałaby pamiętać o drugiej ścieżce. Ustawienia oceny (T-39) i treści maili o wyniku (T-43) nie są w żądaniu kreatora, więc przechodzą osobno.

**Dokumenty jako wersja 1 kopii.** Formularz, obie karty i wzór sprawozdania przechodzą przez `FormDefinitionService.PublishAsync`, a wzór umowy przez `ContractService.PublishTemplateAsync`. To te same bramki, co przy ręcznej publikacji. Nowe wiersze w nowym konkursie oznaczają, że zmiana w kopii nie dotyka oryginału. Dokument źródła, który nie przechodzi dzisiejszego kontraktu, odmawia całej kopii, bo szkic bez niego wyglądałby na kompletny.

**Czego kopia nie bierze:** numeru i dat (podaje je operator), okresu realizacji projektu, terminu wersji papierowej i daty retencji, bo należą do edycji, ani niczego, co zdarzyło się w naborze. Kontakty przechodzą tylko aktywne. Wszystko idzie w jednej transakcji.

### Wzory załączników: osobna tabela, ten sam magazyn, publiczne pobranie (T-102)

**Wzór przy wymogu, nie w wierszu wymogu.** `attachment_templates` trzyma plik z nazwą, formatem i rozmiarem, a filtrowany indeks pozwala na jeden aktywny wzór na wymóg. Podmiana dodaje nowy wiersz i dezaktywuje poprzedni, a wycofanie tylko dezaktywuje. Bajty zostają w magazynie, jak przy załącznikach wnioskodawcy (T-32). Odrzucone: kolumny pliku w `competition_attachments`, bo podmiana zgubiłaby poprzedni plik, a to łamie regułę "nie kasujemy".

**Te same zasady co załącznik wnioskodawcy.** Wzór idzie do `IAttachmentStorage`. Format rozpoznaje `AttachmentFormatDetector` po bajtach, a nie po deklaracji ani nazwie. Limit to 10 MB (`Competition.DefaultMaxAttachmentSizeInBytes`). Pobranie jest zawsze jako plik, z typem zweryfikowanego formatu.

**Publicznie, bo strona konkursu jest publiczna.** `GET /public/attachment-templates/{id}` odpowiada tylko dla konkursu widocznego publicznie (`CompetitionLifecycle.IsPubliclyVisible`) i wymogu na liście. Szkic niczego nie oddaje. Wzór nie może nieść danych osobowych: to plik organizatora dla wszystkich.

### Konta zespołu bez SDK: komendy w obrazie, lista tylko do odczytu (T-104)

**Decyzja "rola nadawana komendą, nigdy przez HTTP" zostaje.** T-104 sprawia, że da się z niej korzystać na produkcji. `grant-role`, `deactivate-account`, `reactivate-account` i `list-accounts` idą przez `AdminCommandRunner` w tym samym `Ocwip.Api.dll` co API (`dotnet Ocwip.Api.dll <komenda>`), więc obraz runtime ich nie wymaga SDK. Job `images` w CI uruchamia je na obrazie.

**Wyłączenie konta** ustawia `is_active = false` i nowy `SecurityStamp`. Znacznik jest sprawdzany przy każdym żądaniu (`ValidationInterval` zero), a `ActiveAccountStampValidator` i tak odrzuca konto nieaktywne, więc sesje kończą się od razu. Nic nie jest kasowane. `reactivate-account` cofa wyłączenie (pomyłka w adresie, powrót osoby do zespołu) bez SQL na produkcji; znacznik z wyłączenia zostaje, więc stare sesje dalej nie działają. Ostatniego aktywnego operatora komenda nie wyłączy: bez operatora nikt nie poprowadzi konkursu, a rolę nadaje się tylko z powłoki, więc najpierw `grant-role` komuś innemu.

**Lista zespołu w panelu** (`GET /accounts/team`) pokazuje operatorów i ekspertów z rolą i stanem, tylko do odczytu. Wnioskodawców nie pokazuje ani ona, ani `list-accounts`.

**Ekspert dostaje mail przy nowym przypisaniu**, także przywróconym, a przy powtórzonym nie. Mail niesie numer wniosku i konkurs, bez treści wniosku. Przypisanie grupowe to seria pojedynczych przypisań, więc daje jeden mail na wniosek. Zbiorcze podsumowanie byłoby osobną zmianą w ekranie przypisań.

### "Moje konto": hasło z obecnym, adres z potwierdzeniem (T-106)

**Hasło.** `ChangePasswordAsync` wymaga obecnego hasła i obraca `SecurityStamp`, więc pozostałe sesje kończą się przy następnym żądaniu. Bieżąca dostaje nowe ciasteczko (`RefreshSignInAsync`), żeby osoba, która zmieniła hasło, nie została wylogowana. Błędne obecne hasło liczy się jak nieudane logowanie (`AccessFailedAsync`), więc przejęta sesja nie jest drogą do zgadywania hasła bez limitu. Właściciel dostaje mail o zmianie.

**Adres.** Prośba wymaga hasła. Na nowy adres idzie link z tokenem `GenerateChangeEmailTokenAsync`, a stary adres od razu dostaje powiadomienie. Adres zmienia się dopiero po otwarciu linku, razem z nazwą konta w jednym zapisie (`ChangeEmailAsync` z ustawionym `UserName`), co kończy wszystkie sesje, a stary adres dostaje drugie powiadomienie. Konto założone na ten adres między sprawdzeniem a zapisem kończy się tą samą odpowiedzią co zły token, nie błędem 500. Token jest związany z adresem w linku i ze znacznikiem bezpieczeństwa, więc podmiana adresu albo zmiana hasła w międzyczasie go unieważnia (test `AccountSettingsTests`). Literówka nikogo nie odetnie od konta, a cudzego adresu nie da się zająć bez dostępu do skrzynki.

**Reguła 3.** Adres, który ma już konto, dostaje tę samą odpowiedź co wolny i nie dostaje żadnego linku, tylko powiadomienie bez linku ("Próba przypisania adresu e-mail"). Dzięki temu oba przypadki wysyłają te same dwa maile i trwają podobnie długo, więc stoper na tym żądaniu nie odróżni adresu zajętego od wolnego. Strona z linku potwierdza przyciskiem, a nie przy otwarciu, żeby podgląd linku w programie pocztowym nie zużył jednorazowego tokenu.

### Adres oczekujący na potwierdzenie: kolumna na koncie, nie parametr w linku (T-106)

Link potwierdzający zmianę adresu niósł nowy adres w zapytaniu (`?userId=...&email=...&token=...`), więc adres trafiał do historii przeglądarki i do logu każdego pośrednika, choć konto i token wystarczały. Adres czeka teraz w `users.pending_email`, a potwierdzenie czyta go stamtąd; token Identity nadal rozstrzyga, bo jest generowany dla konkretnego adresu i sprawdzany względem tego zapisanego. Druga prośba nadpisuje oczekujący adres, więc działa nowszy link, a starszy przestaje: to właściwa kolejność, bo ostatnia prośba jest tą, o którą właściciel konta prosił. Brak oczekującego adresu (link otwarty drugi raz) daje tę samą odmowę co zły token, więc ekran nie mówi, co dokładnie się stało.

### Zgody przy rejestracji: tekst jako dane, wersja z treści (T-107)

**Tekst to plik, nie kod.** Regulamin i klauzula leżą w `seed/consents/*.md`, które obraz produkcyjny kopiuje razem z resztą `seed/`. Podmiana treści od IOD to podmiana pliku (ZR-15). Tytuł to pierwsza linia pliku.

**Wersja wynika z treści.** Wersja to początek skrótu SHA-256 tekstu, więc nikt nie musi pamiętać, żeby ją podbić. Formularz odsyła wersje dokumentów, które pokazał, a `/register` odmawia, gdy któraś nie jest wersją w mocy. Ktoś, kto zaakceptował tekst podmieniony w międzyczasie, dostaje odmowę i widzi nowy tekst. Odrzucone: numer wersji wpisywany ręcznie, bo zapomniany numer oznacza akceptację tekstu, którego nikt nie widział.

**Zapis jak deklaracja bezstronności.** `consent_acceptances` trzyma pełny tekst i chwilę, bo sam skrót nie odtworzy, co ktoś zaakceptował, gdy plik już się zmienił. Wiersz powstaje w tej samej transakcji co konto, przed mailem weryfikacyjnym: konto bez akceptacji nie dałoby się już naprawić, bo kolejna rejestracja tym adresem dostaje odpowiedź jak dla zajętego. Dla adresu zajętego nie powstaje nic, a odpowiedź jest ta sama (reguła 3). Odmowa zgody przychodzi przed próbą założenia konta, więc też nie mówi nic o adresie.

**Czego to nie robi (R-40).** Akceptacja powstaje tylko przy `/register`. Podmiana pliku wymusza nową akceptację u kolejnych rejestrujących się, ale konta już istniejące zostają z akceptacją poprzedniej wersji, a konta zespołu założone komendą z powłoki nie mają żadnej. Ponowna akceptacja przy logowaniu to decyzja OCWIP i IOD (czy zmiana regulaminu wymaga zgody, czy wystarcza informacja), nie poprawka.

**Klauzula dla osób trzecich** to zwykłe oświadczenie w formularzu wniosku (`o_rodo_osoby_trzecie`), bo formularz jest już danymi (T-94). Nowe pole trafia do bazy dopiero przy ponownej publikacji formularza (`seed.py` na pustej bazie).

### Powtórzenie adresu i hasła: front, nie kontrakt (T-124)

Raport wymienia w kroku 2.1 dziewięć pól rejestracji, my mamy sześć. Dwa brakujące to powtórzenia i **obsługuje je wyłącznie przeglądarka**: `RegisterRequest` zostaje bez zmian, bo powtórzenie nie jest daną o człowieku, tylko pytaniem "czy na pewno to wpisałeś". Wysłanie go na serwer dołożyłoby polu kontraktu, kolumnie w OpenAPI i regule walidacji rzecz, która ma sens wyłącznie w tym jednym formularzu.

**Adres porównujemy bez rozróżniania wielkości liter, hasło dosłownie.** Za rejestracją stoi unikalny indeks na `NormalizedEmail`, więc `Biuro@` i `biuro@` to jedno konto i odmowa dla tej pary byłaby alarmem o pomyłce, której nie ma. W haśle wielkość liter i spacje są treścią, więc tam każda różnica jest różnicą. Białych znaków **nie obcinamy** przy żadnym z nich: adres ze spacją na końcu jest dla backendu innym adresem (ta sama reguła co przy `grant-role`), więc lepiej o niej powiedzieć, niż ją po cichu zjeść.

**Sprawdzenie następuje przy wysłaniu, nie przy każdym znaku.** Dopóki drugi adres jest wpisywany, zdanie "adresy są różne" jest prawdziwe i bezużyteczne. Wynik ma kształt odmowy backendu (`fixFieldsMessage` plus `fieldErrors`), więc formularz mówi raz w swoim alercie, że coś wymaga poprawy, a czytnik ekranu podaje powód przy polu, na które wejdzie. Odmowa polityki hasła czyści **oba** pola hasła: wyczyszczenie tylko pierwszego zostawiałoby w drugim poprzednie hasło i następna próba odbijałaby się od niezgodności, której człowiek nie widzi.

**Czego to nie zamyka.** Telefon kontaktowy z tego samego kroku raportu zostaje otwarty, bo odwraca decyzję o niezbieraniu numeru (wyżej, sekcja o kolumnach Identity). To pytanie do zamawiającego, nie poprawka.

### Archiwum wyników: z publikacji, nie obok niej (T-108)

`GET /public/results` składa archiwum z `PublishedAsync`, konkurs po konkursie, zamiast pisać własne zapytanie o wnioski. Archiwum nie pokaże więc nigdy więcej niż opublikowane wyniki: to samo zatwierdzenie, te same wiersze, tylko bez listy rezerwowej, która po rozdaniu środków niczego już nie znaczy. Wchodzą konkursy `Resolved` i `Archived`, bo "Archiwalny" zdejmuje konkurs z bieżącej listy, a nie z zapisu, kto dostał pieniądze. Wiersz ma tylko nazwę podmiotu, tytuł i kwotę; dla grupy nieformalnej nazwa podmiotu to nazwa grupy, więc imiona i nazwiska członków nie mają którędy wyjść (RD3). Jeden odczyt rankingu na konkurs jest tani przy kilku konkursach w roku; przy setkach trzeba by zapisać wynik przy zatwierdzeniu.

### Budżet sprawozdania w kilku tabelach (T-95)

Wzór sprawozdania 2026 dzieli budżet na części A, B i C, tak jak wniosek, a każda część przepisuje wiersze z własnej tabeli wniosku (`prefillFrom` przyjmuje jedną tabelę). Rola `reportBudget` może więc stać na kilku tabelach, a rozliczenie dodaje je wszystkie. Odrzucone: jedna tabela budżetu z kolumną "część", bo przepisałaby tylko jedną tabelę wniosku, oraz rola tylko na części A, bo wydatki z B i C nie weszłyby do uznanych i zwrot wyszedłby za wysoki.

Ocena kosztu wskazuje tabelę kluczem (`budget`) i wiersz w niej. Ocena bez klucza, także zapisana przed T-95, dotyczy pierwszej tabeli budżetu, więc wzory z jedną tabelą i dotychczasowe zapisy działają bez migracji. Każdy zapis oceny dostaje już klucz tabeli.

### Umowy hurtem: ta sama ścieżka co jedna umowa, luka zostaje poza plikiem (T-45b)

`ContractBundleService` nie ma własnego wypełniania: sporządza i drukuje każdą umowę przez `IContractService`, więc umowa w ZIP-ie jest tą samą, którą drukuje jej strona. Umowa z pustym polem operatora nie trafia do archiwum, bo wyglądałaby na gotową do podpisu, a zatrzymanie całej paczki przez jedną lukę byłoby gorsze; jej numer i brakujące pola wymienia `braki.txt` w tym samym pliku. Odrzucone: zadanie w tle i plik na dysku, bo przy około 60 umowach ZIP w pamięci ma kilka do kilkunastu MB (od T-45c około 140 KB na umowę, wcześniej około 430 KB).

**Członkowie grupy przez rolę, nie przez klucz.** `{{czlonkowie_grupy}}` czyta tabelę wniosku z rolą `groupMembers`, jak lista wniosków czyta tytuł z roli `projectTitle`: klucze wybiera operator, a literówka w kluczu zostawiłaby umowę po cichu bez podpisujących. Przy organizacji wartość brzmi "nie dotyczy", bo pola systemowego operator nie uzupełni.

**Wzór jako plik.** `seed/templates/contract-2026.txt` idzie do konkursu przez `import-content --contract`, z tym samym sprawdzeniem znaczników co ekran operatora i bez nowej wersji dla identycznej treści, więc zamiana na wersję od prawnika (P15) to podmiana pliku.

### Podzbiór czcionki: puste kontury zamiast przenumerowania (T-45c)

Każdy PDF osadzał całą Noto Sans, około 314 KB po kompresji, więc umowa 2026 miała około 430 KB, a paczka umów konkursu kilkadziesiąt MB. `TrueTypeSubset` zostawia w pliku kontury tylko tych glifów, których dokument używa, oraz składowych glifów złożonych (polskie litery z ogonkiem bywają złożone z litery i znaku) i `.notdef`. Numery glifów się nie zmieniają: PDF adresuje glify numerem (`/CIDToGIDMap /Identity`), więc szerokości, strumienie treści i mapa ToUnicode zostają bez zmian, a `hmtx`, `maxp` i `cmap` pozostają poprawne. Odrzucone przenumerowanie, bo wymagałoby przepisania `cmap`, `hmtx` i mapy CID w każdym dokumencie w zamian za kilkanaście KB. Podzbiór dostaje nazwę z przedrostkiem `ABCDEF+` (ISO 32000-1, 9.6.4), wyprowadzonym ze zbioru glifów. Umowa 2026 ma teraz około 140 KB, z czego czcionka to około 26 KB; resztę stanowią nieskompresowane strumienie treści, które testy czytają wprost.

### Test w przeglądarce: ludzie przez ekrany, serwer przez komendy, bez SQL (T-100)

Scenariusz zakłada konta tylko przez formularz rejestracji i link z maila w Mailpicie, rolę operatora nadaje `grant-role`, a treść konkursu wgrywa `import-content`, tak jak opisuje `docs/wdrozenie.md`. Konkurs, karta podmiotu, odpowiedzi i załącznik idą przez API w sesji danej osoby, a złożenie wniosku przez ekran z oknem potwierdzenia. Odrzucone: wypełnianie formularza 2026 pole po polu na ekranie, bo to kilkadziesiąt pól, które sprawdzają już testy frontu, a test przeglądarki ma złapać rozjazd między warstwami (L1), nie przeklikać każdy input. Komendy idą przez zbudowany `Ocwip.Api.dll` w kontenerze, a nie `dotnet run`, bo drugi build obok `dotnet watch` walczy o te same pliki `obj`. Od T-100a na ekranie dzieje się też to, co operator i ekspert robią jednym przyciskiem z potwierdzeniem (deklaracja, zamknięcie naboru, początek oceny, zatwierdzenie wyników, maile o wynikach); karty oceny wypełnia API z tego samego powodu co wniosek. Od T-100b tak samo rezygnacja, dofinansowanie z listy rezerwowej i zapis podpisania umowy (ekran), a wartości umowy (API).

### Za proxy: zaufani nadawcy z konfiguracji, jeden limit ciała, błędy jako ProblemDetails (T-111)

**Nagłówki przekazane tylko od listy.** `ForwardedHeadersConfiguration` wierzy `X-Forwarded-For` wyłącznie od adresów i sieci z konfiguracji, na jeden przeskok; compose produkcyjne daje sieci wewnętrznej stałą podsieć i to ją wpisuje. Odrzucone `ASPNETCORE_FORWARDEDHEADERS_ENABLED`, bo czyści listę zaufanych: każdy klient mógłby wtedy podać dowolny adres i obejść limit logowania.

**`/api` zdejmuje Caddy, nie API.** Trasy API nie mają prefiksu, a `/competitions` jest i trasą API, i stroną frontu, więc jedno wejście musi je rozdzielić prefiksem. `handle_path` zdejmuje go przed API, zamiast `UsePathBase`, dzięki czemu stos deweloperski i testy nie znają prefiksu wcale.

**Jeden limit ciała żądania w trzech miejscach.** Największy plik, jaki może dopuścić konkurs, to 25 MB (`CompetitionRequestValidator.MaxAttachmentSizeCeiling`), a Kestrel i Caddy przyjmują 26 MiB, czyli plik plus koperta multipart. Limit wyższy niż to, co przepuszcza proxy, byłby limitem nie do osiągnięcia.

**Błędy zawsze jako ProblemDetails.** `UseExceptionHandler` i `UseStatusCodePages` w każdym środowisku: nieobsłużony wyjątek w `Production` nie jest pustym 500 ani śladem stosu. Wyjątek `BadHttpRequestException` (nieczytelne ciało JSON) obsługuje osobno `UnreadableRequestHandler` (T-123): status bierze z samego wyjątku, czyli 400 dla uciętego JSON-a, złego typu pola i bajtów spoza UTF-8, a 413 dla ciała ponad limit, komunikat jest stały i nie cytuje wyjątku, bo ten potrafi zawierać fragment ciała (hasło, dane osobowe), a wpis w logu nie jest `Error`, bo to błąd klienta, nie serwera (monitoring z T-116 liczy 5xx i błędy jako awarię). Reszta wyjątków dalej kończy się 500. Trasa zapasowa łapie teraz każdą ścieżkę (`{*path}`): bez parametru pomijała ścieżki wyglądające jak plik, więc `/openapi/v1.json` poza Development odpowiadał 401.

### Nagłówki bezpieczeństwa: API bez niczego, strony z nonce (T-112)

API odpowiada tylko JSON-em i plikami do pobrania, więc jego polityka to `default-src 'none'; frame-ancestors 'none'`, a nagłówki ustawia się przy starcie odpowiedzi, bo obsługa wyjątków czyści nagłówki nieudanej odpowiedzi. `Referrer-Policy: no-referrer` jest na obu połowach, bo tokeny resetu i weryfikacji są w adresie.

Strony dostają CSP z nonce generowanym w `middleware.ts` na każde żądanie, z `'strict-dynamic'`. Polityka jedzie też na żądaniu, bo stamtąd Next.js bierze nonce do swoich skryptów, a layout czyta go z `x-nonce` dla skryptu kontrastu. Przez to każda strona renderuje się na żądanie; przy tej skali to nic nie kosztuje. Odrzucone samo `'self'`: Next.js wstrzykuje skrypty inline przy hydracji i `'self'` bez `'unsafe-inline'` zablokowałoby wszystkie ekrany. Style mają `'unsafe-inline'`, bo komponenty używają atrybutów `style`, których nonce nie obejmuje. `'unsafe-eval'` tylko w development (React Refresh). Test przeglądarki zbiera naruszenia z każdej przeglądarki scenariusza i nie przechodzi przy żadnym, także na compose produkcyjnym.

### Kopie zapasowe: restic, serwer tylko dopisuje, odtworzenie sprawdzane w CI (T-114)

Restic zamiast samego `pg_dump` na dysk z innego serwera: szyfruje po stronie serwera aplikacji, deduplikuje załączniki i ma jedną komendę odtworzenia dla bazy i plików. Kopia obejmuje bazę, załączniki i klucze DataProtection, bo sama baza po odtworzeniu nie otwiera żadnego pliku i unieważnia sesje oraz linki z maili. Klucz szyfrowania pól i hasło restic nie są w kopii: to sekrety do przechowania osobno, inaczej kopia i klucz leżałyby w jednym miejscu.

Klucz magazynu na serwerze może tylko dopisywać, więc usuwanie starych kopii (`forget --prune`) robi się z zaufanej maszyny, a `backup.sh` na serwerze kasuje wyłącznie przy `BACKUP_PRUNE=1`. Odtworzenie idzie do pustych woluminów i skrypt odmawia bazy z tabelami, żeby pomyłka nie nadpisała żywych danych. Zadanie CI `backup` sprawdza całą pętlę tak, jak zobaczy ją człowiek: dane ze scenariusza przeglądarki, kopia, usunięcie wszystkich woluminów, odtworzenie i logowanie z pobraniem załącznika, PDF-u wniosku i umowy.

### Obrazy i wdrożenie: jeden obraz na commit, wdrożenie ręczne z akceptacją (T-115)

Obrazy powstają w tym samym zadaniu CI, które stawia na nich compose produkcyjne i puszcza smoke test przez Caddy, więc do GHCR trafia dokładnie to, co przeszło sprawdzenie; na pull requeście zadanie tylko buduje i skanuje, wypycha dopiero push do `dev` albo `main`. Front dostał względny adres API (`/api`), bo `NEXT_PUBLIC_API_URL` jest wpiekany w kod przeglądarki i adres bezwzględny wiązałby obraz z jedną domeną. Serwer front używa `API_SERVER_URL`, więc względny adres dotyczy tylko przeglądarki.

Wdrożenie jest ręczne i stoi w środowisku GitHub, gdzie akceptację ustawia administrator, bo termin naboru jest jedynym momentem, w którym awaria kosztuje wnioskodawców, i o jego ryzyku decyduje człowiek. Blokada kalendarza czyta publiczne API, więc działa bez dostępu do bazy. Wejście workflow przechodzi do powłoki tylko przez zmienną środowiskową i tylko jako pełny SHA. Wycofanie wersji cofa obrazy, nie migracje; niezgodny schemat to odtworzenie z kopii zrobionej przez `deploy.sh` tuż przed aktualizacją.

### Logi i monitoring: JSON z identyfikatorem żądania, monitor poza serwerem (T-116)

Poza Development log to jedna linia JSON na wpis, z zakresami, które ASP.NET Core otwiera dla każdego żądania, więc identyfikator żądania trafia do każdej linii bez zmian w kodzie domeny. Ten sam identyfikator wraca w `X-Request-Id`, żeby osoba zgłaszająca błąd miała co przekazać. Zakresy niosą tylko identyfikatory i ścieżkę, nigdy treści ani nagłówków (reguła 4). Development zostaje przy zwykłej konsoli, bo czyta ją człowiek.

Monitor odpytuje osobno `/health` i `/health/db`, bo to różne awarie: pierwsza to API, druga to baza albo sieć do niej. Stoi na innej maszynie niż aplikacja, a pisze tylko przy zmianie stanu, bo alarm powtarzany co pięć minut przestaje być czytany. Odrzucony monitor wewnątrz compose produkcyjnego: zamilkłby razem z serwerem.

### Staging: nakładka zamiast kopii, maszyna z pliku (T-117)

Staging używa tego samego `docker-compose.prod.yml` i tych samych obrazów z GHCR co produkcja, z `Production` włącznie, a różnice trzyma nakładka `docker-compose.staging.yml`. Kopia pliku compose rozjechałaby się z produkcją przy pierwszej zmianie, a staging, który nie przypomina produkcji, niczego nie sprawdza. Hasło i `noindex` dokłada do Caddy fragment `site.d/*.caddy` podany przez `configs`, więc Caddyfile produkcji pozostaje jeden, a obrazy nadal nie montują ścieżek z hosta. Serwer powstaje z `infra/staging/cloud-init.yaml`, żeby dało się go postawić od nowa bez pamiętania ręcznych kroków. Użytkownik wdrożeń bez sudo ma za to grupę `docker`, co na tej maszynie znaczy prawie tyle co root; dlatego jego klucz służy wyłącznie do wdrożeń.

### Deklaracja dostępności i strony informacyjne: dane w jednym pliku, teksty prawne z jednego źródła (T-121)

Deklaracja ma nagłówki i identyfikatory wersji 2.0 wzoru, bo czyta ją walidator, a nie tylko człowiek; test pilnuje obowiązkowych `id` i kolejności nagłówków. Dane (osoba, telefon, daty, opis siedziby, lista niedostępnych treści) siedzą w `lib/accessibility-statement.ts`, więc coroczna aktualizacja to zmiana danych. Stan zgodności to "częściowo zgodna", choć audyt T-46 poprawił wszystko, co znalazł: PDF-y tworzone przez system nie mają struktury znaczników, a deklaracja, która to przemilcza, obiecywałaby więcej, niż serwis daje. Regulamin i klauzula informacyjna jako strony czytają ten sam `/public/consents` co formularz rejestracji, więc tekst na stronie i tekst zaakceptowany nie mogą się rozjechać. Stopka z czterema linkami jest jednym komponentem, w ramie publicznej i we wszystkich panelach.

### Dwa formaty kwoty: zdanie dla człowieka, komórka dla arkusza

`ApplicationListLabels.Amount` pisze kwotę bez separatora tysięcy ("20000,00"), bo CSV i XLSX mają się otworzyć w polskim arkuszu jako liczba, a nie jako tekst. Zdania, które czyta człowiek (odmowa kwoty ponad pulę, mail o wyniku, mail o przyznaniu z listy rezerwowej, mail o przyjęciu sprawozdania), idą przez `PolishNumbers.Amount`, czyli "20 000,00 zł" ze spacją nierozdzielającą, tak jak `formatAmount` na ekranach. Jeden format dla obu zastosowań zawsze psuje jedno z nich: operator czytał "20000,00 zł" pod polem, które pokazywało "20 000,00 zł".

### Wynik oceny formalnej na liście wniosków (T-35, T-38)

Lista wniosków operatora niesie `FormalStanding` tego samego kształtu co lista rankingowa, bo nabór prowadzi się z listy wniosków, a wynik oceny formalnej był tylko o ekran dalej. Reguła odczytu (brak karty, karta niezakończona, wynik z `EvaluationScores`) siedzi w jednym miejscu, `FormalStandingReader`, a nie w dwóch kopiach, które rozjechałyby się przy pierwszej poprawce. Lista czyta oceny formalne konkursu jednym zapytaniem i karty jedną analizą na wersję, tak jak czyta wnioski: zapytanie na wiersz to 120 zapytań na jedno otwarcie ekranu.

### Zapis do wniosku: blokada wiersza i odmowa zamiast cichego nadpisania

Autozapis odpowiedzi i zapis załącznika biorą blokadę wiersza wniosku (`SELECT ... FOR UPDATE`) na czas sprawdzeń i zapisu, nie tylko na czas samego `UPDATE`. Powód jest w obu miejscach ten sam: między odczytem wniosku a zapisem mieści się całe okno edycji (T-21, T-103) oraz suma rozmiarów załączników, więc bez blokady równoległe żądanie potrafiło dopisać plik do wniosku już złożonego albo przekroczyć limit rozmiaru sumą, której żadne z dwóch żądań samo nie widziało. Plik jest wczytywany przed wzięciem blokady, żeby trzymać ją przez sprawdzenia i zapis, a nie przez przesył.

Złożenie idzie o krok dalej: porównuje `UpdatedAt` odczytany wewnątrz blokady z kopią, którą samo sprawdziło. Różnica znaczy, że odpowiedzi zapisano jeszcze raz (autozapis z drugiej karty) już po walidacji na poziomie złożenia, więc zamrożone zostałyby odpowiedzi, których nikt nie sprawdził. Odpowiedzią jest 409 i prośba o ponowne złożenie, nie ciche zamrożenie: "ostatni zapis wygrywa" jest tu gorsze niż powtórzone kliknięcie, bo zamrożonej wersji wnioskodawca już nie poprawi. Front nie wymagał zmiany, bo pokazuje `detail` z ProblemDetails.

Do tego dwa zawężenia z reguły "brak reguły oznacza brak dostępu": zapis odpowiedzi, zapis i usunięcie załącznika oraz usunięcie wniosku wymagają roli wnioskodawcy, bo operator i ekspert wniosek czytają, a nie piszą; przypisanie eksperta jest możliwe dopiero na wniosku złożonym, bo samo przypisanie jest całym prawem do odczytu (`EntityScopedHandler`), a szkic to praca wnioskodawcy w toku.

## Czego tu jeszcze nie ma

Cykl konkursu działa od rejestracji po rozliczenie sprawozdania. Brakuje tego, co czeka na zamawiającego albo na serwer: budowy formularza od zera i przestawiania sekcji w kreatorze (`T-26a`, B-10, odłożone decyzją D16), szyfrowania danych wrażliwych i retencji (`T-47`, `T-47b`), sprawozdania częściowego i terminu sprawozdania (`T-50c`), środowiska produkcyjnego i stagingu (`T-48`, `T-117`) oraz instrukcji dla operatora (`T-49`).

Aktualny stan jest w [`runbook/kolejka.md`](runbook/kolejka.md), blokery w [`runbook/blokery.md`](runbook/blokery.md), a każde z tych zadań ma kartę na Trello. Model danych i jawne założenia: [`model-danych.md`](model-danych.md).
