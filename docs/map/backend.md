# Mapa: backend

Usługa .NET (minimal API). Warstwy i wzorce: [`../konwencje.md`](../konwencje.md).

| Plik | Co robi |
|---|---|
| `backend/Ocwip.slnx` | Solucja: projekt API plus projekt testów. Cel dla `dotnet restore` i `dotnet test` |
| `backend/Dockerfile` | Obraz backendu: build i publish wieloetapowy, wejście `dotnet Ocwip.Api.dll` |
| `backend/.dockerignore` | Wyklucza `bin/`, `obj/` i pozostałe artefakty lokalne z kontekstu obrazu |
| `backend/src/Ocwip.Api/Ocwip.Api.csproj` | `net10.0`, nullable, `TreatWarningsAsErrors`. Pakiety: OpenAPI, EF Core 10, `Identity.EntityFrameworkCore`, Npgsql.EF, NamingConventions, Npgsql. `InternalsVisibleTo` dla `Ocwip.Api.Tests` |
| `backend/src/Ocwip.Api/Program.cs` | Wyłącznie składanie aplikacji: OpenAPI, ProblemDetails, `AppDbContext` przez `UseOcwipPostgres`, `ApplyPendingMigrations`, CORS z `Cors:Origins` (`AllowCredentials`), mapowanie endpointów. `IAccountService` i Identity (`AddIdentityCore<User>`, `AddIdentityConfiguration`) rejestrowane tylko, gdy jest connection string, bo `AddEntityFrameworkStores<AppDbContext>` potrzebuje `AppDbContext` żeby się zbudować, a DI musi dać się zwalidować także bez bazy. `public partial class Program` na końcu istnieje po to, żeby host testowy startował prawdziwą aplikację |
| `backend/src/Ocwip.Api/Data/AppDbContext.cs` | `IdentityDbContext<User, IdentityRole<Guid>, Guid>` plus `DbSet` dla `Competitions` i `FormDefinitions`, konfiguracje wczytywane z assembly. `ConfigureConventions` narzuca `UtcDateTimeOffsetConverter` na każdą właściwość `DateTimeOffset`. `SaveChanges` stempluje `IAuditedEntity`, bo domyślna wartość `now()` odpala się tylko przy INSERT |
| `backend/src/Ocwip.Api/Models/IAuditedEntity.cs` | Kontrakt `CreatedAt` i `UpdatedAt`, po którym `AppDbContext` znajduje encje do stemplowania |
| `backend/src/Ocwip.Api/Data/AppDbContextFactory.cs` | `IDesignTimeDbContextFactory` dla `dotnet ef`: `Create` i `BuildConfiguration` czytają `ConnectionStrings:Postgres` z tych samych źródeł co runtime, bez niego rzucają wyjątkiem zamiast zgadywać adres |
| `backend/src/Ocwip.Api/Data/PostgresDbContextOptions.cs` | `UseOcwipPostgres`: Npgsql plus konwencja `snake_case`. Jedyne miejsce, w którym konfiguruje się model EF |
| `backend/src/Ocwip.Api/Data/Converters/UtcDateTimeOffsetConverter.cs` | Normalizuje każdy `DateTimeOffset` do UTC przed zapisem. Npgsql odrzuca timestamptz z niezerowym offsetem, więc to jest kontrakt, a nie komentarz |
| `backend/src/Ocwip.Api/Data/DatabaseStartup.cs` | `ApplyPendingMigrations`: migracje przy starcie pod flagą `Database:MigrateOnStartup`, pięć prób z narastającym opóźnieniem tylko dla błędów chwilowych |
| `backend/src/Ocwip.Api/Data/Migrations/20260819110449_InitialCreate.cs` | Migracja bazowa, pusta. `Down()` to no-op, bo `Up()` nic nie tworzy |
| `backend/src/Ocwip.Api/Data/Migrations/20260819110449_InitialCreate.Designer.cs` | Metadane EF dla `InitialCreate` (generowane) |
| `backend/src/Ocwip.Api/Data/Migrations/AppDbContextModelSnapshot.cs` | Bieżący snapshot modelu EF (generowany) |
| `backend/src/Ocwip.Api/Endpoints/HealthEndpoints.cs` | `MapHealthEndpoints`: `GET /health` (liveness) i `GET /health/db` (sonda PostgreSQL przez Npgsql, zwraca 503 i generyczny komunikat, żeby nie ujawnić hosta ani poświadczeń) |
| `backend/src/Ocwip.Api/appsettings.json` | Domyślne poziomy logowania, pusty `ConnectionStrings:Postgres`, `Database:MigrateOnStartup` fałsz, `Cors:Origins`. Wartości nadpisuje środowisko |
| `backend/src/Ocwip.Api/appsettings.Development.json` | Gadatliwsze logowanie ASP.NET Core lokalnie, `Database:MigrateOnStartup` prawda |
| `backend/src/Ocwip.Api/Properties/launchSettings.json` | Profile `dotnet run` lokalnie: porty i zmienne środowiskowe dla uruchomienia z IDE |
| `backend/tests/Ocwip.Api.Tests/Ocwip.Api.Tests.csproj` | xunit plus `Microsoft.AspNetCore.Mvc.Testing`, referencja do projektu API |
| `backend/tests/Ocwip.Api.Tests/OcwipWebApplicationFactory.cs` | `WebApplicationFactory` z wyłączonym `Database:MigrateOnStartup`. Każdy test startujący aplikację idzie tędy, żeby nie robić DDL na wspólnej bazie |
| `backend/tests/Ocwip.Api.Tests/RequiresDatabaseFactAttribute.cs` | `[RequiresDatabaseFact]` plus `ConnectionString` ze środowiska: fakt raportujący Skipped, a nie Passed, gdy nie ma bazy (xUnit 2 nie ma dynamicznego pomijania) |
| `backend/tests/Ocwip.Api.Tests/RequiresDatabaseTheoryAttribute.cs` | `[RequiresDatabaseTheory]`: to samo dla teorii. Nie da się tego dziedziczyć po wersji faktowej, bo xUnit rozróżnia `FactAttribute` i `TheoryAttribute`, a teoria z atrybutem faktowym traci swoje wiersze danych |
| `backend/tests/Ocwip.Api.Tests/HealthEndpointsTests.cs` | Cztery testy przez `OcwipWebApplicationFactory`: `/health` zwraca 200 także przy nieosiągalnej bazie, sonda bazy zwraca 503 bez connection stringa, sonda nigdy nie zwraca w ciele hasła ani użytkownika |
| `backend/tests/Ocwip.Api.Tests/MigrationTests.cs` | Migracje na czystej bazie przez `UseOcwipPostgres` (CREATE DATABASE, Migrate, DROP). Skip, gdy brak connection stringa |
| `backend/tests/Ocwip.Api.Tests/DatabaseConfigurationTests.cs` | Fabryka design-time rzuca przy braku connection stringa, `UseOcwipPostgres` daje Npgsql plus konwencję nazw |
| `backend/src/Ocwip.Api/Models/User.cs` | Model konta użytkownika: `IdentityUser<Guid>` plus `FirstName`, `LastName`, `Role`, `Pesel` (dane wrażliwe, do zaszyfrowania), `IsVerified`, znaczniki audytowe i relacja 1:1 z `Entity.cs` |
| `backend/src/Ocwip.Api/Models/Role.cs` | Enum trzech ról: `Applicant`, `Operator`, `Reviewer` |
| `backend/src/Ocwip.Api/Contracts/RegisterRequest.cs` | Rekord `RegisterRequest(Email, Password, FirstName, LastName, Pesel)`, ciało `POST /register` |
| `backend/src/Ocwip.Api/Configuration/IdentityConfiguration.cs` | `AddIdentityConfiguration`: hasło minimum 8 znaków, wymaga cyfry, wielkiej litery, małej litery i znaku specjalnego |
| `backend/src/Ocwip.Api/Configuration/CustomPasswordErrorConfiguration.cs` | `CustomPasswordErrorConfiguration : IdentityErrorDescriber`, polskie komunikaty błędów haseł (`PasswordTooShort`, `PasswordRequiresDigit`, `PasswordRequiresUpper`, `PasswordRequiresLower`, `PasswordRequiresNonAlphanumeric`) |
| `backend/src/Ocwip.Api/Data/Configurations/UserConfiguration.cs` | Konfiguracja EF Core konta: `gen_random_uuid()`, unikalny indeks na `Email`, `Role` jako tekst, `Pesel` z check constraintem `ck_user_pesel_length` (dokładnie 11 cyfr), `now()` dla znaczników czasu |
| `backend/src/Ocwip.Api/Data/Migrations/20260831135836_AddUserModelwIdentity.cs` | Tabele ASP.NET Core Identity (`AspNetUsers` i pozostałe, nazwy nietknięte przez konwencję `snake_case`, w przeciwieństwie do kolumn) plus kolumny domenowe konta i FK do `entity` |
| `backend/src/Ocwip.Api/Data/Migrations/20260831135836_AddUserModelwIdentity.Designer.cs` | Metadane EF dla `AddUserModelwIdentity` (generowane) |
| `backend/src/Ocwip.Api/Endpoints/AccountEndpoints.cs` | `MapRegisterEndpoints`: `POST /register`. 201 przy sukcesie, 409 przy zdublowanym mailu, 400 dla pozostałych błędów `IdentityResult`. `service` jako `[FromServices]` jawnie, bo `IAccountService` jest zarejestrowany tylko warunkowo (patrz `Program.cs`) i minimalne API bez tego nie potrafi wywnioskować źródła parametru |
| `backend/src/Ocwip.Api/Services/IAccountService.cs` | Kontrakt `RegisterAsync(RegisterRequest)` |
| `backend/src/Ocwip.Api/Services/AccountService.cs` | `AccountService : IAccountService`, `RegisterAsync` przez `UserManager<User>`. Zdublowany mail zwraca fałszywy sukces zamiast błędu, żeby nie ujawniać istnienia konta |
| `backend/tests/Ocwip.Api.Tests/Endpoints/RegistrationTests.cs` | Niezmienniki rejestracji na prawdziwym PostgreSQL, kolekcja `postgres`: konto dostaje id i `created_at` przy insercie przez EF i obok EF, mail unikalny, `password_hash` zamiast hasła jawnego w kolumnie |
| `backend/tests/Ocwip.Api.Tests/Data/Configurations/TestUser.cs` | `TestUser.New`: konto spełniające wszystkie check constraints (w tym PESEL), żeby test mówił tylko o swoim jednym polu |
| `backend/src/Ocwip.Api/Models/Entity.cs` | Model podmiotu |
| `backend/src/Ocwip.Api/Models/EntityType.cs` | Enum zawierający trzy typy podmiotów |
| `backend/src/Ocwip.Api/Models/Competition.cs` | Model konkursu: kwota dotacji, status, znaczniki czasu i flaga `IsActive` (bez twardego kasowania). Settery `StartDate` i `EndDate` ucinają do pełnej minuty w UTC, celowo nie konwerterem, patrz komentarz w pliku. Relacja jeden do wielu z `FormDefinition.cs` |
| `backend/src/Ocwip.Api/Models/CompetitionStatus.cs` | Enum pięciu statusów konkursu: `Draft`, `Published`, `Closed`, `Resolved`, `Archived`. W bazie zapisywany jako tekst, nie jako ordynał |
| `backend/src/Ocwip.Api/Models/FormDefinition.cs` | Model definicji formularza: struktura jako `JsonElement` w kolumnie jsonb plus numer wersji, unikalny w obrębie konkursu |
| `backend/src/Ocwip.Api/Data/Configurations/CompetitionConfiguration.cs` | Konfiguracja EF Core konkursu: limity długości, `gen_random_uuid()`, `now()` dla znaczników czasu, pięć check constraints (kolejność dat, dodatnia kwota, pełna minuta na `start_date` i `end_date`, sparowanie `is_active` z `deactivated_at`) w jednym wywołaniu `ToTable`, indeks na `(status, end_date)`, FK bez kaskady |
| `backend/src/Ocwip.Api/Data/Configurations/FormDefinitionConfiguration.cs` | Konfiguracja EF Core definicji formularza: kolumna jsonb, trzy check constraints (sparowanie soft delete, `version_number > 0`, korzeń JSON-a jako obiekt albo tablica), unikalny indeks na `(competition_id, version_number)` |
| `backend/src/Ocwip.Api/Data/Migrations/20260827130815_AddDataModels.cs` | Tabele `competitions` i `form_definitions`, pięć check constraints na `competitions` i trzy na `form_definitions`, indeks na `(status, end_date)` dla listy publicznej, unikalny indeks na wersję formularza |
| `backend/src/Ocwip.Api/Data/Migrations/20260827130815_AddDataModels.Designer.cs` | Metadane EF dla `AddDataModels` (generowane) |
| `backend/tests/Ocwip.Api.Tests/Data/Configurations/CompetitionConfigurationTests.cs` | Metadane konkursu: nazwa tabeli, klucz, limity długości, status jako tekst |
| `backend/tests/Ocwip.Api.Tests/Data/Configurations/CompetitionTimestampConfigurationTests.cs` | Metadane czasu w konkursie: pełna precyzja znaczników audytowych, pełna minuta w oknie, brak konwertera ucinającego |
| `backend/tests/Ocwip.Api.Tests/Data/Configurations/CompetitionSchemaConfigurationTests.cs` | Metadane schematu konkursu: wszystkie check constraints, indeks listy publicznej, FK bez kaskady |
| `backend/tests/Ocwip.Api.Tests/Data/Configurations/FormDefinitionConfigurationTests.cs` | Metadane modelu definicji formularza: nazwa tabeli, kolumna jsonb, unikalny indeks na `(competition_id, version_number)` |
| `backend/tests/Ocwip.Api.Tests/Data/TestModel.cs` | Model EF budowany tak jak w aplikacji, przez `IDesignTimeModel`, bez łączenia z bazą. Model runtime gubi check constraints i komentarze |
| `backend/tests/Ocwip.Api.Tests/Data/PostgresDatabaseFixture.cs` | Jednorazowa baza na klasę testową: CREATE DATABASE, migracje, DROP. Milczy bez connection stringa, bo xUnit tworzy fixture nawet dla pominiętych testów |
| `backend/tests/Ocwip.Api.Tests/Data/PostgresCollection.cs` | Kolekcja `postgres`: wszystkie testy dotykające bazy w jednej kolekcji, jedna baza jako `ICollectionFixture`. Równoległe `CREATE DATABASE` kopiuje `template1` i wywala się na 55006 |
| `backend/tests/Ocwip.Api.Tests/Data/CompetitionDatabaseTests.cs` | Reszta niezmienników konkursu na prawdziwym PostgreSQL: kwota, status jako tekst, szerokości kolumn, insert omijający EF |
| `backend/tests/Ocwip.Api.Tests/Data/CompetitionWindowDatabaseTests.cs` | Okno konkursu na prawdziwym PostgreSQL: kolejność dat, pełna minuta na obu końcach, offset `+02:00`, regresja na operandzie zapytania |
| `backend/tests/Ocwip.Api.Tests/Data/CompetitionLifecycleDatabaseTests.cs` | Soft delete i znaczniki audytowe na prawdziwym PostgreSQL: sparowanie `is_active` z `deactivated_at`, ruch `updated_at` |
| `backend/tests/Ocwip.Api.Tests/Data/FormDefinitionConstraintDatabaseTests.cs` | Czego schemat nie przyjmie w definicji formularza: `version_number` niedodatni, korzeń JSON-a jako skalar |
| `backend/tests/Ocwip.Api.Tests/Data/TestCompetition.cs` | Konkurs spełniający wszystkie check constraints, żeby test mówił tylko o swoim jednym polu |
| `backend/tests/Ocwip.Api.Tests/Data/PostgresAssert.cs` | Wyłuskuje `PostgresException` z `DbUpdateException` plus stałe SQLSTATE, żeby test twierdził o nazwie constraintu, nie o komunikacie |
| `backend/tests/Ocwip.Api.Tests/Data/FormDefinitionDatabaseTests.cs` | Niezmienniki definicji formularza na prawdziwym PostgreSQL: unikalna wersja w konkursie, FK bez kaskady, round trip jsonb |


## Czego tu jeszcze nie ma

`Domain/` (encje, konwencje.md je tam przewiduje, w praktyce na razie leżą w `Models/`), uwierzytelnianie sesyjne i autoryzacja (rejestracja jest, logowania jeszcze nie ma). Każde ma kartę na Trello. Katalogi zakładamy razem z pierwszym prawdziwym plikiem.
